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

    /// <summary>
    /// A fetchable address for a stored asset URL. Site-relative paths, such as
    /// the default achievement badges shipped under the web app's /badges
    /// folder, become absolute on the app origin so that Telegram and the
    /// native client can fetch them. Absolute URLs (uploads on the media host)
    /// pass through unchanged; null stays null.
    /// </summary>
    string? Asset(string? url);

    /// <summary>The web app's own address, which a campaign's <c>{appLink}</c> stands for.</summary>
    string Home();

    /// <summary>The page that turns off news and offers in one tap for whoever the token names.</summary>
    string MarketingOptOut(string token);
}

/// <summary>
/// The token in a marketing message's opt-out link. It names the person, so the link works signed
/// out, and it is signed, so nobody can turn off someone else's messages by guessing.
/// </summary>
public interface INotificationOptOutTokens
{
    string Create(Guid userId);

    /// <summary>The person the token names, or null when it is not one of ours.</summary>
    Guid? Read(string? token);
}
