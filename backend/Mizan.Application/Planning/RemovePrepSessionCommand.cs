using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Planning;

/// <summary>Removes a cooking session. Its entries stay in the plan without a session.</summary>
public record RemovePrepSessionCommand(Guid PlanId, Guid SessionId) : IRequest<Unit>;

public class RemovePrepSessionCommandHandler : IRequestHandler<RemovePrepSessionCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public RemovePrepSessionCommandHandler(IMizanDbContext context, PlanAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(RemovePrepSessionCommand request, CancellationToken cancellationToken)
    {
        var plan = await _access.LoadEditableAsync(request.PlanId, cancellationToken);
        var session = plan.PrepSessions.FirstOrDefault(s => s.Id == request.SessionId)
            ?? throw new EntityNotFoundException("Prep session", request.SessionId);
        foreach (var entry in plan.Entries.Where(e => e.PrepSessionId == session.Id))
            entry.PrepSessionId = null;
        _context.PrepSessions.Remove(session);
        plan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return Unit.Value;
    }
}
