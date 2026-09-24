using Mizan.Application.Billing;
using Mizan.Application.Interfaces;

namespace Mizan.Tests.Integration;

/// <summary>
/// A small in-memory Paddle, so no test reaches the real one. It keeps the
/// products, prices, discounts, and subscriptions that calls create, answers
/// the way Paddle does, and records what was asked for. A test can script a
/// failure to exercise the 502 and 422 paths.
/// </summary>
public sealed class FakePaddleApiClient : IPaddleApiClient
{
    private readonly object _lock = new();
    private bool _fail;
    private PaddleRequestException? _refusal;
    private int _sequence;

    public string? LastCustomerId { get; private set; }
    public string? LastSubscriptionId { get; private set; }

    public Dictionary<string, PaddlePrice> Prices { get; } = new();
    public HashSet<string> ArchivedPrices { get; } = new();
    public Dictionary<string, PaddleDiscountRequest> Discounts { get; } = new();
    public HashSet<string> ArchivedDiscounts { get; } = new();
    public Dictionary<string, PaddleSubscriptionState> Subscriptions { get; } = new();
    public List<PaddleTransaction> Transactions { get; } = new();
    public string? ProductId { get; private set; }

    public void Reset()
    {
        lock (_lock)
        {
            _fail = false;
            _refusal = null;
            LastCustomerId = null;
            LastSubscriptionId = null;
            Prices.Clear();
            ArchivedPrices.Clear();
            Discounts.Clear();
            ArchivedDiscounts.Clear();
            Subscriptions.Clear();
            Transactions.Clear();
            ProductId = null;
        }
    }

    /// <summary>The next call (and every call after, until Reset) fails as if Paddle were unreachable.</summary>
    public void FailNext()
    {
        lock (_lock) _fail = true;
    }

    /// <summary>Every call after this is refused by Paddle with <paramref name="code"/>, until Reset.</summary>
    public void RefuseNext(string code, string detail)
    {
        lock (_lock) _refusal = new PaddleRequestException(400, code, detail);
    }

