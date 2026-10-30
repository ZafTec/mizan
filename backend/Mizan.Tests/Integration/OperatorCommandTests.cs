using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Admin;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Settings;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// <c>Mizan.Api --create-admin</c> and <c>Mizan.Api --import-env-settings</c>, the two commands a fresh or
/// upgraded deployment runs before anyone opens the admin area (docs/ADMIN.md).
/// </summary>
[Collection("ApiIntegration")]
public class OperatorCommandTests
{
    private const string Password = "a-long-enough-password-for-tests";

    private readonly ApiTestFixture _fixture;

    public OperatorCommandTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AnOperatorCreatesTheFirstAdministrator_WhoSetsAPasswordFromTheLink_AndSignsIn()
    {
        await _fixture.ResetDatabaseAsync();
        const string email = "First.Admin@Example.com";

        AdminBootstrapResult result;
        using (var scope = _fixture.Services.CreateScope())
        {
            result = await AdminBootstrap.CreateAsync(
                scope.ServiceProvider.GetRequiredService<MizanDbContext>(),
                scope.ServiceProvider.GetRequiredService<IAppUrls>(),
                email,
                CancellationToken.None);
        }

        result.Created.Should().BeTrue();
        var token = Uri.UnescapeDataString(result.PasswordLink.Split("token=")[1]);
        using var client = _fixture.CreateClient();
        (await client.PostAsJsonAsync("/api/Auth/reset-password", new { Token = token, Password }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync("/api/Auth/reset-password", new { Token = token, Password = Password + "-again" }))
            .IsSuccessStatusCode.Should().BeFalse("the link works once");

        var login = await client.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        using var check = _fixture.Services.CreateScope();
        var user = await check.ServiceProvider.GetRequiredService<MizanDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == result.UserId);
        user.Role.Should().Be("admin");
        user.EmailVerified.Should().BeTrue();

        // Run again for the same address, it promotes rather than duplicating.
        using var again = _fixture.Services.CreateScope();
        (await AdminBootstrap.CreateAsync(
            again.ServiceProvider.GetRequiredService<MizanDbContext>(),
            again.ServiceProvider.GetRequiredService<IAppUrls>(),
            email.ToLowerInvariant(),
            CancellationToken.None)).Created.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("Admin <admin@example.com>")]
    public async Task CreateAdmin_RefusesSomethingThatIsNotAnEmailAddress(string email)
    {
        using var scope = _fixture.Services.CreateScope();
        var create = () => AdminBootstrap.CreateAsync(
            scope.ServiceProvider.GetRequiredService<MizanDbContext>(),
            scope.ServiceProvider.GetRequiredService<IAppUrls>(),
            email,
            CancellationToken.None);

        await create.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnOperatorImportsTheEnvironment_SecretsAreEncrypted_AndStoredSettingsAreKept()
    {
        await _fixture.ResetDatabaseAsync();
        const string secret = "storage-secret-from-env";
        var environment = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            // The names the production Compose file mapped from .env, and the Section__Key form.
            ["STORAGE_SERVICE_URL"] = "https://account.r2.example",
            ["STORAGE_ACCESS_KEY_ID"] = "access",
            ["STORAGE_SECRET_ACCESS_KEY"] = secret,
            ["Smtp:Host"] = "smtp.example.test",
            ["SMTP_PORT"] = "2525",
            ["TelegramBot:MiniAppMaxAge"] = "00:10:00",
            ["GOOGLE_CLIENT_ID"] = "google-client.apps.googleusercontent.com",
            ["GOOGLE_CLIENT_SECRET"] = "google-secret-from-env",
            ["Authentication:GitHub:ClientId"] = "github-client",
            ["NEXT_PUBLIC_PADDLE_CLIENT_TOKEN"] = "test_paddle_token",
            // Already set in the admin area by the fixture, so the environment does not win.
            ["Telegram:BotUsername"] = "env_bot",
            ["Ai:Free:DailyRequests"] = "not-a-number",
        }).Build();

        using var scope = _fixture.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IRuntimeSettings>();
        var output = new StringWriter();

        var exit = await EnvironmentSettingsImport.RunAsync(environment, settings, output, CancellationToken.None);

        exit.Should().Be(0, output.ToString());
        output.ToString().Should().NotContain(secret).And.Contain("imported  Storage.SecretAccessKey (encrypted)");
        (await settings.GetAsync("Storage.ServiceUrl")).Should().Be("https://account.r2.example");
        (await settings.GetAsync("Storage.Region")).Should().Be("us-east-1", "the production Compose file defaulted it");
        (await settings.GetAsync("Smtp.Port")).Should().Be("2525");
        (await settings.GetAsync("Telegram.MiniAppMaxAgeSeconds")).Should().Be("600");
        (await settings.GetAsync("Telegram.BotUsername")).Should().Be("mizan_test_bot");
        (await settings.GetAsync("Ai.Free.DailyRequests")).Should().Be("3");
        (await settings.GetAsync("Authentication.Google.ClientId")).Should().Be("google-client.apps.googleusercontent.com");
        (await settings.GetAsync("Authentication.GitHub.ClientId")).Should().Be("github-client");
        (await settings.GetAsync("Paddle.ClientToken")).Should().Be("test_paddle_token");
        output.ToString().Should().NotContain("google-secret-from-env").And.Contain("imported  Authentication.Google.ClientSecret (encrypted)");

        var row = await scope.ServiceProvider.GetRequiredService<MizanDbContext>().SystemSettings.AsNoTracking()
            .SingleAsync(s => s.Key == "Storage.SecretAccessKey");
        JsonSerializer.Serialize(row).Should().NotContain(secret);

        // Twice changes nothing.
        var second = new StringWriter();
        (await EnvironmentSettingsImport.RunAsync(environment, settings, second, CancellationToken.None)).Should().Be(0);
        second.ToString().Should().Contain("0 setting(s) imported");
    }

    [Fact]
    public async Task Import_SkipsAnInvalidValue_NamesTheKey_AndExitsWithAFailure()
    {
        await _fixture.ResetDatabaseAsync();
        var environment = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SMTP_PORT"] = "70000",
        }).Build();
        using var scope = _fixture.Services.CreateScope();
        var output = new StringWriter();

        var exit = await EnvironmentSettingsImport.RunAsync(
            environment, scope.ServiceProvider.GetRequiredService<IRuntimeSettings>(), output, CancellationToken.None);

        exit.Should().Be(1);
        output.ToString().Should().Contain("skipped   Smtp.Port: SMTP port must be a whole number from 1 to 65535.");
    }
}
