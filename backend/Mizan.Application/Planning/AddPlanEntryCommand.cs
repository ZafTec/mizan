using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Planning;

/// <summary>Adds one entry to a plan or template.</summary>
public record AddPlanEntryCommand(Guid PlanId, PlanEntryRequest Entry) : IRequest<PlanEntryCreatedResult>;

public class AddPlanEntryCommandValidator : AbstractValidator<AddPlanEntryCommand>
{
    public AddPlanEntryCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Entry).NotNull().SetValidator(new PlanEntryRequestValidator());
    }
}

public class AddPlanEntryCommandHandler : IRequestHandler<AddPlanEntryCommand, PlanEntryCreatedResult>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly PlanEntryBuilder _entries;
    private readonly HybridCache _cache;

    public AddPlanEntryCommandHandler(IMizanDbContext context, PlanAccess access, PlanEntryBuilder entries, HybridCache cache)
    {
        _context = context;
        _access = access;
        _entries = entries;
        _cache = cache;
    }

    public async Task<PlanEntryCreatedResult> Handle(AddPlanEntryCommand request, CancellationToken cancellationToken)
    {
        var plan = await _access.LoadEditableAsync(request.PlanId, cancellationToken);
        var entry = (await _entries.BuildAsync(plan, [request.Entry], null, cancellationToken)).Single();
        // Through the set, not the navigation: an entity found by change
        // detection with its key already set is taken for an existing row.
        _context.PlanEntries.Add(entry);
        plan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return new PlanEntryCreatedResult(entry.Id);
    }
}
