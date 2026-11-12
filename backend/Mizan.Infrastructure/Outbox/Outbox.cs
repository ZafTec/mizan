using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Outbox;

public class Outbox : IOutbox
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMizanDbContext _context;

    public Outbox(IMizanDbContext context) => _context = context;

    public Task<Guid> EnqueueAsync<T>(
        string type,
        T payload,
        string? dedupeKey = null,
        CancellationToken cancellationToken = default) =>
        StageAsync(type, payload, DateTime.UtcNow, dedupeKey, cancellationToken);

    public Task<Guid> ScheduleAsync<T>(
        string type,
        T payload,
        DateTime runAfter,
        string dedupeKey,
        CancellationToken cancellationToken = default) =>
        StageAsync(type, payload, DateTime.SpecifyKind(runAfter, DateTimeKind.Utc), dedupeKey, cancellationToken);

    public async Task<int> ScheduleAllAsync<T>(
        string type,
        IEnumerable<ScheduledJob<T>> jobs,
        CancellationToken cancellationToken = default)
    {
        var batch = jobs.ToList();
        if (batch.Count == 0) return 0;

        var keys = batch.Select(j => j.DedupeKey).Distinct().ToList();
        var taken = (await _context.OutboxJobs.AsNoTracking()
            .Where(j => j.DedupeKey != null && keys.Contains(j.DedupeKey))
            .Select(j => j.DedupeKey!)
            .ToListAsync(cancellationToken)).ToHashSet();

        var now = DateTime.UtcNow;
        var staged = 0;
        foreach (var job in batch)
        {
            if (!taken.Add(job.DedupeKey)) continue;
            _context.OutboxJobs.Add(new OutboxJob
            {
                Id = Guid.CreateVersion7(),
                Type = type,
                Payload = JsonSerializer.Serialize(job.Payload, Json),
                Status = OutboxJobStatus.Pending,
                RunAfter = DateTime.SpecifyKind(job.RunAfter, DateTimeKind.Utc),
                DedupeKey = job.DedupeKey,
                CreatedAt = now,
            });
            staged++;
        }
        return staged;
    }

    private async Task<Guid> StageAsync<T>(
        string type,
        T payload,
        DateTime runAfter,
        string? dedupeKey,
        CancellationToken cancellationToken)
    {
        if (dedupeKey is not null)
        {
            var existing = await _context.OutboxJobs.AsNoTracking()
                .Where(j => j.DedupeKey == dedupeKey)
                .Select(j => j.Id)
                .FirstOrDefaultAsync(cancellationToken);

            // Checked here as well as enforced by the index: losing a race is
            // fine and rare, but doing a round trip to the error handler for
            // the common case is not.
            if (existing != Guid.Empty) return existing;
        }

        var job = new OutboxJob
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Payload = JsonSerializer.Serialize(payload, Json),
            Status = OutboxJobStatus.Pending,
            RunAfter = runAfter,
            DedupeKey = dedupeKey,
            CreatedAt = DateTime.UtcNow,
        };

        // Staged, not saved. The caller's SaveChangesAsync commits this with
        // the rest of its unit of work - that is what makes the outbox
        // transactional rather than merely asynchronous.
        _context.OutboxJobs.Add(job);

        return job.Id;
    }
}
