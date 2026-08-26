namespace Mizan.Application.Interfaces;

/// <summary>
/// Hands work to the dispatcher.
///
/// A transactional outbox: the job is staged on the caller's own DbContext and
/// committed by the caller's <c>SaveChangesAsync</c>, alongside whatever else
/// that request wrote. That is the property worth having - a registration that
/// rolls back does not leave a verification email queued for a user who does
/// not exist, and one that commits cannot lose the email.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Stages a job. <b>It is not queued until the caller saves.</b>
    ///
    /// <paramref name="dedupeKey"/> makes this idempotent: enqueueing the same
    /// key twice keeps the first job, enforced by a unique index as well as
    /// checked here. Use it wherever a retry could double up.
    /// </summary>
    Task<Guid> EnqueueAsync<T>(
        string type,
        T payload,
        string? dedupeKey = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One job type's worth of work.
///
/// Throwing means "retry me"; returning means done. A handler that fails for a
/// reason retrying cannot fix should throw <see cref="OutboxPermanentException"/>
/// so the dispatcher dead-letters it immediately instead of spending five
/// attempts proving the point.
/// </summary>
public interface IOutboxHandler
{
    string Type { get; }

    /// <summary>
    /// How many of this type may run at once. Email is small and frequent; an
    /// eval run is minutes of provider calls, and one of those must not sit in
    /// front of somebody's password reset.
    /// </summary>
    int Concurrency => 1;

    Task HandleAsync(string payload, CancellationToken cancellationToken);
}

public sealed class OutboxPermanentException : Exception
{
    public OutboxPermanentException(string message) : base(message) { }

    public OutboxPermanentException(string message, Exception inner) : base(message, inner) { }
}
