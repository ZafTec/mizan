using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mizan.Api.Authentication;
using Mizan.Api.Middleware;
using Mizan.Infrastructure.Identity;
using Xunit;

namespace Mizan.Tests.Authentication;

public class ExternalProviderCompatibilityTests
{
    [Fact]
    public void CallbackPreservesTheValidatedReturnTargetStoredBeforeTheOAuthRoundTrip()
    {
        var urls = new AppUrls(Options.Create(new AppOptions { PublicUrl = "https://mizan.example.test" }));
        var storedTarget = urls.SafeReturnUrl("/history?tab=meals");

        var target = ExternalProviders.ReturnUrl(AuthenticatedReturnTarget(storedTarget), urls);

        target.Should().Be("https://mizan.example.test/history?tab=meals");
    }

    [Theory]
    [InlineData("https://evil.example/history")]
    [InlineData("https://mizan.example.test.evil.example/history")]
    [InlineData("https://mizan.example.test@evil.example/history")]
    [InlineData("javascript:alert(1)")]
    public void CallbackDiscardsAnUntrustedStoredReturnTarget(string storedTarget)
    {
        var urls = new AppUrls(Options.Create(new AppOptions { PublicUrl = "https://mizan.example.test" }));

        var target = ExternalProviders.ReturnUrl(AuthenticatedReturnTarget(storedTarget), urls);

        target.Should().Be("https://mizan.example.test/");
    }

    private static AuthenticateResult AuthenticatedReturnTarget(string storedTarget)
    {
        var properties = new AuthenticationProperties();
        properties.Items[ExternalProviders.ReturnUrlKey] = storedTarget;
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity()), properties, ExternalProviders.CookieScheme));
    }

    public static IEnumerable<object[]> UntrustedAuthorizationDestinations()
    {
        foreach (var (provider, host, path, otherEndpoint) in new[]
        {
            ("google", "accounts.google.com", "/o/oauth2/v2/auth", "https://github.com/login/oauth/authorize"),
            ("github", "github.com", "/login/oauth/authorize", "https://accounts.google.com/o/oauth2/v2/auth"),
        })
        {
            foreach (var destination in new[]
            {
                "https://evil.example/authorize",
                $"https://{host}.evil.example{path}",
                $"https://{host}@evil.example{path}",
                $"https://evil@{host}{path}",
                $"http://{host}{path}",
                $"https://{host}:8443{path}",
                $"https://{host}/redirect",
                $"https://{host}{path}#https://evil.example",
                $"//{host}{path}",
                "/https://evil.example",
                "javascript:alert(1)",
                otherEndpoint,
            })
            {
                yield return [provider, destination];
            }
        }
    }

    [Theory]
    [MemberData(nameof(UntrustedAuthorizationDestinations))]
    public async Task UnexpectedAuthorizationDestinationsAreRejectedWithoutRedirecting(string provider, string destination)
    {
        var context = CreateRedirectContext(provider, destination);

        var redirect = () => context.Options.Events.OnRedirectToAuthorizationEndpoint(context);

        await redirect.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unexpected OAuth authorization destination.");
        context.Response.Headers.Location.Should().BeEmpty();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("google", "https://accounts.google.com/o/oauth2/v2/auth")]
    [InlineData("github", "https://github.com/login/oauth/authorize")]
    public async Task AllowlistedRedirectPreservesOAuthStateAndForcesOneLegacyCallback(string provider, string endpoint)
    {
        var context = CreateRedirectContext(provider, endpoint
            + "?client_id=synthetic-client&response_type=code&scope=openid%20email"
            + "&state=opaque%2Bstate%26key%3Dvalue&code_challenge=synthetic-pkce&code_challenge_method=S256"
            + "&redirect_uri=https%3A%2F%2Fevil.example%2Ffirst&redirect_uri=%2F%2Fevil.example%2Fsecond");
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("evil.example");

        await context.Options.Events.OnRedirectToAuthorizationEndpoint(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        var destination = new Uri(context.Response.Headers.Location.ToString());
        destination.GetLeftPart(UriPartial.Path).Should().Be(endpoint);
        destination.UserInfo.Should().BeEmpty();
        destination.Fragment.Should().BeEmpty();
        var query = QueryHelpers.ParseQuery(destination.Query);
        query["client_id"].ToString().Should().Be("synthetic-client");
        query["response_type"].ToString().Should().Be("code");
        query["scope"].ToString().Should().Be("openid email");
        query["state"].ToString().Should().Be("opaque+state&key=value");
        query["code_challenge"].ToString().Should().Be("synthetic-pkce");
        query["code_challenge_method"].ToString().Should().Be("S256");
        query["redirect_uri"].Should().ContainSingle()
            .Which.Should().Be($"https://mizan.example.test/api/auth/callback/{provider}");
    }

    private static RedirectContext<OAuthOptions> CreateRedirectContext(string provider, string destination)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:PublicUrl"] = "https://mizan.example.test",
        }).Build();
        var options = new OAuthOptions();
        ExternalProviders.Configure(options, configuration, provider);
        return new RedirectContext<OAuthOptions>(new DefaultHttpContext(),
            new AuthenticationScheme(provider, provider, typeof(OAuthHandler<OAuthOptions>)),
            options, new AuthenticationProperties(), destination);
    }

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
