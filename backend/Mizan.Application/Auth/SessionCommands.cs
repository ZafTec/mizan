using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Identity;

namespace Mizan.Application.Auth;

public record GetCurrentUserQuery : IRequest<AuthUserDto?>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, AuthUserDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetCurrentUserQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AuthUserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return null;

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is null ? null : AuthUserMapper.ToDto(user);
    }
}

public record ListSessionsQuery(string? CurrentSessionToken) : IRequest<List<SessionSummaryDto>>;

public class ListSessionsQueryHandler : IRequestHandler<ListSessionsQuery, List<SessionSummaryDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ListSessionsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<SessionSummaryDto>> Handle(ListSessionsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var currentHash = string.IsNullOrEmpty(request.CurrentSessionToken)
            ? null
            : SecureToken.Hash(request.CurrentSessionToken);

        return await _context.UserSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new SessionSummaryDto(
                s.Id, s.CreatedAt, s.LastSeenAt, s.ExpiresAt, s.IpAddress, s.UserAgent,
                currentHash != null && s.TokenHash == currentHash))
            .ToListAsync(cancellationToken);
    }
}

public record RevokeSessionCommand(Guid SessionId) : IRequest<Unit>;

public class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand, Unit>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessions;

    public RevokeSessionCommandHandler(ICurrentUserService currentUser, ISessionService sessions)
    {
        _currentUser = currentUser;
        _sessions = sessions;
    }

    public async Task<Unit> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        await _sessions.RevokeByIdAsync(userId, request.SessionId, cancellationToken);
        return Unit.Value;
    }
}

public record DeleteAccountCommand : IRequest<Unit>;

public class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessions;
    private readonly IUserCacheInvalidator _cache;

    public DeleteAccountCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        ISessionService sessions,
        IUserCacheInvalidator cache)
    {
        _context = context;
        _currentUser = currentUser;
        _sessions = sessions;
        _cache = cache;
    }

    public async Task<Unit> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new EntityNotFoundException("User", userId);

        await _sessions.RevokeAllAsync(userId, cancellationToken);
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateAsync(userId, cancellationToken);
        return Unit.Value;
    }
}
