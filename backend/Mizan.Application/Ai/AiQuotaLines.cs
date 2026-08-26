namespace Mizan.Application.Ai;

/// <summary>
/// Which allowance a feature spends from.
///
/// Most things share the caller's personal tier allowance, which is what a
/// daily cap is for. Two do not, and for the same reason: they are work the
/// product wants to happen and would otherwise be paid for out of the user's
/// own budget.
///
/// Onboarding is the sharper case. A single setup turn can make several
/// provider calls as the model records what it is told, so billing it to the
/// free chat allowance means a new user's first act is emptying it - the one
/// surface where the assistant earns its cost, priced so that using it costs
/// you the rest of the day (docs/REFOCUS.md §10).
///
/// Every line still lands in the same ledger and still passes under the same
/// global ceiling. This decides which per-user cap applies, nothing more.
/// </summary>
public enum AiQuotaLine
{
    /// <summary>The caller's tier allowance: chat, food analysis, suggestions.</summary>
    Personal = 0,

    /// <summary>An admin proving a prompt draft.</summary>
    Eval = 1,

    /// <summary>Setting a new user up.</summary>
    Onboarding = 2,
}

public static class AiQuotaLines
{
    public static AiQuotaLine For(string feature) => feature switch
    {
        AiFeatures.Eval => AiQuotaLine.Eval,
        AiFeatures.Onboarding => AiQuotaLine.Onboarding,
        _ => AiQuotaLine.Personal,
    };

    /// <summary>The features that spend from a line, for the usage query to group by.</summary>
    public static bool IsOnLine(string feature, AiQuotaLine line) => For(feature) == line;
}
