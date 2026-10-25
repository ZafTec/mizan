using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// The plugin and the MCP server must teach Claude the same things. One folder is the source and the
/// other is a copy, so a skill edited in only one place fails here instead of drifting.
/// </summary>
public class PluginSkillsTests
{
    [Fact]
    public void ThePluginCarriesExactlyTheSkillsTheServerServes()
    {
        var root = RepositoryRoot();
        var source = Path.Combine(root, "backend", "Mizan.Mcp.Server", "Skills");
        var copy = Path.Combine(root, "plugin", "skills");

        Directory.Exists(copy).Should().BeTrue("the plugin ships its skills");

        Files(copy).Keys.Should().BeEquivalentTo(Files(source).Keys,
            "run scripts/sync-plugin-skills.sh after changing a skill");
        foreach (var (path, text) in Files(source))
            Files(copy)[path].Should().Be(text, $"{path} differs; run scripts/sync-plugin-skills.sh");
    }

    [Fact]
    public void TheManifestAndConnectorPointAtTheRealServer()
    {
        var plugin = Path.Combine(RepositoryRoot(), "plugin");

        File.ReadAllText(Path.Combine(plugin, ".claude-plugin", "plugin.json")).Should().Contain("\"name\": \"mizan\"");
        var connector = File.ReadAllText(Path.Combine(plugin, ".mcp.json"));
        connector.Should().Contain("https://mizan.zaftech.co/mcp").And.Contain("\"type\": \"http\"");
        connector.Should().NotContainAny("key", "token", "secret", "Authorization");
    }

    private static Dictionary<string, string> Files(string folder) =>
        Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(folder, f).Replace('\\', '/'), f => File.ReadAllText(f).ReplaceLineEndings("\n"));

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
