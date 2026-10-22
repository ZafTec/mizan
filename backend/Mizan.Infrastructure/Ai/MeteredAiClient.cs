using System.Diagnostics;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// Reserve, call, settle. Settling happens in a finally, so a failed or cancelled call is still
/// recorded with what it cost, and a reservation is never left pending by a provider error
/// (docs/AI.md#metering).
/// </summary>
public sealed class MeteredAiClient : IAiMeteredClient
{
    private readonly IAiProvider _provider;
    private readonly IAiQuotaService _quota;

    public MeteredAiClient(IAiProvider provider, IAiQuotaService quota)
    {
        _provider = provider;
        _quota = quota;
    }

    public bool IsConfigured => _provider.IsConfigured;

    public async Task<AiCompletionResponse> CompleteAsync(
        AiMeteredCall call, AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (!_provider.IsConfigured)
        {
            throw new AiUnavailableException("The assistant is not configured on this server.");
        }

        var lease = await _quota.ReserveAsync(
            call.UserId, call.HouseholdId, call.Feature, call.EstimatedTokens, call.PromptVersionId, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var usage = AiTokenUsage.None;
        var model = _provider.Model;
        var outcome = AiCallOutcome.ProviderError;

        try
        {
            var response = await _provider.CompleteAsync(request, cancellationToken);
            usage = response.Usage;
            model = response.Model;
            outcome = AiCallOutcome.Succeeded;
            return response;
        }
        catch (OperationCanceledException)
        {
            outcome = AiCallOutcome.Timeout;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            // The reservation must be settled even when the caller is gone, so
            // this deliberately does not observe the request's cancellation.
            await _quota.SettleAsync(
                lease, usage, model, (int)stopwatch.ElapsedMilliseconds, outcome, CancellationToken.None);
        }
    }
}
