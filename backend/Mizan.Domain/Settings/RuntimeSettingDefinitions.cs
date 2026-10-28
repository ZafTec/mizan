using System.Globalization;
using System.Net.Mail;
using System.Text.Json;

namespace Mizan.Domain.Settings;

public enum RuntimeSettingType
{
    String,
    Int,
    Bool,
    Enum,
}

/// <summary>What a string setting must look like, beyond being text.</summary>
public enum RuntimeSettingFormat
{
    Text,
    Host,
    Url,
    Email,
    Json,

    /// <summary>Letters, digits, underscore and hyphen only, such as a Telegram webhook secret.</summary>
    Token,
}

/// <summary>
/// A feature a group's settings turn on, and what the page says while the listed keys have no value.
/// </summary>
public sealed record RuntimeSettingNotice(IReadOnlyList<string> RequiredKeys, string Message);

/// <summary>One section of the admin settings page.</summary>
public sealed record RuntimeSettingGroup(string Id, string Title, IReadOnlyList<RuntimeSettingNotice> Notices)
{
    /// <summary>The first notice whose keys are not all set, or null when the group's features are on.</summary>
    public string? NoticeFor(Func<string, bool> hasValue) =>
        Notices.FirstOrDefault(n => !n.RequiredKeys.All(hasValue))?.Message;
}

/// <summary>The sections of the admin settings page, in the order it shows them.</summary>
public static class RuntimeSettingGroups
{
    public const string Email = "email";
    public const string AiProvider = "ai-provider";
    public const string AiLimits = "ai-limits";
    public const string Storage = "storage";
    public const string Telegram = "telegram";
    public const string Push = "push";
    public const string Billing = "billing";
    public const string Prices = "prices";
    public const string OAuth = "oauth";
    public const string Outbox = "outbox";

    public static readonly IReadOnlyList<RuntimeSettingGroup> All =
    [
        new(Email, "Email", [new(["Smtp.Host"], "Email is not configured. Verification and password emails are not sent.")]),
        new(AiProvider, "AI provider", [new(["Ai.BaseUrl", "Ai.ApiKey"], "The AI provider is not configured. The assistant and meal planning are off.")]),
        new(AiLimits, "AI limits", []),
        new(Storage, "Storage", [new(["Storage.ServiceUrl", "Storage.AccessKeyId", "Storage.SecretAccessKey"], "Storage is not configured. Image uploads are refused.")]),
        new(Telegram, "Telegram",
        [
            new(["Telegram.BotToken", "Telegram.BotUsername"], "The Telegram bot is not configured. It does not answer and accounts cannot be linked."),
            new(["Telegram.BotServiceUrl"], "The bot service URL is empty. Reminders are shown in the app only."),
        ]),
        new(Push, "Push", [new(["Push.FcmProjectId", "Push.FcmServiceAccountJson"], "Push is not configured. Phones get no notifications.")]),
        new(Billing, "Billing", [new(["Paddle.ApiKey", "Paddle.WebhookSecret"], "Billing is not configured. Upgrades are not offered.")]),
        new(Prices, "Price crawler", []),
        new(OAuth, "Connected apps", []),
        new(Outbox, "Background jobs", []),
    ];
}

/// <summary>
/// One setting an administrator may change at runtime. <see cref="Key"/> is the options path with
/// dots: <c>Smtp.Host</c> is <c>SmtpOptions.Host</c>. The value is the stored one, else
/// <see cref="Default"/>; configuration and the environment are never read for it.
/// </summary>
public sealed record RuntimeSettingDefinition(
    string Key,
    string Group,
    string Label,
    string Description,
    RuntimeSettingType Type,
    bool IsSecret = false)
{
    public RuntimeSettingFormat Format { get; init; } = RuntimeSettingFormat.Text;

    public long? Min { get; init; }

    public long? Max { get; init; }

    public IReadOnlyList<string> AllowedValues { get; init; } = [];

    /// <summary>An empty value is meaningful (it turns the feature off or uses a fallback) rather than a mistake.</summary>
    public bool AllowEmpty { get; init; }

    public int MaxLength { get; init; } = 2000;

    /// <summary>The value when nothing is stored, as text. Null means none (a secret, or a feature that is off).</summary>
    public string? Default { get; init; }

    /// <summary>The configuration section the key belongs to, such as <c>Smtp</c>.</summary>
    public string Section => Key[..Key.IndexOf('.', StringComparison.Ordinal)];

    /// <summary>The configuration path, such as <c>Smtp:Host</c>.</summary>
    public string ConfigurationPath => Key.Replace('.', ':');
}

