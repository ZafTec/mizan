using System.Diagnostics;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// Reserve, call, settle. Settling happens in a finally, so a failed, refused or cancelled call is
/// still recorded with what it cost, and a reservation is never left pending by a provider error or
/// a gateway's limit, rate or content refusal (docs/AI.md#metering).
/// </summary>
public sealed class MeteredAiClient : IAiMeteredClient
{
    private readonly IAiProvider _provider;
    private readonly IAiQuotaService _quota;
    private readonly IAiProviderIncidents _incidents;
    private readonly ICurrentUserService _currentUser;

    public MeteredAiClient(
        IAiProvider provider, IAiQuotaService quota, IAiProviderIncidents incidents, ICurrentUserService currentUser)
    {
        _provider = provider;
        _quota = quota;
        _incidents = incidents;
        _currentUser = currentUser;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) =>
        (await _provider.GetStatusAsync(cancellationToken)).IsConfigured;

    public async Task<AiCompletionResponse> CompleteAsync(
        AiMeteredCall call, AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var status = await _provider.GetStatusAsync(cancellationToken);
        if (!status.IsConfigured)
        {
            throw new AiUnavailableException("The assistant is not configured on this server.", "not_configured");
        }

        var lease = await _quota.ReserveAsync(
            call.UserId, call.HouseholdId, call.Feature, call.EstimatedTokens, call.PromptVersionId, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var usage = AiTokenUsage.None;
        var model = status.Model;
        var outcome = AiCallOutcome.ProviderError;

        try
        {
            // The person making this request, so a gateway can limit or block them by name; work with
            // no signed-in person (background jobs) carries no identity (docs/AI.md#behind-a-gateway).
            var response = await _provider.CompleteAsync(request with { EndUserId = _currentUser.UserId }, cancellationToken);
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
        catch (AiProviderRejectedException refusal) when (refusal.IsConfigurationError)
        {
            // Retrying never fixes a refused configuration, so the administrators hear of it.
            await _incidents.ReportAsync(refusal, CancellationToken.None);
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
