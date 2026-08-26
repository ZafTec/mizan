using System.Text;

namespace Mizan.Domain.Media;

/// <summary>
/// Pure. A browser's Content-Type is a claim, not evidence: it is set by the
/// client and a .png header on a script is the oldest trick there is. The
/// first bytes of the file decide what we store.
/// </summary>
public static class ImageFormat
{
    /// <summary>Enough bytes for every signature below.</summary>
    public const int HeaderBytes = 12;

    /// <summary>The real media type, or null when the bytes are not an image we accept.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (StartsWith(header, [0xFF, 0xD8, 0xFF])) return "image/jpeg";
        if (StartsWith(header, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";
        if (StartsWithAscii(header, "GIF87a") || StartsWithAscii(header, "GIF89a")) return "image/gif";

        // RIFF....WEBP - the four length bytes in between are not part of it.
        if (StartsWithAscii(header, "RIFF") && header.Length >= 12
            && Encoding.ASCII.GetString(header[8..12]) == "WEBP")
        {
            return "image/webp";
        }

        return null;
    }

    private static bool StartsWith(ReadOnlySpan<byte> header, ReadOnlySpan<byte> signature) =>
        header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature);

    private static bool StartsWithAscii(ReadOnlySpan<byte> header, string signature) =>
        StartsWith(header, Encoding.ASCII.GetBytes(signature));
}
