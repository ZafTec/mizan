using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Mizan.Application.Queries;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The Paddle webhook is anonymous, so its signature is the only thing that
/// stops a forged "subscription active" event. A genuine event must also end
/// the cached Free entitlement at once rather than when the cache expires.
/// </summary>
[Collection("ApiIntegration")]
public class PaddleWebhookTests(ApiTestFixture fixture)
{
    private static string Payload(Guid userId, string eventId) => JsonSerializer.Serialize(new
    {
        event_id = eventId,
        event_type = "subscription.created",
        data = new
        {
            id = "sub_test",
            customer_id = "ctm_test",
            status = "active",
            custom_data = new { user_id = userId },
            items = new[] { new { price = new { id = "pri_monthly" } } },
            current_billing_period = new { ends_at = "2030-01-01T00:00:00Z" }
        }
    });

    private static string Sign(string body, string secret, long? timestamp = null)
    {
        var ts = (timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return $"ts={ts};h1={Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}:{body}")))}";
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string body, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/paddle")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (signature is not null) request.Headers.Add("Paddle-Signature", signature);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task ForgedOrStaleSignaturesAreRejected_AndAGenuineEventGrantsProImmediately()
    {
        await fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var email = $"buyer-{userId:N}@example.com";
        await fixture.SeedUserAsync(userId, email);
        using var user = fixture.CreateAuthenticatedClient(userId, email);
        using var paddle = fixture.CreateClient();
        var body = Payload(userId, $"evt_{Guid.NewGuid():N}");

        (await user.GetFromJsonAsync<MySubscriptionDto>("/api/Subscriptions/me"))!.IsPro.Should().BeFalse();

        (await PostAsync(paddle, body, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await PostAsync(paddle, body, Sign(body, "wrong-secret"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await PostAsync(paddle, body, Sign(body, ApiTestFixture.PaddleWebhookSecret, DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds())))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await PostAsync(paddle, body + " ", Sign(body, ApiTestFixture.PaddleWebhookSecret))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await user.GetFromJsonAsync<MySubscriptionDto>("/api/Subscriptions/me"))!.IsPro.Should().BeFalse();

        (await PostAsync(paddle, body, Sign(body, ApiTestFixture.PaddleWebhookSecret))).StatusCode.Should().Be(HttpStatusCode.OK);
        var subscription = await user.GetFromJsonAsync<MySubscriptionDto>("/api/Subscriptions/me");
        subscription!.IsPro.Should().BeTrue();
        subscription.Status.Should().Be("active");

        // Paddle redelivers; the second copy is acknowledged and changes nothing.
        (await PostAsync(paddle, body, Sign(body, ApiTestFixture.PaddleWebhookSecret))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnotherAppsEventForTheSameCustomer_LeavesTheMizanSubscriptionAlone()
    {
        await fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var email = $"shared-{userId:N}@example.com";
        await fixture.SeedUserAsync(userId, email);
        using var user = fixture.CreateAuthenticatedClient(userId, email);
        using var paddle = fixture.CreateClient();

        var mizan = Payload(userId, $"evt_{Guid.NewGuid():N}");
        (await PostAsync(paddle, mizan, Sign(mizan, ApiTestFixture.PaddleWebhookSecret))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await user.GetFromJsonAsync<MySubscriptionDto>("/api/Subscriptions/me"))!.IsPro.Should().BeTrue();

        // A Convia subscription on the same Paddle account and the same
        // customer (Paddle keeps one customer per email), now canceled.
        var convia = JsonSerializer.Serialize(new
        {
            event_id = $"evt_{Guid.NewGuid():N}",
            event_type = "subscription.canceled",
            data = new
            {
                id = "sub_convia",
                customer_id = "ctm_test",
                status = "canceled",
                custom_data = new { product = "convia", org_id = "org_1", user_id = "7s6h6RwyVIB9norQZawEWf76op42ncfx" },
                items = new[] { new { price = new { id = "pri_convia_pro" } } },
                canceled_at = "2026-09-25T00:00:00Z"
            }
        });
        (await PostAsync(paddle, convia, Sign(convia, ApiTestFixture.PaddleWebhookSecret))).StatusCode.Should().Be(HttpStatusCode.OK);

        var subscription = await user.GetFromJsonAsync<MySubscriptionDto>("/api/Subscriptions/me");
        subscription!.IsPro.Should().BeTrue();
        subscription.Status.Should().Be("active");
    }
}
