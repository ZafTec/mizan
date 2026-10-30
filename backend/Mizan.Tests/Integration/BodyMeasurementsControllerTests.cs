using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class BodyMeasurementsControllerTests
{
    private readonly ApiTestFixture _fixture;

    public BodyMeasurementsControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UserCanLogAndRetrieveBodyMeasurements()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"athlete-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var logCommand = new
        {
            Date = DateTime.UtcNow,
            WeightKg = 80.5m,
            BodyFatPercentage = 18.5m,
            Notes = "Morning measurement"
        };

        var logResponse = await client.PostAsJsonAsync("/api/BodyMeasurements", logCommand);
        logResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var logResult = await logResponse.Content.ReadFromJsonAsync<LogMeasurementResponse>();
        logResult.Should().NotBeNull();
        logResult!.Id.Should().NotBe(Guid.Empty);

        var listResponse = await client.GetAsync("/api/BodyMeasurements");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var measurements = await listResponse.Content.ReadFromJsonAsync<MeasurementPagedResult>();
        measurements.Should().NotBeNull();
        measurements!.Items.Should().Contain(m => m.WeightKg == 80.5m);
    }

    [Fact]
    public async Task UserCanDeleteOwnMeasurement()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"athlete-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var logCommand = new { Date = DateTime.UtcNow, WeightKg = 75.0m };
        var logResponse = await client.PostAsJsonAsync("/api/BodyMeasurements", logCommand);
        logResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var logResult = await logResponse.Content.ReadFromJsonAsync<LogMeasurementResponse>();
        logResult.Should().NotBeNull();
        var measurementId = logResult!.Id;

        var deleteResponse = await client.DeleteAsync($"/api/BodyMeasurements/{measurementId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await client.GetAsync("/api/BodyMeasurements");
        var measurements = await listResponse.Content.ReadFromJsonAsync<MeasurementPagedResult>();
        measurements!.Items.Should().NotContain(m => m.Id == measurementId);
    }

    /// <summary>
    /// The live finding: log_day wrote the morning weight, then log_workout's bodyweight wrote a second row
    /// for the same day. A day has one measurement; the later weight updates it, and the waist and the
    /// note stay: a workout's bodyweight carries no note, so it never replaces one.
    /// </summary>
    [Fact]
    public async Task A_person_who_logs_a_weight_and_then_a_workout_bodyweight_the_same_day_has_one_weight_the_later_one()
    {
        await _fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var email = $"one-weight-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);
        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        (await client.PostAsJsonAsync("/api/BodyMeasurements", new { Date = DateTime.UtcNow, WeightKg = 82.0m, WaistCm = 90m, Notes = "Morning, fasted" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Guid exerciseId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            var exercise = new Exercise
            {
                Id = Guid.NewGuid(), Name = "Weigh-in squat", Category = "Strength", MuscleGroup = "Legs",
                IsCustom = true, IsApproved = true, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow,
            };
            db.Exercises.Add(exercise);
            await db.SaveChangesAsync();
            exerciseId = exercise.Id;
        }
        var workout = await client.PostAsJsonAsync("/api/Workouts", new
        {
            name = "Legs",
            workoutDate = today,
            bodyweightKg = 81.6m,
            exercises = new[] { new { exerciseId, sets = new[] { new { reps = 5, weightKg = 60m, completed = true } } } },
        });
        workout.IsSuccessStatusCode.Should().BeTrue(await workout.Content.ReadAsStringAsync());

        var measurements = await client.GetFromJsonAsync<MeasurementPagedResult>("/api/BodyMeasurements");
        var day = measurements!.Items.Should().ContainSingle().Subject;
        day.WeightKg.Should().Be(81.6m);
        day.WaistCm.Should().Be(90m);
        day.Notes.Should().Be("Morning, fasted");
    }

    [Fact]
    public async Task UserCannotSeeOtherUsersMeasurements()
    {
        await _fixture.ResetDatabaseAsync();

        var userId1 = Guid.NewGuid();
        var email1 = $"athlete1-{userId1:N}@example.com";
        await _fixture.SeedUserAsync(userId1, email1);

        var userId2 = Guid.NewGuid();
        var email2 = $"athlete2-{userId2:N}@example.com";
        await _fixture.SeedUserAsync(userId2, email2);

        using var client1 = _fixture.CreateAuthenticatedClient(userId1, email1);
        await client1.PostAsJsonAsync("/api/BodyMeasurements", new { Date = DateTime.UtcNow, WeightKg = 90.0m });

        using var client2 = _fixture.CreateAuthenticatedClient(userId2, email2);
        var listResponse = await client2.GetAsync("/api/BodyMeasurements");
        var measurements = await listResponse.Content.ReadFromJsonAsync<MeasurementPagedResult>();
        measurements!.Items.Should().BeEmpty();
    }

    private sealed record MeasurementPagedResult(List<MeasurementItem> Items, int TotalCount);
    private sealed record MeasurementItem(Guid Id, decimal? WeightKg, decimal? BodyFatPercentage, decimal? WaistCm, DateTime Date, string? Notes = null);
    private sealed record LogMeasurementResponse(Guid Id);
}
