using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Mizan.Infrastructure.Services;

/// <summary>
/// Draws the card of an achievement that has no badge of its own: a teal medallion on the ink
/// background with the achievement's initials, as a 512px PNG. Written by hand (an RGB raster,
/// zlib and the PNG chunks) so the API takes on no image library for one picture per achievement.
/// </summary>
public static class AchievementCardImage
{
    public const int Size = 512;

    private static readonly (byte R, byte G, byte B) Ink = (0x1C, 0x1A, 0x17);
    private static readonly (byte R, byte G, byte B) Paper = (0xFB, 0xFA, 0xF7);
    private static readonly (byte R, byte G, byte B) Teal = (0x1F, 0x7A, 0x72);
    private static readonly (byte R, byte G, byte B) TealLight = (0x7C, 0xCF, 0xC4);

    /// <summary>The card of an achievement named <paramref name="name"/>.</summary>
    public static byte[] Render(string name)
    {
        var pixels = new byte[Size * Size * 3];
        Fill(pixels, Ink);

        const int cx = Size / 2, cy = Size / 2;
        Disc(pixels, cx, cy, 196, TealLight);
        Disc(pixels, cx, cy, 184, Ink);
        Disc(pixels, cx, cy, 170, Teal);

        var initials = Initials(name);
        const int scale = 22;
        var width = initials.Length * 5 * scale + (initials.Length - 1) * scale;
        var x = cx - width / 2;
        var y = cy - 7 * scale / 2;
        foreach (var letter in initials)
        {
            Glyph(pixels, letter, x, y, scale, Paper);
            x += 6 * scale;
        }

        return Encode(pixels);
    }

    /// <summary>Up to two letters or digits: the first of the first two words, or the first two of a single word.</summary>
    public static string Initials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant())
            .Where(w => w.Length > 0)
            .ToList();
        if (words.Count == 0) return "M";
        if (words.Count == 1) return words[0][..Math.Min(2, words[0].Length)];
        return $"{words[0][0]}{words[1][0]}";
    }

    private static void Fill(byte[] pixels, (byte R, byte G, byte B) color)
    {
        for (var i = 0; i < pixels.Length; i += 3)
        {
            pixels[i] = color.R;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.B;
        }
    }

    private static void Disc(byte[] pixels, int cx, int cy, int radius, (byte R, byte G, byte B) color)
    {
        var r2 = radius * radius;
        for (var y = Math.Max(0, cy - radius); y <= Math.Min(Size - 1, cy + radius); y++)
        {
            for (var x = Math.Max(0, cx - radius); x <= Math.Min(Size - 1, cx + radius); x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                if (dx * dx + dy * dy <= r2) Set(pixels, x, y, color);
            }
        }
    }

    private static void Glyph(byte[] pixels, char letter, int left, int top, int scale, (byte R, byte G, byte B) color)
    {
        if (!Font.TryGetValue(letter, out var rows)) return;
        for (var row = 0; row < 7; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                if ((rows[row] & (1 << (4 - col))) == 0) continue;
                for (var y = 0; y < scale; y++)
                    for (var x = 0; x < scale; x++)
                        Set(pixels, left + col * scale + x, top + row * scale + y, color);
            }
        }
    }

    private static void Set(byte[] pixels, int x, int y, (byte R, byte G, byte B) color)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size) return;
        var i = (y * Size + x) * 3;
        pixels[i] = color.R;
        pixels[i + 1] = color.G;
        pixels[i + 2] = color.B;
    }

    private static byte[] Encode(byte[] pixels)
    {
        // Each scanline starts with filter type 0 (none).
        var raw = new byte[Size * (Size * 3 + 1)];
        for (var y = 0; y < Size; y++)
            Buffer.BlockCopy(pixels, y * Size * 3, raw, y * (Size * 3 + 1) + 1, Size * 3);

        byte[] compressed;
        using (var buffer = new MemoryStream())
        {
            using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(raw);
            compressed = buffer.ToArray();
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Size);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Size);
        header[8] = 8;  // bit depth
        header[9] = 2;  // truecolour
        header[10] = 0; // deflate
        header[11] = 0; // adaptive filtering
        header[12] = 0; // no interlace

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed);
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in type) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    /// <summary>A 5×7 capital and digit font, one row per byte, the high bit on the left.</summary>
    private static readonly Dictionary<char, byte[]> Font = new()
    {
        ['A'] = [0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
        ['B'] = [0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E],
        ['C'] = [0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E],
        ['D'] = [0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E],
        ['E'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F],
        ['F'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10],
        ['G'] = [0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F],
        ['H'] = [0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
        ['I'] = [0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E],
        ['J'] = [0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C],
        ['K'] = [0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11],
        ['L'] = [0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F],
        ['M'] = [0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11],
        ['N'] = [0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11],
        ['O'] = [0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['P'] = [0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10],
        ['Q'] = [0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D],
        ['R'] = [0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11],
        ['S'] = [0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E],
        ['T'] = [0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04],
        ['U'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['V'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04],
        ['W'] = [0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A],
        ['X'] = [0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11],
        ['Y'] = [0x11, 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04],
        ['Z'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F],
        ['0'] = [0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E],
        ['1'] = [0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E],
        ['2'] = [0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F],
        ['3'] = [0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E],
        ['4'] = [0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02],
        ['5'] = [0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E],
        ['6'] = [0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E],
        ['7'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08],
        ['8'] = [0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E],
        ['9'] = [0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C],
    };
}
