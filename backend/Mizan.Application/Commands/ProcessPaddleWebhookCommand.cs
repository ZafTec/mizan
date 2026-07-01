using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record ProcessPaddleWebhookCommand(string RawJson) : IRequest<ProcessPaddleWebhookResult>;

public record ProcessPaddleWebhookResult
{
    public bool Handled { get; init; }
    public bool Duplicate { get; init; }
}

public class ProcessPaddleWebhookCommandHandler
    : IRequestHandler<ProcessPaddleWebhookCommand, ProcessPaddleWebhookResult>
{
    private readonly IMizanDbContext _context;
    private readonly IEntitlementService _entitlements;
    private readonly PaddleOptions _options;
    private readonly ILogger<ProcessPaddleWebhookCommandHandler> _logger;

    public ProcessPaddleWebhookCommandHandler(
        IMizanDbContext context,
        IEntitlementService entitlements,
        IOptions<PaddleOptions> options,
        ILogger<ProcessPaddleWebhookCommandHandler> logger)
    {
        _context = context;
        _entitlements = entitlements;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProcessPaddleWebhookResult> Handle(ProcessPaddleWebhookCommand request, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(request.RawJson);
        var root = doc.RootElement;

        var eventId = GetString(root, "event_id");
        var eventType = GetString(root, "event_type");
        if (eventId is null || eventType is null)
        {
            _logger.LogWarning("Paddle webhook missing event_id or event_type; ignoring");
            return new ProcessPaddleWebhookResult { Handled = false };
        }

        // Idempotency: Paddle redelivers events. Record first, skip if already seen.
        var alreadyProcessed = await _context.PaddleWebhookEvents
            .AnyAsync(e => e.EventId == eventId, cancellationToken);
        if (alreadyProcessed)
        {
            _logger.LogInformation("Paddle webhook {EventId} ({EventType}) already processed; skipping", eventId, eventType);
            return new ProcessPaddleWebhookResult { Handled = true, Duplicate = true };
        }

        _context.PaddleWebhookEvents.Add(new PaddleWebhookEvent
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = eventType,
            ReceivedAt = DateTime.UtcNow
        });

        if (!root.TryGetProperty("data", out var data))
        {
            await _context.SaveChangesAsync(cancellationToken);
            return new ProcessPaddleWebhookResult { Handled = false };
        }

        // Paddle can deliver multiple events for one checkout (e.g. subscription.created
        // and subscription.trialing) in close succession. Two concurrent requests can
        // both see "no row for this user" and both try to insert, tripping the unique
        // index on user_id. Detect that race and retry once, which re-resolves against
        // the row the other request just committed.
        Guid? affectedUserId = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (eventType.StartsWith("subscription.", StringComparison.Ordinal))
            {
                affectedUserId = ApplySubscriptionEvent(data, eventType);
            }
            else if (eventType == "transaction.completed")
            {
                affectedUserId = ApplyLifetimeIfApplicable(data);
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException ex) when (attempt == 0 && IsDuplicateUserSubscription(ex))
            {
                _logger.LogInformation("Concurrent webhook created the subscription row first for {EventId}; retrying as update", eventId);
                DetachPendingSubscriptionInserts();
            }
        }

        if (affectedUserId is not null)
        {
            await _entitlements.InvalidateAsync(affectedUserId.Value, cancellationToken);
        }

        return new ProcessPaddleWebhookResult { Handled = true };
    }

    private Guid? ApplySubscriptionEvent(JsonElement data, string eventType)
    {
        var userId = TryGetUserId(data);
        var subscriptionId = GetString(data, "id");
        var customerId = GetString(data, "customer_id");
        var status = GetString(data, "status") ?? "active";
        var priceId = FirstPriceId(data);
        var periodEnd = GetNestedDateTime(data, "current_billing_period", "ends_at");

        var sub = Resolve(userId, subscriptionId, customerId);
        if (sub is null)
        {
            if (userId is null)
            {
                _logger.LogWarning("Subscription event {EventType} for {SubId} has no resolvable user; skipping", eventType, subscriptionId);
                return null;
            }

            sub = NewSubscription(userId.Value);
            _context.Subscriptions.Add(sub);
        }

        if (!sub.IsLifetime)
        {
            sub.Plan = "pro";
        }
        sub.Status = status;
        sub.PaddleSubscriptionId = subscriptionId ?? sub.PaddleSubscriptionId;
        sub.PaddleCustomerId = customerId ?? sub.PaddleCustomerId;
        sub.PaddlePriceId = priceId ?? sub.PaddlePriceId;
        sub.CurrentPeriodEnd = periodEnd ?? sub.CurrentPeriodEnd;
        sub.TrialEndsAt = status == "trialing" ? (periodEnd ?? sub.TrialEndsAt) : sub.TrialEndsAt;
        sub.CanceledAt = status == "canceled"
            ? (GetDateTime(data, "canceled_at") ?? DateTime.UtcNow)
            : sub.CanceledAt;
        sub.UpdatedAt = DateTime.UtcNow;
        return sub.UserId;
    }

    private Guid? ApplyLifetimeIfApplicable(JsonElement data)
    {
        // Lifetime is a one-time purchase: a completed transaction with no subscription
        // whose items include the configured Lifetime price.
        var subscriptionId = GetString(data, "subscription_id");
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            return null; // subscription-related charge, handled by subscription.* events
        }

        if (string.IsNullOrEmpty(_options.LifetimePriceId) || !ContainsPriceId(data, _options.LifetimePriceId))
        {
            return null;
        }

        var userId = TryGetUserId(data);
        var customerId = GetString(data, "customer_id");

        var sub = Resolve(userId, null, customerId);
        if (sub is null)
        {
            if (userId is null)
            {
                _logger.LogWarning("Lifetime transaction has no resolvable user; skipping");
                return null;
            }

            sub = NewSubscription(userId.Value);
            _context.Subscriptions.Add(sub);
        }

        sub.Plan = "lifetime";
        sub.IsLifetime = true;
        sub.Status = "active";
        sub.PaddleCustomerId = customerId ?? sub.PaddleCustomerId;
        sub.PaddlePriceId = _options.LifetimePriceId;
        sub.UpdatedAt = DateTime.UtcNow;
        return sub.UserId;
    }

    private static bool IsDuplicateUserSubscription(DbUpdateException ex) =>
        ex.InnerException is System.Data.Common.DbException { SqlState: "23505" } dbEx
            && dbEx.Message.Contains("IX_subscriptions_user_id", StringComparison.Ordinal);

    private void DetachPendingSubscriptionInserts()
    {
        if (_context is not DbContext dbContext)
        {
            return;
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<Subscription>()
            .Where(e => e.State == EntityState.Added)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private Subscription? Resolve(Guid? userId, string? subscriptionId, string? customerId)
    {
        // Local lookups against tracked + DB; one subscription row per user (unique index).
        if (userId is not null)
        {
            var byUser = _context.Subscriptions.Local.FirstOrDefault(s => s.UserId == userId)
                ?? _context.Subscriptions.FirstOrDefault(s => s.UserId == userId);
            if (byUser is not null) return byUser;
        }

        if (!string.IsNullOrEmpty(subscriptionId))
        {
            var bySub = _context.Subscriptions.FirstOrDefault(s => s.PaddleSubscriptionId == subscriptionId);
            if (bySub is not null) return bySub;
        }

        if (!string.IsNullOrEmpty(customerId))
        {
            var byCustomer = _context.Subscriptions.FirstOrDefault(s => s.PaddleCustomerId == customerId);
            if (byCustomer is not null) return byCustomer;
        }

        return null;
    }

    private static Subscription NewSubscription(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Guid? TryGetUserId(JsonElement data)
    {
        if (!data.TryGetProperty("custom_data", out var cd) || cd.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var raw = GetString(cd, "user_id") ?? GetString(cd, "userId");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static string? FirstPriceId(JsonElement data)
    {
        if (!data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("price", out var price) && price.TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }

        return null;
    }

    private static bool ContainsPriceId(JsonElement data, string priceId)
    {
        if (!data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("price", out var price)
                && price.TryGetProperty("id", out var id)
                && id.GetString() == priceId)
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? GetDateTime(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(v.GetString(), out var dto)
            ? dto.UtcDateTime
            : null;

    private static DateTime? GetNestedDateTime(JsonElement el, string parent, string child) =>
        el.TryGetProperty(parent, out var obj) && obj.ValueKind == JsonValueKind.Object
            ? GetDateTime(obj, child)
            : null;
}
