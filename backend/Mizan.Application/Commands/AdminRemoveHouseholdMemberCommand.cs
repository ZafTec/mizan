using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

// Admin-only variant: no household-role check. The controller guards with
// [Authorize(Policy="RequireAdmin")].
public record AdminRemoveHouseholdMemberCommand(Guid HouseholdId, Guid TargetUserId) : IRequest<RemoveHouseholdMemberResult>;

public class AdminRemoveHouseholdMemberCommandValidator : AbstractValidator<AdminRemoveHouseholdMemberCommand>
{
    public AdminRemoveHouseholdMemberCommandValidator()
    {
        RuleFor(x => x.HouseholdId).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
    }
}

public class AdminRemoveHouseholdMemberCommandHandler : IRequestHandler<AdminRemoveHouseholdMemberCommand, RemoveHouseholdMemberResult>
{
    private readonly IMizanDbContext _context;

    public AdminRemoveHouseholdMemberCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<RemoveHouseholdMemberResult> Handle(AdminRemoveHouseholdMemberCommand request, CancellationToken cancellationToken)
    {
        var target = await _context.HouseholdMembers
            .FirstOrDefaultAsync(m => m.HouseholdId == request.HouseholdId && m.UserId == request.TargetUserId, cancellationToken);
        if (target == null)
        {
            return new RemoveHouseholdMemberResult { Success = false, Message = "Member not found." };
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
