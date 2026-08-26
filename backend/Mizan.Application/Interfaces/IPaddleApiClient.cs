namespace Mizan.Application.Interfaces;

/// <summary>
/// The outbound half of the Paddle integration. Everything else in this
/// codebase's Paddle handling is inbound - the webhook pipeline reacts to
/// what Paddle tells it. This is the one place Mizan calls Paddle back, to
/// mint a customer portal session: a page Paddle hosts and Paddle secures,
/// where a subscriber cancels, changes plan, or updates a card without any
/// of that ever touching our servers or our database directly.
/// </summary>
public interface IPaddleApiClient
{
    /// <summary>
    /// Null when Paddle refuses the request (the customer id is stale, or
    /// Paddle is unreachable) - the caller shows "try again" rather than a
    /// portal link that would 404.
    /// </summary>
    Task<PaddlePortalSession?> CreatePortalSessionAsync(
        string customerId, string? subscriptionId, CancellationToken cancellationToken);
}

/// <summary>
/// The links to send the browser to. All three are single-use and
/// short-lived - Paddle regenerates them per session, so nothing here is
/// ever cached or shown twice.
/// </summary>
public record PaddlePortalSession(
    string OverviewUrl,
    string? CancelSubscriptionUrl,
    string? UpdatePaymentMethodUrl);
