using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// A coach asking about a client. Two properties: the intersection decides
/// what reaches the model, and the coach pays (docs/REFOCUS.md §11).
/// </summary>
[Collection("ApiIntegration")]
public class AiTrainerClientTests
{
    private readonly ApiTestFixture _fixture;

    public AiTrainerClientTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AGrantedAndConsentedAxisReachesTheModel()
    {
        await _fixture.ResetDatabaseAsync();
        var pair = await PairAsync(nutrition: true, workouts: false, measurements: false);
        await ConsentAsync(pair.ClientId, nutrition: true, training: true, body: true);
        await SeedMealAsync(pair.ClientId);
        _fixture.Ai.Reply("Their protein is holding up.");

        var answer = await AskAsync(pair, "How is their protein?");

        answer.AxesSeen.Should().Contain("nutrition");
        answer.AxesSeen.Should().NotContain("training");
    }

    /// <summary>
    /// The intersection: the client shares workouts with this coach but has
    /// not agreed to AI use of them, so the model never sees them.
    /// </summary>
    [Fact]
    public async Task AGrantWithoutAiConsentSendsNothing()
    {
        await _fixture.ResetDatabaseAsync();
        var pair = await PairAsync(nutrition: true, workouts: true, measurements: true);
        await ConsentAsync(pair.ClientId, nutrition: false, training: false, body: false);
        await SeedMealAsync(pair.ClientId);
        _fixture.Ai.Reply("They have not shared anything with me.");

        var answer = await AskAsync(pair, "How are they doing?");

        answer.AxesSeen.Should().BeEmpty();
        var systemTurns = _fixture.Ai.LastCall.Messages
            .Where(m => m.Role == AiRole.System)
            .Select(m => m.Content)
            .ToList();
        systemTurns.Should().Contain(c => c.Contains("shared nothing"));
    }

    [Fact]
    public async Task ConsentWithoutAGrantSendsNothing()
    {
        await _fixture.ResetDatabaseAsync();
        var pair = await PairAsync(nutrition: false, workouts: false, measurements: false);
        await ConsentAsync(pair.ClientId, nutrition: true, training: true, body: true);
        await SeedMealAsync(pair.ClientId);
        _fixture.Ai.Reply("Nothing shared with me.");

        var answer = await AskAsync(pair, "How are they doing?");

        answer.AxesSeen.Should().BeEmpty();
    }

    /// <summary>Read-only over client data, guaranteed by offering nothing to call.</summary>
    [Fact]
    public async Task NoToolsAreOfferedOnATrainerCall()
    {
        await _fixture.ResetDatabaseAsync();
        var pair = await PairAsync(nutrition: true, workouts: true, measurements: true);
        await ConsentAsync(pair.ClientId, nutrition: true, training: true, body: true);
        _fixture.Ai.Reply("Here is what I would suggest.");

        await AskAsync(pair, "Should they eat more?");

        _fixture.Ai.LastCall.Tools.Should().BeEmpty();
    }

    /// <summary>
    /// One coach with twenty clients must not leave twenty people
    /// rate-limited by questions they never asked.
    /// </summary>
    [Fact]
    public async Task TheCoachPaysAndTheClientDoesNot()
    {
        await _fixture.ResetDatabaseAsync();
        var pair = await PairAsync(nutrition: true, workouts: false, measurements: false);
        await ConsentAsync(pair.ClientId, nutrition: true, training: false, body: false);
        _fixture.Ai.Reply("Fine.");

        await AskAsync(pair, "How are they doing?");

        (await LedgerAsync(pair.TrainerId)).Should()
            .ContainSingle(log => log.Feature == AiFeatures.TrainerClient);
        (await LedgerAsync(pair.ClientId)).Should().BeEmpty();
    }

    /// <summary>
    /// The coach's client questions must not eat the coach's own chat
    /// allowance either - they are separate lines.
    /// </summary>
    [Fact]
    public async Task ClientQuestionsDoNotSpendTheCoachesOwnChatAllowance()
    {
        await _fixture.ResetDatabaseAsync();
        var pair = await PairAsync(nutrition: true, workouts: false, measurements: false);
        await ConsentAsync(pair.ClientId, nutrition: true, training: false, body: false);

        // The fixture's free tier is three requests a day.
        for (var i = 0; i < 5; i++)
        {
            _fixture.Ai.Reply("Fine.");
            await AskAsync(pair, $"Question {i}?");
        }

        var snapshot = await SnapshotAsync(pair.TrainerId);
        snapshot.RequestsUsed.Should().Be(0, "client questions are on the trainer line");
    }

