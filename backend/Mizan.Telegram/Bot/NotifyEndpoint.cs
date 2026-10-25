using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Telegram;

namespace Mizan.Telegram.Bot;

/// <summary>
/// <c>POST /internal/notify</c>: the API's outbox asks the bot to send a line to
/// a linked chat, such as the evening reminder. Internal only: the reverse proxy
/// routes the webhook and the Mini App session here, not this, and the request
/// must carry the shared service key, compared in constant time. The API never
/// holds the bot token, which is why it asks rather than sends.
/// </summary>
public sealed class NotifyEndpoint
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly TelegramClient _telegram;
    private readonly TelegramBotOptions _options;
    private readonly ILogger<NotifyEndpoint> _logger;

    public NotifyEndpoint(TelegramClient telegram, IOptions<TelegramBotOptions> options, ILogger<NotifyEndpoint> logger)
    {
        _telegram = telegram;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context, CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            await WriteAsync(context, HttpStatusCode.ServiceUnavailable, new { errorCode = "not_configured", error = "The bot is not configured." }, ct);
            return;
        }

        var provided = Encoding.UTF8.GetBytes(context.Request.Headers["X-Api-Key"].ToString());
        var expected = Encoding.UTF8.GetBytes(_options.ServiceApiKey!);
        if (provided.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(provided, expected))
        {
            await WriteAsync(context, HttpStatusCode.Unauthorized, new { errorCode = "unauthorized", error = "A service key is required." }, ct);
            return;
        }

        TelegramNotifyRequest? body;
        try { body = await context.Request.ReadFromJsonAsync<TelegramNotifyRequest>(Json, ct); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { body = null; }

        if (body is null || body.TelegramUserId <= 0 || string.IsNullOrWhiteSpace(body.Text))
        {
            await WriteAsync(context, HttpStatusCode.BadRequest, new { errorCode = "bad_request", error = "Send { telegramUserId, text }." }, ct);
            return;
        }

        object? keyboard = body.OpenPath is { Length: > 0 } path && _options.MiniAppButtonUrl(path) is { } url
            ? InlineKeyboardMarkup.Column(InlineKeyboardButton.OpenWebApp("Open", url))
            : null;

        if (await _telegram.TrySendMessageAsync(body.TelegramUserId, Escape(body.Text), keyboard, ct))
        {
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return;
        }

        _logger.LogWarning("Telegram did not take a notification for a linked chat");
        await WriteAsync(context, HttpStatusCode.BadGateway, new { errorCode = "telegram_unavailable", error = "Telegram did not take the message." }, ct);
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static Task WriteAsync<T>(HttpContext context, HttpStatusCode status, T body, CancellationToken ct)
    {
        context.Response.StatusCode = (int)status;
        return context.Response.WriteAsJsonAsync(body, Json, ct);
    }
}
