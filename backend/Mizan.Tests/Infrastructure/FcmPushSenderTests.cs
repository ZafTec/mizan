using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Push;
using Xunit;

namespace Mizan.Tests.Infrastructure;

public class FcmPushSenderTests
{
    [Theory]
    [InlineData(404, "{}", PushOutcome.TokenInvalid)]
    [InlineData(404, "not json", PushOutcome.TokenInvalid)]
    [InlineData(400, """{"error":{"message":"The registration token is not a valid FCM registration token","status":"INVALID_ARGUMENT"}}""", PushOutcome.TokenInvalid)]
    [InlineData(404, """{"error":{"details":[{"errorCode":"UNREGISTERED"}]}}""", PushOutcome.TokenInvalid)]
    [InlineData(400, """{"error":{"message":"Invalid JSON payload","status":"INVALID_ARGUMENT"}}""", PushOutcome.Failed)]
    [InlineData(429, """{"error":{"status":"RESOURCE_EXHAUSTED"}}""", PushOutcome.Failed)]
    [InlineData(500, "", PushOutcome.Failed)]
    [InlineData(503, "<html>", PushOutcome.Failed)]
    public void AnAnswerIsRead_AsAGoneDevice_OnlyWhenTheDeviceIsGone(int status, string body, PushOutcome expected)
    {
        FcmPushSender.Classify((HttpStatusCode)status, body).Should().Be(expected);
    }

    [Fact]
    public void WithoutCredentials_PushIsOff()
    {
        Sender(new FakeFcm(), serviceAccount: "").IsConfigured.Should().BeFalse();
        Sender(new FakeFcm(), serviceAccount: "{not json").IsConfigured.Should().BeFalse();
        Sender(new FakeFcm(), projectId: "").IsConfigured.Should().BeFalse();
        Sender(new FakeFcm()).IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task ASend_SignsIn_ThenPostsTheMessage_AndReusesTheTokenForTheNext()
    {
        var fcm = new FakeFcm();
        var sender = Sender(fcm);

        var first = await sender.SendAsync(new PushMessage("device-1", "Hello", "Body", "/today", "n-1"), CancellationToken.None);
        var second = await sender.SendAsync(new PushMessage("device-2", "Hello", null, null, null), CancellationToken.None);

        first.Should().Be(PushOutcome.Sent);
        second.Should().Be(PushOutcome.Sent);
        fcm.TokenRequests.Should().Be(1, "the access token is kept until it nears its end");

        var message = JsonDocument.Parse(fcm.Sends[0].Body).RootElement.GetProperty("message");
        message.GetProperty("token").GetString().Should().Be("device-1");
        message.GetProperty("notification").GetProperty("title").GetString().Should().Be("Hello");
        message.GetProperty("data").GetProperty("linkUrl").GetString().Should().Be("/today");
        fcm.Sends[0].Path.Should().Be("/v1/projects/mizan-test/messages:send");
        fcm.Sends[0].Authorization.Should().Be("Bearer access-1");

        // The sign-in assertion is a JWT signed by the service account key.
        var assertion = fcm.LastAssertion!.Split('.');
        assertion.Should().HaveCount(3);
        JsonDocument.Parse(Convert.FromBase64String(Pad(assertion[1]))).RootElement.GetProperty("iss").GetString().Should().Be("svc@mizan-test.iam.gserviceaccount.com");
    }

    [Fact]
    public async Task AnUnregisteredDevice_IsReportedAsGone()
    {
        var fcm = new FakeFcm { SendStatus = HttpStatusCode.NotFound };

        var outcome = await Sender(fcm).SendAsync(new PushMessage("old", "t", null, null, null), CancellationToken.None);

        outcome.Should().Be(PushOutcome.TokenInvalid);
    }

    [Fact]
    public async Task WhenGoogleRefusesToSignIn_TheSendFailsForARetry()
    {
        var fcm = new FakeFcm { TokenStatus = HttpStatusCode.BadRequest };

        var outcome = await Sender(fcm).SendAsync(new PushMessage("d", "t", null, null, null), CancellationToken.None);

        outcome.Should().Be(PushOutcome.Failed);
        fcm.Sends.Should().BeEmpty();
    }

    private static string Pad(string s) => s.Replace('-', '+').Replace('_', '/').PadRight(s.Length + (4 - s.Length % 4) % 4, '=');

    private static FcmPushSender Sender(FakeFcm fcm, string? serviceAccount = null, string projectId = "mizan-test")
    {
        using var rsa = RSA.Create(2048);
        var account = serviceAccount ?? JsonSerializer.Serialize(new
        {
            client_email = "svc@mizan-test.iam.gserviceaccount.com",
            private_key = rsa.ExportPkcs8PrivateKeyPem(),
            token_uri = "https://oauth2.example/token",
        });
        return new FcmPushSender(
            new FakeFactory(fcm),
            Options.Create(new PushOptions { FcmProjectId = projectId, FcmServiceAccountJson = account }),
            NullLogger<FcmPushSender>.Instance);
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FakeFcm : HttpMessageHandler
    {
        public HttpStatusCode SendStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode TokenStatus { get; set; } = HttpStatusCode.OK;
        public int TokenRequests { get; private set; }
        public string? LastAssertion { get; private set; }
        public List<(string Path, string Body, string? Authorization)> Sends { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.RequestUri!.Host == "oauth2.example")
            {
                TokenRequests++;
                LastAssertion = Uri.UnescapeDataString(body.Split('&').First(p => p.StartsWith("assertion=")).Split('=')[1]);
                return new HttpResponseMessage(TokenStatus)
                {
                    Content = new StringContent($$"""{"access_token":"access-{{TokenRequests}}","expires_in":3600}""", Encoding.UTF8, "application/json"),
                };
            }

            Sends.Add((request.RequestUri.AbsolutePath, body, request.Headers.Authorization?.ToString()));
            return new HttpResponseMessage(SendStatus) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }
}
