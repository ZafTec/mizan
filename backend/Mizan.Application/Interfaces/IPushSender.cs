namespace Mizan.Application.Interfaces;

public sealed record PushMessage(string Token, string Title, string? Body, string? LinkUrl, string? NotificationId);

public enum PushOutcome
{
    Sent,

    /// <summary>The provider no longer knows this device, for example after an uninstall. The token should go.</summary>
    TokenInvalid,

    /// <summary>Something that may work on a retry: the provider was down or rate limited us.</summary>
    Failed,
}

/// <summary>Delivers one push notification to one device.</summary>
public interface IPushSender
{
    /// <summary>False when no provider credentials are set, so nothing is queued that can never be sent.</summary>
    bool IsConfigured { get; }

    Task<PushOutcome> SendAsync(PushMessage message, CancellationToken cancellationToken);
}
