using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// A photo produces a proposal and nothing else. The property that matters is
/// the absence: however the call goes, no diary row appears until the user
/// posts one (docs/REFOCUS.md §12).
/// </summary>
[Collection("ApiIntegration")]
public class FoodPhotoTests
{
    /// <summary>A minimal JPEG header. The endpoint sniffs bytes, not the Content-Type.</summary>
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    private const string GoodAnalysis = """
        {"foods":[{"name":"Grilled chicken breast","portionGrams":150,"calories":248,"protein":46.5,"carbs":0,"fat":5.4}],
         "totalCalories":248,"confidence":0.72,"note":"Portion estimated from the plate."}
        """;

    private readonly ApiTestFixture _fixture;

    public FoodPhotoTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task APhotoComesBackAsStructuredFoods()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await ProUserAsync();
        _fixture.Ai.Reply(GoodAnalysis);

        var result = await AnalyzeAsync(client);

        result.Should().NotBeNull();
        result!.Foods.Should().ContainSingle();
        result.Foods[0].Name.Should().Be("Grilled chicken breast");
        result.Foods[0].PortionGrams.Should().Be(150);
        result.Confidence.Should().Be(0.72m);
    }

    [Fact]
    public async Task NothingIsWrittenToTheDiary()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await ProUserAsync();
        _fixture.Ai.Reply(GoodAnalysis);

        await AnalyzeAsync(client);

        (await DiaryCountAsync(userId)).Should().Be(0);
    }

    /// <summary>
    /// Prose where the schema was declared is a failed call, not something to
    /// scrape. The user gets an error rather than a plausible half-answer.
    /// </summary>
    [Fact]
    public async Task ProseInsteadOfJsonIsAFailedCall()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await ProUserAsync();
        _fixture.Ai.Reply("Looks like chicken and rice, maybe 600 calories?");

        var response = await PostPhotoAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await DiaryCountAsync(userId)).Should().Be(0);
    }

    /// <summary>A failed call still spent tokens at the provider, so it still counts.</summary>
    [Fact]
    public async Task AFailedAnalysisStillLandsInTheLedger()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await ProUserAsync();
        _fixture.Ai.Reply("Not JSON.");

        await PostPhotoAsync(client);

        var ledger = await LedgerAsync(userId);
        ledger.Should().ContainSingle(log => log.Feature == AiFeatures.FoodAnalysis);
    }

    [Fact]
    public async Task ANonImageIsRejectedBeforeItCostsAnything()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await ProUserAsync();
        var before = _fixture.Ai.Calls.Count;

        var response = await PostPhotoAsync(client, [0x25, 0x50, 0x44, 0x46]); // %PDF

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _fixture.Ai.Calls.Count.Should().Be(before);
    }

    [Fact]
    public async Task FreeUsersAreWalled()
    {
        await _fixture.ResetDatabaseAsync();
        var id = Guid.NewGuid();
        var email = $"photo-free-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        using var client = _fixture.CreateAuthenticatedClient(id, email);

        var response = await PostPhotoAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(HttpClient Client, Guid UserId)> ProUserAsync()
    {
        _fixture.Ai.Reset();
        var id = Guid.NewGuid();
        var email = $"photo-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        await _fixture.GrantProAsync(id);
        return (_fixture.CreateAuthenticatedClient(id, email), id);
    }

    private static async Task<FoodAnalysisResult?> AnalyzeAsync(HttpClient client)
    {
        var response = await PostPhotoAsync(client);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FoodAnalysisResult>();
    }

    private static async Task<HttpResponseMessage> PostPhotoAsync(HttpClient client, byte[]? bytes = null)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes ?? Jpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "image", "plate.jpg");
        return await client.PostAsync("/api/Nutrition/ai/analyze-image", content);
    }

    private async Task<int> DiaryCountAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.FoodDiaryEntries.CountAsync(e => e.UserId == userId);
    }

    private async Task<List<Domain.Entities.AiUsageLog>> LedgerAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.AiUsageLogs.AsNoTracking().Where(l => l.UserId == userId).ToListAsync();
    }
}
