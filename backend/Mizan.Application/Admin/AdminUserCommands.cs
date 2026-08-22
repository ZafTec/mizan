using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Auth;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Admin;

public static class UserRoles
{
    public static readonly string[] All = { "user", "trainer", "admin" };

    public static bool IsValid(string? role) =>
        role is not null && All.Contains(role, StringComparer.Ordinal);
}

public record CreateAdminUserCommand(
    string Email,
    string Password,
    string? Name,
    string Role,
    bool EmailVerified) : IRequest<Guid>;

public class CreateAdminUserCommandValidator : AbstractValidator<CreateAdminUserCommand>
{
    public CreateAdminUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10).MaximumLength(256);
        RuleFor(x => x.Name).MaximumLength(255);
        RuleFor(x => x.Role).Must(UserRoles.IsValid).WithMessage("Role must be user, trainer or admin.");
    }
}

public class CreateAdminUserCommandHandler : IRequestHandler<CreateAdminUserCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public CreateAdminUserCommandHandler(IMizanDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<Guid> Handle(CreateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var email = AuthEmailAddress.Normalize(request.Email);
        if (await _context.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new DomainValidationException("An account with that email already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = request.Role,
            EmailVerified = request.EmailVerified,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}

/// <summary>
/// Partial update: only the fields supplied change. Ban, role and password are
/// the three things the admin screen actually does.
/// </summary>
public record UpdateAdminUserCommand(
    Guid UserId,
    string? Role = null,
    bool? Banned = null,
    string? BanReason = null,
    DateTime? BanExpires = null,
    bool? EmailVerified = null,
    string? NewPassword = null) : IRequest<Unit>;

public class UpdateAdminUserCommandValidator : AbstractValidator<UpdateAdminUserCommand>
{
    public UpdateAdminUserCommandValidator()
    {
        RuleFor(x => x.Role).Must(UserRoles.IsValid)
            .When(x => x.Role is not null)
            .WithMessage("Role must be user, trainer or admin.");
        RuleFor(x => x.NewPassword).MinimumLength(10).MaximumLength(256)
            .When(x => x.NewPassword is not null);
    }
}

public class UpdateAdminUserCommandHandler : IRequestHandler<UpdateAdminUserCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionService _sessions;
    private readonly IUserCacheInvalidator _cache;

    public UpdateAdminUserCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        ISessionService sessions,
        IUserCacheInvalidator cache)
    {
        _context = context;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _cache = cache;
    }

    public async Task<Unit> Handle(UpdateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        // An admin who bans or demotes themselves locks everyone out of the
        // panel, so the one guard worth having is against self-harm.
        var isSelf = _currentUser.UserId == user.Id;
        if (isSelf && (request.Banned == true || (request.Role is not null && request.Role != "admin")))
        {
            throw new DomainValidationException("You cannot ban or demote your own account.");
        }

        if (request.Role is not null) user.Role = request.Role;
        if (request.EmailVerified is { } verified) user.EmailVerified = verified;

        if (request.Banned is { } banned)
        {
            user.Banned = banned;
            user.BanReason = banned ? request.BanReason : null;
            user.BanExpires = banned ? request.BanExpires : null;
        }

        if (request.NewPassword is not null)
        {
            user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        // A ban or a forced password change is worthless while the old session
        // still works.
        if (request.Banned == true || request.NewPassword is not null)
        {
            await _sessions.RevokeAllAsync(user.Id, cancellationToken);
        }

        await _cache.InvalidateAsync(user.Id, cancellationToken);
        return Unit.Value;
    }
}

public record DeleteAdminUserCommand(Guid UserId) : IRequest<Unit>;

public class DeleteAdminUserCommandHandler : IRequestHandler<DeleteAdminUserCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserCacheInvalidator _cache;

    public DeleteAdminUserCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IUserCacheInvalidator cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Unit> Handle(DeleteAdminUserCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == request.UserId)
        {
            throw new DomainValidationException("You cannot delete your own account from here.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateAsync(request.UserId, cancellationToken);
        return Unit.Value;
    }
}

public record RevokeUserSessionsCommand(Guid UserId) : IRequest<Unit>;

public class RevokeUserSessionsCommandHandler : IRequestHandler<RevokeUserSessionsCommand, Unit>
{
    private readonly ISessionService _sessions;

    public RevokeUserSessionsCommandHandler(ISessionService sessions) => _sessions = sessions;

    public async Task<Unit> Handle(RevokeUserSessionsCommand request, CancellationToken cancellationToken)
    {
        await _sessions.RevokeAllAsync(request.UserId, cancellationToken);
        return Unit.Value;
    }
}

public record RevokeAdminSessionCommand(Guid SessionId) : IRequest<Unit>;

public class RevokeAdminSessionCommandHandler : IRequestHandler<RevokeAdminSessionCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ISessionService _sessions;

    public RevokeAdminSessionCommandHandler(IMizanDbContext context, ISessionService sessions)
    {
        _context = context;
        _sessions = sessions;
    }

    public async Task<Unit> Handle(RevokeAdminSessionCommand request, CancellationToken cancellationToken)
    {
        var owner = await _context.UserSessions
            .Where(s => s.Id == request.SessionId)
            .Select(s => (Guid?)s.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (owner is null) return Unit.Value;

        await _sessions.RevokeByIdAsync(owner.Value, request.SessionId, cancellationToken);
        return Unit.Value;
    }
}
