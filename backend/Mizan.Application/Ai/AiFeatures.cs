namespace Mizan.Application.Ai;

/// <summary>
/// Feature names as they appear in the usage ledger. Constants because the
/// usage tab groups by them and a typo would quietly create a second bucket.
/// </summary>
public static class AiFeatures
{
    public const string Chat = "chat";
    public const string FoodAnalysis = "food-analysis";
}
