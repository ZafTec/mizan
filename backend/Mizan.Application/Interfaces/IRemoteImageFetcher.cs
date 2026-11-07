namespace Mizan.Application.Interfaces;

/// <summary>An image downloaded from an address someone gave us. The bytes decided its type.</summary>
public sealed record FetchedImage(byte[] Bytes, string ContentType);

/// <summary>
/// Downloads an image from a caller-supplied address. Refuses, with a message the caller can act on, anything
/// that is not a public https address serving a JPEG, PNG, WebP or GIF of at most 4 MB.
/// </summary>
public interface IRemoteImageFetcher
{
    Task<FetchedImage> FetchAsync(string url, CancellationToken cancellationToken);
}
