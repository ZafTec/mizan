using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Email;
using StackExchange.Redis;

namespace Mizan.Infrastructure.Services;

/// <summary>
/// Real round trips for the system health page, each with a short timeout so
/// one dead service cannot hold the page. Errors are reduced to their type: a
/// connection string or an address has no place on an admin screen.
/// </summary>
public sealed class SystemProbes : ISystemProbes
{
    public const string HttpClientName = "system-probes";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly MizanDbContext _db;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _http;
    private readonly IOptions<SmtpOptions> _smtp;
    private readonly IAiProvider _ai;
    private readonly IPushSender _push;
    private readonly IPricesWorkerClient _prices;

    public SystemProbes(
        MizanDbContext db,
        IServiceProvider services,
        IConfiguration configuration,
        IHttpClientFactory http,
        IOptions<SmtpOptions> smtp,
        IAiProvider ai,
        IPushSender push,
        IPricesWorkerClient prices)
    {
        _db = db;
        _services = services;
        _configuration = configuration;
        _http = http;
        _smtp = smtp;
        _ai = ai;
        _push = push;
        _prices = prices;
    }

    public bool SmtpConfigured => !string.IsNullOrWhiteSpace(_smtp.Value.Host);
    public bool AiConfigured => _ai.IsConfigured;
    public bool PushConfigured => _push.IsConfigured;
    public bool PricesWorkerConfigured => _prices.IsConfigured;
    public bool StorageConfigured => !string.IsNullOrWhiteSpace(_configuration["Storage:ServiceUrl"]);

    public Task<ProbeResult> DatabaseAsync(CancellationToken cancellationToken) =>
        TimeAsync(async ct => await _db.Database.ExecuteSqlRawAsync("SELECT 1", ct), cancellationToken);

    public Task<ProbeResult> RedisAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration.GetConnectionString("Redis")))
            return Task.FromResult(new ProbeResult(false, null, null));

        return TimeAsync(async _ =>
        {
            var redis = _services.GetService<IConnectionMultiplexer>()
                ?? throw new InvalidOperationException("Redis is configured but no connection is registered.");
            await redis.GetDatabase().PingAsync();
        }, cancellationToken);
    }

    public Task<ProbeResult> TelegramBotAsync(CancellationToken cancellationToken)
    {
        var url = _configuration["Telegram:BotServiceUrl"];
        if (string.IsNullOrWhiteSpace(url)) return Task.FromResult(new ProbeResult(false, null, null));

        return TimeAsync(async ct =>
        {
            var client = _http.CreateClient(HttpClientName);
            using var response = await client.GetAsync(new Uri(new Uri(url.TrimEnd('/') + "/"), "health"), ct);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"The bot service answered {(int)response.StatusCode}.");
        }, cancellationToken);
    }

    private static async Task<ProbeResult> TimeAsync(Func<CancellationToken, Task> probe, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        var watch = Stopwatch.StartNew();
        try
        {
            await probe(timeout.Token).WaitAsync(timeout.Token);
            return new ProbeResult(true, watch.Elapsed, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProbeResult(true, null, $"No answer within {Timeout.TotalSeconds:0} seconds.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ProbeResult(true, null, ex is HttpRequestException { Message: var m } && m.StartsWith("The bot service answered", StringComparison.Ordinal)
                ? m
                : $"Failed: {ex.GetType().Name}.");
        }
    }
}
