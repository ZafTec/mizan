namespace Mizan.Domain.Goals;

/// <summary>
/// The goal types a goal can have. The command validator, the assistant's tool schema and the MCP
/// tool description all read this list, so none of them can offer a type another refuses.
/// </summary>
public static class GoalTypes
{
    public const string WeightLoss = "weight_loss";
    public const string MuscleGain = "muscle_gain";
    public const string Maintenance = "maintenance";
    public const string General = "general";

    public static IReadOnlyList<string> All { get; } = [WeightLoss, MuscleGain, Maintenance, General];

    /// <summary>The list as prose, constant so an attribute can carry it.</summary>
    public const string Described = WeightLoss + ", " + MuscleGain + ", " + Maintenance + " or " + General;

    /// <summary>The list as a JSON array, for a tool schema's enum.</summary>
    public static string Json { get; } = System.Text.Json.JsonSerializer.Serialize(All);

    public static bool IsKnown(string? value) =>
        value is not null && All.Contains(value.Trim().ToLowerInvariant());
}
