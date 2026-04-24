using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

// Admin-only. Deletes the household and cascades members via EF config.
public record AdminDeleteHouseholdCommand(Guid HouseholdId) : IRequest<AdminDeleteHouseholdResult>;

public record AdminDeleteHouseholdResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class AdminDeleteHouseholdCommandValidator : AbstractValidator<AdminDeleteHouseholdCommand>
{
    public AdminDeleteHouseholdCommandValidator()
    {
        RuleFor(x => x.HouseholdId).NotEmpty();
    }
}

public class AdminDeleteHouseholdCommandHandler : IRequestHandler<AdminDeleteHouseholdCommand, AdminDeleteHouseholdResult>
{
    private readonly IMizanDbContext _context;

    public AdminDeleteHouseholdCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDeleteHouseholdResult> Handle(AdminDeleteHouseholdCommand request, CancellationToken cancellationToken)
    {
        var household = await _context.Households.FirstOrDefaultAsync(h => h.Id == request.HouseholdId, cancellationToken);
        if (household == null)
        {
            return new AdminDeleteHouseholdResult { Success = false, Message = "Household not found." };
        }

        // Clear any user preferences that pointed at this household.
        var prefs = await _context.UserHouseholdPreferences
            .Where(p => p.ActiveHouseholdId == request.HouseholdId)
            .ToListAsync(cancellationToken);
        foreach (var p in prefs)
        {
            p.ActiveHouseholdId = null;
            p.UpdatedAt = DateTime.UtcNow;
        }

        _context.Households.Remove(household);
        await _context.SaveChangesAsync(cancellationToken);

        return new AdminDeleteHouseholdResult { Success = true };
    }
}
