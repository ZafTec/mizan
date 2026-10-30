using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Ai;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// Reads the goal a plan's targets come from and resolves them with <see cref="MacroTargets"/>.
/// The goal is the plan owner's, read through <see cref="IDataAccessPolicy"/> for the viewer and
/// the purpose: a viewer who may not read the owner's nutrition gets the targets without it, and
/// a template has no owner's goal at all.
/// </summary>
public sealed class PlanTargetsReader
{
    private readonly IMizanDbContext _context;
    private readonly IDataAccessPolicy _policy;

    public PlanTargetsReader(IMizanDbContext context, IDataAccessPolicy policy)
    {
        _context = context;
        _policy = policy;
    }

    public async Task<PlanTargets> ForPlanAsync(MealPlan plan, Guid viewerId, CancellationToken cancellationToken)
    {
        var owner = plan.Kind == PlanKind.Personal ? plan.UserId : null;
        var goal = owner is { } ownerId ? await GoalAsync(viewerId, ownerId, AccessPurpose.Display, cancellationToken) : null;
        return MacroTargets.Resolve(plan.Rules, goal);
    }

    /// <summary>The subject's current goal when the principal may read their nutrition for this purpose, else null.</summary>
    public async Task<GoalTargets?> GoalAsync(Guid principalId, Guid subjectId, AccessPurpose purpose, CancellationToken cancellationToken)
    {
        if (!await _policy.CanReadAsync(principalId, subjectId, DataAxis.Nutrition, purpose, cancellationToken)) return null;
        return await _context.UserGoals.AsNoTracking()
            .Where(g => g.UserId == subjectId && g.IsActive)
            .OrderByDescending(g => g.UpdatedAt)
            .Select(g => new GoalTargets(g.TargetCalories, g.TargetProteinGrams, g.TargetCarbsGrams, g.TargetFatGrams))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static PlanTargetsDto ToDto(PlanRules rules, PlanTargets targets) => new()
    {
        MacroMode = rules.MacroMode.ToString(),
        Calories = ToDto(targets.Calories),
        Protein = ToDto(targets.Protein),
        MaxCarbs = ToDto(targets.MaxCarbs),
        Fat = ToDto(targets.Fat),
        GoalMissing = targets.GoalMissing,
    };

    private static PlanTargetDto ToDto(EffectiveTarget target) => new(target.Value, target.Source);
}

/// <summary>
/// GET /api/MealPlans/rules/defaults: the targets a mode gives the signed-in person, with what
/// they typed so far, so the rules form shows them without computing anything.
/// </summary>
public record GetPlanTargetsQuery(
    string? MacroMode = null,
    decimal? TargetCalories = null,
    decimal? TargetProtein = null,
    decimal? MaxCarbs = null,
    decimal? TargetFat = null) : IRequest<PlanTargetsDto>;

public class GetPlanTargetsQueryValidator : AbstractValidator<GetPlanTargetsQuery>
{
    public GetPlanTargetsQueryValidator()
    {
        RuleFor(x => x.MacroMode).Must(PlanRulesMapping.IsEnumName<MacroMode>)
            .WithMessage("Macro mode must be None, Keto, HighProtein or Custom.");
        RuleFor(x => x.TargetCalories).GreaterThan(0).When(x => x.TargetCalories.HasValue);
        RuleFor(x => x.TargetProtein).GreaterThanOrEqualTo(0).When(x => x.TargetProtein.HasValue);
        RuleFor(x => x.MaxCarbs).GreaterThanOrEqualTo(0).When(x => x.MaxCarbs.HasValue);
        RuleFor(x => x.TargetFat).GreaterThanOrEqualTo(0).When(x => x.TargetFat.HasValue);
    }
}

public class GetPlanTargetsQueryHandler : IRequestHandler<GetPlanTargetsQuery, PlanTargetsDto>
{
    private readonly PlanTargetsReader _targets;
    private readonly ICurrentUserService _currentUser;

    public GetPlanTargetsQueryHandler(PlanTargetsReader targets, ICurrentUserService currentUser)
    {
        _targets = targets;
        _currentUser = currentUser;
    }

    public async Task<PlanTargetsDto> Handle(GetPlanTargetsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        var rules = new PlanRules
        {
            MacroMode = PlanRulesMapping.ParseEnum(request.MacroMode, MacroMode.None),
            TargetCalories = request.TargetCalories,
            TargetProtein = request.TargetProtein,
            MaxCarbs = request.MaxCarbs,
            TargetFat = request.TargetFat,
        };
        var goal = await _targets.GoalAsync(userId, userId, AccessPurpose.Display, cancellationToken);
        return PlanTargetsReader.ToDto(rules, MacroTargets.Resolve(rules, goal));
    }
}
