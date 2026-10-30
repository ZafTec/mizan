using System.Collections.Concurrent;
using Mizan.Application.Interfaces;

namespace Mizan.Tests.Integration;

/// <summary>The prices worker's Run now endpoint, in memory. Unconfigured until a test turns it on.</summary>
public sealed class FakePricesWorker : IPricesWorkerClient
{
    public bool IsConfigured { get; set; }

    public ConcurrentQueue<Guid> Runs { get; } = new();

    public Task<string> RunAsync(Guid sourceId, CancellationToken cancellationToken)
    {
        Runs.Enqueue(sourceId);
        return Task.FromResult("started");
    }

    public void Reset()
    {
        IsConfigured = false;
        Runs.Clear();
    }
}
