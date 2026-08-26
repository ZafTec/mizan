using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Ai;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// Runs a draft against the synthetic suite for its key. This is what stops
/// the draft flow from being theatre (docs/REFOCUS.md §12): a version with no
/// passing adversarial run does not get a publish button.
///
/// Every case is a real provider call, reserved and settled like any other, so
/// the cost of proving a prompt shows up in the same ledger as the cost of
/// using one.
/// </summary>
public class AiEvalRunner : IAiEvalRunner
{
    private readonly IMizanDbContext _context;
    private readonly IAiProvider _provider;
    private readonly IAiQuotaService _quota;
    private readonly IAiPromptResolver _prompts;
    private readonly AiOptions _options;
    private readonly ILogger<AiEvalRunner> _logger;

    public AiEvalRunner(
        IMizanDbContext context,
        IAiProvider provider,
        IAiQuotaService quota,
        IAiPromptResolver prompts,
        IOptions<AiOptions> options,
        ILogger<AiEvalRunner> logger)
    {
        _context = context;
        _provider = provider;
        _quota = quota;
        _prompts = prompts;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EvalSummary> RunAsync(
        Guid versionId, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        if (!_provider.IsConfigured)
        {
            throw new AiUnavailableException("The assistant is not configured on this server.");
        }

        var version = await _context.AiPromptVersions
            .Include(v => v.Prompt)
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken)
            ?? throw new EntityNotFoundException("Prompt version", versionId);

        var key = version.Prompt!.Key;
        var systemPrompt = _prompts.Compose(version.Body, version.SoftPolicy);

        var cases = await _context.AiEvalCases.AsNoTracking()
            .Where(c => c.PromptKey == key)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        // Replacing rather than appending: "the results for this version" is
        // one set, and a stale pass from an earlier run must not gate a publish.
        await _context.AiEvalRuns
            .Where(r => r.VersionId == versionId)
            .ExecuteDeleteAsync(cancellationToken);

        var results = new List<AiEvalRun>(cases.Count);
        foreach (var evalCase in cases)
        {
            results.Add(await RunCaseAsync(version, systemPrompt, evalCase, adminUserId, cancellationToken));
        }

        _context.AiEvalRuns.AddRange(results);
        await _context.SaveChangesAsync(cancellationToken);

        var adversarialIds = cases.Where(c => c.IsAdversarial).Select(c => c.Id).ToHashSet();

        return new EvalSummary(
            versionId,
            results.Count,
            results.Count(r => r.Outcome == AiEvalOutcome.Passed),
            results.Count(r => r.Outcome == AiEvalOutcome.Failed),
            results.Count(r => r.Outcome == AiEvalOutcome.Errored),
            results.Count(r => adversarialIds.Contains(r.CaseId) && r.Outcome != AiEvalOutcome.Passed),
            results.Sum(r => r.PromptTokens + r.CompletionTokens),
            results.Sum(r => r.CostMicros));
    }

    private async Task<AiEvalRun> RunCaseAsync(
        AiPromptVersion version,
        string systemPrompt,
        AiEvalCase evalCase,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var messages = new List<AiMessage> { new(AiRole.System, systemPrompt) };
        if (!string.IsNullOrWhiteSpace(evalCase.Context))
        {
            messages.Add(new AiMessage(AiRole.System, evalCase.Context));
        }
        messages.Add(new AiMessage(AiRole.User, evalCase.Input));

        var estimated = Math.Max(256, (systemPrompt.Length + evalCase.Input.Length) / 4);
        var lease = await _quota.ReserveAsync(
            adminUserId, null, AiFeatures.Eval, estimated, version.Id, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var usage = AiTokenUsage.None;
        var model = _provider.Model;
        var callOutcome = AiCallOutcome.ProviderError;
        AiCompletionResponse? response = null;
        string? errorReason = null;

        try
        {
            response = await _provider.CompleteAsync(
                new AiCompletionRequest { Messages = messages, Temperature = 0.2 }, cancellationToken);
            usage = response.Usage;
            model = response.Model;
            callOutcome = AiCallOutcome.Succeeded;
        }
        catch (AiUnavailableException ex)
        {
            // An errored case is not a failed case: the draft was never judged,
            // and the matrix says so rather than pretending it lost.
            errorReason = ex.Message;
            _logger.LogWarning(ex, "Eval case {Case} could not reach the provider", evalCase.Id);
        }
        finally
        {
            stopwatch.Stop();
            await _quota.SettleAsync(
                lease, usage, model, (int)stopwatch.ElapsedMilliseconds, callOutcome, CancellationToken.None);
        }

        var verdict = response is null
            ? new EvalVerdict(false, false, errorReason)
            : EvalAssertions.Evaluate(evalCase.Assertions, response.Content);

        return new AiEvalRun
        {
            Id = Guid.CreateVersion7(),
            VersionId = version.Id,
            CaseId = evalCase.Id,
            Outcome = response is null
                ? AiEvalOutcome.Errored
                : verdict.Passed ? AiEvalOutcome.Passed : AiEvalOutcome.Failed,
            SchemaValid = verdict.SchemaValid,
            Output = response?.Content,
            FailureReason = verdict.Reason,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            CostMicros = CostOf(usage),
            LatencyMs = (int)stopwatch.ElapsedMilliseconds,
            CreatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// The ledger holds the same number; this copy is here so the eval matrix
    /// can show a cost delta between two versions without joining to it.
    /// </summary>
    private long CostOf(AiTokenUsage usage) =>
        (usage.PromptTokens * _options.PromptCostPerMillionMicros / 1_000_000)
        + (usage.CompletionTokens * _options.CompletionCostPerMillionMicros / 1_000_000);
}
