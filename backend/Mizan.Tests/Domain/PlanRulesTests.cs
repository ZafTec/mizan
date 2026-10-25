using FluentAssertions;
using Mizan.Domain.Planning;
using Xunit;

namespace Mizan.Tests.PlanningRules;

public class PlanRulesTests
{
    public static TheoryData<string, PlanRules, string?> Cases => new()
    {
        { "empty rules are usable", new PlanRules(), null },
        { "keto with explicit targets is usable", new PlanRules { MacroMode = MacroMode.Keto, TargetCalories = 1800, TargetProtein = 120 }, null },
        { "zero calories", new PlanRules { TargetCalories = 0 }, "Target calories must be above zero." },
        { "negative protein", new PlanRules { TargetProtein = -1 }, "Target protein cannot be negative." },
        { "negative carbs", new PlanRules { MaxCarbs = -5 }, "Maximum carbs cannot be negative." },
        { "negative fat", new PlanRules { TargetFat = -5 }, "Target fat cannot be negative." },
        { "negative budget", new PlanRules { WeeklyBudget = -10, Currency = "ETB" }, "Weekly budget cannot be negative." },
        { "budget without currency", new PlanRules { WeeklyBudget = 500 }, "A weekly budget needs a currency." },
        { "negative daily budget", new PlanRules { DailyBudget = -1, Currency = "ETB" }, "Daily budget cannot be negative." },
        { "daily budget without currency", new PlanRules { DailyBudget = 80 }, "A daily budget needs a currency." },
        { "two-letter currency", new PlanRules { Currency = "ET" }, "Currency must be a three-letter code." },
        { "window with only a start", new PlanRules { EatingWindowStart = new TimeOnly(12, 0) }, "An eating window needs both a start and an end." },
        { "window that ends first", new PlanRules { EatingWindowStart = new TimeOnly(20, 0), EatingWindowEnd = new TimeOnly(12, 0) }, "The eating window must end after it starts." },
        { "batch without days", new PlanRules { PrepStyle = PrepStyle.Batch }, "Batch cooking needs at least one cooking day." },
        { "negative batch day", new PlanRules { PrepStyle = PrepStyle.Batch, BatchDays = [-1] }, "Cooking days are counted from the first day of the plan." },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Problems_NamesWhatAPersonMustFix(string _, PlanRules rules, string? expected)
    {
        var problems = rules.Problems();
        if (expected is null) problems.Should().BeEmpty();
        else problems.Should().Contain(expected);
    }

    [Fact]
    public void AFoodCannotBeBothAllowedAndForbidden()
    {
        var food = Guid.NewGuid();
        new PlanRules { AllowedFoodIds = [food], ForbiddenFoodIds = [food] }.Problems()
            .Should().Contain("A food cannot be both allowed and forbidden.");
    }

    [Theory]
    [InlineData(MacroMode.None, null, null)]
    [InlineData(MacroMode.Keto, null, 30)]
    [InlineData(MacroMode.Keto, 20, 20)]
    [InlineData(MacroMode.HighProtein, null, null)]
    [InlineData(MacroMode.Custom, 100, 100)]
    public void EffectiveMaxCarbs_IsTheExplicitCeilingElseKetosDefault(MacroMode mode, int? explicitCarbs, int? expected)
    {
        new PlanRules { MacroMode = mode, MaxCarbs = explicitCarbs }.EffectiveMaxCarbs.Should().Be(expected);
    }
}