    [Fact]
    public async Task SomeoneElsesClientIsRefused()
    {
        await _fixture.ResetDatabaseAsync();
        var pair = await PairAsync(nutrition: true, workouts: true, measurements: true);
        var stranger = await UserAsync();

        var response = await pair.Client.PostAsJsonAsync(
            $"/api/Ai/clients/{stranger}/ask", new { threadId = (Guid?)null, message = "Tell me about them." });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task APendingRelationshipIsNotEnoughToAsk()
    {
        await _fixture.ResetDatabaseAsync();
        var pair = await PairAsync(nutrition: true, workouts: true, measurements: true, status: "pending");

        var response = await pair.Client.PostAsJsonAsync(
            $"/api/Ai/clients/{pair.ClientId}/ask",
            new { threadId = (Guid?)null, message = "How are they doing?" });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnOrdinaryUserCannotAskAboutAnyone()
    {
        await _fixture.ResetDatabaseAsync();
        var id = await UserAsync();
        var other = await UserAsync();
        using var client = _fixture.CreateAuthenticatedClient(id, $"trainer-ai-{id:N}@example.com");

        var response = await client.PostAsJsonAsync(
            $"/api/Ai/clients/{other}/ask", new { threadId = (Guid?)null, message = "Tell me." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The coach's working notes are theirs; a client must not find them in their history.</summary>
    [Fact]
    public async Task TheThreadBelongsToTheCoach()
    {
        await _fixture.ResetDatabaseAsync();
        var pair = await PairAsync(nutrition: true, workouts: false, measurements: false);
        await ConsentAsync(pair.ClientId, nutrition: true, training: false, body: false);
        _fixture.Ai.Reply("Fine.");

        var answer = await AskAsync(pair, "How are they doing?");

        (await ThreadOwnerAsync(answer.ThreadId)).Should().Be(pair.TrainerId);
    }

    private record Pair(Guid TrainerId, Guid ClientId, HttpClient Client);

    private async Task<Guid> UserAsync()
    {
        var id = Guid.NewGuid();
        await _fixture.SeedUserAsync(id, $"trainer-ai-{id:N}@example.com");
        return id;
    }

    private async Task<Pair> PairAsync(
        bool nutrition, bool workouts, bool measurements, string status = "active")
    {
        _fixture.Ai.Reset();

        var trainerId = Guid.NewGuid();
        var trainerEmail = $"coach-{trainerId:N}@example.com";
        await _fixture.SeedUserAsync(trainerId, trainerEmail, role: "trainer");
        var clientId = await UserAsync();

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            db.TrainerClientRelationships.Add(new TrainerClientRelationship
            {
                Id = Guid.CreateVersion7(),
                TrainerId = trainerId,
                ClientId = clientId,
                Status = status,
                CanViewNutrition = nutrition,
                CanViewWorkouts = workouts,
                CanViewMeasurements = measurements,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        return new Pair(
            trainerId, clientId, _fixture.CreateAuthenticatedClient(trainerId, trainerEmail, "trainer"));
    }

    private async Task ConsentAsync(Guid userId, bool nutrition, bool training, bool body)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.UserAiConsents.Add(new UserAiConsent
        {
            UserId = userId,
            Enabled = nutrition || training || body,
            ShareNutrition = nutrition,
            ShareTraining = training,
            ShareBody = body,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedMealAsync(Guid userId)
    {
        var food = await _fixture.SeedFoodAsync($"Chicken {Guid.NewGuid():N}", 165, 31, 0, 3.6m);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.FoodDiaryEntries.Add(new FoodDiaryEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FoodId = food.Id,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "LUNCH",
            Servings = 2,
            Calories = 330,
            ProteinGrams = 62,
            CarbsGrams = 0,
            FatGrams = 7.2m,
            LoggedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<AiTrainerAnswerDto> AskAsync(Pair pair, string message)
    {
        var response = await pair.Client.PostAsJsonAsync(
            $"/api/Ai/clients/{pair.ClientId}/ask", new { threadId = (Guid?)null, message });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AiTrainerAnswerDto>())!;
    }

    private async Task<List<AiUsageLog>> LedgerAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.AiUsageLogs.AsNoTracking().Where(l => l.UserId == userId).ToListAsync();
    }

    private async Task<AiQuotaSnapshot> SnapshotAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAiQuotaService>()
            .GetUserSnapshotAsync(userId);
    }

    private async Task<Guid> ThreadOwnerAsync(Guid threadId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.AiChatThreads.AsNoTracking()
            .Where(t => t.Id == threadId).Select(t => t.UserId).FirstAsync();
    }
}
