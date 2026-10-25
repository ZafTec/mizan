using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Mizan.Application.Behaviors;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Email;
using Mizan.Infrastructure.Outbox;
using Mizan.Tests.Infrastructure.Email;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Runtime settings through /api/admin/settings (docs/ADMIN.md#runtime-settings): what an
/// administrator sets is what the next send uses, a secret goes in and never comes out, and
/// clearing a value hands it back to configuration.
/// </summary>
[Collection("ApiIntegration")]
public class AdminSettingsTests
{
    private const string Secret = "smtp-password-that-must-not-leak";

    private readonly ApiTestFixture _fixture;

    public AdminSettingsTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AnAdminSetsTheSmtpHost_AndTheNextOutboundEmailUsesIt()
    {
        await _fixture.ResetDatabaseAsync();
        var (admin, email) = await AdminAsync();
        await using var server = new SmtpTestServer();

        await PutAsync(admin, "Smtp.Host", "127.0.0.1");
        await PutAsync(admin, "Smtp.Port", server.Port.ToString());
        await PutAsync(admin, "Smtp.Security", "None");
        await PutAsync(admin, "Smtp.FromAddress", "admin-test@example.test");

        var queued = await admin.PostAsync("/api/admin/settings/smtp/test", null);
        queued.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await queued.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("to").GetString().Should().Be(email);
        var jobId = result.GetProperty("jobId").GetGuid();

        // The fixture swaps the real sender out, so this instance keeps it: the same API with real
        // SMTP, reading the settings the administrator just saved.
        await using var withSmtp = _fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }));
        var dispatcher = withSmtp.Services.GetServices<IHostedService>().OfType<OutboxDispatcher>().Single();
        while (await dispatcher.RunOnceAsync(CancellationToken.None)) { }

        await server.Completion;
        server.AcceptedMessages.Should().Be(1);
        server.Commands.Should().Contain(["MAIL", "RCPT", "DATA"]);

        using var scope = _fixture.Services.CreateScope();
        var job = await scope.ServiceProvider.GetRequiredService<MizanDbContext>().OutboxJobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
        job.Status.Should().Be(OutboxJobStatus.Succeeded);
    }

    [Fact]
    public async Task ASecretIsNeverReturned_AndIsEncryptedAtRest()
    {
        await _fixture.ResetDatabaseAsync();
        var (admin, _) = await AdminAsync();

        var saved = await admin.PutAsJsonAsync("/api/admin/settings/Smtp.Password", new { value = Secret });
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await saved.Content.ReadAsStringAsync()).Should().NotContain(Secret);

        var listing = await admin.GetStringAsync("/api/admin/settings");
        listing.Should().NotContain(Secret);
        var password = Setting(JsonDocument.Parse(listing).RootElement, "Smtp.Password");
        password.GetProperty("isSecret").GetBoolean().Should().BeTrue();
        password.GetProperty("hasValue").GetBoolean().Should().BeTrue();
        password.GetProperty("source").GetString().Should().Be("database");
        password.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);

        using var scope = _fixture.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<MizanDbContext>().SystemSettings.AsNoTracking().SingleAsync(s => s.Key == "Smtp.Password");
        row.IsSecret.Should().BeTrue();
        row.Value.Should().NotContain(Secret, "the stored value is the protected payload");

        // Nor can Application code read it back as text.
        var read = () => scope.ServiceProvider.GetRequiredService<IRuntimeSettings>().GetAsync("Smtp.Password");
        await read.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ClearingASetting_ReturnsItToConfiguration()
    {
        await _fixture.ResetDatabaseAsync();
        var (admin, _) = await AdminAsync();
        var userId = Guid.NewGuid();
        await _fixture.SeedUserAsync(userId, $"free-{userId:N}@example.com");
        var user = _fixture.CreateAuthenticatedClient(userId, $"free-{userId:N}@example.com");

        // The fixture's configuration gives Free three requests a day.
        (await FreeLimitAsync(user)).Should().Be(3);

        await PutAsync(admin, "Ai.Free.DailyRequests", "7");
        (await FreeLimitAsync(user)).Should().Be(7);
        var set = Setting(await admin.GetFromJsonAsync<JsonElement>("/api/admin/settings"), "Ai.Free.DailyRequests");
        set.GetProperty("source").GetString().Should().Be("database");
        set.GetProperty("value").GetString().Should().Be("7");

        var cleared = await admin.DeleteAsync("/api/admin/settings/Ai.Free.DailyRequests");
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await cleared.Content.ReadFromJsonAsync<JsonElement>();
        after.GetProperty("source").GetString().Should().Be("configuration");
        after.GetProperty("value").GetString().Should().Be("3");
        (await FreeLimitAsync(user)).Should().Be(3);
    }

    [Fact]
    public async Task ABadPortIsRefused_WithTheCatalogsMessage()
    {
        await _fixture.ResetDatabaseAsync();
        var (admin, _) = await AdminAsync();

        var refused = await admin.PutAsJsonAsync("/api/admin/settings/Smtp.Port", new { value = "70000" });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("validation_failed");
        body.GetProperty("error").GetString().Should().Be("SMTP port must be a whole number from 1 to 65535.");
        var port = Setting(await admin.GetFromJsonAsync<JsonElement>("/api/admin/settings"), "Smtp.Port");
        port.GetProperty("source").GetString().Should().Be("configuration", "nothing was stored");

        (await admin.PutAsJsonAsync("/api/admin/settings/Smtp.Nowhere", new { value = "1" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ANonAdmin_IsRefusedEverySettingsCall()
    {
        await _fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        await _fixture.SeedUserAsync(userId, $"plain-{userId:N}@example.com");
        var user = _fixture.CreateAuthenticatedClient(userId, $"plain-{userId:N}@example.com");

        (await user.GetAsync("/api/admin/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await user.PutAsJsonAsync("/api/admin/settings/Smtp.Host", new { value = "smtp.example.test" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await user.DeleteAsync("/api/admin/settings/Smtp.Host")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await user.PostAsync("/api/admin/settings/smtp/test", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await user.PostAsync("/api/admin/settings/ai/test", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = _fixture.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<MizanDbContext>().SystemSettings.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task TheAuditRow_NamesTheKey_AndNotTheSecret()
    {
        await _fixture.ResetDatabaseAsync();
        var (admin, _) = await AdminAsync();

        await PutAsync(admin, "Smtp.Password", Secret);
        await PutAsync(admin, "Smtp.Host", "smtp.example.test");
        (await admin.DeleteAsync("/api/admin/settings/Smtp.Password")).EnsureSuccessStatusCode();

        using var scope = _fixture.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<MizanDbContext>().AuditLogs.AsNoTracking()
            .Where(a => a.Action == "SetRuntimeSettingCommand" || a.Action == "ClearRuntimeSettingCommand")
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(a => !a.Details!.Contains(Secret));

        var secret = rows[0];
        secret.EntityId.Should().Be("Smtp.Password");
        var details = JsonDocument.Parse(secret.Details!).RootElement;
        details.GetProperty("Key").GetString().Should().Be("Smtp.Password");
        details.GetProperty("Value").GetString().Should().Be(AuditRedaction.Placeholder);

        JsonDocument.Parse(rows[1].Details!).RootElement.GetProperty("Value").GetString()
            .Should().Be("smtp.example.test", "a value that is not secret is worth keeping in the record");
        rows[2].Action.Should().Be("ClearRuntimeSettingCommand");
        rows[2].EntityId.Should().Be("Smtp.Password");
    }

    [Fact]
    public async Task TheAiTest_MakesOneMeteredCallOnTheEvaluationLine()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Ai.Reset();
        var (admin, _) = await AdminAsync();

        var reachable = await (await admin.PostAsync("/api/admin/settings/ai/test", null)).Content.ReadFromJsonAsync<JsonElement>();
        reachable.GetProperty("reachable").GetBoolean().Should().BeTrue();
        reachable.GetProperty("model").GetString().Should().Be("test-model");
        _fixture.Ai.Calls.Should().HaveCount(1);

        _fixture.Ai.Fail("The assistant returned an error (401). Try again shortly.");
        var failed = await (await admin.PostAsync("/api/admin/settings/ai/test", null)).Content.ReadFromJsonAsync<JsonElement>();
        failed.GetProperty("reachable").GetBoolean().Should().BeFalse();
        failed.GetProperty("errorClass").GetString().Should().Be("unavailable");

        using (var scope = _fixture.Services.CreateScope())
        {
            var ledger = await scope.ServiceProvider.GetRequiredService<MizanDbContext>().AiUsageLogs.AsNoTracking().ToListAsync();
            ledger.Should().HaveCount(2).And.OnlyContain(l => l.Feature == "eval", "both calls were reserved on the evaluation line");
            ledger.Select(l => l.Outcome).Should().BeEquivalentTo([AiCallOutcome.Succeeded, AiCallOutcome.ProviderError], "each was settled, the failure too");
        }

        _fixture.Ai.IsConfigured = false;
        var off = await (await admin.PostAsync("/api/admin/settings/ai/test", null)).Content.ReadFromJsonAsync<JsonElement>();
        off.GetProperty("errorClass").GetString().Should().Be("not_configured");
        _fixture.Ai.Reset();
    }

    private async Task<(HttpClient Client, string Email)> AdminAsync()
    {
        var id = Guid.NewGuid();
        var email = $"settings-admin-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email, role: "admin");
        return (_fixture.CreateAuthenticatedClient(id, email, "admin"), email);
    }

    private static async Task PutAsync(HttpClient client, string key, string value)
    {
        var response = await client.PutAsJsonAsync($"/api/admin/settings/{key}", new { value });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<int> FreeLimitAsync(HttpClient user) =>
        (await user.GetFromJsonAsync<JsonElement>("/api/Ai/usage")).GetProperty("today").GetProperty("requestLimit").GetInt32();

    private static JsonElement Setting(JsonElement listing, string key) =>
        listing.GetProperty("groups").EnumerateArray()
            .SelectMany(g => g.GetProperty("settings").EnumerateArray())
            .Single(s => s.GetProperty("key").GetString() == key);
}
