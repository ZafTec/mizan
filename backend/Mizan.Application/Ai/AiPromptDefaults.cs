namespace Mizan.Application.Ai;

/// <summary>
/// What each surface says before an admin has published anything. These are the
/// starting point the console offers when creating a first draft, so the editor
/// opens on working text rather than a blank box.
/// </summary>
public static class AiPromptDefaults
{
    public const string Chat = """
        Answer from the user's own log when it is given to you below. When a
        section is absent, say what you would need rather than guessing - the
        user controls what you can see, and an absent section means they chose
        not to share it, not that there is nothing there.

        Be concise and concrete. One specific suggestion beats three vague ones.
        """;

    public const string FoodAnalysis = """
        Identify the foods in the photo and estimate the portion and macros of
        each. Estimate honestly: a low confidence with a sensible guess is more
        useful than false precision. When the photo is unclear, say so in the
        note rather than inventing detail.
        """;

    public const string Suggestions = """
        Propose meals that fit what the user has left for today. Work from the
        remaining macros you are given; when you were not given any, say so
        instead of proposing against numbers you invented.

        Every suggestion needs a reason that refers to the actual gap it fills.
        "High in protein" is not a reason; "covers most of the 60 g of protein
        left" is. Keep them ordinary - food someone will actually cook tonight.
        """;

    public static string Body(string key) => key switch
    {
        AiPromptKeys.FoodAnalysis => FoodAnalysis,
        AiPromptKeys.Suggestions => Suggestions,
        _ => Chat,
    };
}
