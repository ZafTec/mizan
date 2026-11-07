using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Mizan.Contracts.Planning;
using Mizan.Contracts.Users;
using Mizan.Domain.Entities;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Notifications in Telegram are opt-in: a linked Pro account gets them in its chat only after
/// turning them on, the API refuses the switch without a linked chat, and unlinking turns it off.
/// The notification here is a coach's plan draft, made through the API and sent by the outbox.
/// </summary>
[Collection("ApiIntegration")]
public class TelegramNotificationTests
{
    private readonly ApiTestFixture _fixture;

    public TelegramNotificationTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ALinkedProUser_WhoOptsIn_GetsNotificationsInTelegram_AndOneWhoDoesNot_DoesNot()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (coachId, coach) = await world.PersonAsync("coach", role: "trainer");
        var (optedInId, optedIn) = await world.PersonAsync("opted-in");
        var (optedOutId, optedOut) = await world.PersonAsync("opted-out");
        var (_, unlinked) = await world.PersonAsync("unlinked");
        foreach (var id in new[] { optedInId, optedOutId })
        {
            await _fixture.GrantProAsync(id);
            await world.CoachingAsync(coachId, id, canPlanMeals: true);
        }
        await world.DbAsync(db =>
        {
            db.TelegramLinks.Add(new TelegramLink { Id = Guid.NewGuid(), UserId = optedInId, TelegramUserId = 1001, LinkedAt = DateTime.UtcNow });
            db.TelegramLinks.Add(new TelegramLink { Id = Guid.NewGuid(), UserId = optedOutId, TelegramUserId = 1002, LinkedAt = DateTime.UtcNow });
            return db.SaveChangesAsync();
        });

        // Linking alone starts nothing.
        (await MeAsync(optedIn)).NotifyByTelegram.Should().BeFalse();

        // Without a linked chat the switch is refused; with one it turns on.
        var refused = await unlinked.PutAsJsonAsync("/api/Users/me", new UpdateUserRequest(null, null, NotifyByTelegram: true));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()
            .Should().Be("Connect a Telegram chat before turning on Telegram notifications.");
        (await MeAsync(unlinked)).NotifyByTelegram.Should().BeFalse();

        var turnedOn = await optedIn.PutAsJsonAsync("/api/Users/me", new UpdateUserRequest(null, null, NotifyByTelegram: true));
        turnedOn.StatusCode.Should().Be(HttpStatusCode.NoContent, await turnedOn.Content.ReadAsStringAsync());
        (await MeAsync(optedIn)).NotifyByTelegram.Should().BeTrue();

        var planIds = new Dictionary<Guid, Guid>();
        foreach (var clientId in new[] { optedInId, optedOutId })
        {
            var drafted = await coach.PostAsJsonAsync($"/api/Trainers/clients/{clientId}/meal-plans", new CreateMealPlanRequest
            {
                Name = "Week",
                StartDate = new DateOnly(2026, 10, 26),
                DurationDays = 7,
            });
            drafted.StatusCode.Should().Be(HttpStatusCode.Created, await drafted.Content.ReadAsStringAsync());
            planIds[clientId] = (await drafted.Content.ReadFromJsonAsync<MealPlanCreatedResult>())!.Id;
        }
        await _fixture.DrainOutboxAsync();

        // Both have the notification in the app; only the one who opted in has it in Telegram.
        foreach (var client in new[] { optedIn, optedOut })
        {
            var notifications = await client.GetFromJsonAsync<JsonElement>("/api/Notifications");
            notifications.GetProperty("items").EnumerateArray().Should().ContainSingle()
                .Which.GetProperty("type").GetString().Should().Be("meal_plan_drafted");
        }
        var sent = _fixture.TelegramBot.Sent.Should().ContainSingle().Subject;
        sent.TelegramUserId.Should().Be(1001);
        sent.Text.Should().Be("Test User drafted a meal plan for you\n\"Week\" is waiting for you to accept or decline.");
        sent.OpenPath.Should().Be($"plan?planId={planIds[optedInId]:D}");

        // Disconnecting the chat turns the switch off again.
        (await optedIn.DeleteAsync("/api/Telegram/link")).EnsureSuccessStatusCode();
        (await MeAsync(optedIn)).NotifyByTelegram.Should().BeFalse();
    }

    private static async Task<UserDto> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<UserDto>("/api/Users/me"))!;
}
