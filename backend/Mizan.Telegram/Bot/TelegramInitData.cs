using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Mizan.Telegram.Bot;

/// <summary>
/// The person a Mini App was opened by, as Telegram signed it.
/// </summary>
public sealed record TelegramMiniAppUser(long Id, string? Username, string? FirstName);

/// <summary>
/// One of two things: who opened the Mini App, or why the claim was refused.
/// The reason is for the log and the page; it never says which byte differed.
/// </summary>
public sealed record TelegramInitDataResult(TelegramMiniAppUser? User, string? Reason)
{
    public bool IsValid => User is not null;

    public static TelegramInitDataResult Valid(TelegramMiniAppUser user) => new(user, null);

    public static TelegramInitDataResult Invalid(string reason) => new(null, reason);
}

/// <summary>
/// Verifies <c>window.Telegram.WebApp.initData</c>, the signed statement from
/// Telegram about who opened a Mini App and when.
///
/// This is the only place the bot token is used for anything but calling
/// Telegram, and it is pure: a string, a token, a clock, an answer. Telegram's
/// scheme is HMAC-SHA256 twice over - the token is first turned into a key
/// with the constant <c>WebAppData</c>, then that key signs every field but
/// <c>hash</c>, sorted by name and joined with newlines. A wrong token, a
/// changed field and a reordered field all land on the same answer.
/// </summary>
public static class TelegramInitData
{
    /// <summary>A statement dated later than this far into the future is not a clock skew, it is a forgery.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(1);

    private static readonly byte[] WebAppDataKey = Encoding.UTF8.GetBytes("WebAppData");

    public static TelegramInitDataResult Validate(string? initData, string? botToken, DateTimeOffset now, TimeSpan maxAge)
    {
        if (string.IsNullOrWhiteSpace(botToken)) return TelegramInitDataResult.Invalid("The bot is not configured.");
        if (string.IsNullOrWhiteSpace(initData)) return TelegramInitDataResult.Invalid("No initData was sent.");

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, values) in QueryHelpers.ParseQuery(initData))
        {
            // Telegram sends each field once. A second copy of any of them is
            // somebody looking for a parser that signs one value and reads the other.
            if (values.Count != 1 || !fields.TryAdd(key, values[0] ?? string.Empty))
            {
                return TelegramInitDataResult.Invalid("initData is malformed.");
            }
        }

        if (!fields.Remove("hash", out var providedHash)) return TelegramInitDataResult.Invalid("initData carries no hash.");

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(providedHash);
        }
        catch (FormatException)
        {
            return TelegramInitDataResult.Invalid("initData carries no hash.");
        }

        var expected = Sign(botToken, DataCheckString(fields));
        if (provided.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(provided, expected))
        {
            return TelegramInitDataResult.Invalid("initData is not signed by this bot.");
        }

        // Only after the signature: an attacker learns nothing about the
        // clock or the user from an unsigned statement.
        if (!fields.TryGetValue("auth_date", out var authDateText)
            || !long.TryParse(authDateText, out var authDateSeconds))
        {
            return TelegramInitDataResult.Invalid("initData carries no auth_date.");
        }

        var authDate = DateTimeOffset.FromUnixTimeSeconds(authDateSeconds);
        if (now - authDate > maxAge) return TelegramInitDataResult.Invalid("initData has expired; open the Mini App again.");
        if (authDate - now > MaxClockSkew) return TelegramInitDataResult.Invalid("initData is dated in the future.");

        if (!fields.TryGetValue("user", out var userJson)) return TelegramInitDataResult.Invalid("initData names no user.");

        return ParseUser(userJson) is { } user
            ? TelegramInitDataResult.Valid(user)
            : TelegramInitDataResult.Invalid("initData names no user.");
    }

    /// <summary>
    /// Every field but <c>hash</c>, as <c>key=value</c> lines sorted by key. Values
    /// are the decoded ones: the signature is over what the fields say, not over
    /// how the query string spelled them.
    /// </summary>
    public static string DataCheckString(IEnumerable<KeyValuePair<string, string>> fields) =>
        string.Join('\n', fields.Where(f => f.Key != "hash").OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}={f.Value}"));

    public static byte[] Sign(string botToken, string dataCheckString)
    {
        var secret = HMACSHA256.HashData(WebAppDataKey, Encoding.UTF8.GetBytes(botToken));
        return HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
    }

    private static TelegramMiniAppUser? ParseUser(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var userId)) return null;

            return new TelegramMiniAppUser(userId, Text(root, "username"), Text(root, "first_name"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
