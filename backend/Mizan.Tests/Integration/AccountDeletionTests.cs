using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>Deleting the account is final: the person is signed out everywhere and their log goes with them.</summary>
[Collection("ApiIntegration")]
public class AccountDeletionTests
{
    private readonly ApiTestFixture _fixture;

    public AccountDeletionTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DeletingTheAccount_SignsOutEverySession_AndRemovesThePersonAndTheirLog()
    {
        // No reset: the achievement evaluator caches the catalog, and a truncated catalog would fail the first unlock.
        var id = Guid.NewGuid();
        var email = $"gone-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        var client = _fixture.CreateAuthenticatedClient(id, email);
        var other = _fixture.CreateAuthenticatedClient(id, email);
        var logged = await client.PostAsJsonAsync("/api/BodyMeasurements", new { date = "2026-10-10", weightKg = 74.2 });
        logged.IsSuccessStatusCode.Should().BeTrue(await logged.Content.ReadAsStringAsync());

        var deleted = await client.DeleteAsync("/api/Auth/account");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await other.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "every session ends, not only the one that asked");
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.Users.AnyAsync(u => u.Id == id)).Should().BeFalse();
        (await db.BodyMeasurements.AnyAsync(m => m.UserId == id)).Should().BeFalse();
        (await db.UserSessions.AnyAsync(s => s.UserId == id)).Should().BeFalse();
    }
}
