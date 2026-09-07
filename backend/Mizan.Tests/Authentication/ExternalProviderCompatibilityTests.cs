using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Api.Authentication;
using Mizan.Api.Middleware;
using Xunit;

namespace Mizan.Tests.Authentication;

public class ExternalProviderCompatibilityTests
{
    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    public async Task ApiChallengeAndWebCallbackUseTheSameLegacyRedirectUri(string provider)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:PublicUrl"] = "https://mizan.example.test",
            ["App:CookieDomain"] = ".mizan.example.test",
            ["ReverseProxy:KnownProxy"] = "172.18.0.2",
        }).Build();
        using var backchannel = new ProviderBackchannel();
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.Configure<ForwardedHeadersOptions>(options => ReverseProxyHeaders.Configure(options, configuration));
                services.AddAuthentication(ExternalProviders.CookieScheme)
                    .AddCookie(ExternalProviders.CookieScheme)
                    .AddGoogle(options =>
                    {
                        options.ClientId = "synthetic-client";
                        options.ClientSecret = "synthetic-secret";
                        ExternalProviders.Configure(options, configuration, "google");
                        options.BackchannelHttpHandler = backchannel;
                    })
                    .AddGitHub(options =>
                    {
                        options.ClientId = "synthetic-client";
                        options.ClientSecret = "synthetic-secret";
                        ExternalProviders.Configure(options, configuration, "github");
                        options.BackchannelHttpHandler = backchannel;
                    });
            })
            .Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.2");
                    await next(context);
                });
                app.UseForwardedHeaders();
                app.UseAuthentication();
                app.Run(context => context.ChallengeAsync(ExternalProviders.Resolve(provider)!, new AuthenticationProperties
                {
                    RedirectUri = ExternalProviders.CallbackPath,
                }));
            }));
        using var client = server.CreateClient();
        client.BaseAddress = new Uri("http://api.mizan.example.test");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");

        var challenge = await client.GetAsync("/challenge");

        challenge.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        var expectedCallback = $"https://mizan.example.test/api/auth/callback/{provider}";
        query["redirect_uri"].ToString().Should().Be(expectedCallback);

        // Model the browser's domain rules: the API's correlation cookie must
        // also be sent to the callback on the public web hostname.
        var cookies = new CookieContainer();
        foreach (var header in challenge.Headers.GetValues("Set-Cookie"))
        {
            cookies.SetCookies(new Uri("https://api.mizan.example.test"), header);
        }
        var callbackCookie = cookies.GetCookieHeader(new Uri(expectedCallback));
        callbackCookie.Should().Contain(".AspNetCore.Correlation.");
        var callback = new UriBuilder(expectedCallback)
        {
            Scheme = "http",
            Port = -1,
            Query = $"code=synthetic-code&state={Uri.EscapeDataString(query["state"].ToString())}",
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, callback.Uri);
        request.Headers.Add("Cookie", callbackCookie);

        var completed = await client.SendAsync(request);

        completed.StatusCode.Should().Be(HttpStatusCode.Redirect);
        completed.Headers.Location!.ToString().Should().Be(ExternalProviders.CallbackPath);
        backchannel.ExchangedRedirectUri.Should().Be(expectedCallback);
    }

    private sealed class ProviderBackchannel : HttpMessageHandler
    {
        public string? ExchangedRedirectUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body;
            if (request.Method == HttpMethod.Post)
            {
                var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
                ExchangedRedirectUri = form["redirect_uri"].ToString();
                body = """{"access_token":"synthetic-token","token_type":"Bearer","expires_in":3600}""";
            }
            else if (request.RequestUri!.AbsolutePath.EndsWith("/emails", StringComparison.Ordinal))
            {
                body = """[{"email":"legacy@example.test","primary":true,"verified":true}]""";
            }
            else
            {
                body = """{"id":123,"sub":"123","login":"legacy","name":"Legacy User","email":"legacy@example.test","email_verified":true}""";
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
