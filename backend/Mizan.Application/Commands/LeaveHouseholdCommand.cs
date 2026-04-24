using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record LeaveHouseholdCommand(Guid HouseholdId, Guid UserId) : IRequest<LeaveHouseholdResult>;

public record LeaveHouseholdResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class LeaveHouseholdCommandValidator : AbstractValidator<LeaveHouseholdCommand>
{
    public LeaveHouseholdCommandValidator()
    {
        RuleFor(x => x.HouseholdId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class LeaveHouseholdCommandHandler : IRequestHandler<LeaveHouseholdCommand, LeaveHouseholdResult>
{
    private readonly IMizanDbContext _context;

    public LeaveHouseholdCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<LeaveHouseholdResult> Handle(LeaveHouseholdCommand request, CancellationToken cancellationToken)
    {
        var membership = await _context.HouseholdMembers
            .FirstOrDefaultAsync(m => m.HouseholdId == request.HouseholdId && m.UserId == request.UserId, cancellationToken);
        if (membership == null)
        {
            return new LeaveHouseholdResult { Success = false, Message = "You're not a member of this household." };
        }

        // If the leaving user is the last admin/owner, block, the household would be orphaned.
        if (membership.Role == "admin" || membership.Role == "owner")
        {
            var otherAdmins = await _context.HouseholdMembers
                .CountAsync(m => m.HouseholdId == request.HouseholdId
                    && m.UserId != request.UserId
                    && (m.Role == "admin" || m.Role == "owner"), cancellationToken);
            if (otherAdmins == 0)
            {
                return new LeaveHouseholdResult { Success = false, Message = "Promote another member to admin before leaving." };
            }
        }

        _context.HouseholdMembers.Remove(membership);

        // If this household was their active one, clear the preference so the
        // next request falls back to "first joined household".
        var pref = await _context.UserHouseholdPreferences
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);
        if (pref != null && pref.ActiveHouseholdId == request.HouseholdId)
        {
            pref.ActiveHouseholdId = null;
            pref.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new LeaveHouseholdResult { Success = true };
    }
}
