using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// Copies a template into a new active personal plan starting on a date: its
/// rules, its sessions and its entries, with the entries re-pointed at the
/// copied sessions. The template itself is never changed by the copy.
/// </summary>
public record ApplyTemplateCommand(Guid TemplateId, DateOnly StartDate, Guid? HouseholdId, string? Name) : IRequest<MealPlanCreatedResult>;

public class ApplyTemplateCommandValidator : AbstractValidator<ApplyTemplateCommand>
{
    public ApplyTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(255);
    }
}

public class ApplyTemplateCommandHandler : IRequestHandler<ApplyTemplateCommand, MealPlanCreatedResult>
{
    private readonly IMizanDbContext _context;
    private readonly IHouseholdAccess _households;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public ApplyTemplateCommandHandler(IMizanDbContext context, IHouseholdAccess households, PlanAccess access, HybridCache cache)
    {
        _context = context;
        _households = households;
        _access = access;
        _cache = cache;
    }

    public async Task<MealPlanCreatedResult> Handle(ApplyTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _access.RequireUserId();
        var template = await _access.LoadReadableAsync(request.TemplateId, cancellationToken);
        if (template.Kind != PlanKind.Template)
            throw new EntityNotFoundException("Template", request.TemplateId);
        if (request.HouseholdId.HasValue && !await _households.CanAccessAsync(request.HouseholdId.Value, cancellationToken))
            throw new ForbiddenAccessException("You are not a member of this household");

        var plan = Copy(template, userId, request.StartDate, request.Name, request.HouseholdId, PlanStatus.Active, authoredBy: null);
        _context.MealPlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return new MealPlanCreatedResult(plan.Id, plan.Name, plan.Status.ToString(), plan.Entries.Count);
    }

    /// <summary>
    /// A deep copy with new ids into a personal plan of <paramref name="ownerId"/>.
    /// A person applying a template gets an active plan; a coach applying one for
    /// a client gets a draft that names the coach as its author. Pure so a test
    /// can hold it to the template.
    /// </summary>
    public static MealPlan Copy(
        MealPlan template, Guid ownerId, DateOnly startDate, string? name, Guid? householdId, PlanStatus status, Guid? authoredBy)
    {
        var now = DateTime.UtcNow;
        var plan = new MealPlan
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            HouseholdId = householdId,
            AuthoredByUserId = authoredBy,
            Kind = PlanKind.Personal,
            Status = status,
            Name = string.IsNullOrWhiteSpace(name) ? template.Name : name.Trim(),
            Description = template.Description,
            StartDate = startDate,
            DurationDays = template.DurationDays,
            Rules = PlanRulesMapping.ToDomain(PlanRulesMapping.ToDto(template.Rules)),
            CreatedAt = now,
            UpdatedAt = now,
        };

        var sessionIds = new Dictionary<Guid, Guid>();
        foreach (var session in template.PrepSessions.OrderBy(s => s.DayIndex))
        {
            var copy = new PrepSession { Id = Guid.NewGuid(), MealPlanId = plan.Id, DayIndex = session.DayIndex, Title = session.Title, Notes = session.Notes };
            sessionIds[session.Id] = copy.Id;
            plan.PrepSessions.Add(copy);
        }

        foreach (var entry in template.Entries.OrderBy(e => e.DayIndex).ThenBy(e => PlanSlots.Order(e.Slot)).ThenBy(e => e.SortOrder))
        {
            plan.Entries.Add(new PlanEntry
            {
                Id = Guid.NewGuid(),
                MealPlanId = plan.Id,
                DayIndex = entry.DayIndex,
                Slot = entry.Slot,
                Kind = entry.Kind,
                RecipeId = entry.RecipeId,
                Servings = entry.Servings,
                FoodId = entry.FoodId,
                Grams = entry.Grams,
                Text = entry.Text,
                Notes = entry.Notes,
                SortOrder = entry.SortOrder,
                PrepSessionId = entry.PrepSessionId is { } old && sessionIds.TryGetValue(old, out var mapped) ? mapped : null,
            });
        }
        return plan;
    }
}
