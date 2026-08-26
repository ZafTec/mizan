using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Admin;
using Mizan.Application.Common;
using Mizan.Application.Queries;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The three admin surfaces phase 3 deleted, rebuilt. Each one is only useful
/// if it can be narrowed, so the filters are what these test.
/// </summary>
[Collection("ApiIntegration")]
public class AdminSurfacesTests
{
    private readonly ApiTestFixture _fixture;

    public AdminSurfacesTests(ApiTestFixture fixture) => _fixture = fixture;

    // ---- Audit log --------------------------------------------------------

    [Fact]
    public async Task TheAuditLogFiltersByDateRangeInclusively()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedAuditAsync("create", "Food", today.AddDays(-5));
        await SeedAuditAsync("update", "Food", today);

        var page = await client.GetFromJsonAsync<PagedResult<AuditLogDto>>(
            $"/api/AuditLogs?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");

        page!.Items.Should().ContainSingle("the bound day itself must be included");
        page.Items[0].Action.Should().Be("update");
    }

    [Fact]
    public async Task TheAuditLogFiltersByActionAndEntity()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedAuditAsync("create", "Food", today);
        await SeedAuditAsync("delete", "Recipe", today);

        var byAction = await client.GetFromJsonAsync<PagedResult<AuditLogDto>>("/api/AuditLogs?action=delete");
        byAction!.Items.Should().OnlyContain(l => l.Action == "delete");

        var byEntity = await client.GetFromJsonAsync<PagedResult<AuditLogDto>>("/api/AuditLogs?entityType=Food");
        byEntity!.Items.Should().OnlyContain(l => l.EntityType == "Food");
    }

    [Fact]
    public async Task TheFacetsListWhatIsActuallyInTheLog()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedAuditAsync("publish", "AiPromptVersion", today);

        var facets = await client.GetFromJsonAsync<AuditLogFacetsDto>("/api/AuditLogs/facets");

        facets!.Actions.Should().Contain("publish");
        facets.EntityTypes.Should().Contain("AiPromptVersion");
    }

    [Fact]
    public async Task TheExportIsCsvAndQuotesEveryField()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        await SeedAuditAsync("create", "Food", DateOnly.FromDateTime(DateTime.UtcNow));

        var response = await client.GetAsync("/api/AuditLogs/export");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().StartWith("timestamp,actor,action,entityType,entityId,ipAddress,details");
        csv.Should().Contain("\"create\"");
    }

    /// <summary>
    /// A details field beginning with = is a formula as far as a spreadsheet is
    /// concerned, and an audit log is exactly where someone would plant one.
    /// </summary>
    [Fact]
    public async Task TheExportNeutralisesSpreadsheetFormulas()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        await SeedAuditAsync("create", "Food", DateOnly.FromDateTime(DateTime.UtcNow),
            details: "=HYPERLINK(\"http://evil\",\"click\")");

        var csv = await (await client.GetAsync("/api/AuditLogs/export")).Content.ReadAsStringAsync();

        csv.Should().Contain("\"\\t=HYPERLINK".Replace("\\t", "\t"));
        csv.Should().NotContain(",=HYPERLINK");
    }

    [Fact]
    public async Task AnOrdinaryUserCannotReadTheAuditLog()
    {
        await _fixture.ResetDatabaseAsync();
        var id = Guid.NewGuid();
        await _fixture.SeedUserAsync(id, $"plain-{id:N}@example.com");
        using var client = _fixture.CreateAuthenticatedClient(id, $"plain-{id:N}@example.com");

        (await client.GetAsync("/api/AuditLogs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/AuditLogs/export")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Relationships ----------------------------------------------------

    [Fact]
    public async Task RelationshipsCarryTheGrantsTheClientGave()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        await SeedRelationshipAsync(nutrition: true, workouts: false, measurements: false);

        var page = await client.GetFromJsonAsync<PagedResult<AdminRelationshipDto>>(
            "/api/Admin/Relationships");

        var row = page!.Items.Should().ContainSingle().Subject;
        row.CanViewNutrition.Should().BeTrue();
        row.CanViewWorkouts.Should().BeFalse();
        row.CanViewMeasurements.Should().BeFalse();
    }

    [Fact]
    public async Task RelationshipsAreSearchableFromEitherSide()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var (trainerEmail, clientEmail) = await SeedRelationshipAsync(true, true, true);

        foreach (var term in new[] { trainerEmail, clientEmail })
        {
            var page = await client.GetFromJsonAsync<PagedResult<AdminRelationshipDto>>(
                $"/api/Admin/Relationships?search={Uri.EscapeDataString(term)}");
            page!.Items.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task EndingARelationshipRevokesAccessButKeepsTheGrants()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        await SeedRelationshipAsync(nutrition: true, workouts: true, measurements: true);

        var listed = await client.GetFromJsonAsync<PagedResult<AdminRelationshipDto>>(
            "/api/Admin/Relationships");
        var id = listed!.Items[0].Id;

        var response = await client.PostAsJsonAsync(
            $"/api/Admin/Relationships/{id}/end", new { reason = "client asked" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await RelationshipAsync(id);
        after.Status.Should().Be("ended");
        after.EndedAt.Should().NotBeNull();

        // The client's choices are theirs; re-accepting should restore them
        // rather than starting from nothing.
        after.CanViewNutrition.Should().BeTrue();
    }

    [Fact]
    public async Task ThereIsNoWayToEditGrantsFromTheAdminApi()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        await SeedRelationshipAsync(true, false, false);
        var listed = await client.GetFromJsonAsync<PagedResult<AdminRelationshipDto>>(
            "/api/Admin/Relationships");
        var id = listed!.Items[0].Id;

        // Admin is operational access, not super-user access over what a client
        // shares (docs/REFOCUS.md §11). No route exists, and that is the point.
        var response = await client.PutAsJsonAsync(
            $"/api/Admin/Relationships/{id}", new { canViewMeasurements = true });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await RelationshipAsync(id)).CanViewMeasurements.Should().BeFalse();
    }

    [Fact]
    public async Task AnOrdinaryUserCannotListRelationships()
    {
        await _fixture.ResetDatabaseAsync();
        var id = Guid.NewGuid();
        await _fixture.SeedUserAsync(id, $"plain-{id:N}@example.com");
        using var client = _fixture.CreateAuthenticatedClient(id, $"plain-{id:N}@example.com");

        (await client.GetAsync("/api/Admin/Relationships")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task<HttpClient> AdminAsync()
    {
        var id = Guid.NewGuid();
        var email = $"admin-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email, role: "admin");
        return _fixture.CreateAuthenticatedClient(id, email, "admin");
    }

    private async Task SeedAuditAsync(string action, string entityType, DateOnly on, string? details = null)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            Action = action,
            EntityType = entityType,
            EntityId = Guid.NewGuid().ToString(),
            Details = details,
            IpAddress = "203.0.113.7",
            Timestamp = on.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();
    }

    private async Task<(string Trainer, string Client)> SeedRelationshipAsync(
        bool nutrition, bool workouts, bool measurements)
    {
        var trainerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var trainerEmail = $"coach-{trainerId:N}@example.com";
        var clientEmail = $"client-{clientId:N}@example.com";

        await _fixture.SeedUserAsync(trainerId, trainerEmail, role: "trainer");
        await _fixture.SeedUserAsync(clientId, clientEmail);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.TrainerClientRelationships.Add(new TrainerClientRelationship
        {
            Id = Guid.CreateVersion7(),
            TrainerId = trainerId,
            ClientId = clientId,
            Status = "active",
            CanViewNutrition = nutrition,
            CanViewWorkouts = workouts,
            CanViewMeasurements = measurements,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return (trainerEmail, clientEmail);
    }

    private async Task<TrainerClientRelationship> RelationshipAsync(Guid id)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.TrainerClientRelationships.AsNoTracking().FirstAsync(r => r.Id == id);
    }
}
