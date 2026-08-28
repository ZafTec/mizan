namespace Mizan.Application.Interfaces;

/// <summary>
/// Links that land in the user's inbox point at the web app, not the API, so
/// the API needs to know where the web app lives.
/// </summary>
public interface IAppUrls
{
    string VerifyEmail(string token);
    string ResetPassword(string token);

    /// <summary>Validates an OAuth return target against the configured app origin.</summary>
    string SafeReturnUrl(string? candidate);
}
