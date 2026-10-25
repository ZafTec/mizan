using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Outbox;

/// <summary>
/// Outbound email, with retries, and the email log kept true.
///
/// What this replaces: a call inside a try/catch that logged the failure and
/// carried on. A password reset that never arrived left no trace anyone would
/// look at, and there was nothing to retry it. Five attempts over about twenty
/// minutes covers a provider blip; after that it dead-letters, which is
/// visible.
///
/// Each attempt updates the message's row in the email log on the job's own
/// context, so the row and the job are saved together: Sent when the server
/// accepted it, an attempt count and the redacted error while it retries, and
/// Failed when the dispatcher gives up.
/// </summary>
public class EmailJobHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IEmailSender _sender;
    private readonly IMizanDbContext _context;

    public EmailJobHandler(IEmailSender sender, IMizanDbContext context)
    {
        _sender = sender;
        _context = context;
    }

    public string Type => OutboxJobTypes.Email;

    /// <summary>
    /// Small and frequent. Four at a time keeps a burst of verification mails
    /// from queueing behind each other without opening a connection per user.
    /// </summary>
    public int Concurrency => 4;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var job = Read(payload);
        var log = await LogRowAsync(job, cancellationToken);

        if (string.IsNullOrWhiteSpace(job.To))
        {
            // No amount of retrying supplies a recipient.
            throw new OutboxPermanentException("The queued email has no recipient.");
        }

        if (log is not null) log.Attempts++;

        try
        {
            await _sender.SendAsync(job.ToMessage(), cancellationToken);
        }
        catch (Exception ex) when (log is not null)
        {
            log.LastError = OutboxError.Redact(ex.Message);
            throw;
        }

        if (log is not null)
        {
            log.Status = EmailMessageStatus.Sent;
            log.SentAt = DateTime.UtcNow;
            log.LastError = null;
        }
    }

    public async Task OnDeadLetteredAsync(string payload, string? error, CancellationToken cancellationToken)
    {
        EmailJobPayload job;
        try
        {
            job = Read(payload);
        }
        catch (OutboxPermanentException)
        {
            return;
        }

        var log = await LogRowAsync(job, cancellationToken);
        if (log is null) return;

        log.Status = EmailMessageStatus.Failed;
        log.LastError = OutboxError.Redact(error) ?? log.LastError;
    }

    private async Task<EmailMessage?> LogRowAsync(EmailJobPayload job, CancellationToken cancellationToken) =>
        job.EmailMessageId is { } id
            ? await _context.EmailMessages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            : null;

    /// <summary>
    /// A payload that will not parse now will not parse on the fifth attempt
    /// either, so this is permanent rather than transient.
    /// </summary>
    private static EmailJobPayload Read(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<EmailJobPayload>(payload, Json)
                ?? throw new OutboxPermanentException("The queued email was empty.");
        }
        catch (JsonException ex)
        {
            throw new OutboxPermanentException("The queued email could not be read.", ex);
        }
    }
}
