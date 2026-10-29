using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mizan.Application.Notifications;
using Mizan.Contracts.Notifications;
using Mizan.Domain.Entities;
using Mizan.Domain.Notifications;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Outbox;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Notification campaigns and lifecycle messages (docs/ADMIN.md#notification-campaigns): an administrator
/// writes a message, counts its audience, confirms that count, and the one notification scheduler sends it.
/// </summary>
[Collection("ApiIntegration")]
public class NotificationCampaignStoryTests
{
    private const string Base = "/api/admin/notification-campaigns";
    private readonly ApiTestFixture _fixture;

    public NotificationCampaignStoryTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AWelcomeOneDayAfterSignUp_IsQueuedADayAfterAPersonSignsUp()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlatformAdminWorld(_fixture);
        var (adminId, admin) = await world.AdminAsync();
        // The administrator has had an account for a month; a welcome follows only recent sign-ups.
        await world.DbAsync(db => db.Users.Where(u => u.Id == adminId).ExecuteUpdateAsync(u => u.SetProperty(x => x.CreatedAt, DateTime.UtcNow.AddDays(-30))));

        var welcome = await PlatformAdminWorld.ReadAsync<NotificationCampaignDto>(admin.PostAsJsonAsync(Base, new SaveNotificationCampaignRequest(
            "Welcome",
            "lifecycle",
            new CampaignContent("Welcome, {firstName}", "Log your first meal at {appLink}", LinkPath: "/today"),
            new CampaignTiming("event", Event: CampaignEvents.SignedUp, EventDays: 1),
            Channels: ["inApp"])));
        welcome.Status.Should().Be("draft");
        welcome.Summary.Should().NotBeNullOrWhiteSpace();

        var audience = await PlatformAdminWorld.ReadAsync<CampaignAudiencePreviewDto>(admin.PostAsJsonAsync($"{Base}/audience",
            new CampaignAudienceRequest("lifecycle", Channels: ["inApp"])));
        var started = await PlatformAdminWorld.ReadAsync<NotificationCampaignDto>(admin.PostAsJsonAsync($"{Base}/{welcome.Id}/activate",
            new ActivateNotificationCampaignRequest(audience.Reachable)));
        started.Phase.Should().Be("live");

        // Someone signs up after the welcome was started.
        var email = $"selam-{Guid.NewGuid():N}@example.com";
        var anonymous = _fixture.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/Auth/register", new { Email = email, Password = "synthetic-long-password", Name = "Selam Tesfaye" }))
            .IsSuccessStatusCode.Should().BeTrue();
        var person = await world.DbAsync(db => db.Users.AsNoTracking().Where(u => u.Email == email).Select(u => new { u.Id, u.CreatedAt }).SingleAsync());
        var due = CampaignTriggerRules.Occurrence(person.CreatedAt, 24 * 60);

        // Nothing is queued on the day of sign-up; the scan an hour before the day is up queues one delivery.
        await ScanAsync();
        (await DeliveryJobsAsync(welcome.Id)).Should().BeEmpty();
        try
        {
            _fixture.Clock.Set(due.AddHours(-1));
            await ScanAsync();
            var job = (await DeliveryJobsAsync(welcome.Id)).Should().ContainSingle().Subject;
            job.DedupeKey.Should().Be($"schedule:{welcome.Id:N}:{due:yyyyMMddTHHmm}:{person.Id:N}:inApp");
            job.RunAfter.Should().Be(due.UtcDateTime);

            // Scanning again finds the same person and adds nothing.
            await ScanAsync();
            (await DeliveryJobsAsync(welcome.Id)).Should().ContainSingle();

            _fixture.Clock.Set(due);
            await RunDueAsync();
        }
        finally
        {
            _fixture.Clock.Reset();
        }

        // They confirm their address, which signing in needs, and open the app.
        await world.DbAsync(db => db.Users.Where(u => u.Id == person.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.EmailVerified, true)));
        var inbox = await _fixture.CreateAuthenticatedClient(person.Id, email).GetFromJsonAsync<NotificationListResult>("/api/Notifications");
        var row = inbox!.Items.Should().ContainSingle().Subject;
        row.Title.Should().Be("Welcome, Selam");
        row.Body.Should().StartWith("Log your first meal at http");
        row.Kind.Should().Be(NotificationKinds.Account);

        var report = await admin.GetFromJsonAsync<NotificationCampaignDto>($"{Base}/{welcome.Id}");
        report!.Report.Should().ContainSingle().Which.Should().Be(new CampaignChannelReport("inApp", 0, 1, 0, 0, 0));
    }

    [Fact]
    public async Task AMarketingCampaign_SkipsPeopleWhoHaveNotOptedIn()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlatformAdminWorld(_fixture);
        var (_, admin) = await world.AdminAsync();
        var (inId, optedIn) = await world.PersonAsync("opted-in", name: "Hana Bekele");
        var (outId, notOptedIn) = await world.PersonAsync("not-opted-in", name: "Dawit Alemu");
        await PlatformAdminWorld.OkAsync(optedIn.PutAsJsonAsync("/api/Notifications/preferences",
            new UpdateNotificationPreferencesRequest([new NotificationKindPreferenceUpdate(NotificationKinds.Marketing, InApp: true)])));

        var offer = await PlatformAdminWorld.ReadAsync<NotificationCampaignDto>(admin.PostAsJsonAsync(Base, new SaveNotificationCampaignRequest(
            "Ramadan recipes",
            "marketing",
            new CampaignContent("New for you, {firstName}", "Twenty iftar recipes"),
            new CampaignTiming("now"),
            Channels: ["inApp"])));

        // Three people match, but only the one who opted in can be reached.
        var audience = await PlatformAdminWorld.ReadAsync<CampaignAudiencePreviewDto>(admin.PostAsJsonAsync($"{Base}/audience",
            new CampaignAudienceRequest("marketing", Channels: ["inApp"])));
        audience.Matched.Should().Be(3);
        audience.Reachable.Should().Be(1);

        // Confirming a count that is not the audience's is refused, and nothing is sent.
        var stale = await admin.PostAsJsonAsync($"{Base}/{offer.Id}/activate", new ActivateNotificationCampaignRequest(3));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DeliveryJobsAsync(offer.Id)).Should().BeEmpty();

        var started = await PlatformAdminWorld.ReadAsync<NotificationCampaignDto>(admin.PostAsJsonAsync($"{Base}/{offer.Id}/activate",
            new ActivateNotificationCampaignRequest(1)));
        started.ConfirmedAudience.Should().Be(1);
        (await DeliveryJobsAsync(offer.Id)).Should().ContainSingle().Which.DedupeKey.Should().EndWith($"{inId:N}:inApp");
        await RunDueAsync();

        var inbox = await optedIn.GetFromJsonAsync<NotificationListResult>("/api/Notifications");
        inbox!.Items.Should().ContainSingle().Which.Title.Should().Be("New for you, Hana");
        (await notOptedIn.GetFromJsonAsync<NotificationListResult>("/api/Notifications"))!.Items.Should().BeEmpty();

        var report = await admin.GetFromJsonAsync<NotificationCampaignDto>($"{Base}/{offer.Id}");
        report!.Phase.Should().Be("sent");
        report.Totals.Sent.Should().Be(1);

        // Every step is in the audit log.
        var audited = await world.DbAsync(db => db.AuditLogs.AsNoTracking().Select(a => a.Action).ToListAsync());
        audited.Should().Contain(a => a.Contains("CreateNotificationCampaign")).And.Contain(a => a.Contains("ActivateNotificationCampaign"));
        outId.Should().NotBe(inId);
    }

    private Task<int> ScanAsync() =>
        _fixture.Services.GetServices<IHostedService>().OfType<NotificationSchedulerService>().Single().RunOnceAsync(CancellationToken.None);

    private async Task<List<OutboxJob>> DeliveryJobsAsync(Guid campaignId)
    {
        using var scope = _fixture.Services.CreateScope();
        var prefix = $"schedule:{campaignId:N}:";
        return await scope.ServiceProvider.GetRequiredService<MizanDbContext>().OutboxJobs.AsNoTracking()
            .Where(j => j.Type == OutboxJobTypes.NotificationDelivery && j.DedupeKey!.StartsWith(prefix))
            .ToListAsync();
    }

    /// <summary>The dispatcher claims by the database's clock, which the test clock does not move. Bring the jobs forward, then run the queue.</summary>
    private async Task RunDueAsync()
    {
        using (var scope = _fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MizanDbContext>().OutboxJobs
                .Where(j => j.Type == OutboxJobTypes.NotificationDelivery && j.Status == OutboxJobStatus.Pending)
                .ExecuteUpdateAsync(j => j.SetProperty(x => x.RunAfter, DateTime.UtcNow.AddSeconds(-1)));
        }
        await _fixture.DrainOutboxAsync();
    }
}
