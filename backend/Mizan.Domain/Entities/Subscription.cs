namespace Mizan.Domain.Entities;

/// <summary>
/// Billing subscription state for a user. Backend-owned (EF Core), source of truth
/// for entitlements. Written only by the Paddle webhook pipeline.
/// </summary>
public class Subscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string Plan { get; set; } = "free";     // free | pro | lifetime
    public string Status { get; set; } = "none";   // none | trialing | active | past_due | paused | canceled
    public bool IsLifetime { get; set; }           // one-time purchase: permanent Pro

    public string? PaddleCustomerId { get; set; }       // ctm_...
    public string? PaddleSubscriptionId { get; set; }   // sub_... (null for lifetime)
    public string? PaddlePriceId { get; set; }          // pri_...

    public DateTime? CurrentPeriodEnd { get; set; }     // renewal / access expiry for recurring
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CanceledAt { get; set; }

    public DateTime? NextBilledAt { get; set; }

    /// <summary>
    /// A change Paddle will make at <see cref="ScheduledChangeAt"/>: "cancel",
    /// "pause", or "resume". A subscriber who cancels keeps an active status
    /// until then; this is what says it is ending.
    /// </summary>
    public string? ScheduledChangeAction { get; set; }
    public DateTime? ScheduledChangeAt { get; set; }

    /// <summary>
    /// Paddle's updated_at for the state stored here. Paddle does not promise
    /// webhook order, so an event describing an older state is ignored.
    /// </summary>
    public DateTime? PaddleUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
