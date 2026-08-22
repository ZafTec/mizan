using FluentAssertions;
using Microsoft.Extensions.Options;
using Mizan.Infrastructure.Identity;
using Xunit;

namespace Mizan.Tests.Infrastructure;

/// <summary>
/// SafeReturnUrl is the barrier between an OAuth `returnUrl` query parameter
/// and a redirect, so it gets tested like one.
/// </summary>
public class AppUrlsTests
{
    private const string AppOrigin = "https://mizan.example";

    private static AppUrls Create() =>
        new(Options.Create(new AppOptions { PublicUrl = AppOrigin }));

    [Theory]
    [InlineData("/dashboard", AppOrigin + "/dashboard")]
    [InlineData("/meals?date=2026-08-22", AppOrigin + "/meals?date=2026-08-22")]
    [InlineData("/", AppOrigin + "/")]
    public void SafeReturnUrl_KeepsSameOriginPaths(string candidate, string expected)
    {
        Create().SafeReturnUrl(candidate).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.example/steal")]
    [InlineData("//evil.example/steal")]
    [InlineData("/\\evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("dashboard")]
    [InlineData("\\\\evil.example")]
    public void SafeReturnUrl_FallsBackToTheAppRoot(string? candidate)
    {
        Create().SafeReturnUrl(candidate).Should().Be(AppOrigin + "/");
    }

    [Fact]
    public void MailedLinks_PointAtTheWebApp_NotTheApi()
    {
        var urls = Create();

        urls.VerifyEmail("abc-123").Should().Be(AppOrigin + "/verifyemail?token=abc-123");
        urls.ResetPassword("abc-123").Should().Be(AppOrigin + "/reset-password?token=abc-123");
    }

    [Fact]
    public void MailedLinks_EscapeTheToken()
    {
        Create().ResetPassword("a+b/c=").Should().Contain("token=a%2Bb%2Fc%3D");
    }
}
