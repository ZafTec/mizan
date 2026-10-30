using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;

namespace Mizan.Application.Planning;

/// <summary>Adds a cooking session to a plan. Entries point at it; the shopping list groups by it.</summary>
public record AddPrepSessionCommand(Guid PlanId, PrepSessionRequest Session) : IRequest<PrepSessionCreatedResult>;

public class AddPrepSessionCommandValidator : AbstractValidator<AddPrepSessionCommand>
{
    public AddPrepSessionCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Session).NotNull().SetValidator(new PrepSessionRequestValidator());
    }
}

public class AddPrepSessionCommandHandler : IRequestHandler<AddPrepSessionCommand, PrepSessionCreatedResult>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public AddPrepSessionCommandHandler(IMizanDbContext context, PlanAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<PrepSessionCreatedResult> Handle(AddPrepSessionCommand request, CancellationToken cancellationToken)
    {
        var plan = await _access.LoadEditableAsync(request.PlanId, cancellationToken);
        if (request.Session.DayIndex >= plan.DurationDays)
            throw new DomainValidationException($"Day {request.Session.DayIndex} is outside the plan, which has {plan.DurationDays} day(s) counted from 0.");

        var session = new PrepSession
        {
            Id = Guid.NewGuid(),
            MealPlanId = plan.Id,
            DayIndex = request.Session.DayIndex,
            Title = request.Session.Title.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Session.Notes) ? null : request.Session.Notes.Trim(),
        };
        _context.PrepSessions.Add(session);
        plan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return new PrepSessionCreatedResult(session.Id);
    }
}
