using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Application.Mail;
using Mizan.Application.Notifications;
using Mizan.Application.Telegram;
using Mizan.Contracts.Notifications;
using Mizan.Domain.Entities;
using Mizan.Domain.Notifications;
using Mizan.Domain.Streaks;
using Mizan.Infrastructure.Outbox;

namespace Mizan.Infrastructure.Services;

/// <summary>
/// The one place a notification fans out (docs/ARCHITECTURE.md#notifications): the in-app row, a push
/// job where a phone is registered, a Telegram message when the person has opted in, and an email when
/// they chose email for the kind. Each channel goes out only where the person allows the message's kind.
/// Every job is staged with the caller's unit of work, so it goes out only if the caller saves. Push and
/// Telegram wait until the person's quiet hours end.
/// </summary>
public sealed class NotificationWriter : INotificationWriter
{
    private readonly IMizanDbContext _context;
    private readonly IOutbox _outbox;
    private readonly IPushSender _push;
    private readonly ITelegramMessenger _telegram;
    private readonly IEmailQueue _emails;
    private readonly IAppUrls _urls;
    private readonly TimeProvider _time;

    public NotificationWriter(
        IMizanDbContext context,
        IOutbox outbox,
        IPushSender push,
        ITelegramMessenger telegram,
        IEmailQueue emails,
        IAppUrls urls,
        TimeProvider time)
    {
        _context = context;
        _outbox = outbox;
        _push = push;
        _telegram = telegram;
        _emails = emails;
        _urls = urls;
        _time = time;
    }

    public async Task AddAsync(Guid userId, string type, string title, string? body = null, string? linkUrl = null, CancellationToken cancellationToken = default) =>
        await SendAsync(new NotificationSend(userId, type, NotificationMessages.Plain(type, title, body), linkUrl), cancellationToken);

    public async Task<Guid?> SendAsync(NotificationSend send, CancellationToken cancellationToken = default)
    {
        var message = send.Message;
        var kind = NotificationKinds.IsKnown(message.Kind) ? message.Kind : NotificationKinds.ForType(send.Type);
        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == send.UserId)
            .Select(u => new { u.Email, u.EmailVerified, u.Name, u.NotifyByTelegram, u.TimeZoneId, u.QuietHoursStart, u.QuietHoursEnd })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null) return null;

        // A preference staged earlier in this unit of work counts too.
        var row = _context.NotificationPreferences.Local.FirstOrDefault(p => p.UserId == send.UserId && p.Kind == kind)
            ?? await _context.NotificationPreferences.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == send.UserId && p.Kind == kind, cancellationToken);
        var channels = send.Channels & NotificationPreferenceRules.Allowed(kind, row);
        if (channels == NotificationChannels.None) return null;

        var now = _time.GetUtcNow();
        var id = Guid.NewGuid();
        var key = send.DeliveryKey ?? id.ToString();
        var actions = (message.Actions ?? []).Take(NotificationMessage.MaxActions).ToList();
        var facts = message.Facts ?? [];
        Guid? stored = null;

        if (channels.HasFlag(NotificationChannels.InApp))
        {
            _context.Notifications.Add(new Notification
            {
                Id = id,
                UserId = send.UserId,
                Type = send.Type,
                Kind = kind,
                Title = message.Title,
                Body = message.Body,
                LinkUrl = send.LinkUrl,
                ImageUrl = message.ImageUrl,
                Actions = actions.Select(a => new NotificationActionItem { Label = a.Label, Path = a.Path }).ToList(),
                Facts = facts.Select(f => new NotificationFactItem { Label = f.Label, Value = f.Value }).ToList(),
                CreatedAt = now.UtcDateTime,
            });
            stored = id;
        }

        // Quiet hours hold the channels that make a sound. The in-app row is there when the person looks.
        var release = QuietHours.Release(now, StreakClock.Zone(user.TimeZoneId), user.QuietHoursStart, user.QuietHoursEnd).UtcDateTime;

        if (channels.HasFlag(NotificationChannels.Push)
            && await _push.IsConfiguredAsync(cancellationToken)
            && await _context.DeviceTokens.AnyAsync(d => d.UserId == send.UserId, cancellationToken))
        {
            var push = new PushJob(send.UserId, id, message.Title, message.Body, send.LinkUrl);
            if (release > now.UtcDateTime)
                await _outbox.ScheduleAsync(OutboxJobTypes.Push, push, release, $"push:{key}", cancellationToken);
            else
                await _outbox.EnqueueAsync(OutboxJobTypes.Push, push, $"push:{key}", cancellationToken);
        }

        // Opt-in only. The messenger still checks that a chat is linked and the account is Pro.
        if (channels.HasFlag(NotificationChannels.Telegram) && user.NotifyByTelegram)
        {
            await _telegram.QueueAsync(
                send.UserId,
                TelegramCards.For(message with { Actions = actions, Facts = facts }, send.LinkUrl, send.ReminderId),
                $"telegram:notification:{key}",
                release > now.UtcDateTime ? release : null,
                cancellationToken);
        }

        if (channels.HasFlag(NotificationChannels.Email) && user.EmailVerified && !string.IsNullOrWhiteSpace(user.Email))
        {
            var links = actions.Select(a => (a.Label, Url: _urls.Asset(a.Path)))
                .Concat(actions.Count == 0 && send.LinkUrl is { Length: > 0 } link ? [("Open", _urls.Asset(link))] : [])
                .Where(l => l.Url is { Length: > 0 })
                .Select(l => (l.Label, l.Url!));
            await _emails.QueueAsync(
                MailTemplates.Notification(user.Email, user.Name, message.Title, message.Body, facts.Select(f => (f.Label, f.Value)), links),
                EmailKinds.Notification,
                toUserId: send.UserId,
                dedupeKey: $"email:notification:{key}",
                cancellationToken: cancellationToken);
        }

        return stored;
    }
}
