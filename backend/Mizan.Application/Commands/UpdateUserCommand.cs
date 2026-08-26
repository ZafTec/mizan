using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Streaks;

namespace Mizan.Application.Commands;

/// <summary>
/// Appearance preferences live on the user row and used to be written by
/// BetterAuth's updateUser. Since v2 they come through here - null means
/// "leave alone", so a name change cannot silently reset a theme.
/// </summary>
public record UpdateUserCommand(
    Guid UserId,
    string? Name,
    string? Image,
    string? ThemePreference = null,
    bool? CompactMode = null,
    bool? ReduceAnimations = null,
    string? TimeZoneId = null) : IRequest<bool>;

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
    private readonly IStorageService _storage;
    private readonly IUserCacheInvalidator _cache;

    public UpdateUserCommandHandler(
        IMizanDbContext context, IStorageService storage, IUserCacheInvalidator cache)
    {
        _context = context;
        _storage = storage;
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

        var replacedImage = request.Image is not null && request.Image != user.Image ? user.Image : null;
        user.Image = request.Image ?? user.Image;

        if (request.ThemePreference is "light" or "dark" or "system")
        {
            user.ThemePreference = request.ThemePreference;
        }
        if (request.CompactMode is { } compact) user.CompactMode = compact;
        if (request.ReduceAnimations is { } reduce) user.ReduceAnimations = reduce;
        if (request.TimeZoneId is { } zone) user.TimeZoneId = zone;

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // The zone is cached on the logging path, so a change has to be
        // published or the next few hours of logs use the old day boundary.
        await _cache.InvalidateAsync(request.UserId, cancellationToken);

        // A replaced avatar is otherwise an orphan nobody ever collects.
        // TryGetKey returns null for anything we did not store - a Google
        // avatar URL, say - so this only ever deletes our own objects.
        if (_storage.TryGetKey(replacedImage) is { } key)
        {
            await _storage.DeleteAsync(key, cancellationToken);
        }

        return true;
    }
}
