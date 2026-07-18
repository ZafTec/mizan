using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public sealed class WorkoutSocialIntegrationTests
{
    private readonly ApiTestFixture _fixture;

    public WorkoutSocialIntegrationTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateWorkout_RejectsEmptyWorkout()
    {
        await _fixture.ResetDatabaseAsync();
        var (userId, email) = await SeedUserAsync("empty-workout");
        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/Workouts", new
        {
            name = "Empty workout",
            workoutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            exercises = Array.Empty<object>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        error!.ErrorCode.Should().Be("validation_failed");
    }

    [Fact]
    public async Task MarkNotificationRead_ReturnsNotFoundForUnknownNotification()
    {
        await _fixture.ResetDatabaseAsync();
        var (userId, email) = await SeedUserAsync("notification-owner");
        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var response = await client.PostAsync($"/api/Notifications/{Guid.NewGuid()}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAndDeleteWorkout_HideAnotherUsersWorkout()
    {
        await _fixture.ResetDatabaseAsync();
        var (ownerId, ownerEmail) = await SeedUserAsync("workout-owner");
        var exerciseId = await SeedExerciseAsync(ownerId);
        var (otherId, otherEmail) = await SeedUserAsync("workout-other");
        using var ownerClient = _fixture.CreateAuthenticatedClient(ownerId, ownerEmail);
        using var otherClient = _fixture.CreateAuthenticatedClient(otherId, otherEmail);
        var workoutId = await CreateWorkoutAsync(ownerClient, exerciseId);

        var updateResponse = await otherClient.PutAsJsonAsync($"/api/Workouts/{workoutId}", new
        {
            id = workoutId,
            name = "Stolen workout",
            workoutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            exercises = new[]
            {
                new
                {
                    exerciseId,
                    sets = new[] { new { reps = 5, weightKg = 100m, completed = true } }
                }
            }
        });
        var deleteResponse = await otherClient.DeleteAsync($"/api/Workouts/{workoutId}");

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ownerClient.GetAsync($"/api/Workouts/{workoutId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SaveTemplate_RejectsAnotherUsersCustomExercise()
    {
        await _fixture.ResetDatabaseAsync();
        var (ownerId, ownerEmail) = await SeedUserAsync("template-owner");
        var (otherId, _) = await SeedUserAsync("template-other");
        var inaccessibleExerciseId = await SeedExerciseAsync(otherId);
        using var client = _fixture.CreateAuthenticatedClient(ownerId, ownerEmail);

        var response = await client.PostAsJsonAsync("/api/WorkoutTemplates", new
        {
            name = "Invalid template",
            programName = (string?)null,
            sessionOrder = 1,
            notes = (string?)null,
            sortOrder = 0,
            isBuiltIn = false,
            exercises = new[]
            {
                new
                {
                    exerciseId = inaccessibleExerciseId,
                    sortOrder = 0,
                    sets = 3,
                    repsPerSet = 5,
                    targetWeightKg = 50m,
                    restSecondsMin = 60,
                    restSecondsMax = 120,
                    restSecondsFailure = 180,
                    supersetWithNext = false,
                    notes = (string?)null,
                    progressionType = "IncreaseAllEvenly",
                    progressionStrategy = "all",
                    progressionAmountKg = 2.5m,
                    targetType = "Reps",
                    targetSeconds = (int?)null,
                    targetDistanceMeters = (decimal?)null
                }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        error!.ErrorCode.Should().Be("domain_validation_failed");
    }

    [Fact]
    public async Task Feed_RequiresAcceptedFollowAndRevocationTakesEffectImmediately()
    {
        await _fixture.ResetDatabaseAsync();
        var (ownerId, ownerEmail) = await SeedUserAsync("feed-owner");
        var (followerId, followerEmail) = await SeedUserAsync("feed-follower");
        using var ownerClient = _fixture.CreateAuthenticatedClient(ownerId, ownerEmail);
        using var followerClient = _fixture.CreateAuthenticatedClient(followerId, followerEmail);
        await SaveSocialProfileAsync(ownerClient, "Owner");
        await SaveSocialProfileAsync(followerClient, "Follower");
        var ownerProfile = await ownerClient.GetFromJsonAsync<SocialProfileResponse>("/api/Social/profile");
        var feedItemId = await PublishFeedItemAsync(ownerClient);

        var followResponse = await followerClient.PostAsJsonAsync("/api/Social/follows", new { shareToken = ownerProfile!.ShareToken });
        followResponse.EnsureSuccessStatusCode();
        var follow = await followResponse.Content.ReadFromJsonAsync<IdResponse>();

        (await GetFeedAsync(followerClient)).Items.Should().BeEmpty();

        var acceptResponse = await ownerClient.PostAsJsonAsync($"/api/Social/follows/{follow!.Id}/respond", new { accept = true });
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetFeedAsync(followerClient)).Items.Should().ContainSingle(item => item.Id == feedItemId);

        var revokeResponse = await followerClient.DeleteAsync($"/api/Social/follows/{follow.Id}");
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetFeedAsync(followerClient)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Reaction_IsUniquePerUserItemAndEmoji()
    {
        await _fixture.ResetDatabaseAsync();
        var (userId, email) = await SeedUserAsync("reaction-owner");
        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        await SaveSocialProfileAsync(client, "Reaction owner");
        var feedItemId = await PublishFeedItemAsync(client);

        var first = await client.PostAsJsonAsync($"/api/Social/feed/{feedItemId}/reactions", new { emoji = "❤️" });
        var second = await client.PostAsJsonAsync($"/api/Social/feed/{feedItemId}/reactions", new { emoji = "❤️" });
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var firstResult = await first.Content.ReadFromJsonAsync<IdResponse>();
        var secondResult = await second.Content.ReadFromJsonAsync<IdResponse>();

        firstResult!.Id.Should().Be(secondResult!.Id);
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.FeedReactions.CountAsync(r => r.FeedItemId == feedItemId && r.UserId == userId && r.Emoji == "❤️")).Should().Be(1);
    }

    [Fact]
    public async Task DeleteComment_SoftDeletesAndRemovesItFromFeed()
    {
        await _fixture.ResetDatabaseAsync();
        var (userId, email) = await SeedUserAsync("comment-owner");
        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        await SaveSocialProfileAsync(client, "Comment owner");
        var feedItemId = await PublishFeedItemAsync(client);
        var createResponse = await client.PostAsJsonAsync($"/api/Social/feed/{feedItemId}/comments", new { body = "Keep the history" });
        createResponse.EnsureSuccessStatusCode();
        var comment = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        var deleteResponse = await client.DeleteAsync($"/api/Social/comments/{comment!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            var stored = await db.FeedComments.SingleAsync(c => c.Id == comment.Id);
            stored.DeletedAt.Should().NotBeNull();
            stored.DeletedByUserId.Should().Be(userId);
        }
        (await GetFeedAsync(client)).Items.Single(item => item.Id == feedItemId).Comments.Should().BeEmpty();
    }

    private async Task<(Guid Id, string Email)> SeedUserAsync(string prefix)
    {
        var id = Guid.NewGuid();
        var email = $"{prefix}-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        return (id, email);
    }

    private async Task<Guid> SeedExerciseAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Name = "Test Squat",
            Category = "Strength",
            MuscleGroup = "Legs",
            Equipment = "Barbell",
            IsCustom = true,
            IsApproved = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        return exercise.Id;
    }

    private static async Task<Guid> CreateWorkoutAsync(HttpClient client, Guid exerciseId)
    {
        var response = await client.PostAsJsonAsync("/api/Workouts", new
        {
            name = "Owned workout",
            workoutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            exercises = new[]
            {
                new
                {
                    exerciseId,
                    sets = new[] { new { reps = 5, weightKg = 100m, completed = true } }
                }
            }
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static async Task SaveSocialProfileAsync(HttpClient client, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/Social/profile", new
        {
            displayName,
            bio = (string?)null,
            avatarUrl = (string?)null,
            defaultPublishWorkouts = false
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<Guid> PublishFeedItemAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/Social/feed", new
        {
            type = "StreakMilestone",
            workoutId = (Guid?)null,
            templateId = (Guid?)null,
            achievementId = (Guid?)null,
            caption = "Still moving"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static async Task<FeedResponse> GetFeedAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/Social/feed");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FeedResponse>())!;
    }

    private sealed record ApiError(string ErrorCode);
    private sealed record IdResponse(Guid Id);
    private sealed record SocialProfileResponse(Guid UserId, string DisplayName, string? ShareToken);
    private sealed record FeedResponse(List<FeedItemResponse> Items);
    private sealed record FeedItemResponse(Guid Id, List<FeedCommentResponse> Comments);
    private sealed record FeedCommentResponse(Guid Id, string Body);
}

public sealed class LiftLogSeedCatalogTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public LiftLogSeedCatalogTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migration_SeedsExerciseTemplateAndAchievementCatalogs()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var exerciseNames = await db.Exercises.Where(e => !e.IsCustom).Select(e => e.Name).ToListAsync();
        exerciseNames.Should().HaveCountGreaterThanOrEqualTo(100);
        exerciseNames.Should().Contain(new[] { "Back Squat", "Bench Press", "Conventional Deadlift", "Treadmill Run", "Hamstring Stretch" });

        var templateNames = await db.WorkoutTemplates.Where(t => t.IsBuiltIn).Select(t => t.Name).ToListAsync();
        templateNames.Should().Contain(new[] { "Starting Strength A", "Starting Strength B", "StrongLifts 5x5 A", "StrongLifts 5x5 B", "PPL Push", "PPL Pull", "PPL Legs" });

        var achievementNames = await db.Achievements.Select(a => a.Name).ToListAsync();
        achievementNames.Should().Contain(new[] { "First Workout Shared", "First Follower", "Personal Best", "Hype Crew", "Training Partner" });
    }
}
