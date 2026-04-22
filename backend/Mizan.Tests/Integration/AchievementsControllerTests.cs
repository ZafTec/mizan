using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Exercises the new server-side search/sort/pagination added to
/// GetAchievementsQuery and GetAchievementAnalyticsQuery.
/// </summary>
[Collection("ApiIntegration")]
public class AchievementsControllerTests
{
    private readonly ApiTestFixture _fixture;

    public AchievementsControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------- GET /api/Achievements : pagination / search / sort ----------

    [Fact]
    public async Task Get_PaginatesResults()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var userId = Guid.NewGuid();
        var email = $"ach-page-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);
        await SeedAchievementsAsync(30);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var page1 = await client.GetFromJsonAsync<AchievementListResponse>("/api/Achievements?page=1&pageSize=10");
        var page2 = await client.GetFromJsonAsync<AchievementListResponse>("/api/Achievements?page=2&pageSize=10");

        page1!.TotalCount.Should().Be(30);
        page1.Items.Should().HaveCount(10);
        page2!.Items.Should().HaveCount(10);
        page1.Items.Select(i => i.Id).Should().NotIntersectWith(page2.Items.Select(i => i.Id));
        page1.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Get_SearchesByName()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var userId = Guid.NewGuid();
        var email = $"ach-search-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);
        await SeedSpecificAchievementsAsync(
            ("Marathon Runner", "fitness", "workouts_logged", 50),
            ("Sprint Champion", "fitness", "workouts_logged", 10),
            ("Protein Boss", "nutrition", "meals_logged", 100));

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var result = await client.GetFromJsonAsync<AchievementListResponse>("/api/Achievements?searchTerm=sprint");

