using FluentAssertions;
using Mizan.Domain.Usage;
using Xunit;

namespace Mizan.Tests.PlatformRules;

/// <summary>The allowance precedence, field by field: user, then household, then plan, then the tier default.</summary>
public class UsageLimitCompositionTests
{
    private static readonly UsageLimitValues Tier = new(5, 20_000, null, null);

    private static UsageLimitLayer User(int? requests = null, long? tokens = null, long? daily = null, long? monthly = null) =>
        new(UsageLimitSource.User, new UsageLimitValues(requests, tokens, daily, monthly));

    private static UsageLimitLayer Household(int? requests = null, long? tokens = null, long? daily = null, long? monthly = null) =>
        new(UsageLimitSource.Household, new UsageLimitValues(requests, tokens, daily, monthly));

    private static UsageLimitLayer Plan(int? requests = null, long? tokens = null, long? daily = null, long? monthly = null) =>
        new(UsageLimitSource.Plan, new UsageLimitValues(requests, tokens, daily, monthly));

    public static TheoryData<string, UsageLimitLayer[], string> Cases => new()
    {
        { "no overrides keep the tier, with no cost caps", [], "5 Default | 20000 Default | - Default | - Default" },
        { "the user's value wins over the household's", [User(requests: 1), Household(requests: 2)], "1 User | 20000 Default | - Default | - Default" },
        { "a field the user leaves empty comes from the household", [User(requests: 1), Household(tokens: 800)], "1 User | 800 Household | - Default | - Default" },
        { "the plan fills what the household leaves", [Household(requests: 2), Plan(requests: 9, monthly: 50)], "2 Household | 20000 Default | - Default | 50 Plan" },
        { "a plan alone sets every field it names", [Plan(requests: 9, tokens: 9, daily: 9, monthly: 9)], "9 Plan | 9 Plan | 9 Plan | 9 Plan" },
        { "zero is a cap, not an empty field", [User(requests: 0), Plan(requests: 9)], "0 User | 20000 Default | - Default | - Default" },
        { "a raise above the tier is allowed", [Household(requests: 50, tokens: 100_000)], "50 Household | 100000 Household | - Default | - Default" },
        { "within a scope the first row (the line's) wins", [User(requests: 1), User(requests: 3, daily: 7)], "1 User | 20000 Default | 7 User | - Default" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Compose_TakesEachFieldFromTheFirstLayerThatSetsIt(string _, UsageLimitLayer[] layers, string expected)
    {
        var composed = UsageLimitComposition.Compose(Tier, layers);

        static string Show<T>(SourcedLimit<T> limit) where T : struct => $"{(limit.Value is { } v ? v.ToString() : "-")} {limit.Source}";
        string.Join(" | ", Show(composed.DailyRequests), Show(composed.DailyTokens), Show(composed.DailyCostMicros), Show(composed.MonthlyCostMicros))
            .Should().Be(expected);
    }
}
