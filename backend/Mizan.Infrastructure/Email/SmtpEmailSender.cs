using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Email;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@mizan.local";
    public string FromName { get; set; } = "Mizan";
    public bool UseStartTls { get; set; } = true;
}

/// <summary>
/// Email moved to the backend with identity in v2.
///
/// Nothing here writes a message body anywhere durable. A verification or
/// reset link is a credential: in the application log it outlives the token,
/// travels to wherever logs are shipped, and is readable by anyone with log
/// access. With no SMTP host configured, a development run prints the body to
/// stdout and every other environment gets an error naming only the subject.
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

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.Html, TextBody = message.Text }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Sent {Subject}", message.Subject);
    }

    private void NotConfigured(EmailMessage message)
    {
        if (!_environment.IsDevelopment())
        {
            _logger.LogError("SMTP is not configured; {Subject} was not sent", message.Subject);
            return;
        }

        // Deliberately Console and not the logger: Serilog also writes to
        // logs/mizan-*.log, and this body must not land on disk. Container
        // stdout is where a developer looks for the link anyway.
        Console.WriteLine(
            $"[dev-mail] SMTP not configured. Message follows.\n{message.Subject}\n{message.Text}");
    }
}
