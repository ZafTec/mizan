using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record RemoveHouseholdMemberCommand(
    Guid HouseholdId,
    Guid TargetUserId,
    Guid ActingUserId
) : IRequest<RemoveHouseholdMemberResult>;

public record RemoveHouseholdMemberResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class RemoveHouseholdMemberCommandValidator : AbstractValidator<RemoveHouseholdMemberCommand>
{
    public RemoveHouseholdMemberCommandValidator()
    {
        RuleFor(x => x.HouseholdId).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();
    }
}

public class RemoveHouseholdMemberCommandHandler : IRequestHandler<RemoveHouseholdMemberCommand, RemoveHouseholdMemberResult>
{
    private readonly IMizanDbContext _context;

    public RemoveHouseholdMemberCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<RemoveHouseholdMemberResult> Handle(RemoveHouseholdMemberCommand request, CancellationToken cancellationToken)
    {
        if (request.TargetUserId == request.ActingUserId)
        {
            return new RemoveHouseholdMemberResult { Success = false, Message = "Use 'leave household' to remove yourself." };
        }

        var acting = await _context.HouseholdMembers
            .FirstOrDefaultAsync(m => m.HouseholdId == request.HouseholdId && m.UserId == request.ActingUserId, cancellationToken);
        if (acting == null || !(acting.Role == "admin" || acting.Role == "owner"))
        {
            return new RemoveHouseholdMemberResult { Success = false, Message = "Only household admins can remove members." };
        }

        var target = await _context.HouseholdMembers
            .FirstOrDefaultAsync(m => m.HouseholdId == request.HouseholdId && m.UserId == request.TargetUserId, cancellationToken);
        if (target == null)
        {
            return new RemoveHouseholdMemberResult { Success = false, Message = "Target is not a member." };
        }

        // Owners can be removed only by other owners.
        if (target.Role == "owner" && acting.Role != "owner")
        {
            return new RemoveHouseholdMemberResult { Success = false, Message = "Only an owner can remove another owner." };
        }

        _context.HouseholdMembers.Remove(target);

        var targetPref = await _context.UserHouseholdPreferences
            .FirstOrDefaultAsync(p => p.UserId == request.TargetUserId, cancellationToken);
        if (targetPref != null && targetPref.ActiveHouseholdId == request.HouseholdId)
        {
            targetPref.ActiveHouseholdId = null;
            targetPref.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new RemoveHouseholdMemberResult { Success = true };
    }
}
