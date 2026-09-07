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
    [InlineData("/https://evil.example/steal", AppOrigin + "/https://evil.example/steal")]
    [InlineData("/javascript:alert(1)", AppOrigin + "/javascript:alert(1)")]
    [InlineData(AppOrigin + "/history?tab=meals", AppOrigin + "/history?tab=meals")]
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
    [InlineData("/dashboard\n")]
    [InlineData("/dashboard\r\nLocation:https://evil.example")]
    [InlineData("/%2f%2fevil.example")]
    [InlineData(AppOrigin + ".evil.example/steal")]
    [InlineData(AppOrigin + "@evil.example/steal")]
    [InlineData(AppOrigin + ":444/steal")]
    [InlineData(AppOrigin + "/\\evil.example")]
    [InlineData(AppOrigin + "//evil.example/steal")]
    [InlineData("http://mizan.example/steal")]
    public void SafeReturnUrl_FallsBackToTheAppRoot(string? candidate)
    {
        Create().SafeReturnUrl(candidate).Should().Be(AppOrigin + "/");
    }

    [Theory]
    [InlineData("/history?tab=meals")]
    [InlineData("/settings?returnUrl=%2Fhistory%3Ftab%3Dmeals")]
    [InlineData("/https://evil.example/steal")]
    public void SafeReturnUrl_PreservesTheValidatedTargetAcrossOAuth(string candidate)
    {
        var urls = Create();
        var stored = urls.SafeReturnUrl(candidate);

        urls.SafeReturnUrl(stored).Should().Be(stored);
        new Uri(stored).GetLeftPart(UriPartial.Authority).Should().Be(AppOrigin);
    }

    [Theory]
    [InlineData("https://mizan.example/path")]
    [InlineData("https://mizan.example/?q=value")]
    [InlineData("https://mizan.example/#fragment")]
    [InlineData("https://user@mizan.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//mizan.example")]
    public void Constructor_RejectsInvalidAppOrigins(string publicUrl)
    {
        var create = () => new AppUrls(Options.Create(new AppOptions { PublicUrl = publicUrl }));

        create.Should().Throw<InvalidOperationException>();
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
