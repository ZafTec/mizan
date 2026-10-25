using MailKit.Net.Smtp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly IRuntimeOptions<SmtpOptions> _settings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IRuntimeOptions<SmtpOptions> settings,
        IHostEnvironment environment,
        ILogger<SmtpEmailSender> logger)
    {
        _settings = settings;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendAsync(OutgoingEmail message, CancellationToken cancellationToken = default)
    {
        // Read per message: a host or password changed in the admin area applies to the next send.
        var options = await _settings.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            NotConfigured(message);
            return;
        }

        var from = MailboxAddress.Parse(options.FromAddress);
        from.Name = options.FromName;
        var localDomain = string.IsNullOrWhiteSpace(options.LocalDomain)
            ? from.Domain
            : options.LocalDomain.Trim();
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
            options.Host,
            options.Port,
            options.GetSocketOptions(),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            await client.AuthenticateAsync(options.Username, options.Password ?? string.Empty, cancellationToken);
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

    private void NotConfigured(OutgoingEmail message)
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException("SMTP is not configured; set Smtp:Host or the SMTP host in the admin area before sending email.");
        }

        // Deliberately Console and not the logger: Serilog also writes to
        // logs/mizan-*.log, and this body must not land on disk. Container
        // stdout is where a developer looks for the link anyway.
        Console.WriteLine(
            $"[dev-mail] SMTP not configured. Message follows.\n{message.Subject}\n{message.Text}");
    }
}
