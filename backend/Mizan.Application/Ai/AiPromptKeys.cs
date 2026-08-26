namespace Mizan.Application.Ai;

/// <summary>
/// The programmable surfaces. Code asks for a key; which version answers is an
/// admin decision (docs/REFOCUS.md §12).
/// </summary>
public static class AiPromptKeys
{
    public const string Chat = "chat.system";
    public const string FoodAnalysis = "food.analysis";
    public const string Suggestions = "nutrition.suggestions";
    public const string Onboarding = "onboarding.agent";

    public static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>
        {
            [Chat] = "How the assistant answers questions about a user's own log.",
            [FoodAnalysis] = "How a food photo is turned into structured nutrition data.",
            [Suggestions] = "How meal ideas are proposed against the macros a user has left today.",
            [Onboarding] = "How a new user is set up through conversation instead of a six-screen form.",
        };
}
