using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Planning;

/// <summary>Rewrites one entry. The entry id is the one the plan lists; it stays the same.</summary>
public record UpdatePlanEntryCommand(Guid PlanId, Guid EntryId, PlanEntryRequest Entry) : IRequest<Unit>;

public class UpdatePlanEntryCommandValidator : AbstractValidator<UpdatePlanEntryCommand>
{
    public UpdatePlanEntryCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.Entry).NotNull().SetValidator(new PlanEntryRequestValidator());
    }
}

public class UpdatePlanEntryCommandHandler : IRequestHandler<UpdatePlanEntryCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly PlanEntryBuilder _entries;
    private readonly HybridCache _cache;

    public UpdatePlanEntryCommandHandler(IMizanDbContext context, PlanAccess access, PlanEntryBuilder entries, HybridCache cache)
    {
        _context = context;
        _access = access;
        _entries = entries;
        _cache = cache;
    }

    public async Task<Unit> Handle(UpdatePlanEntryCommand request, CancellationToken cancellationToken)
    {
        var plan = await _access.LoadEditableAsync(request.PlanId, cancellationToken);
        var entry = plan.Entries.FirstOrDefault(e => e.Id == request.EntryId)
            ?? throw new EntityNotFoundException("Plan entry", request.EntryId);
        await _entries.ApplyAsync(plan, entry, request.Entry, cancellationToken);
        plan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return Unit.Value;
    }
}
