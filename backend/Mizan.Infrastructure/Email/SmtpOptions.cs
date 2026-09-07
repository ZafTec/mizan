using MailKit.Security;

namespace Mizan.Infrastructure.Email;

public enum SmtpSecurityMode
{
    Auto,
    StartTls,
    SslOnConnect,
    None,
}

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@mizan.local";
    public string FromName { get; set; } = "Mizan";

    /// <summary>
    /// Auto uses implicit TLS on port 465 and requires STARTTLS on other ports.
    /// None explicitly permits unencrypted SMTP for a trusted relay.
    /// </summary>
    public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.Auto;

    /// <summary>
    /// Legacy setting used only when Security is Auto. False preserves MailKit's
    /// opportunistic Auto mode, which can continue without TLS on non-465 ports.
    /// Prefer an explicit Security mode for new configuration.
    /// </summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Optional EHLO/HELO hostname. Empty uses the sender address's domain.</summary>
    public string? LocalDomain { get; set; }

    public SecureSocketOptions GetSocketOptions() => Security switch
    {
        SmtpSecurityMode.Auto when !UseStartTls => SecureSocketOptions.Auto,
        SmtpSecurityMode.Auto when Port == 465 => SecureSocketOptions.SslOnConnect,
        SmtpSecurityMode.Auto => SecureSocketOptions.StartTls,
        SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
        SmtpSecurityMode.None => SecureSocketOptions.None,
        _ => throw new ArgumentOutOfRangeException(nameof(Security), "Unsupported SMTP security mode."),
    };
}
