namespace Mizan.Application.Ai;

public record PublishVerdict(bool Publishable, string? Reason);

/// <summary>
/// Whether a draft has earned a publish button, decided from the eval matrix
/// alone. Pure, so the console and the command reach the same answer without
/// the console having to guess at the rule (docs/REFOCUS.md §12).
///
/// Adversarial cases gate; ordinary ones inform. A prompt that gets a
/// nutrition question slightly wrong is a judgement call for the person
/// reading the matrix. A prompt that hands over another user's data on request
/// is not.
/// </summary>
public static class AiPublishGate
{
    public static PublishVerdict Evaluate(
        IReadOnlyCollection<AiEvalCaseDto> cases,
        IReadOnlyCollection<AiEvalRunDto> runs)
    {
        var adversarial = cases.Where(c => c.IsAdversarial).ToList();
        if (adversarial.Count == 0)
        {
            return new PublishVerdict(false, "No adversarial cases are registered for this prompt, so nothing has been proven.");
        }

        var byCase = runs.ToDictionary(r => r.CaseId);

        var unrun = adversarial.Where(c => !byCase.ContainsKey(c.Id)).ToList();
        if (unrun.Count > 0)
        {
            return new PublishVerdict(
                false,
                $"{unrun.Count} adversarial case(s) have not been run against this version.");
        }

        var lost = adversarial
            .Where(c => byCase[c.Id].Outcome != Domain.Entities.AiEvalOutcome.Passed)
            .Select(c => c.Name)
            .ToList();

        return lost.Count == 0
            ? new PublishVerdict(true, null)
            : new PublishVerdict(false, $"Adversarial cases still failing: {string.Join(", ", lost)}.");
    }
}
