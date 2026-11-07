using FluentValidation;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>Rules cross the wire with enumeration names; the plan stores the Domain object.</summary>
public static class PlanRulesMapping
{
    public static PlanRules ToDomain(PlanRulesDto? dto)
    {
        if (dto is null) return new PlanRules();
        return new PlanRules
        {
            MacroMode = ParseEnum<MacroMode>(dto.MacroMode, MacroMode.None),
            TargetCalories = dto.TargetCalories,
            TargetProtein = dto.TargetProtein,
            MaxCarbs = dto.MaxCarbs,
            TargetFat = dto.TargetFat,
            EatingWindowStart = dto.EatingWindowStart,
            EatingWindowEnd = dto.EatingWindowEnd,
            AllowedFoodIds = dto.AllowedFoodIds.Distinct().ToList(),
            ForbiddenFoodIds = dto.ForbiddenFoodIds.Distinct().ToList(),
            WeeklyBudget = dto.WeeklyBudget,
            DailyBudget = dto.DailyBudget,
            Currency = string.IsNullOrWhiteSpace(dto.Currency) ? null : dto.Currency.Trim().ToUpperInvariant(),
            PrepStyle = ParseEnum<PrepStyle>(dto.PrepStyle, PrepStyle.Daily),
            BatchDays = dto.BatchDays.Distinct().OrderBy(d => d).ToList(),
        };
    }

    public static PlanRulesDto ToDto(PlanRules rules) => new()
    {
        MacroMode = rules.MacroMode.ToString(),
        TargetCalories = rules.TargetCalories,
        TargetProtein = rules.TargetProtein,
        MaxCarbs = rules.MaxCarbs,
        TargetFat = rules.TargetFat,
        EatingWindowStart = rules.EatingWindowStart,
        EatingWindowEnd = rules.EatingWindowEnd,
        AllowedFoodIds = rules.AllowedFoodIds.ToList(),
        ForbiddenFoodIds = rules.ForbiddenFoodIds.ToList(),
        WeeklyBudget = rules.WeeklyBudget,
        DailyBudget = rules.DailyBudget,
        Currency = rules.Currency,
        PrepStyle = rules.PrepStyle.ToString(),
        BatchDays = rules.BatchDays.ToList(),
    };

    public static bool IsEnumName<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) || Enum.TryParse<TEnum>(value, ignoreCase: true, out _);

    public static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? fallback
        : Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed
        : throw new Exceptions.DomainValidationException($"'{value}' is not a valid {typeof(TEnum).Name}.");
}

/// <summary>Rules are checked by code: enumeration names, then <see cref="PlanRules.Problems"/>.</summary>
public class PlanRulesDtoValidator : AbstractValidator<PlanRulesDto>
{
    public PlanRulesDtoValidator()
    {
        RuleFor(r => r.MacroMode).Must(PlanRulesMapping.IsEnumName<MacroMode>)
            .WithMessage("Macro mode must be None, Keto, HighProtein or Custom.");
        RuleFor(r => r.PrepStyle).Must(PlanRulesMapping.IsEnumName<PrepStyle>)
            .WithMessage("Prep style must be Daily or Batch.");
        RuleFor(r => r.Currency).Length(3).When(r => !string.IsNullOrWhiteSpace(r.Currency))
            .WithMessage("Currency must be a three-letter code.");
        RuleFor(r => r)
            .Custom((dto, context) =>
            {
                if (!PlanRulesMapping.IsEnumName<MacroMode>(dto.MacroMode) || !PlanRulesMapping.IsEnumName<PrepStyle>(dto.PrepStyle))
                    return;
                foreach (var problem in PlanRulesMapping.ToDomain(dto).Problems())
                    context.AddFailure("Rules", problem);
            });
    }
}

/// <summary>The shape of one entry. Access to the recipe or food is checked by the handler.</summary>
public class PlanEntryRequestValidator : AbstractValidator<PlanEntryRequest>
{
    public PlanEntryRequestValidator()
    {
        RuleFor(e => e.DayIndex).GreaterThanOrEqualTo(0).WithMessage("Days are counted from 0, the first day of the plan.");
        RuleFor(e => e.Slot).Must(PlanSlots.IsValid)
            .WithMessage($"Slot must be one of: {string.Join(", ", PlanSlots.All)}");
        RuleFor(e => e.Kind).Must(PlanRulesMapping.IsEnumName<PlanEntryKind>).WithMessage("Kind must be Recipe, Food or Text.");
        RuleFor(e => e.Notes).MaximumLength(1000);
        RuleFor(e => e.Text).MaximumLength(255);
        RuleFor(e => e.SortOrder).GreaterThanOrEqualTo(0);

        When(e => Is(e, PlanEntryKind.Recipe), () =>
        {
            RuleFor(e => e.RecipeId).NotEmpty().WithMessage("A recipe entry needs a recipe.");
            RuleFor(e => e.Servings).NotNull().GreaterThan(0).WithMessage("A recipe entry needs servings above zero.");
        });
        When(e => Is(e, PlanEntryKind.Food), () =>
        {
            RuleFor(e => e.FoodId).NotEmpty().WithMessage("A food entry needs a food.");
            RuleFor(e => e.Grams).NotNull().GreaterThan(0).WithMessage("A food entry needs a weight in grams above zero.");
        });
        When(e => Is(e, PlanEntryKind.Text), () =>
        {
            RuleFor(e => e.Text).NotEmpty().WithMessage("A text entry needs its text.");
        });
    }

    private static bool Is(PlanEntryRequest e, PlanEntryKind kind) =>
        Enum.TryParse<PlanEntryKind>(e.Kind, ignoreCase: true, out var parsed) && parsed == kind;
}

public class PrepSessionRequestValidator : AbstractValidator<PrepSessionRequest>
{
    public PrepSessionRequestValidator()
    {
        RuleFor(s => s.DayIndex).GreaterThanOrEqualTo(0);
        RuleFor(s => s.Title).NotEmpty().MaximumLength(255);
        RuleFor(s => s.Notes).MaximumLength(2000);
    }
}
