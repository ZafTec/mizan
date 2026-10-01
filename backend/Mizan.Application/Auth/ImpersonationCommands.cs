using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Auth;

public static class ImpersonationRules
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
}

public record StartImpersonationResult(string SessionToken, AuthUserDto User);

/// <summary>
/// An administrator opens a short session as another user, to see what they see. It is refused for another
/// administrator, for oneself, for an account that cannot sign in, and from inside an impersonation session. The
/// attempt is audited with the target as the entity and the administrator as the actor.
/// </summary>
public record StartImpersonationCommand(Guid Id, string? IpAddress, string? UserAgent) : IRequest<StartImpersonationResult>;

public sealed class StartImpersonationCommandHandler : IRequestHandler<StartImpersonationCommand, StartImpersonationResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessions;

    public StartImpersonationCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, ISessionService sessions)
    {
        _context = context;
        _currentUser = currentUser;
        _sessions = sessions;
    }

    public async Task<StartImpersonationResult> Handle(StartImpersonationCommand request, CancellationToken ct)
    {
        var adminId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        if (!_currentUser.IsInRole("admin")) throw new ForbiddenAccessException("Only an administrator can view the site as a user.");
        if (_currentUser.ImpersonatorId is not null) throw new ForbiddenAccessException("Leave the current view before opening another.");
        if (request.Id == adminId) throw new DomainValidationException("You are already signed in as yourself.");

        var target = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new EntityNotFoundException("User not found");

        // Another administrator's account is not for viewing, and an account that cannot sign in would be turned
        // away on its first request anyway, so say so now.
        if (string.Equals(target.Role, "admin", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenAccessException("You cannot view the site as another administrator.");
        if (!target.EmailVerified) throw new DomainValidationException("This account has not confirmed its email address.");
        if (target.Banned && (target.BanExpires is null || target.BanExpires > DateTime.UtcNow))
            throw new DomainValidationException("This account is banned.");

        var token = await _sessions.CreateImpersonationAsync(target.Id, adminId, request.IpAddress, request.UserAgent, ct);
        var expires = DateTime.UtcNow.Add(ImpersonationRules.Lifetime);
        var name = await _context.Users.AsNoTracking().Where(u => u.Id == adminId).Select(u => u.Name ?? u.Email).FirstAsync(ct);

        return new StartImpersonationResult(
            token,
            AuthUserMapper.ToDto(target) with { Impersonation = new ImpersonationDto(adminId, name, expires) });
    }
}

/// <summary>Ends an impersonation session. The token is the session being ended, so it is not kept in the audit trail.</summary>
public record StopImpersonationCommand(string Token) : IRequest<Guid?>, IRedactedAudit
{
    public object AuditDetails => new { };
}

public sealed class StopImpersonationCommandHandler : IRequestHandler<StopImpersonationCommand, Guid?>
{
    private readonly ISessionService _sessions;

    public StopImpersonationCommandHandler(ISessionService sessions) => _sessions = sessions;

    /// <summary>The administrator who had opened it, or null when the token was not an impersonation session.</summary>
    public async Task<Guid?> Handle(StopImpersonationCommand request, CancellationToken ct)
    {
        var session = await _sessions.ResolveIdentityAsync(request.Token, ct);
        if (session?.ImpersonatorId is not { } impersonator) return null;

        await _sessions.RevokeAsync(request.Token, ct);
        return impersonator;
    }
}
