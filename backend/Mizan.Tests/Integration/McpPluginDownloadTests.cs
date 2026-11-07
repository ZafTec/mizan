extern alias McpServer;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Anyone can download the Claude plugin from a deployment, and the plugin they get connects to that
/// deployment: the demo hands out a demo plugin, production a production one.
/// </summary>
public class McpPluginDownloadTests : IClassFixture<WebApplicationFactory<McpServer::Program>>
{
    private const string DemoUrl = "https://mizan-demo.example.test/mcp";
    private readonly WebApplicationFactory<McpServer::Program> _factory;

    public McpPluginDownloadTests(WebApplicationFactory<McpServer::Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mcp:PublicUrl", DemoUrl);
            builder.UseSetting("Mcp:ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:AdminServiceApiKey", "test-admin-api-key");
        });
    }

    [Fact]
    public async Task APersonDownloadsThePlugin_AndItConnectsToTheServerTheyDownloadedItFrom()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/mcp/plugin.zip");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("mizan-plugin.zip");
        response.Headers.ETag.Should().NotBeNull();

        using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        names.Should().Contain(new[] { ".claude-plugin/plugin.json", ".mcp.json", "README.md", "LICENSE", "skills/mizan-food-logging/SKILL.md" });

        using var manifest = JsonDocument.Parse(Read(zip, ".claude-plugin/plugin.json"));
        manifest.RootElement.GetProperty("name").GetString().Should().Be("mizan");
        manifest.RootElement.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();

        using var connector = JsonDocument.Parse(Read(zip, ".mcp.json"));
        var server = connector.RootElement.GetProperty("mcpServers").GetProperty("mizan");
        server.GetProperty("type").GetString().Should().Be("http");
        server.GetProperty("url").GetString().Should().Be(DemoUrl);
        Read(zip, ".mcp.json").Should().NotContainAny("key", "token", "secret", "Authorization");
    }

    [Fact]
    public async Task ASecondDownloadWithTheSameETagIsNotSentAgain()
    {
        using var client = _factory.CreateClient();
        var first = await client.GetAsync("/mcp/plugin.zip");

        using var again = new HttpRequestMessage(HttpMethod.Get, "/mcp/plugin.zip");
        again.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await client.SendAsync(again);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    private static string Read(ZipArchive zip, string name)
    {
        using var reader = new StreamReader(zip.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }
}
