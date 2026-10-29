namespace Mizan.Infrastructure.Identity;

/// <summary>
/// Google and GitHub sign-in, runtime settings under <c>Authentication.*</c> (docs/ADMIN.md#runtime-settings).
/// A provider without both an id and a secret is off.
/// </summary>
public sealed class ExternalSignInOptions
{
    public const string SectionName = "Authentication";

    public ExternalSignInProvider Google { get; set; } = new();

    public ExternalSignInProvider GitHub { get; set; } = new();
}

public sealed class ExternalSignInProvider
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
