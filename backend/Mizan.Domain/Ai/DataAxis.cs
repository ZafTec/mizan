namespace Mizan.Domain.Ai;

/// <summary>
/// The three axes of personal data, matching the three things this app logs
/// (docs/REFOCUS.md §1) and the three grants a trainer relationship already
/// carries. Consent and access are decided per axis, never wholesale.
/// </summary>
public enum DataAxis
{
    Nutrition = 0,
    Training = 1,
    Body = 2,
}

/// <summary>
/// Why a reader wants the data. The same principal can be allowed to see an
/// axis in the app and not allowed to send it to a model, so the purpose is
/// part of the question - see docs/REFOCUS.md §11.
/// </summary>
public enum AccessPurpose
{
    /// <summary>A person reading it in the product.</summary>
    Display = 0,

    /// <summary>Building context for a model call.</summary>
    AiContext = 1,
}
