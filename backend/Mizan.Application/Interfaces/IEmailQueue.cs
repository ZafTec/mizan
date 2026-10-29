namespace Mizan.Application.Interfaces;

/// <summary>
/// The one way the system sends email. It stages two rows on the caller's
/// context: the entry in the email log an administrator reads, and the outbox
/// job that carries the body. Both commit with the caller's
/// <c>SaveChangesAsync</c>, so a request that rolls back leaves neither, and
/// one that commits cannot lose the email or its record.
/// </summary>
public interface IEmailQueue
{
    /// <summary>
    /// Stages the email and returns its log id. <paramref name="kind"/> is one
    /// of <c>EmailKinds</c>. With <paramref name="dedupeKey"/>, queueing the same
    /// key again stages nothing and returns the first message's id. With
    /// <paramref name="notBefore"/> (UTC) the message waits in the outbox until then.
    /// </summary>
    Task<Guid> QueueAsync(
        OutgoingEmail message,
        string kind,
        Guid? toUserId = null,
        string? dedupeKey = null,
        Guid? createdByUserId = null,
        CancellationToken cancellationToken = default,
        DateTime? notBefore = null);
}
