using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The state behind long-running MCP calls. A task is private to the user who
/// started it, ends once, expires, and a task whose worker vanished does not
/// stay "working" forever.
/// </summary>
[Collection("ApiIntegration")]
public class McpTasksTests
{
    private readonly ApiTestFixture _fixture;

    public McpTasksTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ANewTask_IsWorking_AndCanBeRead()
    {
        var user = await UserAsync();
        using var service = Service(user);

        var created = await service.PostAsync("/api/McpTasks", null);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var task = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = task.GetProperty("id").GetString()!;
        id.Should().MatchRegex("^[0-9a-f]{32}$");
        task.GetProperty("status").GetString().Should().Be("working");

        var read = await service.GetAsync($"/api/McpTasks/{id}");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("working");
    }

    [Fact]
    public async Task ACompletedTask_KeepsItsResult_AndIsFinishedOnlyOnce()
    {
        var user = await UserAsync();
        using var service = Service(user);
        var id = await CreateAsync(service);

        (await Post(service, $"/api/McpTasks/{id}/complete", """{"content":[{"type":"text","text":"done"}]}"""))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var read = await (await service.GetAsync($"/api/McpTasks/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        read.GetProperty("status").GetString().Should().Be("completed");
        read.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString().Should().Be("done");

        (await Post(service, $"/api/McpTasks/{id}/fail", """{"code":-32603,"message":"late"}"""))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "a finished task stays as it ended");
        (await Post(service, $"/api/McpTasks/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await (await service.GetAsync($"/api/McpTasks/{id}")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString().Should().Be("completed");
    }

    [Fact]
    public async Task AFailedOrCancelledTask_SaysSo()
    {
        var user = await UserAsync();
        using var service = Service(user);

        var failed = await CreateAsync(service);
        await Post(service, $"/api/McpTasks/{failed}/fail", """{"code":-32603,"message":"provider down"}""");
        var failedRead = await (await service.GetAsync($"/api/McpTasks/{failed}")).Content.ReadFromJsonAsync<JsonElement>();
        failedRead.GetProperty("status").GetString().Should().Be("failed");
        failedRead.GetProperty("error").GetProperty("message").GetString().Should().Be("provider down");

        var cancelled = await CreateAsync(service);
        (await Post(service, $"/api/McpTasks/{cancelled}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await service.GetAsync($"/api/McpTasks/{cancelled}")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString().Should().Be("cancelled");
    }

    [Fact]
    public async Task ATask_IsPrivateToItsOwner()
    {
        var owner = await UserAsync();
        var other = await UserAsync(reset: false);
        using var ownerClient = Service(owner);
        using var otherClient = Service(other);
        var id = await CreateAsync(ownerClient);

        (await otherClient.GetAsync($"/api/McpTasks/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Post(otherClient, $"/api/McpTasks/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "someone else cannot end it, and cannot tell whether it exists");
        (await Post(otherClient, $"/api/McpTasks/{id}/complete", """{"content":[]}""")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await (await ownerClient.GetAsync($"/api/McpTasks/{id}")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString().Should().Be("working");
    }

    [Fact]
    public async Task ATaskPastItsTimeToLive_IsGone()
    {
        var user = await UserAsync();
        using var service = Service(user);
        var id = await CreateAsync(service);
        await ChangeAsync(id, t => t.CreatedAt = DateTime.UtcNow.AddSeconds(-t.TtlSeconds - 5));

        (await service.GetAsync($"/api/McpTasks/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AWorkingTaskNobodyTouched_IsReportedAsInterrupted()
    {
        var user = await UserAsync();
        using var service = Service(user);
        var id = await CreateAsync(service);
        await ChangeAsync(id, t => t.UpdatedAt = DateTime.UtcNow.AddMinutes(-20));

        var read = await (await service.GetAsync($"/api/McpTasks/{id}")).Content.ReadFromJsonAsync<JsonElement>();

        read.GetProperty("status").GetString().Should().Be("failed");
        read.GetProperty("error").GetProperty("message").GetString().Should().Contain("interrupted");
        (await Post(service, $"/api/McpTasks/{id}/complete", """{"content":[]}""")).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a worker that comes back late cannot overwrite the failure the client already saw");
    }

    [Fact]
    public async Task OnlyTheMcpService_CanUseTheTaskEndpoints()
    {
        await UserAsync();
        using var anonymous = _fixture.CreateClient();

        (await anonymous.PostAsync("/api/McpTasks", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TooManyOpenTasks_AreRefused_UntilOneEnds()
    {
        var user = await UserAsync();
        using var service = Service(user);
        var first = await CreateAsync(service);
        for (var i = 1; i < 20; i++) await CreateAsync(service);

        var over = await service.PostAsync("/api/McpTasks", null);
        over.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await Post(service, $"/api/McpTasks/{first}/cancel", null);
        (await service.PostAsync("/api/McpTasks", null)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ---- helpers ----

    private async Task<Guid> UserAsync(bool reset = true)
    {
        if (reset) await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"task-{user:N}@example.com");
        return user;
    }

    private HttpClient Service(Guid user)
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        client.DefaultRequestHeaders.Add("X-Impersonate-User", user.ToString());
        return client;
    }

    private static async Task<string> CreateAsync(HttpClient service) =>
        (await (await service.PostAsync("/api/McpTasks", null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

    private static Task<HttpResponseMessage> Post(HttpClient client, string url, string? json) =>
        client.PostAsync(url, json is null ? null : new StringContent(json, Encoding.UTF8, "application/json"));

    private async Task ChangeAsync(string id, Action<McpTask> change)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var task = await db.McpTasks.FirstAsync(t => t.Id == id);
        change(task);
        await db.SaveChangesAsync();
    }
}
