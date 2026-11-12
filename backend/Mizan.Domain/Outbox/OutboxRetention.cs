using Mizan.Domain.Entities;

namespace Mizan.Domain.Outbox;

/// <summary>
/// How long a finished outbox job is kept (docs/ARCHITECTURE.md#storage-caching-and-jobs). A succeeded job is
/// kept long enough to answer "did it go out", a dead-lettered one longer, because an operator may still retry it
/// from <b>Jobs</b> or the mail log. A job that can still run (pending, running, failed and waiting to retry) is
/// never removed, whatever its age.
/// </summary>
public static class OutboxRetention
{
    public const int DefaultSucceededDays = 30;
    public const int DefaultDeadLetteredDays = 90;

    /// <summary>The finished statuses the retention pass removes.</summary>
    public static readonly IReadOnlyList<OutboxJobStatus> Removable = [OutboxJobStatus.Succeeded, OutboxJobStatus.DeadLettered];

    /// <summary>
    /// The instant before which a job of <paramref name="status"/> that finished is removed, or null for a status
    /// that is never removed.
    /// </summary>
    public static DateTime? CutoffFor(OutboxJobStatus status, DateTime now, int keepSucceededDays, int keepDeadLetteredDays) => status switch
    {
        OutboxJobStatus.Succeeded => now.AddDays(-Math.Max(1, keepSucceededDays)),
        OutboxJobStatus.DeadLettered => now.AddDays(-Math.Max(1, keepDeadLetteredDays)),
        _ => null,
    };
}
