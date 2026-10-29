using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Notifications;
using Mizan.Domain.Entities;
using Mizan.Domain.Notifications;
using Mizan.Domain.Streaks;

namespace Mizan.Application.Notifications;

/// <summary>
/// The channels a person allows for each notification kind, and their quiet hours
/// (docs/ARCHITECTURE.md#notifications). The web settings page, the MCP tools and the bot's
/// <c>/notifications</c> all read and change them through these two requests.
/// </summary>
public static class NotificationPreferenceRules
{
    /// <summary>The channels a kind may use for a person: their row when they changed it, else the kind's defaults.</summary>
    public static NotificationChannels Allowed(string kind, NotificationPreference? row) =>
        row is null
            ? NotificationKinds.Info(kind).Defaults
            : (row.InApp ? NotificationChannels.InApp : 0)
              | (row.Telegram ? NotificationChannels.Telegram : 0)
              | (row.Push ? NotificationChannels.Push : 0)
              | (row.Email ? NotificationChannels.Email : 0);

    public static string? Format(TimeOnly? time) => time?.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static bool TryParse(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value?.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}

public record GetNotificationPreferencesQuery : IRequest<NotificationPreferencesDto>;

public sealed class GetNotificationPreferencesQueryHandler : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationPreferencesQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<NotificationPreferencesDto> Handle(GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        return await NotificationPreferencesReader.ReadAsync(_context, userId, cancellationToken);
    }
}

internal static class NotificationPreferencesReader
{
    public static async Task<NotificationPreferencesDto> ReadAsync(IMizanDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        var user = await context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.TimeZoneId, u.QuietHoursStart, u.QuietHoursEnd, u.NotifyByTelegram })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("User not found");
        var rows = await context.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.Kind, cancellationToken);
        var linked = await context.TelegramLinks.AnyAsync(l => l.UserId == userId, cancellationToken);

        var kinds = NotificationKinds.All.Select(info =>
        {
            var allowed = NotificationPreferenceRules.Allowed(info.Kind, rows.GetValueOrDefault(info.Kind));
            return new NotificationKindPreferenceDto(
                info.Kind,
                info.Label,
                info.Description,
                allowed.HasFlag(NotificationChannels.InApp),
                allowed.HasFlag(NotificationChannels.Telegram),
                allowed.HasFlag(NotificationChannels.Push),
                allowed.HasFlag(NotificationChannels.Email));
        }).ToList();

        var quiet = user.QuietHoursStart is not null && user.QuietHoursEnd is not null && user.QuietHoursStart != user.QuietHoursEnd;
        return new NotificationPreferencesDto(
            kinds,
            quiet ? NotificationPreferenceRules.Format(user.QuietHoursStart) : null,
            quiet ? NotificationPreferenceRules.Format(user.QuietHoursEnd) : null,
            user.TimeZoneId ?? StreakClock.DefaultTimeZone,
            linked,
            user.NotifyByTelegram);
    }
}

/// <summary>Changes some kinds' channels and the quiet hours, and answers with every preference as it now stands.</summary>
public record UpdateNotificationPreferencesCommand(
    IReadOnlyList<NotificationKindPreferenceUpdate>? Kinds,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    bool? ClearQuietHours) : IRequest<NotificationPreferencesDto>;

public sealed class UpdateNotificationPreferencesCommandHandler : IRequestHandler<UpdateNotificationPreferencesCommand, NotificationPreferencesDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _time;

    public UpdateNotificationPreferencesCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, TimeProvider time)
    {
        _context = context;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<NotificationPreferencesDto> Handle(UpdateNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new EntityNotFoundException("User not found");
        var now = _time.GetUtcNow().UtcDateTime;

        foreach (var change in request.Kinds ?? [])
        {
            if (!NotificationKinds.IsKnown(change.Kind))
                throw new DomainValidationException($"\"{change.Kind}\" is not a notification kind. Use one of: {string.Join(", ", NotificationKinds.All.Select(k => k.Kind))}.");
        }

        var changed = (request.Kinds ?? []).Select(k => k.Kind).Distinct().ToList();
        var rows = await _context.NotificationPreferences
            .Where(p => p.UserId == userId && changed.Contains(p.Kind))
            .ToDictionaryAsync(p => p.Kind, cancellationToken);

        foreach (var change in request.Kinds ?? [])
        {
            if (!rows.TryGetValue(change.Kind, out var row))
            {
                var defaults = NotificationKinds.Info(change.Kind).Defaults;
                row = new NotificationPreference
                {
                    UserId = userId,
                    Kind = change.Kind,
                    InApp = defaults.HasFlag(NotificationChannels.InApp),
                    Telegram = defaults.HasFlag(NotificationChannels.Telegram),
                    Push = defaults.HasFlag(NotificationChannels.Push),
                    Email = defaults.HasFlag(NotificationChannels.Email),
                };
                _context.NotificationPreferences.Add(row);
                rows[change.Kind] = row;
            }

            if (change.InApp is { } inApp) row.InApp = inApp;
            if (change.Telegram is { } telegram) row.Telegram = telegram;
            if (change.Push is { } push) row.Push = push;
            if (change.Email is { } email) row.Email = email;
            row.UpdatedAt = now;
        }

        if (request.ClearQuietHours is true)
        {
            user.QuietHoursStart = null;
            user.QuietHoursEnd = null;
        }
        else if (request.QuietHoursStart is not null || request.QuietHoursEnd is not null)
        {
            if (!NotificationPreferenceRules.TryParse(request.QuietHoursStart, out var start) || !NotificationPreferenceRules.TryParse(request.QuietHoursEnd, out var end))
                throw new DomainValidationException("Quiet hours need a start and an end as HH:mm, for example 22:00 and 07:00.");
            if (start == end)
                throw new DomainValidationException("Quiet hours need different start and end times.");
            user.QuietHoursStart = start;
            user.QuietHoursEnd = end;
        }

        user.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
        return await NotificationPreferencesReader.ReadAsync(_context, userId, cancellationToken);
    }
}
