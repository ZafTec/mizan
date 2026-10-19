using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// A write sent twice with one Idempotency-Key happens once. This is what lets a phone
/// that lost signal after sending retry without logging the same weight twice.
/// </summary>
[Collection("ApiIntegration")]
public class IdempotencyTests
{
    private const string Url = "/api/BodyMeasurements";
    private readonly ApiTestFixture _fixture;

    public IdempotencyTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ARetryWithTheSameKey_GetsTheFirstAnswer_AndWritesNothingNew()
    {
        using var client = await ClientAsync();
        var body = new { Date = DateTime.UtcNow, WeightKg = 75.0m };

        var first = await Send(client, body, "key-1");
        var second = await Send(client, body, "key-1");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.GetValues("Idempotent-Replayed").Should().Equal("true");
        first.Headers.Contains("Idempotent-Replayed").Should().BeFalse();
        (await second.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());
        (await CountAsync(client)).Should().Be(1);
    }

    [Fact]
    public async Task WithoutAKey_EveryRequestIsAWrite()
    {
        using var client = await ClientAsync();
        var body = new { Date = DateTime.UtcNow, WeightKg = 75.0m };

        await Send(client, body, key: null);
        await Send(client, body, key: null);

        (await CountAsync(client)).Should().Be(2);
    }

    [Fact]
    public async Task TheSameKeyForADifferentRequest_IsRefused_NotQuietlyReplayed()
    {
        using var client = await ClientAsync();
        await Send(client, new { Date = DateTime.UtcNow, WeightKg = 75.0m }, "key-2");

        var other = await Send(client, new { Date = DateTime.UtcNow, WeightKg = 90.0m }, "key-2");

        other.StatusCode.Should().Be((HttpStatusCode)422);
        (await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("idempotency_key_reused");
        (await CountAsync(client)).Should().Be(1);
    }

    [Fact]
    public async Task AKeyBelongsToTheUserWhoSentIt()
    {
        await _fixture.ResetDatabaseAsync();
        using var alice = await ClientAsync(reset: false);
        using var bob = await ClientAsync(reset: false);
        var body = new { Date = DateTime.UtcNow, WeightKg = 75.0m };

        await Send(alice, body, "shared-key");
        var bobs = await Send(bob, body, "shared-key");

        bobs.Headers.Contains("Idempotent-Replayed").Should().BeFalse("another user's key is not his");
        (await CountAsync(bob)).Should().Be(1);
    }

    [Fact]
    public async Task ARejectedRequest_IsNotRemembered_SoAFixedRetryRuns()
    {
        using var client = await ClientAsync();

        var bad = await Send(client, new { Date = DateTime.UtcNow, WeightKg = -5m }, "key-3");
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var good = await Send(client, new { Date = DateTime.UtcNow, WeightKg = -5m }, "key-3");
        good.Headers.Contains("Idempotent-Replayed").Should().BeFalse("a failed attempt left nothing behind to replay");
    }

    [Fact]
    public async Task AMalformedKey_IsRefused()
    {
        using var client = await ClientAsync();

        var response = await Send(client, new { Date = DateTime.UtcNow, WeightKg = 75.0m }, new string('x', 200));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("invalid_idempotency_key");
    }

    [Fact]
    public async Task TwoRequestsWithOneKeyAtOnce_WriteOnce()
    {
        using var client = await ClientAsync();
        var body = new { Date = DateTime.UtcNow, WeightKg = 75.0m };

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Send(client, body, "key-race")));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK && !r.Headers.Contains("Idempotent-Replayed")).Should().Be(1);
        responses.Where(r => r.StatusCode != HttpStatusCode.OK).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
        (await CountAsync(client)).Should().Be(1);
    }

    private async Task<HttpClient> ClientAsync(bool reset = true)
    {
        if (reset) await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        var email = $"idem-{user:N}@example.com";
        await _fixture.SeedUserAsync(user, email);
        return _fixture.CreateAuthenticatedClient(user, email);
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, object body, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = JsonContent.Create(body) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static async Task<int> CountAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>(Url)).GetProperty("items").GetArrayLength();
}
