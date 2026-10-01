using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Mizan.Mcp.Server.Authorization;

/// <summary>
/// The Skills extension (io.modelcontextprotocol/skills): instructions that
/// travel with the server. Each skill is a folder with a SKILL.md and optional
/// supporting files, embedded in this assembly. The manifest carries a SHA-256
/// digest and byte size for every file so a client can verify what it reads.
/// </summary>
public static class McpSkillCatalog
{
    public const string ExtensionId = "io.modelcontextprotocol/skills";
    private const int TtlMs = 300_000;

    public sealed record SkillFile(string Uri, string Path, byte[] Bytes, string MimeType)
    {
        public string Digest => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Bytes));
        public string Text => Encoding.UTF8.GetString(Bytes);
    }

    public sealed record Skill(string Name, string Uri, IReadOnlyDictionary<string, string> Frontmatter, IReadOnlyList<SkillFile> Files);

    public static IReadOnlyList<Skill> All => Loaded.Value;

    public static IEnumerable<SkillFile> Files => All.SelectMany(skill => skill.Files);

    private static readonly Lazy<IReadOnlyList<Skill>> Loaded = new(Load);

    private static List<Skill> Load()
    {
        var assembly = typeof(McpSkillCatalog).Assembly;
        var files = new Dictionary<string, List<SkillFile>>(StringComparer.Ordinal);

        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(n => n.Replace('\\', '/').StartsWith("skills/", StringComparison.Ordinal)))
        {
            var path = resource.Replace('\\', '/')["skills/".Length..];
            var slash = path.IndexOf('/');
            if (slash <= 0) continue;

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            var name = path[..slash];
            var uri = $"skill://{name}/{path[(slash + 1)..]}";
            var mime = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? "text/markdown" : "text/plain";

            if (!files.TryGetValue(name, out var list)) files[name] = list = new List<SkillFile>();
            list.Add(new SkillFile(uri, path[(slash + 1)..], buffer.ToArray(), mime));
        }

        var skills = new List<Skill>();
        foreach (var (name, list) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var main = list.SingleOrDefault(f => f.Path == "SKILL.md")
                ?? throw new InvalidOperationException($"Skill '{name}' has no SKILL.md");
            var frontmatter = ParseFrontmatter(main.Text);
            if (!frontmatter.TryGetValue("name", out var declared) || declared != name || !frontmatter.ContainsKey("description"))
                throw new InvalidOperationException($"Skill '{name}' must declare name '{name}' and a description in its frontmatter");

            skills.Add(new Skill(name, main.Uri, frontmatter, list.OrderBy(f => f.Path, StringComparer.Ordinal).ToList()));
        }

        return skills;
    }

    /// <summary>Reads the YAML header: flat "key: value" lines between the first two --- markers.</summary>
    public static Dictionary<string, string> ParseFrontmatter(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---") return result;

        for (var i = 1; i < lines.Length && lines[i].Trim() != "---"; i++)
        {
            var colon = lines[i].IndexOf(':');
            if (colon <= 0) continue;
            result[lines[i][..colon].Trim()] = lines[i][(colon + 1)..].Trim().Trim('"');
        }

        return result;
    }

    public static JsonObject Entry(Skill skill) => new()
    {
        ["uri"] = skill.Uri,
        ["frontmatter"] = new JsonObject(skill.Frontmatter.Select(kv => KeyValuePair.Create<string, JsonNode?>(kv.Key, kv.Value))),
        ["resources"] = new JsonArray(skill.Files.Select(file => (JsonNode)new JsonObject
        {
            ["uri"] = file.Uri,
            ["digest"] = file.Digest,
            ["size"] = file.Bytes.Length,
        }).ToArray()),
    };

    public static JsonNode List() => new JsonObject
    {
        ["resultType"] = "complete",
        ["skills"] = new JsonArray(All.Select(skill => (JsonNode)Entry(skill)).ToArray()),
        ["ttlMs"] = TtlMs,
        ["cacheScope"] = "public",
    };

    /// <summary>Null when no skill has that SKILL.md address. The caller answers with Invalid params.</summary>
    public static JsonNode? Get(string? uri)
    {
        var skill = All.FirstOrDefault(s => string.Equals(s.Uri, uri, StringComparison.Ordinal));
        if (skill is null) return null;

        return new JsonObject
        {
            ["resultType"] = "complete",
            ["skill"] = Entry(skill),
            ["ttlMs"] = TtlMs,
            ["cacheScope"] = "public",
        };
    }
}
