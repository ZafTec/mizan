namespace Mizan.Contracts.Media;

/// <summary>
/// An image the API downloads itself, from a public https address, so a client never has to carry the bytes
/// (an MCP client's model would otherwise write them out as base64).
/// </summary>
public sealed record ImageUrlRequest(string ImageUrl);
