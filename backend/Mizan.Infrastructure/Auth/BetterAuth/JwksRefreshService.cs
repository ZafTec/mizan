using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mizan.Infrastructure.Auth.BetterAuth;

public sealed class JwksRefreshService : BackgroundService
{
    private readonly IJwksProvider _provider;
    private readonly JwtOptions _options;
    private readonly ILogger<JwksRefreshService> _logger;

    public JwksRefreshService(
        IJwksProvider provider,
        IOptions<JwtOptions> options,
        ILogger<JwksRefreshService> logger)
    {
        _provider = provider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _provider.RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "JWKS refresh failed and no usable stale snapshot exists");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.JwksCacheMinutes), stoppingToken);
        }
    }
}