    /// <summary>A Paddle subscription as checkout would have created it.</summary>
    public PaddleSubscriptionState SeedSubscription(string subscriptionId, string customerId, string priceId, string status = "active")
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var state = new PaddleSubscriptionState
            {
                Id = subscriptionId,
                CustomerId = customerId,
                Status = status,
                PriceId = priceId,
                CurrentPeriodEnd = now.AddDays(30),
                NextBilledAt = now.AddDays(30),
                UpdatedAt = now
            };
            Subscriptions[subscriptionId] = state;
            return state;
        }
    }

    public Task<PaddlePortalSession?> CreatePortalSessionAsync(
        string customerId, string? subscriptionId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            LastCustomerId = customerId;
            LastSubscriptionId = subscriptionId;

            if (_fail)
            {
                return Task.FromResult<PaddlePortalSession?>(null);
            }
        }

        var session = new PaddlePortalSession(
            OverviewUrl: $"https://sandbox-customer-portal.paddle.com/{customerId}/overview",
            CancelSubscriptionUrl: subscriptionId is null
                ? null
                : $"https://sandbox-customer-portal.paddle.com/{customerId}/subscriptions/{subscriptionId}/cancel",
            UpdatePaymentMethodUrl: subscriptionId is null
                ? null
                : $"https://sandbox-customer-portal.paddle.com/{customerId}/subscriptions/{subscriptionId}/payment-method");

        return Task.FromResult<PaddlePortalSession?>(session);
    }

    public Task<string> EnsureProductAsync(string plan, string name, string description, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Guard();
            ProductId ??= "pro_fake_product";
            return Task.FromResult(ProductId);
        }
    }

    public Task<PaddlePrice> CreatePriceAsync(PaddlePriceRequest request, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Guard();
            var price = new PaddlePrice(
                $"pri_fake_{++_sequence}", request.ProductId, request.Name, request.Description,
                request.Interval, request.AmountCents, request.Currency, request.TrialDays);
            Prices[price.Id] = price;
            return Task.FromResult(price);
        }
    }

    public Task ArchivePriceAsync(string priceId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Guard();
            ArchivedPrices.Add(priceId);
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<PaddlePrice>> ListPricesAsync(string productId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Guard();
            IReadOnlyList<PaddlePrice> active = Prices.Values
                .Where(p => p.ProductId == productId && !ArchivedPrices.Contains(p.Id))
                .ToList();
            return Task.FromResult(active);
        }
    }

    /// <summary>A price that exists in Paddle but not in Mizan, as if made in the Paddle dashboard.</summary>
    public PaddlePrice SeedPrice(string name, string interval, int amountCents)
    {
        lock (_lock)
        {
            ProductId ??= "pro_fake_product";
            var price = new PaddlePrice($"pri_fake_{++_sequence}", ProductId, name, null, interval, amountCents, "USD", null);
            Prices[price.Id] = price;
            return price;
        }
    }

    public Task<string> CreateDiscountAsync(PaddleDiscountRequest request, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Guard();
            var id = $"dsc_fake_{++_sequence}";
            Discounts[id] = request;
            return Task.FromResult(id);
        }
    }

    public Task ArchiveDiscountAsync(string discountId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Guard();
            ArchivedDiscounts.Add(discountId);
            return Task.CompletedTask;
        }
    }

    public Task<PaddleChangePreview> PreviewPriceChangeAsync(string subscriptionId, string priceId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Guard();
            var sub = Subscriptions[subscriptionId];
            var from = Prices.TryGetValue(sub.PriceId ?? string.Empty, out var current) ? current.AmountCents : 0;
            var to = Prices[priceId].AmountCents;
            return Task.FromResult(new PaddleChangePreview(to - from, "USD", sub.NextBilledAt, to));
        }
    }

    public Task<PaddleSubscriptionState> ChangePriceAsync(string subscriptionId, string priceId, CancellationToken cancellationToken)
    {
        return Update(subscriptionId, s => s with { PriceId = priceId });
    }

    public Task<PaddleSubscriptionState> CancelAtPeriodEndAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        return Update(subscriptionId, s => s with { ScheduledChangeAction = "cancel", ScheduledChangeAt = s.CurrentPeriodEnd });
    }

    public Task<PaddleSubscriptionState> RemoveScheduledChangeAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        return Update(subscriptionId, s => s with { ScheduledChangeAction = null, ScheduledChangeAt = null });
    }

    public Task<IReadOnlyList<PaddleTransaction>> ListTransactionsAsync(string customerId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Guard();
            LastCustomerId = customerId;
            return Task.FromResult<IReadOnlyList<PaddleTransaction>>(Transactions.ToList());
        }
    }

    public Task<string?> GetInvoiceUrlAsync(string transactionId, string customerId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Guard();
            LastCustomerId = customerId;
            // Fake transactions belong to the customer named in their id.
            return Task.FromResult<string?>(transactionId.Contains(customerId, StringComparison.Ordinal)
                ? $"https://sandbox-invoices.paddle.com/{transactionId}.pdf"
                : null);
        }
    }

    private Task<PaddleSubscriptionState> Update(string subscriptionId, Func<PaddleSubscriptionState, PaddleSubscriptionState> change)
    {
        lock (_lock)
        {
            Guard();
            LastSubscriptionId = subscriptionId;
            var updated = change(Subscriptions[subscriptionId]) with { UpdatedAt = DateTime.UtcNow };
            Subscriptions[subscriptionId] = updated;
            return Task.FromResult(updated);
        }
    }

    private void Guard()
    {
        if (_fail)
        {
            throw new PaddleRequestException(null, null, "Could not reach Paddle. Try again in a moment.");
        }

        if (_refusal is not null)
        {
            throw _refusal;
        }
    }
}
