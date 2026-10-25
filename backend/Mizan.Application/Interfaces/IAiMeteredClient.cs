namespace Mizan.Application.Interfaces;

/// <summary>Who pays for one provider call, on which line, and which prompt version produced it.</summary>
public record AiMeteredCall(
    Guid UserId,
    Guid? HouseholdId,
    string Feature,
    int EstimatedTokens,
    Guid? PromptVersionId);

/// <summary>
/// The one path to the provider: reserve through <see cref="IAiQuotaService"/>, call, settle.
/// The settle runs whatever happens to the call, so a failed or cancelled call still costs what it
/// cost (docs/AI.md#metering). Application code that needs a completion asks for it here and never
/// holds an <see cref="IAiProvider"/>.
/// </summary>
public interface IAiMeteredClient
{
    /// <summary>False when no endpoint is configured; the call then refuses before reserving.</summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws <see cref="Exceptions.AiQuotaExceededException"/> before the call when a ceiling is
    /// spent, and <see cref="Exceptions.AiUnavailableException"/> when the provider is not configured
    /// or fails.
    /// </summary>
    Task<AiCompletionResponse> CompleteAsync(
        AiMeteredCall call, AiCompletionRequest request, CancellationToken cancellationToken = default);
}
