using FluentAssertions;
using Mizan.Domain.Settings;
using Xunit;

namespace Mizan.Tests.Settings;

/// <summary>The catalog's rules, as the API applies them before anything is stored.</summary>
public class RuntimeSettingDefinitionsTests
{
    [Theory]
    // Ports
    [InlineData("Smtp.Port", "587", "587", null)]
    [InlineData("Smtp.Port", " 465 ", "465", null)]
    [InlineData("Smtp.Port", "0", null, "SMTP port must be a whole number from 1 to 65535.")]
    [InlineData("Smtp.Port", "65536", null, "SMTP port must be a whole number from 1 to 65535.")]
    [InlineData("Smtp.Port", "five", null, "SMTP port must be a whole number from 1 to 65535.")]
    [InlineData("Smtp.Port", "58.7", null, "SMTP port must be a whole number from 1 to 65535.")]
    // Hosts
    [InlineData("Smtp.Host", "smtp.example.test", "smtp.example.test", null)]
    [InlineData("Smtp.Host", "10.0.0.5", "10.0.0.5", null)]
    [InlineData("Smtp.Host", "", "", null)]
    [InlineData("Smtp.Host", "smtp://mail.example.test", null, "SMTP host must be a host name or an IP address, without a scheme or port.")]
    [InlineData("Smtp.Host", "mail.example.test:587", null, "SMTP host must be a host name or an IP address, without a scheme or port.")]
    [InlineData("Smtp.LocalDomain", "", "", null)]
    // Enums and booleans come back in their canonical spelling
    [InlineData("Smtp.Security", "starttls", "StartTls", null)]
    [InlineData("Smtp.Security", "Tls", null, "Security must be one of Auto, StartTls, SslOnConnect, None.")]
    [InlineData("Ai.SupportsTemperature", "TRUE", "true", null)]
    [InlineData("Ai.SupportsTemperature", "yes", null, "Send temperature must be true or false.")]
    [InlineData("Ai.ReasoningEffort", "", "", null)]
    [InlineData("Paddle.Environment", "Production", "production", null)]
    // URLs and addresses
    [InlineData("Ai.BaseUrl", "https://api.example/v1", "https://api.example/v1", null)]
    [InlineData("Ai.BaseUrl", "", "", null)]
    [InlineData("Ai.BaseUrl", "ftp://api.example", null, "Endpoint must be an http or https URL.")]
    [InlineData("Ai.BaseUrl", "https://user:pass@api.example", null, "Endpoint must be an http or https URL.")]
    [InlineData("Telegram.BotServiceUrl", "http://mizan-telegram:5002", "http://mizan-telegram:5002", null)]
    [InlineData("Smtp.FromAddress", "noreply@mizan.example", "noreply@mizan.example", null)]
    [InlineData("Smtp.FromAddress", "Mizan <noreply@mizan.example>", null, "From address must be an email address.")]
    [InlineData("Smtp.FromAddress", "noreply@localhost", null, "From address must be an email address.")]
    // Limits
    [InlineData("Ai.Free.DailyRequests", "0", "0", null)]
    [InlineData("Ai.Free.DailyRequests", "-1", null, "Free personal requests must be a whole number from 0 to 1000000.")]
    [InlineData("Ai.GlobalDailyTokens", "5000000", "5000000", null)]
    // Secrets keep exactly what was typed, but not nothing
    [InlineData("Smtp.Password", " pass word ", " pass word ", null)]
    [InlineData("Smtp.Password", "   ", null, "Password cannot be empty. Clear the setting to use the default.")]
    // Telegram puts the webhook secret in a header, so it allows a short alphabet only
    [InlineData("Telegram.WebhookSecret", "Abc_123-xyz", "Abc_123-xyz", null)]
    [InlineData("Telegram.WebhookSecret", "abc def", null, "Webhook secret may hold only letters, digits, underscores and hyphens.")]
    [InlineData("Telegram.WebhookSecret", "abc+/=", null, "Webhook secret may hold only letters, digits, underscores and hyphens.")]
    // Storage, lifetimes and the crawler
    [InlineData("Storage.ServiceUrl", "https://account.r2.cloudflarestorage.com", "https://account.r2.cloudflarestorage.com", null)]
    [InlineData("Storage.ServiceUrl", "", "", null)]
    [InlineData("Storage.ForcePathStyle", "False", "false", null)]
    [InlineData("OAuth.AccessTokenMinutes", "0", null, "Access token minutes must be a whole number from 1 to 1440.")]
    [InlineData("Prices.RequestDelayMs", "250", "250", null)]
    [InlineData("Push.FcmServiceAccountJson", """{"client_email":"a@b.iam","private_key":"k"}""", """{"client_email":"a@b.iam","private_key":"k"}""", null)]
    [InlineData("Push.FcmServiceAccountJson", """{"client_email":"a@b.iam"}""", null, "Service account key must be the service account key file's JSON, with client_email and private_key.")]
    public void AValueIsStoredInItsCanonicalForm_OrRefusedWithAMessage(string key, string value, string? stored, string? error)
    {
        var definition = RuntimeSettingDefinitions.Find(key);
        definition.Should().NotBeNull();

        var result = RuntimeSettingDefinitions.Validate(definition!, value);

        result.Value.Should().Be(stored);
        result.Error.Should().Be(error);
    }