/// <summary>The outcome of checking a value: the value to store, or the message to show.</summary>
public sealed record RuntimeSettingValidation(string? Value, string? Error)
{
    public bool IsValid => Error is null;

    public static RuntimeSettingValidation Ok(string value) => new(value, null);

    public static RuntimeSettingValidation Fail(string error) => new(null, error);
}

/// <summary>
/// Every setting the admin area can change, with its type and rules. The API validates against it,
/// the page is drawn from it, and the overlay reads only these keys from the database. A key that is
/// not here stays in configuration (docs/ADMIN.md#runtime-settings).
/// </summary>
public static class RuntimeSettingDefinitions
{
    private const long Unlimited = 1_000_000_000_000;

    public static readonly IReadOnlyList<RuntimeSettingDefinition> All = Build();

    private static readonly Dictionary<string, RuntimeSettingDefinition> ByKey =
        All.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>The definition for a key, matched without regard to case, or null.</summary>
    public static RuntimeSettingDefinition? Find(string key) =>
        ByKey.GetValueOrDefault(key ?? string.Empty);

    /// <summary>Checks a value and returns it in its stored form (trimmed, canonical case for enums and booleans).</summary>
    public static RuntimeSettingValidation Validate(RuntimeSettingDefinition definition, string? value)
    {
        // A secret is stored exactly as typed; anything else loses stray whitespace.
        var text = definition.IsSecret ? value ?? string.Empty : (value ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return definition.AllowEmpty
                ? RuntimeSettingValidation.Ok(string.Empty)
                : RuntimeSettingValidation.Fail(
                    $"{definition.Label} cannot be empty. Clear the setting to use the default.");
        }

        if (text.Length > definition.MaxLength)
        {
            return RuntimeSettingValidation.Fail($"{definition.Label} is longer than {definition.MaxLength} characters.");
        }

        return definition.Type switch
        {
            RuntimeSettingType.Int => ValidateInt(definition, text),
            RuntimeSettingType.Bool => ValidateBool(definition, text),
            RuntimeSettingType.Enum => ValidateEnum(definition, text),
            _ => ValidateString(definition, text),
        };
    }

    private static RuntimeSettingValidation ValidateInt(RuntimeSettingDefinition definition, string text)
    {
        var min = definition.Min ?? 0;
        var max = definition.Max ?? Unlimited;
        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            || number < min || number > max)
        {
            return RuntimeSettingValidation.Fail(
                $"{definition.Label} must be a whole number from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}.");
        }

