using Mizan.Application.Billing;

namespace Mizan.Application.Interfaces;

/// <summary>
/// The outbound half of the Paddle integration: the catalogue an admin edits,
/// the changes a subscriber makes from the billing page, and the hosted portal
/// for card updates. Paddle stays the authority; each call returns what Paddle
/// now holds so the caller stores that, not what it asked for.
///
/// Calls that change or read billing state throw <see cref="PaddleRequestException"/>
/// on failure. The portal keeps its null contract.
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

    /// <summary>The Paddle product tagged custom_data.plan = <paramref name="plan"/>, created if there is none.</summary>
    Task<string> EnsureProductAsync(string plan, string name, string description, CancellationToken cancellationToken);

    Task<PaddlePrice> CreatePriceAsync(PaddlePriceRequest request, CancellationToken cancellationToken);
    Task ArchivePriceAsync(string priceId, CancellationToken cancellationToken);

    /// <summary>Active recurring prices of a product.</summary>
    Task<IReadOnlyList<PaddlePrice>> ListPricesAsync(string productId, CancellationToken cancellationToken);

    /// <summary>Returns the new discount's id.</summary>
    Task<string> CreateDiscountAsync(PaddleDiscountRequest request, CancellationToken cancellationToken);
    Task ArchiveDiscountAsync(string discountId, CancellationToken cancellationToken);

    Task<PaddleChangePreview> PreviewPriceChangeAsync(string subscriptionId, string priceId, CancellationToken cancellationToken);
    Task<PaddleSubscriptionState> ChangePriceAsync(string subscriptionId, string priceId, CancellationToken cancellationToken);

    /// <summary>Schedules cancellation for the end of the billing period. Access continues until then.</summary>
    Task<PaddleSubscriptionState> CancelAtPeriodEndAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>Withdraws a scheduled cancellation.</summary>
    Task<PaddleSubscriptionState> RemoveScheduledChangeAsync(string subscriptionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaddleTransaction>> ListTransactionsAsync(string customerId, CancellationToken cancellationToken);

    /// <summary>A short-lived invoice PDF link, or null when the transaction is not this customer's or has no invoice.</summary>
    Task<string?> GetInvoiceUrlAsync(string transactionId, string customerId, CancellationToken cancellationToken);
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

public record PaddlePriceRequest(
    string ProductId,
    string Name,
    string? Description,
    string Interval,
    int AmountCents,
    string Currency,
    int? TrialDays);

public record PaddlePrice(
    string Id,
    string ProductId,
    string Name,
    string? Description,
    string? Interval,
    int AmountCents,
    string Currency,
    int? TrialDays);

public record PaddleDiscountRequest(
    string Label,
    string? Code,
    string Type,
    decimal Amount,
    string Currency,
    bool Recurring,
    int? MaximumRecurringIntervals,
    DateTime? ExpiresAt,
    IReadOnlyList<string> RestrictToPriceIds);

/// <summary>What a plan change costs. <see cref="DueNowCents"/> is negative when it leaves a credit.</summary>
public record PaddleChangePreview(
    int DueNowCents,
    string Currency,
    DateTime? NextBilledAt,
    int? NextAmountCents);

public record PaddleTransaction(
    string Id,
    string Status,
    DateTime? BilledAt,
    int TotalCents,
    string Currency,
    string? InvoiceNumber);

/// <summary>
/// Paddle refused a request or could not be reached. <see cref="Unavailable"/>
/// separates "try again" (network, 5xx, 429) from a refusal the caller should
/// show, such as a change blocked by a pending payment.
/// </summary>
public class PaddleRequestException : Exception
{
    public PaddleRequestException(int? statusCode, string? code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int? StatusCode { get; }
    public string? Code { get; }
    public bool Unavailable => StatusCode is null or >= 500 or 429;
}
