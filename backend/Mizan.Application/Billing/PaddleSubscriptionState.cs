using System.Text.Json;
using Mizan.Domain.Entities;

namespace Mizan.Application.Billing;

/// <summary>
/// The parts of a Paddle subscription entity Mizan stores. Webhooks and API
/// responses carry the same entity, so both go through <see cref="Parse"/> and
/// <see cref="ApplyTo"/>: a change made from the billing page is stored the
/// moment Paddle confirms it, and the webhook that follows finds nothing new.
/// </summary>
public sealed record PaddleSubscriptionState
{
    /// <summary>
    /// Mizan's value of custom_data.product. One Paddle account serves several
    /// ZafTech apps, and every notification destination receives all of their
    /// events, so each app tags its checkouts and products.
    /// </summary>
    public const string ProductTag = "mizan";

    public string? Id { get; init; }
    public string? CustomerId { get; init; }
    public string Status { get; init; } = "active";
    public string? PriceId { get; init; }
    public Guid? UserId { get; init; }
    public DateTime? CurrentPeriodEnd { get; init; }
    public DateTime? NextBilledAt { get; init; }
    public DateTime? CanceledAt { get; init; }
    public string? ScheduledChangeAction { get; init; }
    public DateTime? ScheduledChangeAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static PaddleSubscriptionState Parse(JsonElement data) => new()
    {
        Id = GetString(data, "id"),
        CustomerId = GetString(data, "customer_id"),
        Status = GetString(data, "status") ?? "active",
        PriceId = FirstPriceId(data),
        UserId = UserIdFrom(data),
        CurrentPeriodEnd = GetNestedDateTime(data, "current_billing_period", "ends_at"),
        NextBilledAt = GetDateTime(data, "next_billed_at"),
        CanceledAt = GetDateTime(data, "canceled_at"),
        ScheduledChangeAction = GetNestedString(data, "scheduled_change", "action"),
        ScheduledChangeAt = GetNestedDateTime(data, "scheduled_change", "effective_at"),
        UpdatedAt = GetDateTime(data, "updated_at")
    };

    /// <summary>True when <paramref name="sub"/> already holds a newer state than this one.</summary>
    public bool IsOlderThan(Subscription sub) =>
        UpdatedAt is not null && sub.PaddleUpdatedAt is not null && UpdatedAt < sub.PaddleUpdatedAt;

    public void ApplyTo(Subscription sub, DateTime now)
    {
        if (!sub.IsLifetime)
        {
            sub.Plan = "pro";
        }

        sub.Status = Status;
        sub.PaddleSubscriptionId = Id ?? sub.PaddleSubscriptionId;
        sub.PaddleCustomerId = CustomerId ?? sub.PaddleCustomerId;
        sub.PaddlePriceId = PriceId ?? sub.PaddlePriceId;
        sub.CurrentPeriodEnd = CurrentPeriodEnd ?? sub.CurrentPeriodEnd;
        sub.TrialEndsAt = Status == "trialing" ? (CurrentPeriodEnd ?? sub.TrialEndsAt) : sub.TrialEndsAt;
        sub.NextBilledAt = NextBilledAt;

        // A scheduled cancel is not a cancellation yet, and a withdrawn one
        // must clear: both come from the entity as it is now, not accumulated.
        sub.ScheduledChangeAction = ScheduledChangeAction;
        sub.ScheduledChangeAt = ScheduledChangeAt;
        sub.CanceledAt = Status == "canceled" ? (CanceledAt ?? now) : null;

        sub.PaddleUpdatedAt = UpdatedAt ?? sub.PaddleUpdatedAt;
        sub.UpdatedAt = now;
    }

    /// <summary>
    /// True when custom_data.product names another app (for example Convia).
    /// Untagged entities are Mizan's: checkouts before the tag carry only user_id.
    /// </summary>
    public static bool BelongsToAnotherProduct(JsonElement data) =>
        data.TryGetProperty("custom_data", out var cd)
        && cd.ValueKind == JsonValueKind.Object
        && GetString(cd, "product") is { } product
        && product != ProductTag;

    public static Guid? UserIdFrom(JsonElement data)
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
            if (item.TryGetProperty("price", out var price) && price.ValueKind == JsonValueKind.Object
                && GetString(price, "id") is { } id)
            {
                return id;
            }
        }

        return null;
    }

    public static string? GetString(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public static DateTime? GetDateTime(JsonElement el, string prop) =>
        GetString(el, prop) is { } raw && DateTimeOffset.TryParse(raw, out var dto) ? dto.UtcDateTime : null;

    private static string? GetNestedString(JsonElement el, string parent, string child) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(parent, out var obj) ? GetString(obj, child) : null;

    private static DateTime? GetNestedDateTime(JsonElement el, string parent, string child) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(parent, out var obj) ? GetDateTime(obj, child) : null;
}
