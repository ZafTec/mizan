namespace Mizan.Domain.Entities;

public enum OutboxJobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,

    /// <summary>Failed, and will be retried after a backoff.</summary>
    Failed = 3,

    /// <summary>Out of attempts. Visible in the admin view, never retried on its own.</summary>
    DeadLettered = 4,
}

/// <summary>
/// Work that should happen reliably, but not inside the request that asked for
/// it.
///
/// Two things qualify today and they are here for different reasons. Outbound
/// email was a fire-and-forget call inside a try/catch that logged and
/// shrugged, so a failed password reset was invisible. An eval run is
/// twenty-odd sequential provider calls, which will time out an HTTP request
/// long before it finishes.
///
/// Everything else in the app stays synchronous. A queue is a second place for
/// state to be wrong, and most work does not need one (docs/REFOCUS.md §13b).
/// </summary>
public class OutboxJob
{
    public Guid Id { get; set; }

    /// <summary>Which handler runs it. See <c>OutboxJobTypes</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The handler's own JSON. The dispatcher never looks inside.</summary>
    public string Payload { get; set; } = string.Empty;

    public OutboxJobStatus Status { get; set; } = OutboxJobStatus.Pending;
    public int Attempts { get; set; }

    /// <summary>
    /// Not before this. Backoff lives on the row rather than in a sleep, so a
    /// restart does not lose it and a second worker does not pick the job up
    /// early.
    /// </summary>
    public DateTime RunAfter { get; set; }

    /// <summary>
    /// Optional. A unique index makes enqueueing twice a no-op, which is what
    /// turns at-least-once delivery into something safe to retry.
    /// </summary>
    public string? DedupeKey { get; set; }

    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public static class OutboxJobTypes
{
    public const string Email = "email";
    public const string EvalRun = "eval-run";
}
