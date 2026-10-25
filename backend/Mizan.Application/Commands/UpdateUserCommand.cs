using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Streaks;

namespace Mizan.Application.Commands;

/// <summary>
/// Appearance preferences live on the user row and used to be written by
/// BetterAuth's updateUser. Since v2 they come through here - null means
/// "leave alone", so a name change cannot silently reset a theme.
/// <c>PriceRegionCode</c> picks the region whose prices the person sees; it
/// must be an enabled region. <c>NotifyByTelegram</c> can only be turned on
/// while a Telegram chat is linked; turning it off always works.
/// </summary>
public record UpdateUserCommand(
    Guid UserId,
    string? Name,
    string? Image,
    string? ThemePreference = null,
    bool? CompactMode = null,
    bool? ReduceAnimations = null,
    string? TimeZoneId = null,
    string? PriceRegionCode = null,
    bool? NotifyByTelegram = null) : IRequest<bool>, IRedactedAudit
{
    /// <summary>The region code names a market, not a credential; the details list every field so nothing else is hidden.</summary>
    object IRedactedAudit.AuditDetails => new { UserId, Name, Image, ThemePreference, CompactMode, ReduceAnimations, TimeZoneId, PriceRegionCode, NotifyByTelegram };
}

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Name).MaximumLength(255);
        // A zone the server cannot resolve would silently fall back to UTC and
        // the user would never know why their day ends at the wrong hour.
        RuleFor(x => x.TimeZoneId!)
            .Must(StreakClock.IsKnownZone)
            .When(x => x.TimeZoneId is not null)
            .WithMessage("Unknown time zone.");
    }
}

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, bool>
{
    private readonly IMizanDbContext _context;
    private readonly IUserCacheInvalidator _cache;

    public UpdateUserCommandHandler(
        IMizanDbContext context, IUserCacheInvalidator cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<bool> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        user.Name = request.Name ?? user.Name;

        // Profiles may share an image URL. Object cleanup requires ownership
        // and reference tracking before a replaced image can be removed.
        user.Image = request.Image ?? user.Image;

        if (request.ThemePreference is "light" or "dark" or "system")
        {
            user.ThemePreference = request.ThemePreference;
        }
        if (request.CompactMode is { } compact) user.CompactMode = compact;
        if (request.ReduceAnimations is { } reduce) user.ReduceAnimations = reduce;
        if (request.TimeZoneId is { } zone) user.TimeZoneId = zone;

        if (request.PriceRegionCode is { } regionCode && regionCode != user.PriceRegionCode)
        {
            if (!await _context.PriceRegions.AnyAsync(r => r.Code == regionCode && r.Enabled, cancellationToken))
                throw new DomainValidationException($"There is no enabled price region '{regionCode}'.");
            user.PriceRegionCode = regionCode;
        }

        if (request.NotifyByTelegram is true
            && !await _context.TelegramLinks.AnyAsync(l => l.UserId == user.Id, cancellationToken))
        {
            throw new DomainValidationException("Connect a Telegram chat before turning on Telegram notifications.");
        }
        if (request.NotifyByTelegram is { } notify) user.NotifyByTelegram = notify;

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // The zone is cached on the logging path, so a change has to be
        // published or the next few hours of logs use the old day boundary.
        // Plans and lists are priced in the person's region, so their cached
        // copies go with it.
        await _cache.InvalidateAsync(request.UserId, cancellationToken);

        return true;
    }
}
