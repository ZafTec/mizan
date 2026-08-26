using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class SubscriptionsControllerTests
{
    private readonly ApiTestFixture _fixture;

    public SubscriptionsControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetBillingPortal_NoPaddleCustomer_ReturnsNotFound()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Paddle.Reset();

        var userId = Guid.NewGuid();
        var email = $"portal-free-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var response = await client.PostAsync("/api/Subscriptions/portal", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _fixture.Paddle.LastCustomerId.Should().BeNull();
    }

    [Fact]
    public async Task GetBillingPortal_WithPaddleCustomer_ReturnsPortalUrls()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Paddle.Reset();

        var userId = Guid.NewGuid();
        var email = $"portal-pro-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);
        await _fixture.GrantProWithPaddleAsync(userId, "ctm_test_123", "sub_test_456");

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var response = await client.PostAsync("/api/Subscriptions/portal", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var portal = await response.Content.ReadFromJsonAsync<BillingPortalResponse>();
        portal.Should().NotBeNull();
        portal!.OverviewUrl.Should().Contain("ctm_test_123");
        portal.CancelSubscriptionUrl.Should().Contain("sub_test_456");
        portal.UpdatePaymentMethodUrl.Should().Contain("sub_test_456");

        _fixture.Paddle.LastCustomerId.Should().Be("ctm_test_123");
        _fixture.Paddle.LastSubscriptionId.Should().Be("sub_test_456");
    }

    [Fact]
    public async Task GetBillingPortal_LifetimePlan_ReturnsOverviewOnly()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Paddle.Reset();

        var userId = Guid.NewGuid();
        var email = $"portal-lifetime-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);
        await _fixture.GrantProWithPaddleAsync(userId, "ctm_lifetime_789", null);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var response = await client.PostAsync("/api/Subscriptions/portal", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var portal = await response.Content.ReadFromJsonAsync<BillingPortalResponse>();
        portal.Should().NotBeNull();
        portal!.OverviewUrl.Should().Contain("ctm_lifetime_789");
        portal.CancelSubscriptionUrl.Should().BeNull();
        portal.UpdatePaymentMethodUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetBillingPortal_PaddleUnreachable_ReturnsBadGateway()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Paddle.Reset();
        _fixture.Paddle.FailNext();

        var userId = Guid.NewGuid();
        var email = $"portal-down-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);
        await _fixture.GrantProWithPaddleAsync(userId, "ctm_down_000", "sub_down_000");

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var response = await client.PostAsync("/api/Subscriptions/portal", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    private sealed record BillingPortalResponse(
        string OverviewUrl,
        string? CancelSubscriptionUrl,
        string? UpdatePaymentMethodUrl);
}
