namespace Mizan.Application.Interfaces;

/// <summary>What a run of the whole suite against one draft cost and proved.</summary>
public record EvalSummary(
    Guid VersionId,
    int Total,
    int Passed,
    int Failed,
    int Errored,
    int AdversarialFailures,
    int Tokens,
    long CostMicros);

public interface IAiEvalRunner
{
    /// <summary>
    /// Runs every case registered for the version's prompt key against that
    /// version's composed text, replacing any earlier results for it. Billed to
    /// the admin who asked, on the eval feature, so it lands inside the same
    /// global ceiling as everything else.
    /// </summary>
    Task<EvalSummary> RunAsync(Guid versionId, Guid adminUserId, CancellationToken cancellationToken = default);
}
