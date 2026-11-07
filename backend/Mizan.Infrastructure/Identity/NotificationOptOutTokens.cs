using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Identity;

/// <summary>
/// Opt-out tokens protected with the application's data protection keys, which are kept in the
/// database, so a link in an old email still works after a restart or on another instance.
/// </summary>
public sealed class NotificationOptOutTokens : INotificationOptOutTokens
{
    private const string Purpose = "Mizan.Notifications.MarketingOptOut.v1";
    private readonly IDataProtector _protector;

    public NotificationOptOutTokens(IDataProtectionProvider provider) => _protector = provider.CreateProtector(Purpose);

    public string Create(Guid userId) => _protector.Protect(userId.ToString("N"));

    public Guid? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            return Guid.TryParseExact(_protector.Unprotect(token.Trim()), "N", out var id) ? id : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
