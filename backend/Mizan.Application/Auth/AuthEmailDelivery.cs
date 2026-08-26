using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Auth;

internal static class AuthEmailDelivery
{
    /// <summary>
    /// Queues an identity email rather than sending it inline.
    ///
    /// This used to call the sender inside a try/catch that logged and carried
    /// on. That was the least bad option available - a mail outage must not
    /// fail a sign-in or roll back a registration that already committed - but
    /// it meant a password reset that never arrived left nothing to retry and
    /// nothing anyone would look at.
    ///
    /// The enqueue is an insert on the same context, so it commits with the
    /// user row. A registration that rolls back does not leave a verification
    /// email queued for an account that does not exist, and one that succeeds
    /// cannot lose the email (docs/REFOCUS.md §13b).
    /// </summary>
    public static Task QueueAsync(
        IOutbox outbox,
        EmailMessage message,
        Guid userId,
        string purpose,
        CancellationToken cancellationToken) =>
        outbox.EnqueueAsync(
            OutboxJobTypes.Email,
            message,
            // One live token per purpose per user, so a double-submitted
            // "resend" does not send two.
            dedupeKey: $"auth:{purpose}:{userId}:{message.Subject.GetHashCode():X}",
            cancellationToken);
}
