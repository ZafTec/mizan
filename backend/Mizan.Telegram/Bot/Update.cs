using System.Text.Json.Serialization;

namespace Mizan.Telegram.Bot;

/// <summary>
/// The slice of the Bot API this service reads.
///
/// Hand-written rather than pulled from a wrapper library: the bot handles six
/// message shapes, and a dependency that models all ninety would be more
/// surface than the thing it replaces.
/// </summary>
public sealed record Update
{
    [JsonPropertyName("update_id")] public long UpdateId { get; init; }
    [JsonPropertyName("message")] public Message? Message { get; init; }
    [JsonPropertyName("edited_message")] public Message? EditedMessage { get; init; }
    [JsonPropertyName("callback_query")] public CallbackQuery? CallbackQuery { get; init; }
}

public sealed record Message
{
    [JsonPropertyName("message_id")] public long MessageId { get; init; }
    [JsonPropertyName("from")] public TelegramUser? From { get; init; }
    [JsonPropertyName("chat")] public Chat Chat { get; init; } = new();
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("caption")] public string? Caption { get; init; }
    [JsonPropertyName("photo")] public IReadOnlyList<PhotoSize>? Photo { get; init; }
}

public sealed record TelegramUser
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("is_bot")] public bool IsBot { get; init; }
    [JsonPropertyName("first_name")] public string? FirstName { get; init; }
    [JsonPropertyName("username")] public string? Username { get; init; }
}

public sealed record Chat
{
    [JsonPropertyName("id")] public long Id { get; init; }

    /// <summary>private, group, supergroup or channel. Only the first is served.</summary>
    [JsonPropertyName("type")] public string Type { get; init; } = "private";
}

public sealed record PhotoSize
{
    [JsonPropertyName("file_id")] public string FileId { get; init; } = string.Empty;
    [JsonPropertyName("file_size")] public int? FileSize { get; init; }
    [JsonPropertyName("width")] public int Width { get; init; }
    [JsonPropertyName("height")] public int Height { get; init; }
}

public sealed record CallbackQuery
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("from")] public TelegramUser? From { get; init; }
    [JsonPropertyName("message")] public Message? Message { get; init; }
    [JsonPropertyName("data")] public string? Data { get; init; }
}

public sealed record TelegramFile
{
    [JsonPropertyName("file_path")] public string? FilePath { get; init; }
    [JsonPropertyName("file_size")] public int? FileSize { get; init; }
}
