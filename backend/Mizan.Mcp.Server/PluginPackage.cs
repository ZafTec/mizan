using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mizan.Mcp.Server.Authorization;

namespace Mizan.Mcp.Server;

/// <summary>
/// The Claude plugin as a zip, built from what this server already carries: the manifest, README and
/// licence embedded from <c>Plugin/</c>, the skills it serves, and a connector that points at this
/// deployment's own <c>Mcp:PublicUrl</c>. A demo serves a demo plugin and production a production one,
/// and no address is written into the files. The repository's <c>plugin/</c> folder is a copy of the
/// same sources (<c>PluginSkillsTests</c>).
/// </summary>
public sealed class PluginPackage
{
    public const string FileName = "mizan-plugin.zip";

    /// <summary>Entries carry this fixed time, so the same sources give the same bytes and the same ETag.</summary>
    private static readonly DateTimeOffset Stamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Lazy<(byte[] Bytes, string ETag)> _built;

    public PluginPackage(string mcpPublicUrl)
    {
        McpUrl = mcpPublicUrl.TrimEnd('/');
        _built = new Lazy<(byte[], string)>(Build);
    }

    public string McpUrl { get; }

    public byte[] Bytes => _built.Value.Bytes;

    public string ETag => _built.Value.ETag;

    /// <summary>The connector file: one remote server, this deployment's, and no credential.</summary>
    public string ConnectorJson() => JsonSerializer.Serialize(
        new Dictionary<string, object>
        {
            ["mcpServers"] = new Dictionary<string, object>
            {
                ["mizan"] = new Dictionary<string, string> { ["type"] = "http", ["url"] = McpUrl },
            },
        },
        new JsonSerializerOptions { WriteIndented = true }) + "\n";

    private (byte[], string) Build()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, ".claude-plugin/plugin.json", Embedded("plugin/plugin.json"));
            Add(zip, ".mcp.json", Encoding.UTF8.GetBytes(ConnectorJson()));
            Add(zip, "README.md", Embedded("plugin/README.md"));
            Add(zip, "LICENSE", Embedded("plugin/LICENSE"));
            foreach (var skill in McpSkillCatalog.All)
                foreach (var file in skill.Files)
                    Add(zip, $"skills/{skill.Name}/{file.Path}", file.Bytes);
        }

        var bytes = buffer.ToArray();
        return (bytes, "\"" + Convert.ToHexStringLower(SHA256.HashData(bytes))[..32] + "\"");
    }

    private static void Add(ZipArchive zip, string path, byte[] bytes)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = Stamp;
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static byte[] Embedded(string name)
    {
        var assembly = typeof(PluginPackage).Assembly;
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The plugin source '{name}' is not embedded.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
