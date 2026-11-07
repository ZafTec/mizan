using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Phones that receive push, and the notification that reaches them. Delivery to Firebase
/// itself is not exercised here; the fake sender stands in for it.
/// </summary>
[Collection("ApiIntegration")]
public class PushTests
{
    private readonly ApiTestFixture _fixture;

    public PushTests(ApiTestFixture fixture) => _fixture = fixture;

    // ---- registering a phone ----

    [Fact]
    public async Task ARegisteredPhone_AppearsInTheList_AndRegisteringAgainOnlyRefreshesIt()
    {
        var (_, client) = await ClientAsync();

        var first = await client.PostAsJsonAsync("/api/Devices", new { Token = "tok-1", Platform = "android", DeviceName = "Pixel" });
        var second = await client.PostAsJsonAsync("/api/Devices", new { Token = "tok-1", Platform = "android", DeviceName = "Pixel 9" });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid().Should().Be(id);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/Devices");
        list.GetArrayLength().Should().Be(1);
        list[0].GetProperty("deviceName").GetString().Should().Be("Pixel 9");
        list[0].ToString().Should().NotContain("tok-1", "the push address is never sent back");
    }

    [Fact]
    public async Task APhoneMovesToWhoeverSignsInOnItNext()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, alice) = await ClientAsync(reset: false);
        var (_, bob) = await ClientAsync(reset: false);

        await alice.PostAsJsonAsync("/api/Devices", new { Token = "shared-phone", Platform = "android" });
        await bob.PostAsJsonAsync("/api/Devices", new { Token = "shared-phone", Platform = "android" });

        (await alice.GetFromJsonAsync<JsonElement>("/api/Devices")).GetArrayLength().Should().Be(0);
        (await bob.GetFromJsonAsync<JsonElement>("/api/Devices")).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SigningOut_RemovesOnlyYourOwnPhone()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, alice) = await ClientAsync(reset: false);
        var (_, bob) = await ClientAsync(reset: false);
        var id = (await (await alice.PostAsJsonAsync("/api/Devices", new { Token = "a-phone", Platform = "android" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await bob.DeleteAsync($"/api/Devices/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await alice.DeleteAsync($"/api/Devices/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await alice.GetFromJsonAsync<JsonElement>("/api/Devices")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task BadDeviceRegistrations_AreRefused()
    {
        var (_, client) = await ClientAsync();

        (await client.PostAsJsonAsync("/api/Devices", new { Token = "", Platform = "android" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/Devices", new { Token = "t", Platform = "ios" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- a notification becomes a push ----

    [Fact]
    public async Task ANotification_IsPushedToEveryPhoneOfThatUser()
    {
        var (user, client) = await ClientAsync();
        await client.PostAsJsonAsync("/api/Devices", new { Token = "phone-1", Platform = "android" });
        await client.PostAsJsonAsync("/api/Devices", new { Token = "phone-2", Platform = "android" });

        await NotifyAsync(user, "Dinner is planned");
        await _fixture.DrainOutboxAsync();

        _fixture.Push.Sent.Select(m => m.Token).Should().BeEquivalentTo("phone-1", "phone-2");
        _fixture.Push.Sent.Should().OnlyContain(m => m.Title == "Dinner is planned" && m.NotificationId != null);
    }

    [Fact]
    public async Task NobodyIsPushedWhoHasNoPhone_AndNothingIsQueuedForThem()
    {
        var (user, _) = await ClientAsync();

        await NotifyAsync(user, "Nobody is listening");
        await _fixture.DrainOutboxAsync();

        _fixture.Push.Sent.Should().BeEmpty();
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.OutboxJobs.CountAsync(j => j.Type == "push")).Should().Be(0);
    }

    [Fact]
    public async Task WithoutPushCredentials_NothingIsQueued_AndTheNotificationStillSaves()
    {
        var (user, client) = await ClientAsync();
        await client.PostAsJsonAsync("/api/Devices", new { Token = "phone-1", Platform = "android" });
        _fixture.Push.IsConfigured = false;

        await NotifyAsync(user, "Still saved");
        await _fixture.DrainOutboxAsync();

        _fixture.Push.Sent.Should().BeEmpty();
        (await client.GetFromJsonAsync<JsonElement>("/api/Notifications")).GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task APhoneTheProviderNoLongerKnows_IsForgotten()
    {
        var (user, client) = await ClientAsync();
        await client.PostAsJsonAsync("/api/Devices", new { Token = "uninstalled", Platform = "android" });
        await client.PostAsJsonAsync("/api/Devices", new { Token = "alive", Platform = "android" });
        _fixture.Push.Answer("uninstalled", PushOutcome.TokenInvalid);

        await NotifyAsync(user, "Hello");
        await _fixture.DrainOutboxAsync();

        var devices = await client.GetFromJsonAsync<JsonElement>("/api/Devices");
        devices.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task WhenEverySendFails_TheJobIsRetriedLater()
    {
        var (user, client) = await ClientAsync();
        await client.PostAsJsonAsync("/api/Devices", new { Token = "down-1", Platform = "android" });
        _fixture.Push.Answer("down-1", PushOutcome.Failed);

        await NotifyAsync(user, "Try me again");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _fixture.DrainOutboxAsync(maxPasses: 1));

        (await PushJobAsync()).Status.Should().Be(Mizan.Domain.Entities.OutboxJobStatus.Failed, "a failed job waits for its retry");
    }

    [Fact]
    public async Task WhenSomePhonesHeard_TheJobIsDone_SoTheyAreNotNotifiedTwice()
    {
        var (user, client) = await ClientAsync();
        await client.PostAsJsonAsync("/api/Devices", new { Token = "up", Platform = "android" });
        await client.PostAsJsonAsync("/api/Devices", new { Token = "down", Platform = "android" });
        _fixture.Push.Answer("down", PushOutcome.Failed);

        await NotifyAsync(user, "Once is enough");
        await _fixture.DrainOutboxAsync();

        (await PushJobAsync()).Status.Should().Be(Mizan.Domain.Entities.OutboxJobStatus.Succeeded);
        _fixture.Push.Sent.Count(m => m.Token == "up").Should().Be(1);
    }

    private async Task<Mizan.Domain.Entities.OutboxJob> PushJobAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.OutboxJobs.SingleAsync(j => j.Type == "push");
    }

    // ---- helpers ----

    private async Task<(Guid User, HttpClient Client)> ClientAsync(bool reset = true)
    {
        if (reset)
        {
            await _fixture.ResetDatabaseAsync();
            _fixture.Push.Reset();
        }

        var user = Guid.NewGuid();
        var email = $"push-{user:N}@example.com";
        await _fixture.SeedUserAsync(user, email);
        return (user, _fixture.CreateAuthenticatedClient(user, email));
    }

    private async Task NotifyAsync(Guid user, string title)
    {
        using var scope = _fixture.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<INotificationWriter>();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        await writer.AddAsync(user, "test", title);
        await db.SaveChangesAsync();
    }
}
