using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// Publishes a template: a plan without dates that is copied into personal
/// plans. A global template belongs to no one and needs the admin role; a coach
/// (or an administrator) also keeps templates of their own.
/// </summary>
public record CreateTemplateCommand : CreateTemplateRequest, IRequest<MealPlanCreatedResult>;

public class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.DurationDays).InclusiveBetween(1, 31).WithMessage("A template runs for 1 to 31 days.");
        RuleFor(x => x.Rules!).SetValidator(new PlanRulesDtoValidator()).When(x => x.Rules is not null);
        RuleForEach(x => x.Entries).SetValidator(new PlanEntryRequestValidator());
        RuleForEach(x => x.PrepSessions).SetValidator(new PrepSessionRequestValidator());
        RuleForEach(x => x.Entries).Must((cmd, e) => e.DayIndex < cmd.DurationDays).WithMessage("An entry's day must be inside the template.");
        RuleForEach(x => x.PrepSessions).Must((cmd, s) => s.DayIndex < cmd.DurationDays).WithMessage("A prep session's day must be inside the template.");
    }
}

public class CreateTemplateCommandHandler : IRequestHandler<CreateTemplateCommand, MealPlanCreatedResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PlanAccess _access;
    private readonly PlanEntryBuilder _entries;
    private readonly HybridCache _cache;

    public CreateTemplateCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, PlanAccess access, PlanEntryBuilder entries, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _access = access;
        _entries = entries;
        _cache = cache;
    }

    public async Task<MealPlanCreatedResult> Handle(CreateTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _access.RequireUserId();
        if (request.Global && !_access.IsAdmin)
            throw new ForbiddenAccessException("Only an administrator publishes a global template.");
        if (!request.Global && !(_access.IsAdmin || _currentUser.IsInRole("trainer")))
            throw new ForbiddenAccessException("Templates are kept by coaches and administrators.");

        var payload = new CreateMealPlanRequest
        {
            Name = request.Name,
            Description = request.Description,
            DurationDays = request.DurationDays,
            Rules = request.Rules,
            Entries = request.Entries,
            PrepSessions = request.PrepSessions,
        };
        var template = await PlanFactory.BuildAsync(
            _entries, payload, request.Global ? null : userId, householdId: null, authoredBy: null,
            PlanStatus.Active, cancellationToken, PlanKind.Template);

        _context.MealPlans.Add(template);
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, template, cancellationToken);
        return new MealPlanCreatedResult(template.Id, template.Name, template.Status.ToString(), template.Entries.Count);
    }
}
