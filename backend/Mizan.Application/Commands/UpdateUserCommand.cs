using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

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
    bool? ReduceAnimations = null) : IRequest<bool>;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, bool>
{
    private readonly IMizanDbContext _context;
    private readonly IStorageService _storage;

    public UpdateUserCommandHandler(IMizanDbContext context, IStorageService storage)
    {
        _context = context;
        _storage = storage;
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

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

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
