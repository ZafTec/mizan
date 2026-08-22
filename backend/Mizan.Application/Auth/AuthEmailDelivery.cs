using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Auth;

internal static class AuthEmailDelivery
{
    /// <summary>
    /// A mail outage must not fail a sign-in or roll back a registration that
    /// already committed. It is logged at error, never swallowed silently, and
    /// every affected flow has a "resend" path.
    /// </summary>
    public static async Task TrySendAsync(
        IEmailSender sender,
        EmailMessage message,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await sender.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {Subject} to {To}", message.Subject, message.To);
        }
    }
}
