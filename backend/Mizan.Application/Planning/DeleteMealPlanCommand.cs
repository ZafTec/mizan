using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>Removes a plan with its entries and sessions. Diary rows logged from it keep their nutrition and lose the link.</summary>
public record DeleteMealPlanCommand(Guid Id) : IRequest<Unit>;

public class DeleteMealPlanCommandHandler : IRequestHandler<DeleteMealPlanCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public DeleteMealPlanCommandHandler(IMizanDbContext context, PlanAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(DeleteMealPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _access.LoadEditableAsync(request.Id, cancellationToken);
        if (plan.Kind == PlanKind.Template)
            throw new DomainValidationException("This is a template. Delete it through the templates routes.");

        _context.PlanEntries.RemoveRange(plan.Entries);
        _context.PrepSessions.RemoveRange(plan.PrepSessions);
        _context.MealPlans.Remove(plan);
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return Unit.Value;
    }
}
