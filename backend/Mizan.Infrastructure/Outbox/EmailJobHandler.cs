using System.Text.Json;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Outbox;

/// <summary>
/// Outbound email, with retries.
///
/// What this replaces: a call inside a try/catch that logged the failure and
/// carried on. A password reset that never arrived left no trace anyone would
/// look at, and there was nothing to retry it. Five attempts over about twenty
/// minutes covers a provider blip; after that it dead-letters, which is
/// visible.
/// </summary>
public class EmailJobHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IEmailSender _sender;

    public EmailJobHandler(IEmailSender sender) => _sender = sender;

    public string Type => OutboxJobTypes.Email;

    /// <summary>
    /// Small and frequent. Four at a time keeps a burst of verification mails
    /// from queueing behind each other without opening a connection per user.
    /// </summary>
    public int Concurrency => 4;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var message = Read(payload);

        if (string.IsNullOrWhiteSpace(message.To))
        {
            // No amount of retrying supplies a recipient.
            throw new OutboxPermanentException("The queued email has no recipient.");
        }

        await _sender.SendAsync(message, cancellationToken);
    }

    /// <summary>
    /// A payload that will not parse now will not parse on the fifth attempt
    /// either, so this is permanent rather than transient.
    /// </summary>
    private static EmailMessage Read(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<EmailMessage>(payload, Json)
                ?? throw new OutboxPermanentException("The queued email was empty.");
        }
        catch (JsonException ex)
        {
            throw new OutboxPermanentException("The queued email could not be read.", ex);
        }
    }
}
