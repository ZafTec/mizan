using MailKit.Net.Smtp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Email;

/// <summary>
/// Email moved to the backend with identity in v2.
///
/// Nothing here writes a message body anywhere durable. A verification or
/// reset link is a credential: in the application log it outlives the token,
/// travels to wherever logs are shipped, and is readable by anyone with log
/// access. With no SMTP host configured, a development run prints the body to
/// stdout and every other environment fails the send so the outbox retains it.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<SmtpOptions> options,
        IHostEnvironment environment,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            NotConfigured(message);
            return;
        }

        var from = MailboxAddress.Parse(_options.FromAddress);
        from.Name = _options.FromName;
        var localDomain = string.IsNullOrWhiteSpace(_options.LocalDomain)
            ? from.Domain
            : _options.LocalDomain.Trim();
        if (string.IsNullOrWhiteSpace(localDomain))
        {
            throw new InvalidOperationException("The SMTP sender address must include a domain.");
        }

        var mime = new MimeMessage();
        mime.From.Add(from);
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.Html, TextBody = message.Text }.ToMessageBody();

        using var client = new SmtpClient { LocalDomain = localDomain };

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.GetSocketOptions(),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        _logger.LogInformation("Email accepted by the SMTP server");

        // SendAsync returns after the server accepts DATA. A failed QUIT must
        // not turn accepted delivery into an outbox retry and a duplicate email.
        try
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogDebug("SMTP disconnect failed after acceptance ({ErrorType})", exception.GetType().Name);
        }
    }

    private void NotConfigured(EmailMessage message)
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException("SMTP is not configured; set Smtp:Host before sending email.");
        }

        // Deliberately Console and not the logger: Serilog also writes to
        // logs/mizan-*.log, and this body must not land on disk. Container
        // stdout is where a developer looks for the link anyway.
        Console.WriteLine(
            $"[dev-mail] SMTP not configured. Message follows.\n{message.Subject}\n{message.Text}");
    }
}
