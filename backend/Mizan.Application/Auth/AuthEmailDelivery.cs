using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Auth;

internal static class AuthEmailDelivery
{
    /// <summary>
    /// A mail outage must not fail a sign-in or roll back a registration that
    /// already committed. It is logged at error, never swallowed silently, and
    /// every affected flow has a "resend" path.
    ///
    /// The failure is recorded against the user id, not the address: an id is
    /// what you need to trace the account, and an address in a log line is
    /// personal data shipped to wherever logs go.
    /// </summary>
    public static async Task TrySendAsync(
        IEmailSender sender,
        EmailMessage message,
        ILogger logger,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await sender.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {Subject} for user {UserId}", message.Subject, userId);
        }
    }
}
