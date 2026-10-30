using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Telegram;
using Mizan.Telegram.Backend;

namespace Mizan.Telegram.Bot;

/// <summary>
/// <c>POST /telegram/miniapp/session</c>: the Mini App page hands over Telegram's
/// signed <c>initData</c> and gets back a Mizan session.
///
/// The one public endpoint besides the webhook, and the only place the bot
/// token verifies anything. The exchange is two steps with one owner each:
/// this service proves the statement came from Telegram and is fresh; the API
/// decides whether that Telegram user may sign in at all, and mints the
/// session. The token never leaves here, and the API never sees initData.
/// </summary>
public sealed class MiniAppSessionEndpoint
{
    public const string CookieName = "mizan_session";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly MizanApiClient _api;
    private readonly TelegramBotOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _clock;
    private readonly ILogger<MiniAppSessionEndpoint> _logger;

    public MiniAppSessionEndpoint(
        MizanApiClient api,
        IOptions<TelegramBotOptions> options,
        IHostEnvironment environment,
        TimeProvider clock,
        ILogger<MiniAppSessionEndpoint> logger)
    {
        _api = api;
        _options = options.Value;
        _environment = environment;
        _clock = clock;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context, CancellationToken ct)
    {
        var body = await ReadBodyAsync(context.Request, ct);
        if (body is null)
        {
            await WriteAsync(context, HttpStatusCode.BadRequest, new { errorCode = "bad_request", error = "Send { initData }." }, ct);
            return;
        }

        var verdict = TelegramInitData.Validate(body.InitData, _options.BotToken, _clock.GetUtcNow(), _options.MiniAppMaxAge);
        if (verdict.User is not { } user)
        {
            // The reason is safe to show: none of them says which byte was wrong.
            _logger.LogInformation("Refused a Mini App session: {Reason}", verdict.Reason);
            await WriteAsync(context, HttpStatusCode.Unauthorized, new { errorCode = "invalid_init_data", error = verdict.Reason }, ct);
            return;
        }

        var result = await _api.CreateMiniAppSessionAsync(
            new TelegramMiniAppSessionRequest(user.Id, ClientIp(context), UserAgent(context)), ct);

        if (result is { Ok: true, Value: { } session })
        {
            // Same attributes as the API's own sign-in cookie, so the page is
            // signed in the moment this returns and every /api call carries it.
            var expiresAt = new DateTimeOffset(DateTime.SpecifyKind(session.ExpiresAt, DateTimeKind.Utc));
            context.Response.Cookies.Append(CookieName, session.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = !_environment.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = expiresAt,
            });

            await WriteAsync(context, HttpStatusCode.OK, new MiniAppSessionResponse(session.Token, session.ExpiresAt), ct);
            return;
        }

        var publicUrl = _options.PublicUrl.TrimEnd('/');
        switch (result.Status)
        {
            case HttpStatusCode.NotFound:
                await WriteAsync(context, HttpStatusCode.NotFound, new
                {
                    errorCode = "not_linked",
                    error = "This Telegram account is not connected to a Mizan account yet.",
                    linkUrl = $"{publicUrl}/profile/settings/telegram",
                }, ct);
                return;

            case HttpStatusCode.PaymentRequired:
                await WriteAsync(context, HttpStatusCode.PaymentRequired, new
                {
                    errorCode = "upgrade_required",
                    error = result.Problem ?? "The Telegram bot is part of Mizan Pro.",
                    upgradeUrl = $"{publicUrl}/billing",
                }, ct);
                return;

            case HttpStatusCode.Forbidden:
            case HttpStatusCode.TooManyRequests:
                await WriteAsync(context, result.Status, new
                {
                    errorCode = result.Status == HttpStatusCode.Forbidden ? "forbidden" : "account_locked",
                    error = result.Problem ?? "This account cannot sign in right now.",
                }, ct);
                return;

            default:
                _logger.LogWarning("The API answered {Status} to a Mini App session request", (int)result.Status);
                await WriteAsync(context, HttpStatusCode.BadGateway, new { errorCode = "api_unavailable", error = "Mizan is not reachable right now. Try again in a moment." }, ct);
                return;
        }
    }

    private static async Task<MiniAppSessionRequest?> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        try
        {
            return await request.ReadFromJsonAsync<MiniAppSessionRequest>(Json, ct);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // Not JSON, or not ours. The status code says so.
            return null;
        }
    }

    private static Task WriteAsync<T>(HttpContext context, HttpStatusCode status, T body, CancellationToken ct)
    {
        context.Response.StatusCode = (int)status;
        return context.Response.WriteAsJsonAsync(body, Json, ct);
    }

    private static string? ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString();

    private static string? UserAgent(HttpContext context) =>
        context.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}
