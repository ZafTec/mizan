using Mizan.Domain.Ai;

namespace Mizan.Domain.Entities;

/// <summary>
/// What the AI may see about one user. Every flag defaults to false, and the
/// absence of a row means the same thing: nothing.
///
/// Consent here is withholding, not instructing - a disabled axis is never
/// given to the context builder, rather than given and accompanied by a
/// request not to mention it. See docs/REFOCUS.md §11.
/// </summary>
public class UserAiConsent
{
    public Guid UserId { get; set; }

    /// <summary>Master switch. Off means no axis is shared, whatever the rest say.</summary>
    public bool Enabled { get; set; }

    public bool ShareNutrition { get; set; }
    public bool ShareTraining { get; set; }
    public bool ShareBody { get; set; }

    /// <summary>
    /// Master switch for the assistant acting rather than answering. Reading
    /// and writing are separate decisions: someone may happily let it see the
    /// log and still want to be the only one who writes to it.
    /// </summary>
    public bool AllowWrites { get; set; }

    public bool WriteNutrition { get; set; }
    public bool WriteTraining { get; set; }
    public bool WriteBody { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? User { get; set; }

    /// <summary>The default for a user who has never been asked: nothing at all.</summary>
    public static UserAiConsent None(Guid userId) => new() { UserId = userId };

    public bool Allows(DataAxis axis) => Enabled && axis switch
    {
        DataAxis.Nutrition => ShareNutrition,
        DataAxis.Training => ShareTraining,
        DataAxis.Body => ShareBody,
        _ => false,
    };

    /// <summary>
    /// Whether the assistant may write this axis on the user's behalf. Not
    /// gated on <see cref="Enabled"/>: that switch governs what the model is
    /// shown, and recording a meal someone dictated needs no sight of their
    /// history. The two grants stand on their own.
    /// </summary>
    public bool AllowsWrite(DataAxis axis) => AllowWrites && axis switch
    {
        DataAxis.Nutrition => WriteNutrition,
        DataAxis.Training => WriteTraining,
        DataAxis.Body => WriteBody,
        _ => false,
    };
}
