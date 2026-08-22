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

    /// <summary>
    /// Where to drop messages when no SMTP host is configured. A developer
    /// still needs to open the verification link; the application log is the
    /// wrong place for it, because that link is a credential.
    /// </summary>
    public string PickupDirectory { get; set; } = "logs/mail";
}

/// <summary>
/// Email moved to the backend with identity in v2. With no SMTP host
/// configured the message is written to a pickup directory rather than sent -
/// a silent no-op would look exactly like a delivery failure, and logging the
/// body would put password-reset links in the application log.
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
            await WriteToPickupDirectoryAsync(message, cancellationToken);
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

        _logger.LogInformation("Sent {Subject} to {To}", message.Subject, message.To);
    }

    private async Task WriteToPickupDirectoryAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_options.PickupDirectory);
            var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.html";
            var path = Path.Combine(_options.PickupDirectory, name);
            await File.WriteAllTextAsync(
                path,
                $"<!-- To: {message.To} | Subject: {message.Subject} -->\n{message.Html}",
                cancellationToken);

            _logger.LogWarning(
                "SMTP is not configured; wrote {Subject} for {To} to {Path} instead of sending",
                message.Subject, message.To, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP is not configured and the pickup directory is not writable");
        }
    }
}
