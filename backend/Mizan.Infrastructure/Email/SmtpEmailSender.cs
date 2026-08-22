using MailKit.Net.Smtp;
using MailKit.Security;
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
/// Email moved to the backend with identity in v2. When no SMTP host is
/// configured the message is logged instead of dropped - a developer running
/// locally still needs to see the verification link, and a silent no-op here
/// would look exactly like a delivery failure.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogWarning(
                "SMTP is not configured; email to {To} was not sent. Subject: {Subject}\n{Text}",
                message.To, message.Subject, message.Text);
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
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Sent {Subject} to {To}", message.Subject, message.To);
    }
}
