extern alias McpServer;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using McpServer::Mizan.Mcp.Server.Services;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Token introspection tells two things apart: the API saying the token is no good, and the API
/// not being there to say anything. The first is a rejected token; the second is an outage the
/// authentication handler turns into a 503, so it must never come back looking like a rejection.
/// </summary>
public sealed class BackendApiClientIntrospectionTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AFailingApi_IsReportedAsUnavailable_NotAsARejectedToken(HttpStatusCode status)
    {
        var client = Client(new ApiStub(_ => new HttpResponseMessage(status) { Content = new StringContent("down") }));

        (await client.IntrospectAsync("t")).Should().BeNull();
    }

    [Fact]
    public async Task AnUnreachableApi_IsReportedAsUnavailable()
    {
        var client = Client(new ApiStub(_ => throw new HttpRequestException("connection refused")));

        (await client.IntrospectAsync("t")).Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task AnApiThatRefuses_MeansTheTokenIsInactive(HttpStatusCode status)
    {
        var client = Client(new ApiStub(_ => new HttpResponseMessage(status) { Content = new StringContent("""{"error":"invalid_token"}""", Encoding.UTF8, "application/json") }));

        var result = await client.IntrospectAsync("t");

        result.Should().NotBeNull();
        result!.Active.Should().BeFalse();
    }

    [Fact]
    public async Task AnApiThatSaysInactive_MeansTheTokenIsInactive()
    {
        var client = Client(new ApiStub(_ => Json("""{"active":false}""")));

        var result = await client.IntrospectAsync("t");

        result.Should().NotBeNull();
        result!.Active.Should().BeFalse();
    }

    [Fact]
    public async Task AnActiveAnswer_ComesBackWhole_AndCarriesTheServiceKey()
    {
        HttpRequestMessage? seen = null;
        var user = Guid.NewGuid();
        var client = Client(new ApiStub(request => { seen = request; return Json($$"""{"active":true,"userId":"{{user}}","scopes":["nutrition:read"],"plan":"free","monthlyLimit":15,"usedThisMonth":3}"""); }));

        var result = await client.IntrospectAsync("t");

        result!.Active.Should().BeTrue();
        result.UserId.Should().Be(user);
        result.Scopes.Should().BeEquivalentTo("nutrition:read");
        result.MonthlyLimit.Should().Be(15);
        seen!.RequestUri!.AbsolutePath.Should().Be("/api/oauth/introspect");
        seen.Headers.GetValues("X-Api-Key").Should().BeEquivalentTo("test-api-key");
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static BackendApiClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://api.test") }, new HttpContextAccessor(), NullLogger<BackendApiClient>.Instance);

    private sealed class ApiStub(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
