using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Media;
using Mizan.Contracts.Nutrition;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// A photo produces a proposal and nothing else. The property that matters is
/// the absence: however the call goes, no diary row appears until the user
/// posts one (docs/AI.md).
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

    /// <summary>
    /// The proposal carries each food per 100 g, so a client sends the grams the
    /// person confirmed and the server does the scaling (docs/AI.md).
    /// </summary>
    [Fact]
    public async Task EachFoodComesWithItsNutritionPer100g()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await ProUserAsync();
        _fixture.Ai.Reply(GoodAnalysis);

        var response = await PostPhotoAsync(client);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        var per100g = json.GetProperty("foods")[0].GetProperty("per100g");
        per100g.GetProperty("calories").GetDecimal().Should().Be(165.33m);
        per100g.GetProperty("proteinGrams").GetDecimal().Should().Be(31m);
        per100g.GetProperty("carbsGrams").GetDecimal().Should().Be(0m);
        per100g.GetProperty("fatGrams").GetDecimal().Should().Be(3.6m);
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

    /// <summary>
    /// A client that holds the photo at an address passes the address, and the API downloads it, so the bytes
    /// never pass through the client's model (docs/MCP.md#images).
    /// </summary>
    [Fact]
    public async Task APhotoAtAPublicAddress_IsDownloadedAndAnalyzedLikeAnUploadedOne()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await ProUserAsync();
        _fixture.Ai.Reply(GoodAnalysis);
        const string url = "https://images.example.com/plate.jpg";
        _fixture.RemoteImages.Serve(url, Jpeg);

        var response = await client.PostAsJsonAsync("/api/Nutrition/ai/analyze-image-url", new ImageUrlRequest(url));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<FoodAnalysisResult>();
        result!.Foods.Should().ContainSingle().Which.Name.Should().Be("Grilled chicken breast");
        _fixture.RemoteImages.Requested.Should().Contain(url);
        (await DiaryCountAsync(userId)).Should().Be(0);
    }

    [Theory]
    [InlineData("http://images.example.com/plate.jpg")]
    [InlineData("https://images.example.com:8443/plate.jpg")]
    [InlineData("https://user:secret@images.example.com/plate.jpg")]
    [InlineData("https://localhost/plate.jpg")]
    [InlineData("https://127.0.0.1/plate.jpg")]
    [InlineData("https://10.0.0.5/plate.jpg")]
    [InlineData("https://192.168.1.20/plate.jpg")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://[::1]/plate.jpg")]
    [InlineData("https://[fd00::1]/plate.jpg")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url")]
    public async Task AnAddressThatIsNotPublicHttps_IsRefusedBeforeAnythingIsDownloaded(string url)
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await ProUserAsync();
        var requested = _fixture.RemoteImages.Requested.Count;

        var response = await client.PostAsJsonAsync("/api/Nutrition/ai/analyze-image-url", new ImageUrlRequest(url));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("imageUrl must");
        _fixture.RemoteImages.Requested.Count.Should().Be(requested);
        _fixture.Ai.Calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData("pdf", "does not serve a JPEG")]
    [InlineData("redirect", "redirects elsewhere")]
    [InlineData("missing", "answered 404")]
    [InlineData("huge", "larger than 4 MB")]
    public async Task WhatIsAtTheAddressMustBeAnImageOfAtMost4Mb_OrNothingIsSpent(string kind, string message)
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await ProUserAsync();
        var url = $"https://images.example.com/{kind}-{Guid.NewGuid():N}";
        switch (kind)
        {
            case "pdf": _fixture.RemoteImages.Serve(url, [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37]); break;
            case "redirect": _fixture.RemoteImages.Redirect(url, "https://10.0.0.5/plate.jpg"); break;
            case "huge": _fixture.RemoteImages.Serve(url, [.. Jpeg, .. new byte[4 * 1024 * 1024]]); break;
        }

        var response = await client.PostAsJsonAsync("/api/Nutrition/ai/analyze-image-url", new ImageUrlRequest(url));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(message);
        _fixture.RemoteImages.Requested.Should().NotContain("https://10.0.0.5/plate.jpg", "a redirect is never followed");
        _fixture.Ai.Calls.Should().BeEmpty();
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

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
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
