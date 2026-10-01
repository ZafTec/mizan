namespace Mizan.Domain.Media;

/// <summary>
/// Pure. Recognises a glTF binary (.glb) by its first bytes, for the same reason images are:
/// the client's Content-Type is a claim, and the bytes decide what is stored.
/// </summary>
public static class ModelFormat
{
    public const string ContentType = "model/gltf-binary";

    /// <summary>The magic number and the version, which is all of the header that identifies the format.</summary>
    public const int HeaderBytes = 12;

    /// <summary>The media type when the bytes are a glTF 2 binary, otherwise null.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        // "glTF", then version 2 as a little-endian 32-bit number.
        if (header.Length < 8) return null;
        var magic = header[0] == 0x67 && header[1] == 0x6C && header[2] == 0x54 && header[3] == 0x46;
        var version = header[4] == 2 && header[5] == 0 && header[6] == 0 && header[7] == 0;
        return magic && version ? ContentType : null;
    }
}
