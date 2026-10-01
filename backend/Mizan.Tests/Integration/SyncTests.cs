using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// An app that keeps a copy of the user's data asks what changed since it last looked. It must
/// see new and edited records, learn of deletions, never miss a record at a page boundary,
/// and never see another user's data.
/// </summary>
[Collection("ApiIntegration")]
public class SyncTests
{
    private readonly ApiTestFixture _fixture;

    public SyncTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task TheFirstSync_ReturnsEverythingTheUserHas()
    {
        var (user, client) = await ClientAsync();
        await LogMeasurementAsync(client, 80m);
        await LogWorkoutAsync(client, await SeedExerciseAsync(user));
        await SeedDiaryEntryAsync(user, "Oats");
        await NotifyAsync(user, "Hello");

        var sync = await ChangesAsync(client);

        sync.GetProperty("resyncRequired").GetBoolean().Should().BeFalse();
        sync.GetProperty("bodyMeasurements").GetArrayLength().Should().Be(1);
        sync.GetProperty("workouts").GetArrayLength().Should().Be(1);
        sync.GetProperty("workouts")[0].GetProperty("exercises")[0].GetProperty("sets").GetArrayLength().Should().Be(1);
        sync.GetProperty("diaryEntries")[0].GetProperty("name").GetString().Should().Be("Oats");
        sync.GetProperty("diaryEntries")[0].GetProperty("entryDate").GetString().Should().NotBeNullOrEmpty();
        sync.GetProperty("notifications").EnumerateArray().Should().Contain(n => n.GetProperty("title").GetString() == "Hello");
        sync.GetProperty("hasMore").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ASecondSync_ReturnsOnlyWhatChangedSinceTheCursor()
    {
        var (user, client) = await ClientAsync();
        await LogMeasurementAsync(client, 80m);
        var first = await ChangesAsync(client);
        var cursor = first.GetProperty("nextSince").GetDateTime();

        await BackdateAsync(user, cursor.AddMinutes(-10));
        await LogMeasurementAsync(client, 79m);

        var second = await ChangesAsync(client, since: cursor);

        var weights = second.GetProperty("bodyMeasurements").EnumerateArray().Select(m => m.GetProperty("weightKg").GetDecimal()).ToList();
        weights.Should().Equal(79m);
    }

    [Fact]
    public async Task ADeletion_IsReportedAsATombstone()
    {
        var (user, client) = await ClientAsync();
        var id = await LogMeasurementAsync(client, 80m);
        var cursor = (await ChangesAsync(client)).GetProperty("nextSince").GetDateTime();
        await BackdateAsync(user, cursor.AddMinutes(-10));

        (await client.DeleteAsync($"/api/BodyMeasurements/{id}")).EnsureSuccessStatusCode();
        var sync = await ChangesAsync(client, since: cursor);

        var deleted = sync.GetProperty("deleted").EnumerateArray().Single();
        deleted.GetProperty("type").GetString().Should().Be("body_measurement");
        deleted.GetProperty("id").GetGuid().Should().Be(id);
        sync.GetProperty("bodyMeasurements").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ADeletedWorkout_IsATombstone_AndEditingOnlyItsSetsCountsAsEditingTheWorkout()
    {
        var (user, client) = await ClientAsync();
        var exercise = await SeedExerciseAsync(user);
        await LogWorkoutAsync(client, exercise);
        var workout = (await ChangesAsync(client)).GetProperty("workouts")[0];
        var id = workout.GetProperty("id").GetGuid();
        var cursor = DateTime.UtcNow;
        await BackdateAsync(user, cursor.AddMinutes(-10));

        // Same name and date; only the weight on the set differs.
        var update = await client.PutAsJsonAsync($"/api/Workouts/{id}", new
        {
            id,
            name = workout.GetProperty("name").GetString(),
            workoutDate = workout.GetProperty("workoutDate").GetString(),
            exercises = new[] { new { exerciseId = exercise, sets = new[] { new { reps = 5, weightKg = 120m, completed = true } } } },
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent, await update.Content.ReadAsStringAsync());

        var changed = await ChangesAsync(client, since: cursor);
        changed.GetProperty("workouts").GetArrayLength().Should().Be(1, "the workout is what the app holds a copy of");
        changed.GetProperty("workouts")[0].GetProperty("exercises")[0].GetProperty("sets")[0].GetProperty("weightKg").GetDecimal().Should().Be(120m);

        var afterEdit = DateTime.UtcNow;
        await BackdateAsync(user, afterEdit.AddMinutes(-10));
        var del = await client.DeleteAsync($"/api/Workouts/{id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent, await del.Content.ReadAsStringAsync());
        var gone = await ChangesAsync(client, since: afterEdit);
        gone.GetProperty("deleted").EnumerateArray().Should().ContainSingle(d => d.GetProperty("type").GetString() == "workout" && d.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task ReadingANotification_CountsAsAChange()
    {
        var (user, client) = await ClientAsync();
        await NotifyAsync(user, "Read me");
        var first = await ChangesAsync(client);
        var id = first.GetProperty("notifications")[0].GetProperty("id").GetGuid();
        var cursor = DateTime.UtcNow;
        await BackdateAsync(user, cursor.AddMinutes(-10));

        (await client.PostAsync($"/api/Notifications/{id}/read", null)).EnsureSuccessStatusCode();
        var sync = await ChangesAsync(client, since: cursor);

        sync.GetProperty("notifications").EnumerateArray().Single().GetProperty("readAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task APageBoundary_NeverHidesARecord_EvenWhenManyShareOneTimestamp()
    {
        var (user, client) = await ClientAsync();

        // One save stamps every row alike, which is the worst case for a cursor.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            for (var i = 0; i < 7; i++)
                db.BodyMeasurements.Add(new BodyMeasurement { Id = Guid.NewGuid(), UserId = user, MeasurementDate = DateOnly.FromDateTime(DateTime.UtcNow), WeightKg = 70 + i, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var seen = new HashSet<Guid>();
        DateTime? cursor = null;
        for (var pass = 0; pass < 20; pass++)
        {
            var sync = await ChangesAsync(client, since: cursor, limit: 3);
            foreach (var m in sync.GetProperty("bodyMeasurements").EnumerateArray()) seen.Add(m.GetProperty("id").GetGuid());
            cursor = sync.GetProperty("nextSince").GetDateTime();
            if (!sync.GetProperty("hasMore").GetBoolean()) break;
            pass.Should().BeLessThan(19, "the cursor must keep moving forward");
        }

        seen.Should().HaveCount(7);
    }

    [Fact]
    public async Task ManyPages_AreWalkedInOrder_AcrossSeparateSaves()
    {
        var (user, client) = await ClientAsync();
        for (var i = 0; i < 6; i++) await LogMeasurementAsync(client, 70 + i);

        var seen = new List<Guid>();
        DateTime? cursor = null;
        for (var pass = 0; pass < 20; pass++)
        {
            var sync = await ChangesAsync(client, since: cursor, limit: 2);
            seen.AddRange(sync.GetProperty("bodyMeasurements").EnumerateArray().Select(m => m.GetProperty("id").GetGuid()));
            cursor = sync.GetProperty("nextSince").GetDateTime();
            if (!sync.GetProperty("hasMore").GetBoolean()) break;
        }

        seen.Distinct().Should().HaveCount(6);
    }

    [Fact]
    public async Task AnAppAwayLongerThanDeletionsAreKept_IsToldToStartOver()
    {
        var (_, client) = await ClientAsync();

        var sync = await ChangesAsync(client, since: DateTime.UtcNow.AddDays(-91));

        sync.GetProperty("resyncRequired").GetBoolean().Should().BeTrue();
        sync.GetProperty("bodyMeasurements").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task OneUsersChanges_AreNeverShownToAnother()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, alice) = await ClientAsync(reset: false);
        var (_, bob) = await ClientAsync(reset: false);
        await LogMeasurementAsync(alice, 80m);

        (await ChangesAsync(bob)).GetProperty("bodyMeasurements").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ItNeedsASignedInUser()
    {
        using var anonymous = _fixture.CreateClient();

        (await anonymous.GetAsync("/api/Sync/changes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- helpers ----

    private async Task<(Guid User, HttpClient Client)> ClientAsync(bool reset = true)
    {
        if (reset) await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        var email = $"sync-{user:N}@example.com";
        await _fixture.SeedUserAsync(user, email);
        return (user, _fixture.CreateAuthenticatedClient(user, email));
    }

    private static async Task<JsonElement> ChangesAsync(HttpClient client, DateTime? since = null, int limit = 200)
    {
        var url = $"/api/Sync/changes?limit={limit}" + (since is null ? "" : $"&since={Uri.EscapeDataString(since.Value.ToUniversalTime().ToString("O"))}");
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Guid> LogMeasurementAsync(HttpClient client, decimal weight)
    {
        var response = await client.PostAsJsonAsync("/api/BodyMeasurements", new { Date = DateTime.UtcNow, WeightKg = weight });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task LogWorkoutAsync(HttpClient client, Guid exercise)
    {
        var response = await client.PostAsJsonAsync("/api/Workouts", new
        {
            name = "Legs",
            workoutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            exercises = new[] { new { exerciseId = exercise, sets = new[] { new { reps = 5, weightKg = 100m, completed = true } } } },
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> SeedExerciseAsync(Guid user)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var exercise = new Exercise { Id = Guid.NewGuid(), Name = "Sync Squat", Category = "strength", IsCustom = true, IsApproved = true, CreatedByUserId = user, CreatedAt = DateTime.UtcNow };
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        return exercise.Id;
    }

    private async Task SeedDiaryEntryAsync(Guid user, string name)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.FoodDiaryEntries.Add(new FoodDiaryEntry
        {
            Id = Guid.NewGuid(), UserId = user, Name = name, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow), MealType = "breakfast",
            Servings = 1, Calories = 300, LoggedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task NotifyAsync(Guid user, string title)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = user, Type = "test", Title = title, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    /// <summary>Moves everything the user has back in time, so the next write is the only recent one.</summary>
    private async Task BackdateAsync(Guid user, DateTime to)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        await db.BodyMeasurements.Where(m => m.UserId == user).ExecuteUpdateAsync(s => s.SetProperty(m => m.UpdatedAt, to));
        await db.Workouts.Where(w => w.UserId == user).ExecuteUpdateAsync(s => s.SetProperty(w => w.UpdatedAt, to));
        await db.FoodDiaryEntries.Where(e => e.UserId == user).ExecuteUpdateAsync(s => s.SetProperty(e => e.UpdatedAt, to));
        await db.Notifications.Where(n => n.UserId == user).ExecuteUpdateAsync(s => s.SetProperty(n => n.CreatedAt, to));
        await db.DeletedRecords.Where(d => d.UserId == user).ExecuteUpdateAsync(s => s.SetProperty(d => d.DeletedAt, to));
    }
}
