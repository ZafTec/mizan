using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Mail;

public sealed class EmailQueue : IEmailQueue
{
    private const int SubjectMax = 300;

    private readonly IMizanDbContext _context;
    private readonly IOutbox _outbox;

    public EmailQueue(IMizanDbContext context, IOutbox outbox)
    {
        _context = context;
        _outbox = outbox;
    }

    public async Task<Guid> QueueAsync(
        OutgoingEmail message,
        string kind,
        Guid? toUserId = null,
        string? dedupeKey = null,
        Guid? createdByUserId = null,
        CancellationToken cancellationToken = default,
        DateTime? notBefore = null)
    {
        var id = Guid.CreateVersion7();
        var payload = new EmailJobPayload(message.To, message.Subject, message.Html, message.Text, id);
        // A message held back (quiet hours) waits in the outbox until notBefore; it is logged as queued meanwhile.
        var jobId = notBefore is { } runAfter
            ? await _outbox.ScheduleAsync(OutboxJobTypes.Email, payload, runAfter, dedupeKey ?? $"email:{id}", cancellationToken)
            : await _outbox.EnqueueAsync(OutboxJobTypes.Email, payload, dedupeKey, cancellationToken);

        // The outbox hands back the first job when the key was seen before.
        // That job already has its log row, unless it predates the log.
        var staged = _context.OutboxJobs.Local.Any(j => j.Id == jobId);
        if (!staged)
        {
            var existing = _context.EmailMessages.Local.FirstOrDefault(m => m.OutboxJobId == jobId)?.Id
                ?? await _context.EmailMessages.AsNoTracking()
                    .Where(m => m.OutboxJobId == jobId)
                    .Select(m => (Guid?)m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            return existing ?? Guid.Empty;
        }

        _context.EmailMessages.Add(new EmailMessage
        {
            Id = id,
            ToUserId = toUserId,
            ToAddress = message.To.Trim(),
            Subject = message.Subject.Length > SubjectMax ? message.Subject[..SubjectMax] : message.Subject,
            Kind = kind,
            Status = EmailMessageStatus.Queued,
            OutboxJobId = jobId,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        });

        return id;
    }
}
