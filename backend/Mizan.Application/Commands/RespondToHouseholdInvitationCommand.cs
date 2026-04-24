using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

// Single command handles accept / decline / revoke. action ∈ { accept, decline, revoke }.
// - accept: invitee becomes a household member
// - decline: invitee rejects the invite
// - revoke: inviter (household admin) cancels the invite before it's answered
public record RespondToHouseholdInvitationCommand(
    Guid InvitationId,
    Guid ActingUserId,
    string Action
) : IRequest<RespondToHouseholdInvitationResult>;

public record RespondToHouseholdInvitationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public Guid? HouseholdId { get; init; }
}

public class RespondToHouseholdInvitationCommandValidator : AbstractValidator<RespondToHouseholdInvitationCommand>
{
    public RespondToHouseholdInvitationCommandValidator()
    {
        RuleFor(x => x.InvitationId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();
        RuleFor(x => x.Action)
            .Must(a => a == "accept" || a == "decline" || a == "revoke")
            .WithMessage("Action must be accept, decline, or revoke.");
    }
}

public class RespondToHouseholdInvitationCommandHandler : IRequestHandler<RespondToHouseholdInvitationCommand, RespondToHouseholdInvitationResult>
{
    private readonly IMizanDbContext _context;

    public RespondToHouseholdInvitationCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<RespondToHouseholdInvitationResult> Handle(RespondToHouseholdInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await _context.HouseholdInvitations
            .FirstOrDefaultAsync(i => i.Id == request.InvitationId, cancellationToken);
        if (invitation == null)
        {
            return new RespondToHouseholdInvitationResult { Success = false, Message = "Invitation not found." };
        }

        if (invitation.Status != "pending")
        {
            return new RespondToHouseholdInvitationResult { Success = false, Message = $"Invitation already {invitation.Status}." };
        }

        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = "expired";
            await _context.SaveChangesAsync(cancellationToken);
            return new RespondToHouseholdInvitationResult { Success = false, Message = "Invitation expired." };
        }

        switch (request.Action)
        {
            case "accept":
            case "decline":
                if (invitation.InvitedUserId != request.ActingUserId)
                {
                    return new RespondToHouseholdInvitationResult { Success = false, Message = "Only the invited user can accept or decline." };
                }
                break;

            case "revoke":
                // Inviter or any household admin can revoke.
                var isInviter = invitation.InvitedByUserId == request.ActingUserId;
                var isAdmin = await _context.HouseholdMembers
                    .AnyAsync(m => m.HouseholdId == invitation.HouseholdId
                        && m.UserId == request.ActingUserId
                        && (m.Role == "admin" || m.Role == "owner"), cancellationToken);
                if (!isInviter && !isAdmin)
                {
                    return new RespondToHouseholdInvitationResult { Success = false, Message = "Only the inviter or a household admin can revoke an invite." };
                }
                break;
        }

        invitation.RespondedAt = DateTime.UtcNow;

        if (request.Action == "accept")
        {
            invitation.Status = "accepted";
            _context.HouseholdMembers.Add(new HouseholdMember
            {
                HouseholdId = invitation.HouseholdId,
                UserId = invitation.InvitedUserId,
                Role = invitation.Role,
                CanEditRecipes = true,
                CanEditShoppingList = true,
                CanViewNutrition = invitation.Role == "admin" || invitation.Role == "owner",
                JoinedAt = DateTime.UtcNow
            });
        }
        else if (request.Action == "decline")
        {
            invitation.Status = "declined";
        }
        else
        {
            invitation.Status = "revoked";
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new RespondToHouseholdInvitationResult
        {
            Success = true,
            HouseholdId = invitation.HouseholdId
        };
    }
}
