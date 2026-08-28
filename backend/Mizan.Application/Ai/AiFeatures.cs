namespace Mizan.Application.Ai;

/// <summary>
/// Feature names as they appear in the usage ledger. Constants because the
/// usage tab groups by them and a typo would quietly create a second bucket.
/// </summary>
public static class AiFeatures
{
    public const string Chat = "chat";
    public const string FoodAnalysis = "food-analysis";
    public const string Suggestions = "suggestions";
    public const string Onboarding = "onboarding";

    /// <summary>A trainer reading a client. Bills the trainer, never the client.</summary>
    public const string TrainerClient = "trainer-client";

    /// <summary>An admin running the eval suite. Billed like any other call, and visible in the same tab.</summary>
    public const string Eval = "eval";
}
