using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Exceptions;
using Mizan.Contracts.Ai;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Mizan behind a gateway (docs/AI.md#behind-a-gateway): its refusals reach the person as plain
/// answers with their own codes, a refused turn keeps nothing, and an administrator rotates the key
/// that signs the end-user token while the gateway keeps fetching both halves of the rotation.
/// </summary>
[Collection("ApiIntegration")]
public class AiGatewayStoryTests
{
    private readonly ApiTestFixture _fixture;

    public AiGatewayStoryTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AQuestionTheGatewayBlocks_IsAnsweredPlainly_AndTheThreadKeepsNeitherSide()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        _fixture.Ai.Reply("Fine.");
        var turn = await (await client.PostAsJsonAsync("/api/Ai/chat", new { threadId = (Guid?)null, message = "First question?" }))
            .Content.ReadFromJsonAsync<AiChatTurnDto>();

        _fixture.Ai.Refuse(new AiBlockedException());
        var continued = await client.PostAsJsonAsync("/api/Ai/chat", new { threadId = turn!.ThreadId, message = "Blocked question?" });
        _fixture.Ai.Refuse(new AiBlockedException());
        var opened = await client.PostAsJsonAsync("/api/Ai/chat", new { threadId = (Guid?)null, message = "Another blocked question?" });

        foreach (var response in new[] { continued, opened })
        {
            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("errorCode").GetString().Should().Be("ai_blocked");
            body.GetProperty("error").GetString().Should().Be("I can't help with that.");
        }

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.AiChatThreads.CountAsync(t => t.UserId == userId)).Should().Be(1, "a blocked first message opens no thread");
        (await db.AiChatMessages.Where(m => m.ThreadId == turn.ThreadId).Select(m => m.Content).ToListAsync())
            .Should().BeEquivalentTo("First question?", "Fine.");
    }

    [Fact]
    public async Task ARateLimitedQuestion_SaysWhenToTryAgain_AndItsQuotaIsSettled()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        _fixture.Ai.Refuse(new AiRateLimitedException(TimeSpan.FromSeconds(120)));

        var response = await client.PostAsJsonAsync("/api/Ai/chat", new { threadId = (Guid?)null, message = "How am I doing?" });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(120));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("ai_rate_limited");
        body.GetProperty("error").GetString().Should().Be("The assistant is busy right now. Try again in 2 minutes.");
        body.GetProperty("retryAfterSeconds").GetInt32().Should().Be(120);

        using var scope = _fixture.Services.CreateScope();
        var ledger = await scope.ServiceProvider.GetRequiredService<MizanDbContext>().AiUsageLogs.AsNoTracking()
            .Where(l => l.UserId == userId).ToListAsync();
        ledger.Should().ContainSingle().Which.Outcome.Should().NotBe(AiCallOutcome.Pending, "a refused call is settled too");
    }

    [Fact]
    public async Task AnAdminRotatesTheSigningKey_AndTheGatewayFetchesBothKeysDuringTheRotation()
    {
        await _fixture.ResetDatabaseAsync();
        using var anonymous = _fixture.CreateClient();
        (await anonymous.GetAsync("/.well-known/jwks.json")).StatusCode.Should().Be(HttpStatusCode.NotFound, "no key is published until one is set");

        var adminId = Guid.NewGuid();
        var email = $"gateway-admin-{adminId:N}@example.com";
        await _fixture.SeedUserAsync(adminId, email, role: "admin");
        using var admin = _fixture.CreateAuthenticatedClient(adminId, email, "admin");
        foreach (var (key, value) in new[] { ("EndUser.Header", "X-End-User"), ("EndUser.Issuer", "https://mizan.test"), ("EndUser.Audience", "app-test") })
        {
            var saved = await admin.PutAsJsonAsync($"/api/admin/settings/{key}", new { value });
            saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        }

        var first = await RotateAsync(admin);
        first.GetProperty("previousKeyId").ValueKind.Should().Be(JsonValueKind.Null);
        (await KeysAsync(anonymous)).Select(k => k.GetProperty("kid").GetString()).Should().Equal(first.GetProperty("keyId").GetString());

        var second = await RotateAsync(admin);
        second.GetProperty("previousKeyId").GetString().Should().Be(first.GetProperty("keyId").GetString());
        foreach (var address in new[] { "/.well-known/jwks.json", "/api/.well-known/jwks.json" })
        {
            var keys = await KeysAsync(anonymous, address);
            keys.Select(k => k.GetProperty("kid").GetString()).Should().Equal(
                [second.GetProperty("keyId").GetString(), first.GetProperty("keyId").GetString()],
                "the previous key stays published so the tokens it signed still verify");
            keys.Should().OnlyContain(k => k.GetProperty("alg").GetString() == "ES256" && !k.EnumerateObject().Any(p => p.Name == "d"), "the private part is never served");
        }

        var listing = await admin.GetFromJsonAsync<JsonElement>("/api/admin/settings");
        listing.GetRawText().Should().NotContain("PRIVATE KEY", "the signing key is a secret and never comes back");
    }

    private static async Task<JsonElement> RotateAsync(HttpClient admin)
    {
        var response = await admin.PostAsync("/api/admin/settings/end-user-key/rotate", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<List<JsonElement>> KeysAsync(HttpClient client, string address = "/.well-known/jwks.json")
    {
        var response = await client.GetAsync(address);
        response.StatusCode.Should().Be(HttpStatusCode.OK, address);
        response.Headers.CacheControl!.Public.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("keys").EnumerateArray().ToList();
    }

    private async Task<(HttpClient Client, Guid UserId)> UserAsync()
    {
        _fixture.Ai.Reset();
        var id = Guid.NewGuid();
        var email = $"gateway-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        return (_fixture.CreateAuthenticatedClient(id, email), id);
    }
}
