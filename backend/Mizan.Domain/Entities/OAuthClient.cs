namespace Mizan.Domain.Entities;

/// <summary>
/// An application that asks to act for a user: an AI assistant, the Android
/// app, a script. Dynamic and metadata-document clients are public clients and
/// hold no secret, so their identity is only as strong as the redirect URI and
/// PKCE. The consent screen says so.
/// </summary>
public class OAuthClient
{
    public const string SourceDynamic = "dynamic";
    public const string SourceMetadata = "metadata";
    public const string SourceFirstParty = "first_party";

    public Guid Id { get; set; }

    /// <summary>The public identifier clients send. For a metadata client it is the document URL.</summary>
    public string ClientId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? LogoUri { get; set; }
    public string? ClientUri { get; set; }
    public List<string> RedirectUris { get; set; } = new();
    public string Source { get; set; } = SourceDynamic;

    /// <summary>The host the metadata document was served from, shown as "verified".</summary>
    public string? VerifiedHost { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? MetadataFetchedAt { get; set; }

    public bool IsFirstParty => Source == SourceFirstParty;
}