        result!.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Name.Should().Be("Sprint Champion");
    }

    [Fact]
    public async Task Get_FiltersByCategory()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var userId = Guid.NewGuid();
        var email = $"ach-cat-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);
        await SeedSpecificAchievementsAsync(
            ("A", "fitness", "workouts_logged", 10),
            ("B", "fitness", "workouts_logged", 20),
            ("C", "nutrition", "meals_logged", 10));

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var result = await client.GetFromJsonAsync<AchievementListResponse>("/api/Achievements?category=fitness");

        result!.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(i => i.Category == "fitness");
    }

    [Fact]
    public async Task Get_SortsByNameAscendingAndDescending()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var userId = Guid.NewGuid();
        var email = $"ach-sort-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);
        await SeedSpecificAchievementsAsync(
            ("Charlie", "fitness", "workouts_logged", 10),
            ("Alpha", "fitness", "workouts_logged", 10),
            ("Bravo", "fitness", "workouts_logged", 10));

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var asc = await client.GetFromJsonAsync<AchievementListResponse>("/api/Achievements?sortBy=name&sortOrder=asc");
        var desc = await client.GetFromJsonAsync<AchievementListResponse>("/api/Achievements?sortBy=name&sortOrder=desc");

        asc!.Items.Select(i => i.Name).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
        desc!.Items.Select(i => i.Name).Should().ContainInOrder("Charlie", "Bravo", "Alpha");
    }

    [Fact]
    public async Task Get_SortsByPointsDescending()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var userId = Guid.NewGuid();
        var email = $"ach-points-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);
        await SeedSpecificAchievementsAsync(
            ("Low", "fitness", "workouts_logged", 10, points: 5),
            ("High", "fitness", "workouts_logged", 10, points: 500),
            ("Mid", "fitness", "workouts_logged", 10, points: 50));

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var result = await client.GetFromJsonAsync<AchievementListResponse>("/api/Achievements?sortBy=points&sortOrder=desc");

        result!.Items.Select(i => i.Name).Should().ContainInOrder("High", "Mid", "Low");
    }

    // ---------- GET /api/Achievements/analytics : pagination / search / sort ----------

    [Fact]
    public async Task Analytics_RequiresAdmin()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var userId = Guid.NewGuid();
        var email = $"ach-nonadmin-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, role: "user");

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var response = await client.GetAsync("/api/Achievements/analytics");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Analytics_PaginatesRowsWhileKeepingGlobalStats()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var adminId = Guid.NewGuid();
        var email = $"ach-admin-{adminId:N}@example.com";
        await _fixture.SeedUserAsync(adminId, email, role: "admin");
        await SeedAchievementsAsync(25);

        using var client = _fixture.CreateAuthenticatedClient(adminId, email, role: "admin");

        var result = await client.GetFromJsonAsync<AnalyticsResponse>("/api/Achievements/analytics?page=1&pageSize=10");

        result!.TotalAchievements.Should().Be(25);
        result.RowsTotalCount.Should().Be(25);
        result.Rows.Should().HaveCount(10);
        result.TotalPages.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Analytics_SearchesFilterRows()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var adminId = Guid.NewGuid();
        var email = $"ach-admin-s-{adminId:N}@example.com";
        await _fixture.SeedUserAsync(adminId, email, role: "admin");
        await SeedSpecificAchievementsAsync(
            ("Marathon Runner", "fitness", "workouts_logged", 50),
            ("Sprint Champion", "fitness", "workouts_logged", 10),
            ("Protein Boss", "nutrition", "meals_logged", 100));

        using var client = _fixture.CreateAuthenticatedClient(adminId, email, role: "admin");

        var result = await client.GetFromJsonAsync<AnalyticsResponse>("/api/Achievements/analytics?searchTerm=protein");

        result!.RowsTotalCount.Should().Be(1);
        result.Rows.Should().ContainSingle().Which.Name.Should().Be("Protein Boss");
        // Global counts unchanged by search.
        result.TotalAchievements.Should().Be(3);
    }

    [Fact]
    public async Task Analytics_SortsByName()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var adminId = Guid.NewGuid();
        var email = $"ach-admin-sort-{adminId:N}@example.com";
        await _fixture.SeedUserAsync(adminId, email, role: "admin");
        await SeedSpecificAchievementsAsync(
            ("Zeta", "fitness", "workouts_logged", 10),
            ("Alpha", "fitness", "workouts_logged", 10),
            ("Delta", "fitness", "workouts_logged", 10));

        using var client = _fixture.CreateAuthenticatedClient(adminId, email, role: "admin");

        var result = await client.GetFromJsonAsync<AnalyticsResponse>("/api/Achievements/analytics?sortBy=name&sortOrder=asc");

        result!.Rows.Select(r => r.Name).Should().ContainInOrder("Alpha", "Delta", "Zeta");
    }

    // ---------- authorization: write endpoints must be admin-only ----------

    [Fact]
    public async Task Create_ReturnsForbidden_ForNonAdmin()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var userId = Guid.NewGuid();
        var email = $"ach-noadmin-create-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, role: "user");

        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        var response = await client.PostAsJsonAsync("/api/Achievements", new
        {
            name = "Bad Actor Badge",
            points = 10,
            threshold = 1,
            criteriaType = "meals_logged",
            category = "nutrition"
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ReturnsForbidden_ForNonAdmin()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var userId = Guid.NewGuid();
        var email = $"ach-noadmin-upd-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, role: "user");
        var achId = await SeedOneAchievementAsync("Stable", "nutrition", "meals_logged", 10);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        var response = await client.PutAsJsonAsync($"/api/Achievements/{achId}", new
        {
            id = achId,
            name = "Hijacked",
            points = 10_000,
            threshold = 0,
            criteriaType = (string?)null,
            category = (string?)null
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_ReturnsForbidden_ForNonAdmin()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var userId = Guid.NewGuid();
        var email = $"ach-noadmin-del-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, role: "user");
        var achId = await SeedOneAchievementAsync("Keep Me", "nutrition", "meals_logged", 10);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        var response = await client.DeleteAsync($"/api/Achievements/{achId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_CanCreate_Update_AndDelete()
    {
        await _fixture.ResetDatabaseAsync();
        await ResetAchievementsAsync();

        var adminId = Guid.NewGuid();
        var email = $"ach-admin-crud-{adminId:N}@example.com";
        await _fixture.SeedUserAsync(adminId, email, role: "admin");

        using var client = _fixture.CreateAuthenticatedClient(adminId, email, role: "admin");

        var create = await client.PostAsJsonAsync("/api/Achievements", new
        {
            name = "CRUD Badge",
            description = "round trip",
            points = 25,
            threshold = 5,
            criteriaType = "meals_logged",
            category = "nutrition"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<CreateResponse>();
        created!.Id.Should().NotBeEmpty();

        var getOne = await client.GetAsync($"/api/Achievements/{created.Id}");
        getOne.StatusCode.Should().Be(HttpStatusCode.OK);

        var update = await client.PutAsJsonAsync($"/api/Achievements/{created.Id}", new
        {
            id = created.Id,
            name = "CRUD Badge v2",
            description = "updated",
            points = 30,
            threshold = 6,
            criteriaType = "meals_logged",
            category = "nutrition"
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var delete = await client.DeleteAsync($"/api/Achievements/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfterDelete = await client.GetAsync($"/api/Achievements/{created.Id}");
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StreakPost_NoLongerExists()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"ach-streakpost-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        var response = await client.PostAsJsonAsync("/api/Achievements/streak", new { });

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    private record CreateResponse(Guid Id);

    private async Task<Guid> SeedOneAchievementAsync(string name, string category, string criteria, int threshold)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var ach = new Achievement
        {
            Id = Guid.NewGuid(),
            Name = name,
            Category = category,
            CriteriaType = criteria,
            Threshold = threshold,
            Points = 10
        };
        db.Achievements.Add(ach);
        await db.SaveChangesAsync();
        return ach.Id;
    }

    // ---------- helpers ----------

    // ApiTestFixture.ResetDatabaseAsync does NOT truncate achievements/user_achievements
    // because the migration seeds 20 baseline achievements (referenced by the evaluator).
    // These tests need a clean slate to assert exact counts, so we clear both tables
    // explicitly. Order matters: user_achievements has an FK to achievements.
    private async Task ResetAchievementsAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        if (db.Database.IsInMemory())
        {
            db.UserAchievements.RemoveRange(db.UserAchievements);
            db.Achievements.RemoveRange(db.Achievements);
            await db.SaveChangesAsync();
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE \"user_achievements\", \"achievements\" RESTART IDENTITY CASCADE;");
        }
    }

    private async Task SeedAchievementsAsync(int count)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var items = Enumerable.Range(1, count).Select(i => new Achievement
        {
            Id = Guid.NewGuid(),
            Name = $"Achievement {i:D2}",
            Description = $"Desc {i}",
            Category = i % 2 == 0 ? "fitness" : "nutrition",
            CriteriaType = i % 2 == 0 ? "workouts_logged" : "meals_logged",
            Threshold = i,
            Points = i * 5
        });

        db.Achievements.AddRange(items);
        await db.SaveChangesAsync();
    }

    private async Task SeedSpecificAchievementsAsync(params (string Name, string Category, string Criteria, int Threshold, int Points)[] specs)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        foreach (var (name, category, criteria, threshold, points) in specs)
        {
            db.Achievements.Add(new Achievement
            {
                Id = Guid.NewGuid(),
                Name = name,
                Category = category,
                CriteriaType = criteria,
                Threshold = threshold,
                Points = points
            });
        }

        await db.SaveChangesAsync();
    }

    private Task SeedSpecificAchievementsAsync(params (string Name, string Category, string Criteria, int Threshold)[] specs)
        => SeedSpecificAchievementsAsync(specs.Select(s => (s.Name, s.Category, s.Criteria, s.Threshold, 10)).ToArray());

    private record AchievementListResponse
    {
        public List<AchievementItem> Items { get; init; } = new();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalPages { get; init; }
    }

    private record AchievementItem
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Category { get; init; }
        public int Points { get; init; }
    }

    private record AnalyticsResponse
    {
        public int TotalAchievements { get; init; }
        public int RowsTotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalPages { get; init; }
        public List<AnalyticsRow> Rows { get; init; } = new();
    }

    private record AnalyticsRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Category { get; init; }
        public int UnlockedBy { get; init; }
        public int Points { get; init; }
    }
}
