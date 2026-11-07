namespace Mizan.Contracts.Media;

/// <summary>
/// An image the API downloads itself, from a public https address, so a client never has to carry the bytes
/// (an MCP client's model would otherwise write them out as base64).
/// </summary>
public sealed record ImageUrlRequest(string ImageUrl);

/// <summary>A stored image: its storage key and the address to show it at.</summary>
public sealed record UploadedImageResult(string Key, string Url);
