namespace Mizan.Domain.Ai;

/// <summary>
/// The axes of personal data: the three things this app logs
/// (docs/ARCHITECTURE.md#navigation-and-logging), which are also the three grants a trainer
/// relationship carries, and the meal plans and shopping lists built from them. Consent and access
/// are decided per axis, never wholesale.
/// </summary>
public enum DataAxis
{
    Nutrition = 0,
    Training = 1,
    Body = 2,

    /// <summary>
    /// Meal plans and shopping lists. No consent flag of its own: reading follows the nutrition
    /// share flag and writing the nutrition write flag, because a plan is food the person intends
    /// to eat (docs/AI.md#consent). A trainer grant never covers it.
    /// </summary>
    Planning = 3,
}

/// <summary>
/// Why a reader wants the data. The same principal can be allowed to see an
/// axis in the app and not allowed to send it to a model, so the purpose is
/// part of the question - see docs/AI.md#consent.
/// </summary>
public enum AccessPurpose
{
    /// <summary>A person reading it in the product.</summary>
    Display = 0,

    /// <summary>Building context for a model call.</summary>
    AiContext = 1,
}
