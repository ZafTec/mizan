namespace Mizan.Application.Interfaces;

/// <summary>
/// The prices worker's <c>POST /run/{sourceId}</c>, for the administrator's
/// Run now action. The worker answers at once; the run's result reaches the
/// API later as an ordinary run record.
/// </summary>
public interface IPricesWorkerClient
{
    /// <summary>False when <c>Prices:WorkerUrl</c> is not set.</summary>
    bool IsConfigured { get; }

    /// <summary>Asks the worker to run the source now and returns the status it reported, for example <c>started</c>.</summary>
    Task<string> RunAsync(Guid sourceId, CancellationToken cancellationToken);
}
