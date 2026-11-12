using Mizan.Contracts.Mcp;
using Mizan.Mcp.Server.Services;
using Serilog;

namespace Mizan.Mcp.Server.Authentication;

/// <summary>
/// The gateway settings this server works with now. The API holds them (Assistant gateway in the admin
/// settings) and this server reads them at startup and every minute, so a change applies without a
/// restart. Until the first reading the gateway path is closed; when a reading fails the last good one stays.
/// </summary>
public sealed class GatewaySettings
{
    private readonly IServiceScopeFactory _scopes;
    private readonly McpToolset _toolset;
    private readonly SemaphoreSlim _refreshing = new(1, 1);
    private GatewayOptions _current = GatewayOptions.Off;
    private string? _lastProblems;

    public GatewaySettings(IServiceScopeFactory scopes, McpToolset toolset)
    {
        _scopes = scopes;
        _toolset = toolset;
    }

    /// <summary>The settings as last read. Hold the instance for one request, not longer.</summary>
    public GatewayOptions Current => Volatile.Read(ref _current);

    /// <summary>Reads the settings once. False when the API did not answer, which keeps the last reading.</summary>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshing.WaitAsync(cancellationToken);
        try
        {
            McpGatewaySettings remote;
            using (var scope = _scopes.CreateScope())
            {
                remote = await scope.ServiceProvider.GetRequiredService<IBackendApiClient>().GetGatewaySettingsAsync(cancellationToken);
            }

            var options = GatewayOptions.From(remote, _toolset, out var problems);
            var summary = problems.Count == 0 ? null : string.Join(" ", problems);
            if (summary != _lastProblems)
            {
                if (summary is null) Log.Information("[MCP Gateway] Settings read; the gateway path is {State}", options.IsEnabled ? "open" : "off");
                else Log.Warning("[MCP Gateway] The gateway path is off: {Problems}", summary);
                _lastProblems = summary;
            }

            Volatile.Write(ref _current, options);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or BackendApiException or System.Text.Json.JsonException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            Log.Warning(ex, "[MCP Gateway] The settings could not be read; keeping the last ones");
            return false;
        }
        finally
        {
            _refreshing.Release();
        }
    }
}

/// <summary>Reads <see cref="GatewaySettings"/> at startup, retrying until the API answers, and every minute after.</summary>
public sealed class GatewaySettingsRefresher : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly GatewaySettings _settings;

    public GatewaySettingsRefresher(GatewaySettings settings) => _settings = settings;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            bool read;
            try
            {
                read = await _settings.RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Since .NET 11 an exception that leaves ExecuteAsync stops the host, and with it every tool.
                Log.Error(ex, "[MCP Gateway] Refreshing the settings failed; retrying");
                read = false;
            }

            var wait = read ? Interval : retry;
            retry = read ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(Math.Min(retry.TotalSeconds * 2, 60));
            try { await Task.Delay(wait, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
