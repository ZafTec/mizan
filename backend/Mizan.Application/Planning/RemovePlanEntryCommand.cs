using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Planning;

/// <summary>Removes one entry by its id. Diary rows logged from it keep their nutrition.</summary>
public record RemovePlanEntryCommand(Guid PlanId, Guid EntryId) : IRequest<Unit>;

public class RemovePlanEntryCommandHandler : IRequestHandler<RemovePlanEntryCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public RemovePlanEntryCommandHandler(IMizanDbContext context, PlanAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(RemovePlanEntryCommand request, CancellationToken cancellationToken)
    {
        var plan = await _access.LoadEditableAsync(request.PlanId, cancellationToken);
        var entry = plan.Entries.FirstOrDefault(e => e.Id == request.EntryId)
            ?? throw new EntityNotFoundException("Plan entry", request.EntryId);
        _context.PlanEntries.Remove(entry);
        plan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return Unit.Value;
    }
}