    [Theory]
    [InlineData("smtp.host", "Smtp.Host")]
    [InlineData("AI.FREE.DAILYREQUESTS", "Ai.Free.DailyRequests")]
    [InlineData("telegram.bottoken", "Telegram.BotToken")]
    [InlineData("Storage.SecretAccessKey", "Storage.SecretAccessKey")]
    // What stays in the environment is never a runtime setting
    [InlineData("Mcp.ServiceApiKey", null)]
    [InlineData("App.PublicUrl", null)]
    [InlineData("Prices.WorkerUrl", null)]
    [InlineData("Outbox.Enabled", null)]
    [InlineData("OAuth.Issuer", null)]
    public void OnlyCataloguedKeysAreFound_WithoutRegardToCase(string key, string? found) =>
        RuntimeSettingDefinitions.Find(key)?.Key.Should().Be(found);

    [Fact]
    public void EveryKeyBelongsToAShownSection_AndNamesItsConfigurationPath()
    {
        var groups = RuntimeSettingGroups.All.Select(g => g.Id).ToList();
        foreach (var definition in RuntimeSettingDefinitions.All)
        {
            groups.Should().Contain(definition.Group, definition.Key);
            definition.ConfigurationPath.Should().Be(definition.Key.Replace('.', ':'));
            new[] { "Smtp", "Ai", "Storage", "Telegram", "Push", "Paddle", "Prices", "OAuth", "Authentication", "Outbox" }.Should().Contain(definition.Section, definition.Key);
        }

        RuntimeSettingDefinitions.All.Select(d => d.Key).Should().OnlyHaveUniqueItems();
        RuntimeSettingDefinitions.All.Where(d => d.IsSecret).Select(d => d.Key).Should().BeEquivalentTo(
            ["Smtp.Password", "Ai.ApiKey", "Storage.SecretAccessKey", "Telegram.BotToken", "Telegram.WebhookSecret",
             "Push.FcmServiceAccountJson", "Paddle.ApiKey", "Paddle.WebhookSecret", "Authentication.Google.ClientSecret",
             "Authentication.GitHub.ClientSecret"]);
    }

    [Fact]
    public void EveryDefault_PassesItsOwnRule_AndNoSecretHasOne()
    {
        foreach (var definition in RuntimeSettingDefinitions.All.Where(d => d.Default is not null))
        {
            definition.IsSecret.Should().BeFalse(definition.Key);
            RuntimeSettingDefinitions.Validate(definition, definition.Default).Value.Should().Be(definition.Default, definition.Key);
        }
    }

    [Theory]
    [InlineData("Smtp.Port", "587")]
    [InlineData("Storage.Region", "auto")]
    [InlineData("Storage.Bucket", "mizan")]
    [InlineData("Telegram.UseWebhook", "false")]
    [InlineData("Telegram.MiniAppSessionHours", "24")]
    [InlineData("OAuth.AccessTokenMinutes", "60")]
    [InlineData("Outbox.MaxAttempts", "5")]
    [InlineData("Prices.MaxProductsPerRun", "500")]
    [InlineData("Ai.Free.DailyRequests", "5")]
    [InlineData("Smtp.Host", null)]
    [InlineData("Telegram.BotToken", null)]
    public void AnUnsetKey_FallsBackToItsBuiltInDefault(string key, string? expected) =>
        RuntimeSettingDefinitions.Find(key)!.Default.Should().Be(expected);

    [Theory]
    [InlineData("email", new string[0], "Email is not configured. Verification and password emails are not sent.")]
    [InlineData("email", new[] { "Smtp.Host" }, null)]
    [InlineData("storage", new[] { "Storage.ServiceUrl", "Storage.AccessKeyId" }, "Storage is not configured. Image uploads are refused.")]
    [InlineData("storage", new[] { "Storage.ServiceUrl", "Storage.AccessKeyId", "Storage.SecretAccessKey" }, null)]
    [InlineData("telegram", new[] { "Telegram.BotToken", "Telegram.BotUsername" }, "The bot service URL is empty. Reminders are shown in the app only.")]
    [InlineData("telegram", new[] { "Telegram.BotToken", "Telegram.BotUsername", "Telegram.BotServiceUrl" }, null)]
    [InlineData("ai-limits", new string[0], null)]
    public void AGroupSaysWhatIsOff_UntilTheKeysItNeedsHaveValues(string group, string[] set, string? notice) =>
        RuntimeSettingGroups.All.Single(g => g.Id == group).NoticeFor(set.Contains).Should().Be(notice);
}
