namespace Mizan.Domain.Usage;

/// <summary>Who an allowance override is for. The order is the order of precedence, most specific last.</summary>
public enum UsageLimitScope
{
    Plan = 0,
    Household = 1,
    User = 2,
}

/// <summary>Where one effective value came from.</summary>
public enum UsageLimitSource
{
    Default = 0,
    Plan = 1,
    Household = 2,
    User = 3,
}

/// <summary>A set of caps. A null field says nothing and leaves the decision to the next layer.</summary>
public sealed record UsageLimitValues(
    int? DailyRequests,
    long? DailyTokens,
    long? DailyCostMicros,
    long? MonthlyCostMicros)
{
    public static UsageLimitValues None { get; } = new(null, null, null, null);
}

/// <summary>One layer of caps and the scope it belongs to.</summary>
public sealed record UsageLimitLayer(UsageLimitSource Source, UsageLimitValues Values);

/// <summary>A value with the layer that supplied it. A null value means no cap.</summary>
public readonly record struct SourcedLimit<T>(T? Value, UsageLimitSource Source) where T : struct;

public sealed record ComposedUsageLimits(
    SourcedLimit<int> DailyRequests,
    SourcedLimit<long> DailyTokens,
    SourcedLimit<long> DailyCostMicros,
    SourcedLimit<long> MonthlyCostMicros);

/// <summary>
/// The precedence rule for allowances, field by field: the first layer that
/// sets a field wins, and a field no layer sets keeps the tier default. Layers
/// come most specific first (user, household, plan). The tier defaults have no
/// cost caps, so a cost cap exists only when someone set one.
/// </summary>
public static class UsageLimitComposition
{
    public static ComposedUsageLimits Compose(UsageLimitValues defaults, IEnumerable<UsageLimitLayer> layers)
    {
        var ordered = layers.ToList();

        SourcedLimit<T> Pick<T>(Func<UsageLimitValues, T?> field) where T : struct
        {
            foreach (var layer in ordered)
            {
                if (field(layer.Values) is { } value) return new SourcedLimit<T>(value, layer.Source);
            }

            return new SourcedLimit<T>(field(defaults), UsageLimitSource.Default);
        }

        return new ComposedUsageLimits(
            Pick(v => v.DailyRequests),
            Pick(v => v.DailyTokens),
            Pick(v => v.DailyCostMicros),
            Pick(v => v.MonthlyCostMicros));
    }
}
