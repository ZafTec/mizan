using System.Security.Cryptography;
using System.Text.Json;
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
    /// cannot lose the email (docs/ARCHITECTURE.md#storage-caching-and-jobs).
    /// </summary>
    public static Task QueueAsync(
        IOutbox outbox,
        EmailMessage message,
        Guid userId,
        string purpose,
        CancellationToken cancellationToken)
    {
        // Deduplicate the same delivery, not every email with the same subject.
        // A newly issued token changes the message and must always be queued.
        var messageHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(message)));
        return outbox.EnqueueAsync(
            OutboxJobTypes.Email,
            message,
            dedupeKey: $"auth:{purpose}:{userId}:{messageHash}",
            cancellationToken);
    }
}
