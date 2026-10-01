using Mizan.Contracts.Mcp;
using Mizan.Domain.Ai;

namespace Mizan.Application.Common;

/// <summary>Which scope a connected app needs to read or write one of the three data axes.</summary>
public static class GrantAxes
{
    public static string ReadScope(DataAxis axis) => axis switch
    {
        DataAxis.Nutrition => McpScopes.NutritionRead,
        DataAxis.Training => McpScopes.TrainingRead,
        _ => McpScopes.BodyRead,
    };

    public static string WriteScope(DataAxis axis) => axis switch
    {
        DataAxis.Nutrition => McpScopes.NutritionWrite,
        DataAxis.Training => McpScopes.TrainingWrite,
        _ => McpScopes.BodyWrite,
    };
}
