using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Mizan.Telegram.Bot;

/// <summary>
/// The Bot API, as the eight calls this service actually makes.
///
/// Every method is best-effort: Telegram being unreachable must not take the
/// service down, and a failed sendMessage is a logged warning rather than an
/// exception that kills a polling loop. The one exception is GetUpdates, which
/// the loop needs to know about so it can back off.
/// </summary>
public sealed class TelegramClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ILogger<TelegramClient> _logger;
    private readonly string _base;

    public TelegramClient(HttpClient http, IOptions<TelegramBotOptions> options, ILogger<TelegramClient> logger)
    {
        _http = http;
        _logger = logger;
        _base = $"https://api.telegram.org/bot{options.Value.BotToken}";
        FileBase = $"https://api.telegram.org/file/bot{options.Value.BotToken}";
    }

    private string FileBase { get; }

    /// <summary>Long poll. Throws, because the loop has to distinguish "nothing yet" from "broken".</summary>
    public async Task<IReadOnlyList<Update>> GetUpdatesAsync(
        long offset, int timeoutSeconds, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(
            $"{_base}/getUpdates",
            new
            {
                offset,
                timeout = timeoutSeconds,
                allowed_updates = new[] { "message", "callback_query" },
            },
            Json,
            ct);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<Response<List<Update>>>(Json, ct);
        return body?.Result ?? [];
    }

    public async Task SendMessageAsync(
        long chatId, string text, object? replyMarkup = null, CancellationToken ct = default) =>
        await TrySendMessageAsync(chatId, text, replyMarkup, ct);

    /// <summary>The same send, saying whether Telegram took it, for a caller that retries.</summary>
    public async Task<bool> TrySendMessageAsync(
        long chatId, string text, object? replyMarkup = null, CancellationToken ct = default)
    {
        var sent = await TrySendAsync(chatId, text, "HTML", replyMarkup, ct);
        if (sent) return true;

        // Telegram rejects the whole message when the markup does not parse,
        // and PostAsync only logs that. Falling back to plain text means a
        // reply the converter mangled still reaches the user rather than
        // vanishing into a warning nobody reads.
        _logger.LogWarning("Telegram rejected an HTML message for chat {ChatId}; resending as plain text", chatId);
        return await TrySendAsync(chatId, StripTags(text), parseMode: null, replyMarkup, ct);
    }

    private async Task<bool> TrySendAsync(
        long chatId, string text, string? parseMode, object? replyMarkup, CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_base}/sendMessage", new
            {
                chat_id = chatId,
                text,
                parse_mode = parseMode,
                link_preview_options = new { is_disabled = true },
                reply_markup = replyMarkup,
            }, Json, ct);

            if (response.IsSuccessStatusCode) return true;

            // Never the payload: a sendMessage body is the user's own data.
            _logger.LogWarning("Telegram sendMessage returned {Status}", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Telegram sendMessage failed");
            return false;
        }
    }

    /// <summary>Last resort: readable text beats a message that never arrives.</summary>
    private static string StripTags(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty)
            .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&amp;", "&");

    /// <summary>The "typing…" indicator. Worth it: an AI turn takes seconds.</summary>
    public Task SendTypingAsync(long chatId, CancellationToken ct = default) =>
        PostAsync("sendChatAction", new { chat_id = chatId, action = "typing" }, ct);

    /// <summary>
    /// Answers the callback so Telegram stops showing a spinner on the button.
    /// Required within a few seconds whether or not there is anything to say.
    /// </summary>
    public Task AnswerCallbackAsync(string callbackId, string? text = null, CancellationToken ct = default) =>
        PostAsync("answerCallbackQuery", new { callback_query_id = callbackId, text }, ct);

    /// <summary>Removes the keyboard from a card that has been acted on, so it cannot be pressed twice.</summary>
    public Task ClearKeyboardAsync(long chatId, long messageId, CancellationToken ct = default) =>
        PostAsync("editMessageReplyMarkup", new { chat_id = chatId, message_id = messageId }, ct);

    /// <summary>
    /// Tells Telegram where to POST updates. Idempotent - safe to call on every startup. Checked,
    /// unlike the other calls: a webhook Telegram refused leaves the bot deaf, so the caller needs
    /// Telegram's own reason (<c>description</c>) to log and retry on.
    /// </summary>
    public Task<TelegramCallResult> SetWebhookAsync(string url, string secretToken, CancellationToken ct = default) =>
        CheckedPostAsync("setWebhook", new
        {
            url,
            secret_token = secretToken,
            allowed_updates = new[] { "message", "callback_query" },
        }, ct);

    /// <summary>
    /// What Telegram says about the registered webhook: its URL and the last delivery error, if
    /// any. Null when Telegram could not be asked.
    /// </summary>
    public async Task<WebhookInfo?> GetWebhookInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_base}/getWebhookInfo", new { }, Json, ct);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadFromJsonAsync<Response<WebhookInfo>>(Json, ct);
            return body is { Ok: true } ? body.Result : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Telegram getWebhookInfo failed");
            return null;
        }
    }

    /// <summary>
    /// Telegram refuses <c>getUpdates</c> with a 409 while a webhook is registered,
    /// so long polling needs this cleared first. Harmless if none was set.
    /// </summary>
    public Task DeleteWebhookAsync(CancellationToken ct = default) =>
        PostAsync("deleteWebhook", new { }, ct);

    public Task SetCommandsAsync(CancellationToken ct = default) =>
        PostAsync("setMyCommands", new
        {
            commands = new object[]
            {
                new { command = "today", description = "Today's totals against your targets" },
                new { command = "weight", description = "Log a weigh-in, e.g. /weight 82.4" },
                new { command = "plan", description = "Today's meal plan, or /plan tomorrow" },
                new { command = "shop", description = "Your shopping list, ten items at a time" },
                new { command = "planme", description = "Draft a week of meals, e.g. /planme high protein, cheap" },
                new { command = "notifications", description = "Get the app's notifications here too, /notifications on or off" },
                new { command = "help", description = "What this bot can do" },
                new { command = "unlink", description = "Disconnect this chat from your account" },
            },
        }, ct);

    public async Task<byte[]?> DownloadAsync(string fileId, int maxBytes, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync($"{_base}/getFile", new { file_id = fileId }, Json, ct);
        if (!response.IsSuccessStatusCode) return null;

        var file = (await response.Content.ReadFromJsonAsync<Response<TelegramFile>>(Json, ct))?.Result;
        if (file?.FilePath is null) return null;
        if (file.FileSize > maxBytes) return null;

        var bytes = await _http.GetByteArrayAsync($"{FileBase}/{file.FilePath}", ct);
        return bytes.Length > maxBytes ? null : bytes;
    }

    /// <summary>
    /// A call whose outcome matters: success only when Telegram answers <c>ok: true</c>, whatever the
    /// HTTP status, and Telegram's <c>description</c> otherwise. Never throws for a failure.
    /// </summary>
    private async Task<TelegramCallResult> CheckedPostAsync(string method, object payload, CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_base}/{method}", payload, Json, ct);
            Response<JsonElement>? body = null;
            try { body = await response.Content.ReadFromJsonAsync<Response<JsonElement>>(Json, ct); }
            catch (JsonException) { }

            if (response.IsSuccessStatusCode && body is { Ok: true }) return TelegramCallResult.Success;

            var description = body?.Description is { Length: > 0 } text ? text : $"HTTP {(int)response.StatusCode}";
            _logger.LogWarning("Telegram {Method} returned {Status}: {Description}", method, (int)response.StatusCode, description);
            return new TelegramCallResult(false, description);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Telegram {Method} failed", method);
            return new TelegramCallResult(false, ex.Message);
        }
    }

    private async Task PostAsync(string method, object payload, CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_base}/{method}", payload, Json, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Never the payload: a sendMessage body is the user's own data.
                _logger.LogWarning("Telegram {Method} returned {Status}", method, (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Telegram {Method} failed", method);
        }
    }

    private sealed record Response<T>
    {
        [JsonPropertyName("ok")] public bool Ok { get; init; }
        [JsonPropertyName("result")] public T? Result { get; init; }
        [JsonPropertyName("description")] public string? Description { get; init; }
    }
}

/// <summary>A checked Bot API call: whether Telegram said <c>ok</c>, and its reason when it did not.</summary>
public sealed record TelegramCallResult(bool Ok, string? Description)
{
    public static TelegramCallResult Success { get; } = new(true, null);
}

/// <summary>The part of <c>getWebhookInfo</c> the bot reads.</summary>
public sealed record WebhookInfo
{
    [JsonPropertyName("url")] public string Url { get; init; } = string.Empty;
    [JsonPropertyName("pending_update_count")] public int PendingUpdateCount { get; init; }
    [JsonPropertyName("last_error_message")] public string? LastErrorMessage { get; init; }
}
