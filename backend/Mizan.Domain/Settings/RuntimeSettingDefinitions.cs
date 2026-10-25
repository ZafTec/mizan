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
}

/// <summary>The sections of the admin settings page, in the order it shows them.</summary>
public static class RuntimeSettingGroups
{
    public const string Email = "email";
    public const string AiProvider = "ai-provider";
    public const string AiLimits = "ai-limits";
    public const string Telegram = "telegram";
    public const string Push = "push";
    public const string Billing = "billing";

    public static readonly IReadOnlyList<(string Id, string Title)> All =
    [
        (Email, "Email"),
        (AiProvider, "AI provider"),
        (AiLimits, "AI limits"),
        (Telegram, "Telegram"),
        (Push, "Push"),
        (Billing, "Billing"),
    ];
}

/// <summary>
/// One setting an administrator may change at runtime. <see cref="Key"/> is the configuration path
/// with dots for colons: <c>Smtp.Host</c> overrides <c>Smtp:Host</c> (<c>Smtp__Host</c> in the
/// environment).
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
                    $"{definition.Label} cannot be empty. Clear the setting to use the configuration value.");
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

        var list = new List<RuntimeSettingDefinition>
        {
            new("Smtp.Host", email, "SMTP host", "The mail server's host name or IP address. Without one, Development prints mail to the console and other environments fail delivery.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Host },
            new("Smtp.Port", email, "SMTP port", "Usually 587 for STARTTLS or 465 for implicit TLS.", RuntimeSettingType.Int) { Min = 1, Max = 65535 },
            new("Smtp.Security", email, "Security", "Auto uses implicit TLS on port 465 and requires STARTTLS elsewhere. None is for a trusted plaintext relay only.", RuntimeSettingType.Enum) { AllowedValues = ["Auto", "StartTls", "SslOnConnect", "None"] },
            new("Smtp.Username", email, "Username", "Leave empty to send without authentication.", RuntimeSettingType.String) { AllowEmpty = true },
            new("Smtp.Password", email, "Password", "The SMTP password. Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true),
            new("Smtp.FromAddress", email, "From address", "The sender address on every message.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Email },
            new("Smtp.FromName", email, "From name", "The sender name on every message.", RuntimeSettingType.String) { MaxLength = 100 },
            new("Smtp.LocalDomain", email, "EHLO name", "The host name sent in EHLO. Empty uses the sender address's domain.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Host, AllowEmpty = true },

            new("Ai.BaseUrl", provider, "Endpoint", "An OpenAI-compatible endpoint, such as https://api.example/v1. Empty turns the assistant off.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Url, AllowEmpty = true },
            new("Ai.ApiKey", provider, "API key", "The provider key. Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true),
            new("Ai.Model", provider, "Model", "The model or deployment name sent with every call.", RuntimeSettingType.String) { MaxLength = 200 },
            new("Ai.MaxOutputTokens", provider, "Output tokens", "The default limit on tokens in one answer.", RuntimeSettingType.Int) { Min = 1, Max = 200_000 },
            new("Ai.TimeoutSeconds", provider, "Timeout (seconds)", "How long one call may take.", RuntimeSettingType.Int) { Min = 5, Max = 600 },
            new("Ai.SupportsTemperature", provider, "Send temperature", "False for models that reject a temperature.", RuntimeSettingType.Bool),
            new("Ai.ReasoningEffort", provider, "Reasoning effort", "Empty for providers that do not accept the field.", RuntimeSettingType.Enum) { AllowedValues = ["none", "minimal", "low", "medium", "high"], AllowEmpty = true },
            new("Ai.PromptCostPerMillionMicros", provider, "Prompt price", "Millionths of a currency unit per million prompt tokens. The cost ceiling relies on it.", RuntimeSettingType.Int) { Max = Unlimited },
            new("Ai.CompletionCostPerMillionMicros", provider, "Completion price", "Millionths of a currency unit per million completion tokens.", RuntimeSettingType.Int) { Max = Unlimited },

            new("Ai.GlobalDailyTokens", limits, "Global tokens per day", "Every line together stops here each UTC day.", RuntimeSettingType.Int) { Max = Unlimited },
            new("Ai.GlobalDailyCostMicros", limits, "Global cost per day", "In millionths of a currency unit, every line together.", RuntimeSettingType.Int) { Max = Unlimited },
        };

        foreach (var (line, title) in new[]
        {
            ("Free", "Free personal"), ("Pro", "Pro personal"), ("PlanningFree", "Free planning"),
            ("PlanningPro", "Pro planning"), ("Onboarding", "Onboarding"), ("Trainer", "Trainer-client"), ("Eval", "Evaluation"),
        })
        {
            list.Add(new($"Ai.{line}.DailyRequests", limits, $"{title} requests", $"Calls per person per UTC day on the {title.ToLowerInvariant()} line.", RuntimeSettingType.Int) { Max = 1_000_000 });
            list.Add(new($"Ai.{line}.DailyTokens", limits, $"{title} tokens", $"Tokens per person per UTC day on the {title.ToLowerInvariant()} line.", RuntimeSettingType.Int) { Max = int.MaxValue });
        }

        list.AddRange(
        [
            new("Telegram.BotServiceUrl", RuntimeSettingGroups.Telegram, "Bot service URL", "Where the API reaches the bot service on the internal network, such as http://mizan-telegram:5002. Empty sends reminders in the app only.", RuntimeSettingType.String) { Format = RuntimeSettingFormat.Url, AllowEmpty = true },

            new("Push.FcmProjectId", RuntimeSettingGroups.Push, "Firebase project", "The Firebase project id. Empty turns push off.", RuntimeSettingType.String) { AllowEmpty = true, MaxLength = 200 },
            new("Push.FcmServiceAccountJson", RuntimeSettingGroups.Push, "Service account key", "The Firebase service account key file's JSON. Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true) { Format = RuntimeSettingFormat.Json, MaxLength = 16_000 },

            new("Paddle.Environment", RuntimeSettingGroups.Billing, "Paddle environment", "sandbox or production. Change it together with the key and webhook secret.", RuntimeSettingType.Enum) { AllowedValues = ["sandbox", "production"] },
            new("Paddle.ApiKey", RuntimeSettingGroups.Billing, "Paddle API key", "Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true),
            new("Paddle.WebhookSecret", RuntimeSettingGroups.Billing, "Webhook secret", "From the Paddle notification destination. Stored encrypted and never shown again.", RuntimeSettingType.String, IsSecret: true),
            new("Paddle.GraceDays", RuntimeSettingGroups.Billing, "Grace days", "How long a past-due subscription keeps Pro.", RuntimeSettingType.Int) { Max = 60 },
            new("Paddle.LifetimePriceId", RuntimeSettingGroups.Billing, "Lifetime price id", "The one-time price that grants a lifetime plan. Empty when there is none.", RuntimeSettingType.String) { AllowEmpty = true, MaxLength = 100 },
        ]);

        return list;
    }
}
