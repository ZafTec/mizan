using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.OAuth;

/// <summary>
/// Finds the client a request names. A client whose id is an https URL is
/// described by the document at that URL. A first-party client comes from
/// configuration. Anything else must have registered itself first.
/// </summary>
public sealed class OAuthClientResolver
{
    private static readonly TimeSpan MetadataMaxAge = TimeSpan.FromHours(24);

    private readonly IMizanDbContext _context;
    private readonly IOAuthSettings _settings;
    private readonly IOAuthClientMetadataFetcher _fetcher;

    public OAuthClientResolver(IMizanDbContext context, IOAuthSettings settings, IOAuthClientMetadataFetcher fetcher)
    {
        _context = context;
        _settings = settings;
        _fetcher = fetcher;
    }

    public async Task<OAuthClient?> ResolveAsync(string? clientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Length > 512) return null;

        if (clientId.StartsWith("https://", StringComparison.Ordinal))
        {
            return await ResolveMetadataClientAsync(clientId, cancellationToken);
        }

        var first = _settings.FirstPartyClients.FirstOrDefault(c => c.ClientId == clientId);
        var row = await _context.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

        if (first is not null)
        {
            // Configuration is the source of truth for our own apps.
            if (row is null)
            {
                row = new OAuthClient { Id = Guid.CreateVersion7(), ClientId = clientId, CreatedAt = DateTime.UtcNow };
                _context.OAuthClients.Add(row);
            }

            row.Name = first.Name;
            row.Source = OAuthClient.SourceFirstParty;
            row.RedirectUris = first.RedirectUris.ToList();
            await _context.SaveChangesAsync(cancellationToken);
            return row;
        }

        return row;
    }

    private async Task<OAuthClient?> ResolveMetadataClientAsync(string clientId, CancellationToken cancellationToken)
    {
        if (!_settings.AllowMetadataClients) return null;

        var row = await _context.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);
        var now = DateTime.UtcNow;
        if (row?.MetadataFetchedAt is { } fetched && now - fetched < MetadataMaxAge) return row;

        OAuthClientMetadata metadata;
        try
        {
            metadata = await _fetcher.FetchAsync(clientId, cancellationToken);
        }
        catch (OAuthException) when (row is not null)
        {
            // The host is down or changed its document. Keep what we last saw.
            return row;
        }
        catch (OAuthException)
        {
            return null;
        }

        row ??= new OAuthClient { Id = Guid.CreateVersion7(), ClientId = clientId, CreatedAt = now };
        if (_context.OAuthClients.Entry(row).State == EntityState.Detached) _context.OAuthClients.Add(row);

        row.Name = metadata.Name;
        row.LogoUri = metadata.LogoUri;
        row.ClientUri = metadata.ClientUri;
        row.RedirectUris = metadata.RedirectUris.ToList();
        row.Source = OAuthClient.SourceMetadata;
        row.VerifiedHost = new Uri(clientId).Host;
        row.MetadataFetchedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
        return row;
    }
}
