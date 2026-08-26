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

    public async Task<Guid> EnqueueAsync<T>(
        string type,
        T payload,
        string? dedupeKey = null,
        CancellationToken cancellationToken = default)
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
            RunAfter = DateTime.UtcNow,
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
