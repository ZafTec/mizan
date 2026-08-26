using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Admin;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The queue, and the two properties it exists for: work survives the request
/// that asked for it, and work that fails is visible rather than logged and
/// forgotten.
/// </summary>
[Collection("ApiIntegration")]
public class OutboxTests
{
    private readonly ApiTestFixture _fixture;

    public OutboxTests(ApiTestFixture fixture) => _fixture = fixture;

    // ---- Transactionality -------------------------------------------------

    [Fact]
    public async Task EnqueueingStagesTheJobAndTheCallersSaveCommitsIt()
    {
        await _fixture.ResetDatabaseAsync();

        using var scope = _fixture.Services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        await outbox.EnqueueAsync(OutboxJobTypes.Email, new EmailMessage("queued@example.com", "Staged", "<p>hi</p>", "hi"));

        // Nothing yet: the point of an outbox is that the job lands with the
        // rest of the caller's unit of work, not before it.
        (await CountAsync()).Should().Be(0);

        await db.SaveChangesAsync();

        (await CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ARolledBackUnitOfWorkQueuesNothing()
    {
        await _fixture.ResetDatabaseAsync();

        using (var scope = _fixture.Services.CreateScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            await outbox.EnqueueAsync(OutboxJobTypes.Email, new EmailMessage("never@example.com", "Discarded", "<p>hi</p>", "hi"));

            // Scope disposed without saving - the same shape as a handler that
            // threw after staging the job.
        }

        (await CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TheSameDedupeKeyQueuesOneJob()
    {
        await _fixture.ResetDatabaseAsync();

        using var scope = _fixture.Services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var first = await outbox.EnqueueAsync(
            OutboxJobTypes.Email, Message("dedupe@example.com"), "same-key");
        await db.SaveChangesAsync();

        var second = await outbox.EnqueueAsync(
            OutboxJobTypes.Email, Message("dedupe@example.com"), "same-key");
        await db.SaveChangesAsync();

        second.Should().Be(first);
        (await CountAsync()).Should().Be(1);
    }

    // ---- Dispatch ---------------------------------------------------------

    [Fact]
    public async Task RegisteringQueuesTheVerificationMailAndDrainingSendsIt()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Email.Clear();

        using var client = _fixture.CreateClient();
        var email = $"outbox-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/Auth/register",
            new { Email = email, Password = "a-long-enough-password", Name = "Queued Person" });
        register.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // The request returns before the mail is sent. That is the change.
        _fixture.Email.Sent.Should().BeEmpty();
        (await CountAsync(OutboxJobStatus.Pending)).Should().Be(1);

        await _fixture.DrainOutboxAsync();

        _fixture.Email.Sent.Should().ContainSingle();
        (await CountAsync(OutboxJobStatus.Succeeded)).Should().Be(1);
    }

    [Fact]
    public async Task AJobThatCannotSucceedIsDeadLetteredWithoutBurningItsAttempts()
    {
        await _fixture.ResetDatabaseAsync();

        // No recipient: the handler raises OutboxPermanentException rather than
        // spending five attempts proving a missing address stays missing.
        await QueueAsync(OutboxJobTypes.Email, new EmailMessage("", "Nowhere", "<p>hi</p>", "hi"));

        await _fixture.DrainOutboxAsync();

        var job = await SingleJobAsync();
        job.Status.Should().Be(OutboxJobStatus.DeadLettered);
        job.Attempts.Should().Be(1);
        job.LastError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AJobWithAnUnreadablePayloadDeadLettersRatherThanLooping()
    {
        await _fixture.ResetDatabaseAsync();

        // Valid JSON, wrong shape - what a payload written by an older version
        // of the code looks like. It will not parse on the fifth attempt
        // either, so it must not spend five.
        await QueueRawAsync(OutboxJobTypes.Email, """{"unexpected": [1, 2, 3]}""");

        await _fixture.DrainOutboxAsync();

        var job = await SingleJobAsync();
        job.Status.Should().Be(OutboxJobStatus.DeadLettered);
        job.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task AStuckJobIsReturnedToTheQueue()
    {
        await _fixture.ResetDatabaseAsync();
        var id = await QueueAsync(OutboxJobTypes.Email, Message("stuck@example.com"));

        // The shape a worker that died mid-job leaves behind.
        await UpdateAsync(id, j =>
        {
            j.Status = OutboxJobStatus.Running;
            j.StartedAt = DateTime.UtcNow.AddHours(-2);
        });

        await _fixture.DrainOutboxAsync();

        (await SingleJobAsync()).Status.Should().Be(OutboxJobStatus.Succeeded);
    }

    // ---- Admin surface ----------------------------------------------------

    [Fact]
    public async Task TheAdminViewFiltersByStatusAndNeverShowsAnAddress()
    {
        await _fixture.ResetDatabaseAsync();
        using var admin = await AdminAsync();

        var id = await QueueAsync(OutboxJobTypes.Email, Message("visible@example.com"));
        await UpdateAsync(id, j =>
        {
            j.Status = OutboxJobStatus.DeadLettered;
            j.LastError = "550 5.1.1 <visible@example.com>: recipient rejected";
        });
        await QueueAsync(OutboxJobTypes.Email, Message("pending@example.com"));

        var dead = await admin.GetFromJsonAsync<PagedResult<AdminJobDto>>(
            "/api/Admin/Jobs?status=DeadLettered");

        dead!.Items.Should().ContainSingle();
        dead.Items[0].LastError.Should().NotContain("visible@example.com");
        dead.Items[0].LastError.Should().Contain("recipient rejected",
            "the diagnostic half of the message is the reason to show it at all");

        var stats = await admin.GetFromJsonAsync<AdminJobStats>("/api/Admin/Jobs/stats");
        stats!.DeadLettered.Should().Be(1);
        stats.Pending.Should().Be(1);
    }

    [Fact]
    public async Task RetryingADeadJobRunsItAgain()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Email.Clear();
        using var admin = await AdminAsync();

        var id = await QueueAsync(OutboxJobTypes.Email, Message("retry@example.com"));
        await UpdateAsync(id, j =>
        {
            j.Status = OutboxJobStatus.DeadLettered;
            j.Attempts = 5;
            j.LastError = "connection refused";
        });

        (await admin.PostAsync($"/api/Admin/Jobs/{id}/retry", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var requeued = await SingleJobAsync();
        requeued.Status.Should().Be(OutboxJobStatus.Pending);
        requeued.Attempts.Should().Be(0, "the operator has usually just fixed the cause");

        await _fixture.DrainOutboxAsync();

        _fixture.Email.Sent.Should().ContainSingle();
        (await SingleJobAsync()).Status.Should().Be(OutboxJobStatus.Succeeded);
    }

    [Fact]
    public async Task PendingWorkCannotBeDeleted()
    {
        await _fixture.ResetDatabaseAsync();
        using var admin = await AdminAsync();

        var id = await QueueAsync(OutboxJobTypes.Email, Message("waiting@example.com"));

        (await admin.DeleteAsync($"/api/Admin/Jobs/{id}")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        (await CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TheJobsViewIsAdminOnly()
    {
        await _fixture.ResetDatabaseAsync();

        var id = Guid.NewGuid();
        await _fixture.SeedUserAsync(id, $"plain-{id:N}@example.com");
        using var client = _fixture.CreateAuthenticatedClient(id, $"plain-{id:N}@example.com");

        (await client.GetAsync("/api/Admin/Jobs")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Helpers ----------------------------------------------------------

    private static EmailMessage Message(string to) => new(to, "Test", "<p>hi</p>", "hi");

    private async Task<HttpClient> AdminAsync()
    {
        var id = Guid.NewGuid();
        var email = $"jobs-admin-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email, role: "admin");
        return _fixture.CreateAuthenticatedClient(id, email, "admin");
    }

    private Task<Guid> QueueAsync<T>(string type, T payload) =>
        QueueRawAsync(type, JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private async Task<Guid> QueueRawAsync(string type, string payload)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var job = new OutboxJob
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Payload = payload,
            Status = OutboxJobStatus.Pending,
            RunAfter = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        db.OutboxJobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task UpdateAsync(Guid id, Action<OutboxJob> mutate)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var job = await db.OutboxJobs.SingleAsync(j => j.Id == id);
        mutate(job);
        await db.SaveChangesAsync();
    }

    private async Task<OutboxJob> SingleJobAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.OutboxJobs.AsNoTracking().SingleAsync();
    }

    private async Task<int> CountAsync(OutboxJobStatus? status = null)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var query = db.OutboxJobs.AsNoTracking();
        if (status is not null) query = query.Where(j => j.Status == status);
        return await query.CountAsync();
    }
}
