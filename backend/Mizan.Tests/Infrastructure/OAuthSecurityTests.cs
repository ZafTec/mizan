using System.Net;
using FluentAssertions;
using Mizan.Application.OAuth;
using Mizan.Contracts.Mcp;
using Mizan.Infrastructure.Identity;
using Xunit;

namespace Mizan.Tests.Infrastructure;

/// <summary>The parts of the authorization server that decide who may talk to what, tested without a database.</summary>
public class OAuthSecurityTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")] // cloud metadata
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    public void ClientMetadataFetcher_RefusesPrivateAndInternalAddresses(string address) =>
        OAuthClientMetadataFetcher.IsPublicAddress(IPAddress.Parse(address)).Should().BeFalse();

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.15.0.1")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700:4700::1111")]
    public void ClientMetadataFetcher_AllowsPublicAddresses(string address) =>
        OAuthClientMetadataFetcher.IsPublicAddress(IPAddress.Parse(address)).Should().BeTrue();

    [Theory]
    [InlineData("http://example.com/client.json")]
    [InlineData("https://example.com")]
    [InlineData("https://example.com/")]
    [InlineData("https://user:pass@example.com/client.json")]
    [InlineData("https://example.com:8443/client.json")]
    [InlineData("https://example.com/client.json#frag")]
    [InlineData("https://127.0.0.1/client.json")]
    [InlineData("https://[::1]/client.json")]
    [InlineData("https://localhost/client.json")]
    [InlineData("https://internal/client.json")]
    [InlineData("file:///etc/passwd")]
    public void ClientMetadataFetcher_RejectsUnsafeUrls(string url)
    {
        var act = () => OAuthClientMetadataFetcher.ValidateUrl(url);
        act.Should().Throw<OAuthException>();
    }

    [Fact]
    public void ClientMetadataFetcher_AcceptsAPlainHttpsDocumentUrl() =>
        OAuthClientMetadataFetcher.ValidateUrl("https://app.example.com/.well-known/client.json").Host.Should().Be("app.example.com");

    [Fact]
    public void Parse_RequiresTheDocumentToNameItself()
    {
        const string url = "https://app.example.com/c.json";
        var act = () => OAuthClientMetadataFetcher.Parse(url, """{"client_id":"https://evil.example/c.json","redirect_uris":["https://app.example.com/cb"]}""");
        act.Should().Throw<OAuthException>().Which.Error.Should().Be("invalid_client");
    }

    [Theory]
    [InlineData("""{"client_id":"https://app.example.com/c.json","redirect_uris":["https://app.example.com/cb"],"client_secret":"x"}""")]
    [InlineData("""{"client_id":"https://app.example.com/c.json","redirect_uris":["https://app.example.com/cb"],"token_endpoint_auth_method":"private_key_jwt"}""")]
    [InlineData("""{"client_id":"https://app.example.com/c.json","redirect_uris":["http://evil.example/cb"]}""")]
    [InlineData("""{"client_id":"https://app.example.com/c.json"}""")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Parse_RejectsDocumentsWeCannotHonor(string json)
    {
        var act = () => OAuthClientMetadataFetcher.Parse("https://app.example.com/c.json", json);
        act.Should().Throw<OAuthException>();
    }

    [Fact]
    public void Parse_ReadsAGoodDocument_AndDropsUnsafeExtras()
    {
        var metadata = OAuthClientMetadataFetcher.Parse("https://app.example.com/c.json", """
            {"client_id":"https://app.example.com/c.json","client_name":"Example","logo_uri":"http://insecure.example/l.png",
             "client_uri":"https://app.example.com","redirect_uris":["https://app.example.com/cb","javascript:alert(1)","http://127.0.0.1:9/cb"]}
            """);

        metadata.Name.Should().Be("Example");
        metadata.LogoUri.Should().BeNull("a logo is shown to the user, so it must be https");
        metadata.ClientUri.Should().Be("https://app.example.com");
        metadata.RedirectUris.Should().BeEquivalentTo(new[] { "https://app.example.com/cb", "http://127.0.0.1:9/cb" });
    }

    [Theory]
    [InlineData("https://example.com/cb", true)]
    [InlineData("http://localhost:3000/cb", true)]
    [InlineData("http://127.0.0.1/cb", true)]
    [InlineData("com.example.app:/oauth2redirect", true)]
    [InlineData("cursor://anysphere.cursor/oauth", true)]
    [InlineData("http://example.com/cb", false)]
    [InlineData("https://example.com/cb#x", false)]
    [InlineData("https://user@example.com/cb", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,x", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("relative/path", false)]
    [InlineData("", false)]
    public void RedirectUriRules_AcceptsOnlyUrisThatCannotBeAbused(string uri, bool expected) =>
        RedirectUriRules.TryValidate(uri, out _).Should().Be(expected);

    [Theory]
    [InlineData("https://example.com/cb", "https://example.com/cb", true)]
    [InlineData("https://example.com/cb", "https://example.com/cb2", false)]
    [InlineData("https://example.com/cb", "https://example.com/cb?x=1", false)]
    [InlineData("https://example.com/cb", "https://evil.example.com/cb", false)]
    [InlineData("http://127.0.0.1:1111/cb", "http://127.0.0.1:2222/cb", true)]
    [InlineData("http://localhost:1111/cb", "http://localhost:2222/cb", true)]
    [InlineData("http://127.0.0.1/cb", "http://127.0.0.1:2222/other", false)]
    [InlineData("http://localhost/cb", "http://127.0.0.1/cb", false)]
    [InlineData("https://example.com:1111/cb", "https://example.com:2222/cb", false)]
    public void RedirectUriRules_MatchExactlyExceptTheLoopbackPort(string registered, string requested, bool expected) =>
        RedirectUriRules.Matches(registered, requested).Should().Be(expected);

    [Fact]
    public void Scopes_WriteImpliesRead_AndAdminNeedsExplicitAllowance()
    {
        McpScopes.Normalize(["nutrition:write", "bogus", "admin"], allowAdmin: false)
            .Should().BeEquivalentTo(new[] { "nutrition:read", "nutrition:write" });
        McpScopes.Normalize(["admin"], allowAdmin: true).Should().BeEquivalentTo(new[] { "admin" });

        McpScopes.Allows(["nutrition:write"], "nutrition:read").Should().BeTrue();
        McpScopes.Allows(["nutrition:read"], "nutrition:write").Should().BeFalse();
        McpScopes.Allows(["nutrition:read"], "training:read").Should().BeFalse();
        McpScopes.Allows([McpScopes.Full], "body:write").Should().BeTrue();
        McpScopes.Allows([McpScopes.Full], McpScopes.Admin).Should().BeFalse("full never includes admin by itself");
        McpScopes.Allows([McpScopes.Full, McpScopes.Admin], McpScopes.Admin).Should().BeTrue();
    }

    [Fact]
    public void Scopes_ParseSplitsTheWireFormat() =>
        McpScopes.Parse("a  b a").Should().Equal("a", "b");
}
