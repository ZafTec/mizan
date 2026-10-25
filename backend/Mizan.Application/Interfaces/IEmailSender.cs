namespace Mizan.Application.Interfaces;

/// <summary>An email ready to hand to the sender. The body lives here and in the outbox payload only.</summary>
public record OutgoingEmail(string To, string Subject, string Html, string Text);

public interface IEmailSender
{
    Task SendAsync(OutgoingEmail message, CancellationToken cancellationToken = default);
}

/// <summary>
/// What an email outbox job carries: the message, and the log row to update when
/// it is sent or gives up. Jobs queued before the email log existed have no
/// <see cref="EmailMessageId"/> and are delivered all the same.
/// </summary>
public record EmailJobPayload(string To, string Subject, string Html, string Text, Guid? EmailMessageId = null)
{
    public OutgoingEmail ToMessage() => new(To, Subject, Html, Text);
}
