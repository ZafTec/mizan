using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Mizan.Contracts.Admin;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>The overview's system health grid, through the API (docs/ADMIN.md#overview-and-health).</summary>
[Collection("ApiIntegration")]
public class SystemHealthStoriesTests(ApiTestFixture fixture)
{
    [Fact]
    public async Task TheHealthEndpointReportsNotConfiguredForSmtpWithNoHost_AndOkForTheDatabase()
    {
        await fixture.ResetDatabaseAsync();
        var adminId = Guid.NewGuid();
        await fixture.SeedUserAsync(adminId, $"health-admin-{adminId:N}@example.test", role: "admin");
        using var admin = fixture.CreateAuthenticatedClient(adminId, $"health-admin-{adminId:N}@example.test", "admin");

        var health = await admin.GetFromJsonAsync<SystemHealthDto>("/api/admin/overview/health");

        var checks = health!.Checks.ToDictionary(c => c.Name);
        checks.Keys.Should().BeEquivalentTo(["database", "redis", "outbox", "email", "ai", "telegram", "push", "prices", "storage"]);
        checks["database"].Status.Should().Be("ok");
        checks["database"].Detail.Should().EndWith("ms round trip");
        checks["email"].Status.Should().Be("notConfigured");
        checks["email"].Detail.Should().Be("No SMTP host is set.");
        checks["telegram"].Status.Should().Be("notConfigured", "no bot service address is set in tests");
        checks["storage"].Status.Should().Be("notConfigured");
        checks["outbox"].Status.Should().Be("ok");
        checks.Values.Should().OnlyContain(c => new[] { "ok", "degraded", "down", "notConfigured" }.Contains(c.Status));

        var userId = Guid.NewGuid();
        await fixture.SeedUserAsync(userId, $"plain-{userId:N}@example.test");
        using var user = fixture.CreateAuthenticatedClient(userId, $"plain-{userId:N}@example.test");
        (await user.GetAsync("/api/admin/overview/health")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
