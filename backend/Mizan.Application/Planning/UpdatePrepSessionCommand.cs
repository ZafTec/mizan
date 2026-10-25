using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Planning;

public record UpdatePrepSessionCommand(Guid PlanId, Guid SessionId, PrepSessionRequest Session) : IRequest<Unit>;

public class UpdatePrepSessionCommandValidator : AbstractValidator<UpdatePrepSessionCommand>
{
    public UpdatePrepSessionCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Session).NotNull().SetValidator(new PrepSessionRequestValidator());
    }
}

public class UpdatePrepSessionCommandHandler : IRequestHandler<UpdatePrepSessionCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public UpdatePrepSessionCommandHandler(IMizanDbContext context, PlanAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(UpdatePrepSessionCommand request, CancellationToken cancellationToken)
    {
        var plan = await _access.LoadEditableAsync(request.PlanId, cancellationToken);
        var session = plan.PrepSessions.FirstOrDefault(s => s.Id == request.SessionId)
            ?? throw new EntityNotFoundException("Prep session", request.SessionId);
        if (request.Session.DayIndex >= plan.DurationDays)
            throw new DomainValidationException($"Day {request.Session.DayIndex} is outside the plan, which has {plan.DurationDays} day(s) counted from 0.");

        session.DayIndex = request.Session.DayIndex;
        session.Title = request.Session.Title.Trim();
        session.Notes = string.IsNullOrWhiteSpace(request.Session.Notes) ? null : request.Session.Notes.Trim();
        plan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return Unit.Value;
    }
}
