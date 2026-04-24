using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record SetActiveHouseholdCommand(Guid UserId, Guid? HouseholdId) : IRequest<SetActiveHouseholdResult>;

public record SetActiveHouseholdResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public Guid? ActiveHouseholdId { get; init; }
}

public class SetActiveHouseholdCommandValidator : AbstractValidator<SetActiveHouseholdCommand>
{
    public SetActiveHouseholdCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class SetActiveHouseholdCommandHandler : IRequestHandler<SetActiveHouseholdCommand, SetActiveHouseholdResult>
{
    private readonly IMizanDbContext _context;

    public SetActiveHouseholdCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<SetActiveHouseholdResult> Handle(SetActiveHouseholdCommand request, CancellationToken cancellationToken)
    {
        if (request.HouseholdId.HasValue)
        {
            var isMember = await _context.HouseholdMembers
                .AnyAsync(m => m.HouseholdId == request.HouseholdId.Value && m.UserId == request.UserId, cancellationToken);
            if (!isMember)
            {
                return new SetActiveHouseholdResult { Success = false, Message = "You're not a member of that household." };
            }
        }

        var pref = await _context.UserHouseholdPreferences
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);
        if (pref == null)
        {
            pref = new UserHouseholdPreference
            {
                UserId = request.UserId,
                ActiveHouseholdId = request.HouseholdId,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserHouseholdPreferences.Add(pref);
        }
        else
        {
            pref.ActiveHouseholdId = request.HouseholdId;
            pref.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new SetActiveHouseholdResult { Success = true, ActiveHouseholdId = request.HouseholdId };
    }
}