        return RuntimeSettingValidation.Ok(number.ToString(CultureInfo.InvariantCulture));
    }

    private static RuntimeSettingValidation ValidateBool(RuntimeSettingDefinition definition, string text) =>
        bool.TryParse(text, out var flag)
            ? RuntimeSettingValidation.Ok(flag ? "true" : "false")
            : RuntimeSettingValidation.Fail($"{definition.Label} must be true or false.");

    private static RuntimeSettingValidation ValidateEnum(RuntimeSettingDefinition definition, string text)
    {
        var match = definition.AllowedValues.FirstOrDefault(v => string.Equals(v, text, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? RuntimeSettingValidation.Fail($"{definition.Label} must be one of {string.Join(", ", definition.AllowedValues)}.")
            : RuntimeSettingValidation.Ok(match);
    }

    private static RuntimeSettingValidation ValidateString(RuntimeSettingDefinition definition, string text)
    {
        switch (definition.Format)
        {
            case RuntimeSettingFormat.Host:
                if (text.Contains("://", StringComparison.Ordinal) || Uri.CheckHostName(text) == UriHostNameType.Unknown)
                {
                    return RuntimeSettingValidation.Fail($"{definition.Label} must be a host name or an IP address, without a scheme or port.");
                }

                break;

            case RuntimeSettingFormat.Url:
                if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    || !string.IsNullOrEmpty(uri.UserInfo)
                    || string.IsNullOrEmpty(uri.Host))
                {
                    return RuntimeSettingValidation.Fail($"{definition.Label} must be an http or https URL.");
                }

                break;

            case RuntimeSettingFormat.Email:
                if (!MailAddress.TryCreate(text, out var address)
                    || !string.Equals(address.Address, text, StringComparison.OrdinalIgnoreCase)
                    || !address.Host.Contains('.', StringComparison.Ordinal))
                {
                    return RuntimeSettingValidation.Fail($"{definition.Label} must be an email address.");
                }

                break;

            case RuntimeSettingFormat.Token:
                if (!text.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
                {
                    return RuntimeSettingValidation.Fail($"{definition.Label} may hold only letters, digits, underscores and hyphens.");
                }

                break;

            case RuntimeSettingFormat.Json:
                if (!IsServiceAccountJson(text))
                {
                    return RuntimeSettingValidation.Fail($"{definition.Label} must be the service account key file's JSON, with client_email and private_key.");
                }

                break;
        }

        return RuntimeSettingValidation.Ok(text);
    }

    private static bool IsServiceAccountJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("client_email", out var email) && email.ValueKind == JsonValueKind.String
                && root.TryGetProperty("private_key", out var key) && key.ValueKind == JsonValueKind.String;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<RuntimeSettingDefinition> Build()
    {
        const string email = RuntimeSettingGroups.Email;
        const string provider = RuntimeSettingGroups.AiProvider;
        const string limits = RuntimeSettingGroups.AiLimits;
        const string storage = RuntimeSettingGroups.Storage;
        const string telegram = RuntimeSettingGroups.Telegram;

        var list = new List<RuntimeSettingDefinition>
        {
            new("Smtp.Host", email, "SMTP host", "The mail server's host name or IP address. Without one, Development prints mail to the console and other environments fail delivery.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Host, AllowEmpty = true },
            new("Smtp.Port", email, "SMTP port", "Usually 587 for STARTTLS or 465 for implicit TLS.", RuntimeSettingType.Int) { Min = 1, Max = 65535, Default = "587" },
            new("Smtp.Security", email, "Security", "Auto uses implicit TLS on port 465 and requires STARTTLS elsewhere. None is for a trusted plaintext relay only.", RuntimeSettingType.Enum) { AllowedValues = ["Auto", "StartTls", "SslOnConnect", "None"], Default = "Auto" },
            new("Smtp.Username", email, "Username", "Leave empty to send without authentication.", RuntimeSettingType.String) { AllowEmpty = true },
            new("Smtp.Password", email, "Password", "The SMTP password. Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true),
            new("Smtp.FromAddress", email, "From address", "The sender address on every message.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Email, Default = "noreply@mizan.local" },
            new("Smtp.FromName", email, "From name", "The sender name on every message.", RuntimeSettingType.String) { MaxLength = 100, Default = "Mizan" },
            new("Smtp.LocalDomain", email, "EHLO name", "The host name sent in EHLO. Empty uses the sender address's domain.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Host, AllowEmpty = true },

            new("Ai.BaseUrl", provider, "Endpoint", "An OpenAI-compatible endpoint, such as https://api.example/v1. Empty turns the assistant off.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Url, AllowEmpty = true },
            new("Ai.ApiKey", provider, "API key", "The provider key. Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true),
            new("Ai.Model", provider, "Model", "The model or deployment name sent with every call.", RuntimeSettingType.String) { MaxLength = 200, Default = "gpt-5.6-luna" },
            new("Ai.MaxOutputTokens", provider, "Output tokens", "The default limit on tokens in one answer.", RuntimeSettingType.Int) { Min = 1, Max = 200_000, Default = "1024" },
            new("Ai.TimeoutSeconds", provider, "Timeout (seconds)", "How long one call may take.", RuntimeSettingType.Int) { Min = 5, Max = 600, Default = "60" },
            new("Ai.SupportsTemperature", provider, "Send temperature", "False for models that reject a temperature.", RuntimeSettingType.Bool) { Default = "true" },
            new("Ai.ReasoningEffort", provider, "Reasoning effort", "Empty for providers that do not accept the field.", RuntimeSettingType.Enum) { AllowedValues = ["none", "minimal", "low", "medium", "high"], AllowEmpty = true },
            new("Ai.PromptCostPerMillionMicros", provider, "Prompt price", "Millionths of a currency unit per million prompt tokens. The cost ceiling relies on it.", RuntimeSettingType.Int) { Max = Unlimited, Default = "150000" },
            new("Ai.CompletionCostPerMillionMicros", provider, "Completion price", "Millionths of a currency unit per million completion tokens.", RuntimeSettingType.Int) { Max = Unlimited, Default = "600000" },

            new("Ai.GlobalDailyTokens", limits, "Global tokens per day", "Every line together stops here each UTC day.", RuntimeSettingType.Int) { Max = Unlimited, Default = "5000000" },
            new("Ai.GlobalDailyCostMicros", limits, "Global cost per day", "In millionths of a currency unit, every line together.", RuntimeSettingType.Int) { Max = Unlimited, Default = "20000000" },
        };

        foreach (var (line, title, requests, tokens) in new[]
        {
            ("Free", "Free personal", 5, 20_000), ("Pro", "Pro personal", 200, 500_000), ("PlanningFree", "Free planning", 1, 30_000),
            ("PlanningPro", "Pro planning", 20, 400_000), ("Onboarding", "Onboarding", 60, 60_000), ("Trainer", "Trainer-client", 300, 600_000),
            ("Eval", "Evaluation", 400, 400_000),
        })
        {
            list.Add(new($"Ai.{line}.DailyRequests", limits, $"{title} requests", $"Calls per person per UTC day on the {title.ToLowerInvariant()} line.", RuntimeSettingType.Int) { Max = 1_000_000, Default = Text(requests) });
            list.Add(new($"Ai.{line}.DailyTokens", limits, $"{title} tokens", $"Tokens per person per UTC day on the {title.ToLowerInvariant()} line.", RuntimeSettingType.Int) { Max = int.MaxValue, Default = Text(tokens) });
        }

        list.AddRange(
        [
            new("Storage.ServiceUrl", storage, "Endpoint", "The S3-compatible endpoint, such as a MinIO or Cloudflare R2 URL. Empty refuses uploads.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Url, AllowEmpty = true },
            new("Storage.Region", storage, "Region", "auto for Cloudflare R2; any region name for MinIO.", RuntimeSettingType.String) { MaxLength = 100, Default = "auto" },
            new("Storage.AccessKeyId", storage, "Access key id", "The access key that may write to the bucket.", RuntimeSettingType.String) { AllowEmpty = true, MaxLength = 200 },
            new("Storage.SecretAccessKey", storage, "Secret access key", "Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true) { MaxLength = 400 },
            new("Storage.Bucket", storage, "Bucket", "The bucket that holds uploads.", RuntimeSettingType.String) { MaxLength = 63, Default = "mizan" },
            new("Storage.ForcePathStyle", storage, "Path-style addresses", "True for MinIO and R2, which serve the bucket in the path rather than the host name.", RuntimeSettingType.Bool) { Default = "true" },
            new("Storage.PublicBaseUrl", storage, "Public base URL", "Where browsers read objects, such as a public bucket domain. Empty serves presigned URLs instead.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Url, AllowEmpty = true },
            new("Storage.PresignedUrlMinutes", storage, "Presigned URL minutes", "How long a presigned URL works when there is no public base URL.", RuntimeSettingType.Int) { Min = 1, Max = 10_080, Default = "60" },
            new("Storage.MaxUploadBytes", storage, "Largest image (bytes)", "The largest image one upload may send.", RuntimeSettingType.Int) { Min = 1024, Max = 100L * 1024 * 1024, Default = Text(8 * 1024 * 1024) },
            new("Storage.MaxModelBytes", storage, "Largest 3D model (bytes)", "The largest exercise model one upload may send.", RuntimeSettingType.Int) { Min = 1024, Max = 500L * 1024 * 1024, Default = Text(30 * 1024 * 1024) },

            new("Telegram.BotToken", telegram, "Bot token", "From BotFather. Only the bot service reads it. Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true) { MaxLength = 200 },
            new("Telegram.BotUsername", telegram, "Bot username", "The bot's @name without the @, used in link codes. Empty turns linking off.", RuntimeSettingType.String) { AllowEmpty = true, MaxLength = 64 },
            new("Telegram.UseWebhook", telegram, "Use a webhook", "True receives updates at the app URL's /telegram/webhook; false polls Telegram, for development.", RuntimeSettingType.Bool) { Default = "false" },
            new("Telegram.WebhookSecret", telegram, "Webhook secret", "Telegram sends it with every update. Letters, digits, _ and - only. Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true) { Format = RuntimeSettingFormat.Token, MaxLength = 256 },
            new("Telegram.MiniAppUrl", telegram, "Mini App URL", "Where the Mini App pages live, over HTTPS. Empty uses the app URL's /tg.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Url, AllowEmpty = true },
            new("Telegram.MiniAppMaxAgeSeconds", telegram, "Mini App launch age (seconds)", "How old a Mini App launch may be before it is refused.", RuntimeSettingType.Int) { Min = 30, Max = 86_400, Default = "300" },
            new("Telegram.MiniAppSessionHours", telegram, "Mini App session (hours)", "How long a Mini App sign-in lasts.", RuntimeSettingType.Int) { Min = 1, Max = 720, Default = "24" },
            new("Telegram.BotServiceUrl", telegram, "Bot service URL", "Where the API reaches the bot service on the internal network, such as http://mizan-telegram:5002. Empty sends reminders in the app only.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Url, AllowEmpty = true },

            new("Push.FcmProjectId", RuntimeSettingGroups.Push, "Firebase project", "The Firebase project id. Empty turns push off.", RuntimeSettingType.String) { AllowEmpty = true, MaxLength = 200 },
            new("Push.FcmServiceAccountJson", RuntimeSettingGroups.Push, "Service account key", "The Firebase service account key file's JSON. Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true) { Format = RuntimeSettingFormat.Json, MaxLength = 16_000 },

            new("Paddle.Environment", RuntimeSettingGroups.Billing, "Paddle environment", "sandbox or production. Change it together with the key and webhook secret.", RuntimeSettingType.Enum) { AllowedValues = ["sandbox", "production"], Default = "sandbox" },
            new("Paddle.ApiKey", RuntimeSettingGroups.Billing, "Paddle API key", "Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true),
            new("Paddle.WebhookSecret", RuntimeSettingGroups.Billing, "Webhook secret", "From the Paddle notification destination. Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true),
            new("Paddle.GraceDays", RuntimeSettingGroups.Billing, "Grace days", "How long a past-due subscription keeps Pro.", RuntimeSettingType.Int) { Max = 60, Default = "3" },
            new("Paddle.LifetimePriceId", RuntimeSettingGroups.Billing, "Lifetime price id", "The one-time price that grants a lifetime plan. Empty when there is none.", RuntimeSettingType.String) { AllowEmpty = true, MaxLength = 100, Default = "pri_01kwa2qt2c0q0ntgp0ntrn3tbt" },

            new("Prices.UserAgent", RuntimeSettingGroups.Prices, "User agent", "Sent with every request to a shop and matched against its robots.txt.", RuntimeSettingType.String) { MaxLength = 300, Default = "MizanPriceBot/1.0 (+https://mizan.zaftech.co; prices@zaftech.co)" },
            new("Prices.RequestDelayMs", RuntimeSettingGroups.Prices, "Delay between requests (ms)", "The least gap between two requests to one shop. A larger robots.txt Crawl-delay wins.", RuntimeSettingType.Int) { Max = 60_000, Default = "1000" },
            new("Prices.MaxProductsPerRun", RuntimeSettingGroups.Prices, "Products per run", "Product pages one run may read.", RuntimeSettingType.Int) { Min = 1, Max = 100_000, Default = "500" },

            new("OAuth.AccessTokenMinutes", RuntimeSettingGroups.OAuth, "Access token minutes", "How long an access token issued to a connected app works.", RuntimeSettingType.Int) { Min = 1, Max = 1440, Default = "60" },
            new("OAuth.RefreshTokenDays", RuntimeSettingGroups.OAuth, "Refresh token days", "How long a connected app stays signed in without use.", RuntimeSettingType.Int) { Min = 1, Max = 365, Default = "30" },
            new("OAuth.RequestMinutes", RuntimeSettingGroups.OAuth, "Consent minutes", "How long a person has to approve a connection.", RuntimeSettingType.Int) { Min = 1, Max = 60, Default = "10" },
            new("OAuth.CodeSeconds", RuntimeSettingGroups.OAuth, "Code seconds", "How long an authorization code may wait before it is exchanged.", RuntimeSettingType.Int) { Min = 10, Max = 600, Default = "120" },
            new("OAuth.AllowMetadataClients", RuntimeSettingGroups.OAuth, "Allow metadata clients", "Accept apps whose client id is the URL of a metadata document, as MCP clients use.", RuntimeSettingType.Bool) { Default = "true" },

            new("Outbox.PollSeconds", RuntimeSettingGroups.Outbox, "Poll seconds", "How often the job runner looks for work when the queue is empty.", RuntimeSettingType.Int) { Min = 1, Max = 300, Default = "3" },
            new("Outbox.MaxAttempts", RuntimeSettingGroups.Outbox, "Attempts", "How many times a failing job runs before it is left as failed.", RuntimeSettingType.Int) { Min = 1, Max = 50, Default = "5" },
            new("Outbox.BackoffSeconds", RuntimeSettingGroups.Outbox, "Backoff seconds", "The wait after the first failure. It doubles with each attempt, up to 15 minutes.", RuntimeSettingType.Int) { Min = 1, Max = 900, Default = "30" },
            new("Outbox.StaleAfterMinutes", RuntimeSettingGroups.Outbox, "Stale after (minutes)", "A job claimed this long ago by a runner that stopped is run again.", RuntimeSettingType.Int) { Min = 1, Max = 1440, Default = "30" },
        ]);

        return list;
    }

    private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);
}
