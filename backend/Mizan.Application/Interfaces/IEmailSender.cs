namespace Mizan.Application.Interfaces;

public record EmailMessage(string To, string Subject, string Html, string Text);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
