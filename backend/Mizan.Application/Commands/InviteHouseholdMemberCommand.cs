using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record InviteHouseholdMemberCommand(
    Guid HouseholdId,
    string Email,
    string Role,
    Guid RequestingUserId
) : IRequest<InviteHouseholdMemberResult>;

public record InviteHouseholdMemberResult
{
    public bool Success { get; init; }
    public Guid? InvitationId { get; init; }
    public string? Message { get; init; }
}

public class InviteHouseholdMemberCommandValidator : AbstractValidator<InviteHouseholdMemberCommand>
{
    public InviteHouseholdMemberCommandValidator()
    {
        RuleFor(x => x.HouseholdId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Role)
            .Must(r => r == "admin" || r == "member")
            .WithMessage("Role must be 'admin' or 'member'.");
    }
}

public class InviteHouseholdMemberCommandHandler : IRequestHandler<InviteHouseholdMemberCommand, InviteHouseholdMemberResult>
{
    private static readonly TimeSpan InvitationTtl = TimeSpan.FromDays(14);

    private readonly IMizanDbContext _context;

    public InviteHouseholdMemberCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<InviteHouseholdMemberResult> Handle(InviteHouseholdMemberCommand request, CancellationToken cancellationToken)
    {
        var requester = await _context.HouseholdMembers
            .FirstOrDefaultAsync(m => m.HouseholdId == request.HouseholdId && m.UserId == request.RequestingUserId, cancellationToken);

        if (requester == null || !(requester.Role == "admin" || requester.Role == "owner"))
        {
            return new InviteHouseholdMemberResult { Success = false, Message = "Only household admins can invite members." };
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var invitee = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (invitee == null)
        {
            return new InviteHouseholdMemberResult { Success = false, Message = "No registered user with that email. They must sign up first." };
        }

        if (invitee.Id == request.RequestingUserId)
        {
            return new InviteHouseholdMemberResult { Success = false, Message = "You cannot invite yourself." };
        }

        var alreadyMember = await _context.HouseholdMembers
            .AnyAsync(m => m.HouseholdId == request.HouseholdId && m.UserId == invitee.Id, cancellationToken);
        if (alreadyMember)
        {
            return new InviteHouseholdMemberResult { Success = false, Message = "That user is already a member." };
        }

        var existingPending = await _context.HouseholdInvitations
            .FirstOrDefaultAsync(i => i.HouseholdId == request.HouseholdId
                && i.InvitedUserId == invitee.Id
                && i.Status == "pending", cancellationToken);
        if (existingPending != null)
        {
            return new InviteHouseholdMemberResult { Success = false, InvitationId = existingPending.Id, Message = "They already have a pending invite." };
        }

        var invitation = new HouseholdInvitation
        {
            Id = Guid.NewGuid(),
            HouseholdId = request.HouseholdId,
            InvitedUserId = invitee.Id,
            InvitedByUserId = request.RequestingUserId,
            Role = request.Role,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(InvitationTtl)
        };

        _context.HouseholdInvitations.Add(invitation);
        await _context.SaveChangesAsync(cancellationToken);

        return new InviteHouseholdMemberResult { Success = true, InvitationId = invitation.Id };
    }
}
