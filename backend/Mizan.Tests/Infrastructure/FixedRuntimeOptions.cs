using Mizan.Application.Interfaces;

namespace Mizan.Tests.Infrastructure;

/// <summary>Options that never change, for a unit test of one consumer without the settings table.</summary>
public sealed class FixedRuntimeOptions<TOptions>(TOptions value) : IRuntimeOptions<TOptions>
    where TOptions : class
{
    public ValueTask<TOptions> GetAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(value);
}
