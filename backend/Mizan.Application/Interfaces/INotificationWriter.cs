using Mizan.Contracts.Notifications;
using Mizan.Domain.Notifications;

namespace Mizan.Application.Interfaces;

/// <summary>
/// One notification for one person. <see cref="Channels"/> narrows where it may go (a schedule's
/// delivery job names one channel); the person's own choices for the message's kind always apply.
/// <see cref="DeliveryKey"/> keys the channel jobs it stages, so the same delivery staged twice
/// sends once; without it the notification's id is the key.
/// </summary>
public sealed record NotificationSend(Guid UserId, string Type, NotificationMessage Message, string? LinkUrl = null)
{
    public NotificationChannels Channels { get; init; } = NotificationChannels.All;
    public string? DeliveryKey { get; init; }

    /// <summary>A reminder's id, when this is one: Telegram then carries Done, Snooze and Skip today buttons for it.</summary>
    public Guid? ReminderId { get; init; }
}

/// <summary>
/// The one place a notification fans out (docs/ARCHITECTURE.md#notifications): the in-app row, a
/// push job, a Telegram message and an email, each only where the person allows that kind, all staged
/// with the caller's unit of work. Telegram, push and email wait out the person's quiet hours.
/// </summary>
public interface INotificationWriter
{
    /// <summary>A plain notification: its kind comes from the type, and it has no image, actions or facts.</summary>
    Task AddAsync(Guid userId, string type, string title, string? body = null, string? linkUrl = null, CancellationToken cancellationToken = default);

    /// <summary>Stages the notification and returns the in-app row's id, or null when the person does not see it in the app.</summary>
    Task<Guid?> SendAsync(NotificationSend send, CancellationToken cancellationToken = default);
}
