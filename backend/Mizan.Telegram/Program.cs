using Microsoft.Extensions.Options;
using Mizan.Telegram;
using Mizan.Telegram.Backend;
using Mizan.Telegram.Bot;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Only what the bot needs to reach the API comes from the environment. The token, the webhook switch
// and secret and the Mini App are runtime settings the bot reads from the API (BotSettings), and every
// consumer of IOptions<TelegramBotOptions> gets the latest of them when it is created.
builder.Services.Configure<TelegramBotEnvironment>(
    builder.Configuration.GetSection(TelegramBotEnvironment.SectionName));
builder.Services.AddSingleton<BotSettings>();
builder.Services.AddTransient<IOptions<TelegramBotOptions>>(sp =>
    Options.Create(sp.GetRequiredService<BotSettings>().Current));

// Two clients with different lifetimes of trust: one talks to Telegram over
// the public internet, the other to the API over the Docker network. Keeping
// them separate means the service key can never end up on a request to
// api.telegram.org.
builder.Services.AddHttpClient<TelegramClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(70));

builder.Services.AddHttpClient<MizanApiClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(120));

// The Mini App session records the browser's address, which only the proxy and the web
// app's route handler can tell; see docs/TELEGRAM.md#configure-and-run.
builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
    TrustedProxies.Configure(forwarded, builder.Configuration[$"{TelegramBotEnvironment.SectionName}:TrustedProxies"]));

builder.Services.AddScoped<UpdateHandler>();
builder.Services.AddScoped<MiniAppSessionEndpoint>();
builder.Services.AddScoped<NotifyEndpoint>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<LongPollWorker>();
builder.Services.AddSingleton<WebhookState>();
builder.Services.AddSingleton<WebhookRegistrar>();
builder.Services.AddSingleton<BotSettingsRefresher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BotSettingsRefresher>());

var app = builder.Build();

app.UseForwardedHeaders();

foreach (var retired in TelegramBotEnvironment.RetiredKeys)
{
    if (!string.IsNullOrWhiteSpace(app.Configuration[$"{TelegramBotEnvironment.SectionName}:{retired}"]))
    {
        Log.Warning(
            "TelegramBot:{Key} is set in the environment and ignored; it is a runtime setting now (docs/ADMIN.md#runtime-settings)",
            retired);
    }
}

app.MapGet("/health", (WebhookState webhook, BotSettings settings) => Results.Ok(new
{
    status = "healthy",
    settingsLoaded = settings.Loaded,
    configured = settings.Current.IsConfigured,
    mode = settings.Current.UseWebhook ? "webhook" : "long-poll",
    webhook = new { state = webhook.Current.State, lastError = webhook.Current.LastError, attempts = webhook.Current.Attempts },
}));

/// The Mini App's sign-in. Public by nature - the page in Telegram's WebView
/// calls it - and guarded by Telegram's signature on the body rather than by
/// a header. See docs/TELEGRAM.md#mini-app.
app.MapPost("/telegram/miniapp/session", (HttpContext context, MiniAppSessionEndpoint endpoint, CancellationToken ct) =>
    endpoint.HandleAsync(context, ct));

/// Messages the API asks the bot to send, such as the evening reminder. Internal:
/// the proxy does not route it, and it requires the shared service key.
app.MapPost("/internal/notify", (HttpContext context, NotifyEndpoint endpoint, CancellationToken ct) =>
    endpoint.HandleAsync(context, ct));

/// The API's signal that a Telegram setting changed in the admin area. Internal, and requires the
/// shared service key; answers once the new settings are read and Telegram is being brought in line.
app.MapPost("/internal/settings-changed", async (
    HttpContext context, BotSettingsRefresher refresher, IOptions<TelegramBotEnvironment> environment, CancellationToken ct) =>
{
    var expected = environment.Value.ServiceApiKey;
    if (string.IsNullOrWhiteSpace(expected)
        || !CryptographicEquals(context.Request.Headers["X-Api-Key"].ToString(), expected))
    {
        return Results.Unauthorized();
    }

    return await refresher.RefreshAsync(ct) ? Results.NoContent() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

/// The webhook. Telegram sends the secret in a header on every call; anything
/// without it is not Telegram, and gets nothing back that would confirm the
/// endpoint exists.
app.MapPost("/telegram/webhook", async (
    HttpContext context,
    Update update,
    UpdateHandler handler,
    IOptions<TelegramBotOptions> settings,
    CancellationToken ct) =>
{
    var expected = settings.Value.WebhookSecret;

    if (string.IsNullOrWhiteSpace(expected)) return Results.NotFound();

    var provided = context.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
    if (!CryptographicEquals(provided, expected)) return Results.NotFound();

    // Answered immediately: Telegram retries anything slower than a few
    // seconds, and an AI turn is slower than that. The work continues on the
    // host's own lifetime rather than the request's.
    _ = Task.Run(() => handler.HandleAsync(update, CancellationToken.None), CancellationToken.None);

    await Task.CompletedTask;
    return Results.Ok();
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
