using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The v2 identity endpoints, driven the way a browser drives them: register,
/// follow the mailed link, sign in, get a cookie back.
/// </summary>
[Collection("ApiIntegration")]
public class AuthEndpointTests
{
    private const string Password = "correct-horse-battery";

    private readonly ApiTestFixture _fixture;

    public AuthEndpointTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Register_ThenVerify_ThenLogin_IssuesASessionCookie()
    {
        var email = await StartAsync();
        using var client = _fixture.CreateClient();

        var register = await client.PostAsJsonAsync("/api/Auth/register",
            new { Email = email, Password, Name = "Test Person" });
        register.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Unverified accounts cannot sign in, and the failure is distinguishable
        // from a wrong password so the UI can offer to resend.
        var early = await client.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password });
        early.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await _fixture.DrainOutboxAsync();
        var token = _fixture.Email.LastTokenFor(email, "verifyemail");
        token.Should().NotBeNullOrWhiteSpace();

        var verify = await client.PostAsJsonAsync("/api/Auth/verify-email", new { Token = token });
        verify.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await client.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        login.Headers.GetValues("Set-Cookie")
            .Should().Contain(c => c.StartsWith($"{ApiTestFixture.SessionCookieName}=", StringComparison.Ordinal));

        var me = await login.Content.ReadFromJsonAsync<MeResponse>();
        me!.Email.Should().Be(email);
        me.EmailVerified.Should().BeTrue();
        me.HasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyEmail_RejectsAReusedLink()
    {
        var email = await StartAsync();
        using var client = _fixture.CreateClient();

        await client.PostAsJsonAsync("/api/Auth/register", new { Email = email, Password, Name = (string?)null });
        await _fixture.DrainOutboxAsync();
        var token = _fixture.Email.LastTokenFor(email, "verifyemail");

        (await client.PostAsJsonAsync("/api/Auth/verify-email", new { Token = token }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.PostAsJsonAsync("/api/Auth/verify-email", new { Token = token }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_IsUnauthorized()
    {
        var email = await RegisteredAndVerifiedAsync();
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/login",
            new { Email = email, Password = "not-the-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_LocksTheAccountAfterFiveFailures()
    {
        var email = await RegisteredAndVerifiedAsync();
        using var client = _fixture.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password = "wrong" });
        }

        // The correct password now fails too - that is the point of a lockout.
        var response = await client.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password });
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ForgotPassword_ThenReset_SignsOutEverySession()
    {
        var email = await RegisteredAndVerifiedAsync();
        using var client = _fixture.CreateClient();

        var login = await client.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = SessionCookieFrom(login);

        using var signedIn = _fixture.CreateClient();
        signedIn.DefaultRequestHeaders.Add("Cookie", cookie);
        (await signedIn.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsJsonAsync("/api/Auth/forgot-password", new { Email = email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        await _fixture.DrainOutboxAsync();
        var token = _fixture.Email.LastTokenFor(email, "reset-password");
        token.Should().NotBeNullOrWhiteSpace();

        (await client.PostAsJsonAsync("/api/Auth/reset-password",
                new { Token = token, Password = "a-brand-new-password" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await signedIn.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync("/api/Auth/login",
                new { Email = email, Password = "a-brand-new-password" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_SaysNothingAboutUnknownAddresses()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Email.Clear();
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/forgot-password",
            new { Email = "nobody-at-all@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Nothing was queued either: an unknown address must not become a job
        // somebody can see in the admin console.
        await _fixture.DrainOutboxAsync();
        _fixture.Email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Sessions_ListsTheCurrentOneAndRevokesAnother()
    {
        var email = await RegisteredAndVerifiedAsync();
        using var bootstrap = _fixture.CreateClient();

        var first = SessionCookieFrom(
            await bootstrap.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password }));
        var second = SessionCookieFrom(
            await bootstrap.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password }));

        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", second);

        var sessions = await client.GetFromJsonAsync<List<SessionResponse>>("/api/Auth/sessions");
        sessions.Should().HaveCount(2);
        sessions!.Count(s => s.IsCurrent).Should().Be(1);

        var other = sessions.First(s => !s.IsCurrent);
        (await client.DeleteAsync($"/api/Auth/sessions/{other.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var revoked = _fixture.CreateClient();
        revoked.DefaultRequestHeaders.Add("Cookie", first);
        (await revoked.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_KeepsTheCallerSignedIn()
    {
        var email = await RegisteredAndVerifiedAsync();
        using var bootstrap = _fixture.CreateClient();

        var keep = SessionCookieFrom(
            await bootstrap.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password }));
        var other = SessionCookieFrom(
            await bootstrap.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password }));

        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", keep);

        (await client.PostAsJsonAsync("/api/Auth/change-password",
                new { CurrentPassword = Password, NewPassword = "another-good-password" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        using var kicked = _fixture.CreateClient();
        kicked.DefaultRequestHeaders.Add("Cookie", other);
        (await kicked.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> StartAsync()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Email.Clear();
        return $"auth-{Guid.NewGuid():N}@example.com";
    }

    private async Task<string> RegisteredAndVerifiedAsync()
    {
        var email = await StartAsync();
        using var client = _fixture.CreateClient();
        await client.PostAsJsonAsync("/api/Auth/register", new { Email = email, Password, Name = (string?)null });

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        await db.Users.Where(u => u.Email == email)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.EmailVerified, true));

        return email;
    }

    private static string SessionCookieFrom(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var header = response.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith($"{ApiTestFixture.SessionCookieName}=", StringComparison.Ordinal));
        return header.Split(';')[0];
    }

    private sealed record MeResponse(Guid Id, string Email, bool EmailVerified, bool HasPassword);
    private sealed record SessionResponse(Guid Id, bool IsCurrent);
}
