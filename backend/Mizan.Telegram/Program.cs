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

builder.Services.Configure<TelegramBotOptions>(
    builder.Configuration.GetSection(TelegramBotOptions.SectionName));

// Two clients with different lifetimes of trust: one talks to Telegram over
// the public internet, the other to the API over the Docker network. Keeping
// them separate means the service key can never end up on a request to
// api.telegram.org.
builder.Services.AddHttpClient<TelegramClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(70));

builder.Services.AddHttpClient<MizanApiClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(120));

builder.Services.AddScoped<UpdateHandler>();
builder.Services.AddHostedService<LongPollWorker>();

var app = builder.Build();

var options = app.Services.GetRequiredService<IOptions<TelegramBotOptions>>().Value;

if (!options.IsConfigured)
{
    // Same posture as the AI service: missing configuration disables the
    // feature and the process still starts. A bot that refuses to boot takes
    // its health check with it.
    Log.Warning("Telegram bot is not configured; the service is running but idle");
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    configured = options.IsConfigured,
    mode = options.UseWebhook ? "webhook" : "long-poll",
}));

/// The only public surface. Telegram sends the secret in a header on every
/// call; anything without it is not Telegram, and gets nothing back that would
/// confirm the endpoint exists.
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
