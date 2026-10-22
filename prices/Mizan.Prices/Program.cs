using Microsoft.Extensions.Options;
using Mizan.Contracts.Prices;
using Mizan.Prices;
using Mizan.Prices.Backend;
using Mizan.Prices.Scheduling;
using Mizan.Prices.Scraping;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<PricesOptions>(
    builder.Configuration.GetSection(PricesOptions.SectionName));

// Two clients with different lifetimes of trust, as in Mizan.Telegram: one
// talks to the API over the Docker network and carries the service key, the
// other to shops on the public internet and carries only our User-Agent.
builder.Services.AddHttpClient<IPricesApi, PricesApiClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(120));

builder.Services.AddHttpClient(ScrapeContextFactory.HttpClientName, client =>
    client.Timeout = ScrapeContext.RequestTimeout);

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ScrapeContextFactory>();
builder.Services.AddSingleton<ScraperRegistry>();
builder.Services.AddTransient<SourceRunner>();
builder.Services.AddHostedService<ScrapeScheduler>();

// One line per site. The key is what the administrator types into the source form.
builder.Services.AddSingleton<IPriceScraper, AradaMartScraper>();

builder.Services.AddSingleton<IRunLock>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PricesOptions>>().Value;

    if (string.IsNullOrWhiteSpace(options.Redis))
    {
        // Correct for one instance, wrong for two: both would scrape the same
        // source at once and post every observation twice.
        sp.GetRequiredService<ILogger<Program>>()
            .LogWarning("Prices:Redis is not set; using an in-process run lock, which is only safe with a single worker instance");
        return new InMemoryRunLock();
    }

    return new RedisRunLock(options.Redis, sp.GetRequiredService<ILogger<RedisRunLock>>());
});

var app = builder.Build();

var settings = app.Services.GetRequiredService<IOptions<PricesOptions>>().Value;
var registry = app.Services.GetRequiredService<ScraperRegistry>();

if (!settings.IsConfigured)
{
    // Same posture as the Telegram bot: missing configuration disables the
    // feature and the process still starts, health check included.
    Log.Warning("Prices worker is not configured; the service is running but idle");
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    configured = settings.IsConfigured,
    sources = registry.Keys,
}));

/// "Run now" from the admin pages. The API calls this with the same service key
/// it accepts from us, so neither side holds a second secret. The run happens
/// in the background; its result reaches the API as a run record like any
/// scheduled one, and the lock makes a run that is already in progress a
/// "skipped" record rather than a second crawl.
app.MapPost("/run/{sourceId:guid}", async (
    Guid sourceId,
    HttpContext context,
    IServiceScopeFactory scopes,
    IHostApplicationLifetime lifetime,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var expected = settings.ServiceApiKey;
    if (string.IsNullOrWhiteSpace(expected)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    var provided = context.Request.Headers["X-Api-Key"].ToString();
    if (!CryptographicEquals(provided, expected)) return Results.Unauthorized();

    PriceSourceDto? source;
    using (var scope = scopes.CreateScope())
    {
        try
        {
            var sources = await scope.ServiceProvider.GetRequiredService<IPricesApi>().GetSourcesAsync(ct);
            source = sources.FirstOrDefault(s => s.Id == sourceId);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Could not read the source list from the API");
            return Results.Problem(statusCode: StatusCodes.Status502BadGateway, detail: $"The API did not answer: {ex.Message}");
        }
    }

    if (source is null) return Results.NotFound(new { error = "The source is not enabled or does not exist" });
    if (registry.Find(source.ScraperKey) is null)
    {
        return Results.BadRequest(new { error = $"No scraper is registered for key '{source.ScraperKey}'" });
    }

    _ = Task.Run(async () =>
    {
        using var scope = scopes.CreateScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<SourceRunner>().RunAsync(source, lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Source}: manual run threw past the runner", source.Name);
        }
    }, CancellationToken.None);

    return Results.Accepted($"/run/{sourceId}", new { sourceId, status = "started" });
});

static bool CryptographicEquals(string a, string b)
{
    var left = System.Text.Encoding.UTF8.GetBytes(a);
    var right = System.Text.Encoding.UTF8.GetBytes(b);
    return left.Length == right.Length
        && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
}

app.Run();

public partial class Program;
