using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Common;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>The email log and announcements, through the API (docs/ADMIN.md#mail).</summary>
[Collection("ApiIntegration")]
public class AdminMailStoriesTests(ApiTestFixture fixture)
{
    [Fact]
    public async Task AQueuedAuthEmailAppearsInTheMailLog_AndTurnsSentAfterTheOutboxRuns()
    {
        await fixture.ResetDatabaseAsync();
        fixture.Email.Clear();
        using var admin = await AdminAsync();
        var email = $"log-{Guid.NewGuid():N}@example.test";

        using var anonymous = fixture.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/Auth/register", new { Email = email, Password = "synthetic-long-password", Name = "Log test" }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        var queued = (await ListAsync(admin, $"?search={Uri.EscapeDataString(email)}")).Items.Should().ContainSingle().Which;
        queued.Kind.Should().Be(EmailKinds.AuthVerify);
        queued.Status.Should().Be("Queued");
        queued.Subject.Should().Be("Confirm your email");
        queued.ToUserId.Should().NotBeNull();

        await fixture.DrainOutboxAsync();

        var sent = await admin.GetFromJsonAsync<EmailMessageDto>($"/api/admin/mail/{queued.Id}");
        sent!.Status.Should().Be("Sent");
        sent.SentAt.Should().NotBeNull();
        sent.Attempts.Should().Be(1);
        sent.CanRetry.Should().BeFalse();
        (await admin.PostAsync($"/api/admin/mail/{queued.Id}/retry", null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "a sent message is never sent twice from the log");

        var stats = await admin.GetFromJsonAsync<MailStatsDto>("/api/admin/mail/stats");
        stats!.Sent.Should().Be(1);
        stats.Kinds.Should().Contain(EmailKinds.AuthVerify);
    }

    [Fact]
    public async Task AFailedSendShowsFailedWithTheError_AndRetryQueuesItAgain()
    {
        await fixture.ResetDatabaseAsync();
        fixture.Email.Clear();
        using var admin = await AdminAsync();
        var email = $"fails-{Guid.NewGuid():N}@example.test";
        await fixture.SeedUserAsync(Guid.NewGuid(), email);

        using var anonymous = fixture.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/Auth/forgot-password", new { Email = email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        fixture.Email.FailWith($"550 5.1.1 <{email}>: mailbox unavailable");
        try
        {
            await RunUntilGivenUpAsync();
        }
        finally
        {
            fixture.Email.Recover();
        }

        var failed = (await ListAsync(admin, "?status=Failed")).Items.Should().ContainSingle().Which;
        failed.Kind.Should().Be(EmailKinds.AuthReset);
        failed.Attempts.Should().Be(5);
        failed.LastError.Should().Contain("mailbox unavailable").And.NotContain(email, "addresses are redacted from errors");
        failed.CanRetry.Should().BeTrue();

        var retried = await (await admin.PostAsync($"/api/admin/mail/{failed.Id}/retry", null))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<EmailMessageDto>();
        retried!.Status.Should().Be("Queued");

        await fixture.DrainOutboxAsync();

        (await admin.GetFromJsonAsync<EmailMessageDto>($"/api/admin/mail/{failed.Id}"))!.Status.Should().Be("Sent");
        fixture.Email.Sent.Should().ContainSingle(m => m.To == email && m.Subject == "Reset your password");
    }

    [Fact]
    public async Task AnAnnouncementToAHouseholdQueuesOneMessagePerMember_AndARetryDoesNotDoubleSend()
    {
        await fixture.ResetDatabaseAsync();
        fixture.Email.Clear();
        using var admin = await AdminAsync();
        var householdId = await SeedHouseholdAsync("Announce house", 3);
        var outsider = Guid.NewGuid();
        await fixture.SeedUserAsync(outsider, $"outsider-{outsider:N}@example.test");

        var preview = await admin.GetFromJsonAsync<AnnouncementAudienceDto>(
            $"/api/admin/mail/announcements/audience?audience=household:{householdId}");
        preview!.RecipientCount.Should().Be(3);
        preview.Label.Should().Be("Announce house");

        var request = new SendAnnouncementRequest("Kitchen closed", "The kitchen is closed on Friday.\n\nSee you Monday.", $"household:{householdId}", Guid.NewGuid());
        var first = await (await admin.PostAsJsonAsync("/api/admin/mail/announcements", request))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<AnnouncementResultDto>();
        first!.Queued.Should().Be(3);

        var again = await (await admin.PostAsJsonAsync("/api/admin/mail/announcements", request))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<AnnouncementResultDto>();
        again!.Queued.Should().Be(3, "the retry answers with the first send");

        var log = await ListAsync(admin, $"?kind={EmailKinds.Announcement}");
        log.Items.Should().HaveCount(3).And.OnlyContain(m => m.Subject == "Kitchen closed" && m.CreatedByUserId != null);
        log.Items.Should().NotContain(m => m.ToUserId == outsider);

        await fixture.DrainOutboxAsync();
        fixture.Email.Sent.Where(m => m.Subject == "Kitchen closed").Should().HaveCount(3);
        fixture.Email.Sent.Should().Contain(m => m.Text.Contains("See you Monday."));
    }

    [Fact]
    public async Task AnAnnouncementToEveryone_IsLimitedToOneAnHour()
    {
        await fixture.ResetDatabaseAsync();
        using var admin = await AdminAsync();

        (await admin.PostAsJsonAsync("/api/admin/mail/announcements", new SendAnnouncementRequest("One", "First.", "all")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await admin.PostAsJsonAsync("/api/admin/mail/announcements", new SendAnnouncementRequest("Two", "Second.", "all"));

        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.Should().NotBeNull();
        (await admin.PostAsJsonAsync("/api/admin/mail/announcements", new SendAnnouncementRequest("Bad", "Body", "everyone")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TheMailLogIsAdminOnly()
    {
        await fixture.ResetDatabaseAsync();
        var id = Guid.NewGuid();
        await fixture.SeedUserAsync(id, $"plain-{id:N}@example.test");
        using var client = fixture.CreateAuthenticatedClient(id, $"plain-{id:N}@example.test");

        (await client.GetAsync("/api/admin/mail")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/admin/mail/announcements", new SendAnnouncementRequest("x", "y", "all")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<PagedResult<EmailMessageDto>> ListAsync(HttpClient admin, string query) =>
        (await admin.GetFromJsonAsync<PagedResult<EmailMessageDto>>($"/api/admin/mail{query}"))!;

    /// <summary>The dispatcher's backoff, skipped: each failed attempt is made due again until the job gives up.</summary>
    private async Task RunUntilGivenUpAsync()
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            await fixture.DrainOutboxAsync();
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            var waiting = await db.OutboxJobs.Where(j => j.Status == OutboxJobStatus.Failed).ToListAsync();
            if (waiting.Count == 0) return;
            foreach (var job in waiting) job.RunAfter = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
    }

    private async Task<HttpClient> AdminAsync()
    {
        var id = Guid.NewGuid();
        var email = $"mail-admin-{id:N}@example.test";
        await fixture.SeedUserAsync(id, email, role: "admin");
        return fixture.CreateAuthenticatedClient(id, email, "admin");
    }

    private async Task<Guid> SeedHouseholdAsync(string name, int members)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var ids = new List<Guid>();
        for (var i = 0; i < members; i++)
        {
            var id = Guid.NewGuid();
            await fixture.SeedUserAsync(id, $"member-{i}-{id:N}@example.test");
            ids.Add(id);
        }

        var household = new Household { Id = Guid.NewGuid(), Name = name, CreatedBy = ids[0], CreatedAt = DateTime.UtcNow };
        db.Households.Add(household);
        foreach (var (id, index) in ids.Select((id, index) => (id, index)))
        {
            db.HouseholdMembers.Add(new HouseholdMember
            {
                HouseholdId = household.Id,
                UserId = id,
                Role = index == 0 ? "owner" : "member",
                JoinedAt = DateTime.UtcNow.AddMinutes(index),
            });
        }

        await db.SaveChangesAsync();
        return household.Id;
    }
}
