using System.Globalization;
using Microsoft.Extensions.Configuration;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Domain.Settings;

namespace Mizan.Infrastructure.Settings;

/// <summary>
/// <c>Mizan.Api --import-env-settings</c>: copies the values a deployment used to pass in the
/// environment into <c>system_settings</c>, once, before the environment stops being read
/// (docs/ADMIN.md#moving-settings-out-of-the-environment). A key already stored is left alone, so
/// running it twice changes nothing. Secrets are encrypted on the way in like any other save.
/// Only key names are printed.
/// </summary>
public static class EnvironmentSettingsImport
{
    /// <summary>
    /// The variables each key used to come from, after its own configuration path (such as
    /// <c>Smtp:Host</c>, or <c>Smtp__Host</c> in the environment). The upper-case names are the
    /// ones the Compose files mapped from <c>.env</c>; the <c>TelegramBot:</c> paths are
    /// the bot service's own.
    /// </summary>
    private static readonly Dictionary<string, string[]> LegacySources = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Smtp.Host"] = ["SMTP_HOST"],
        ["Smtp.Port"] = ["SMTP_PORT"],
        ["Smtp.Username"] = ["SMTP_USER"],
        ["Smtp.Password"] = ["SMTP_PASS"],
        ["Smtp.FromAddress"] = ["SMTP_FROM_ADDRESS", "SMTP_FROM"],
        ["Smtp.FromName"] = ["SMTP_FROM_NAME"],
        ["Ai.BaseUrl"] = ["AI_BASE_URL"],
        ["Ai.ApiKey"] = ["AI_API_KEY"],
        ["Ai.Model"] = ["AI_MODEL"],
        ["Ai.SupportsTemperature"] = ["AI_SUPPORTS_TEMPERATURE"],
        ["Ai.ReasoningEffort"] = ["AI_REASONING_EFFORT"],
        ["Ai.GlobalDailyTokens"] = ["AI_GLOBAL_DAILY_TOKENS"],
        ["Ai.GlobalDailyCostMicros"] = ["AI_GLOBAL_DAILY_COST_MICROS"],
        ["Storage.ServiceUrl"] = ["STORAGE_SERVICE_URL"],
        ["Storage.Region"] = ["STORAGE_REGION"],
        ["Storage.AccessKeyId"] = ["STORAGE_ACCESS_KEY_ID"],
        ["Storage.SecretAccessKey"] = ["STORAGE_SECRET_ACCESS_KEY"],
        ["Storage.Bucket"] = ["STORAGE_BUCKET"],
        ["Storage.PublicBaseUrl"] = ["STORAGE_PUBLIC_BASE_URL"],
        ["Push.FcmProjectId"] = ["FCM_PROJECT_ID"],
        ["Push.FcmServiceAccountJson"] = ["FCM_SERVICE_ACCOUNT_JSON"],
        ["Telegram.BotUsername"] = ["TELEGRAM_BOT_USERNAME"],
        ["Telegram.BotServiceUrl"] = ["TELEGRAM_BOT_SERVICE_URL"],
        ["Telegram.BotToken"] = ["TelegramBot:BotToken", "TELEGRAM_BOT_TOKEN"],
        ["Telegram.UseWebhook"] = ["TelegramBot:UseWebhook", "TELEGRAM_USE_WEBHOOK"],
        ["Telegram.WebhookSecret"] = ["TelegramBot:WebhookSecret", "TELEGRAM_WEBHOOK_SECRET"],
        ["Telegram.MiniAppUrl"] = ["TelegramBot:MiniAppUrl", "TELEGRAM_MINIAPP_URL"],
        ["Paddle.Environment"] = ["PADDLE_ENVIRONMENT", "NEXT_PUBLIC_PADDLE_ENV"],
        ["Paddle.ClientToken"] = ["NEXT_PUBLIC_PADDLE_CLIENT_TOKEN"],
        ["Paddle.ApiKey"] = ["PADDLE_API_KEY"],
        ["Paddle.WebhookSecret"] = ["PADDLE_WEBHOOK_SECRET"],
        ["Paddle.GraceDays"] = ["PADDLE_GRACE_DAYS"],
        ["Paddle.LifetimePriceId"] = ["PADDLE_PRICE_LIFETIME_LEGACY"],
        ["Prices.UserAgent"] = ["PRICES_USER_AGENT"],
        ["Prices.RequestDelayMs"] = ["PRICES_REQUEST_DELAY_MS"],
        ["Prices.MaxProductsPerRun"] = ["PRICES_MAX_PRODUCTS_PER_RUN"],
        ["Authentication.Google.ClientId"] = ["GOOGLE_CLIENT_ID"],
        ["Authentication.Google.ClientSecret"] = ["GOOGLE_CLIENT_SECRET"],
        ["Authentication.GitHub.ClientId"] = ["GITHUB_CLIENT_ID"],
        ["Authentication.GitHub.ClientSecret"] = ["GITHUB_CLIENT_SECRET"],
    };

    /// <summary>Old keys that held a TimeSpan where the new key holds a whole number.</summary>
    private static readonly Dictionary<string, (string Path, Func<TimeSpan, double> Convert)> LegacyTimeSpans = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Telegram.MiniAppSessionHours"] = ("Telegram:MiniAppSessionLifetime", t => t.TotalHours),
        ["Telegram.MiniAppMaxAgeSeconds"] = ("TelegramBot:MiniAppMaxAge", t => t.TotalSeconds),
    };

    /// <summary>What the production Compose file used when a variable was missing and the catalog's default differs.</summary>
    private static readonly Dictionary<string, string> ComposeDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Storage.Region"] = "us-east-1",
    };

    public static async Task<int> RunAsync(IConfiguration configuration, IRuntimeSettings settings, TextWriter output, CancellationToken cancellationToken)
    {
        var stored = (await settings.ListAsync(cancellationToken))
            .Where(s => s.Source == RuntimeSettingSources.Database)
            .Select(s => s.Definition.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var storageConfigured = !string.IsNullOrWhiteSpace(Find(configuration, "Storage.ServiceUrl"));
        var imported = 0;
        var failed = 0;

        foreach (var definition in RuntimeSettingDefinitions.All)
        {
            if (stored.Contains(definition.Key))
            {
                output.WriteLine($"kept      {definition.Key} (already set in the admin area)");
                continue;
            }

            var value = Find(configuration, definition.Key);
            if (value is null && definition.Key == "Storage.Region" && storageConfigured)
            {
                value = ComposeDefaults[definition.Key];
            }

            if (value is null) continue;

            var checkedValue = RuntimeSettingDefinitions.Validate(definition, value);
            if (!checkedValue.IsValid)
            {
                // The message names the setting and the rule, never the value.
                output.WriteLine($"skipped   {definition.Key}: {checkedValue.Error}");
                failed++;
                continue;
            }

            await settings.SetAsync(definition.Key, checkedValue.Value!, null, cancellationToken);
            output.WriteLine($"imported  {definition.Key}{(definition.IsSecret ? " (encrypted)" : string.Empty)}");
            imported++;
        }

        output.WriteLine($"{imported} setting(s) imported, {failed} skipped. Remove the imported variables from the environment.");
        return failed == 0 ? 0 : 1;
    }

    private static string? Find(IConfiguration configuration, string key)
    {
        var definition = RuntimeSettingDefinitions.Find(key)!;
        var paths = new List<string> { definition.ConfigurationPath };
        if (LegacySources.TryGetValue(key, out var legacy)) paths.AddRange(legacy);

        foreach (var path in paths)
        {
            var value = configuration[path];
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        if (LegacyTimeSpans.TryGetValue(key, out var span)
            && TimeSpan.TryParse(configuration[span.Path], CultureInfo.InvariantCulture, out var parsed))
        {
            return ((long)Math.Round(span.Convert(parsed))).ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }
}
