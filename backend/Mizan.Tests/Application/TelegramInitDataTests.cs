extern alias telegram;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;
using telegram::Mizan.Telegram.Bot;

namespace Mizan.Tests.Application;

/// <summary>
/// Signs <c>initData</c> the way Telegram does, with a token only the tests know. The algorithm is written out
/// again here rather than borrowed from the code under test, so a change on either side shows up.
/// </summary>
internal static class SignedInitData
{
    public const string Token = "123456:TEST-FAKE-TOKEN";

    public static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 25, 0, TimeSpan.Zero);

    public const string UserJson = """{"id":42,"first_name":"Tester","username":"tester"}""";

    /// <summary>A fresh, valid statement about user 42, as of <see cref="Now"/>.</summary>
    public static string Valid(string token = Token, DateTimeOffset? authDate = null, string user = UserJson) =>
        Build(token, new Dictionary<string, string>
        {
            ["query_id"] = "AAHdF6IQAAAAAN0XohDhrOrc",
            ["user"] = user,
            ["auth_date"] = (authDate ?? Now.AddSeconds(-30)).ToUnixTimeSeconds().ToString(),
        });

    public static string Build(string token, IReadOnlyDictionary<string, string> fields, string? hashOverride = null)
    {
        var dataCheck = string.Join('\n', fields.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}={f.Value}"));
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(token));
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheck)));

        var pairs = fields.Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value)}").Append($"hash={hashOverride ?? hash}");
        return string.Join('&', pairs);
    }
}

public class TelegramInitDataTests
{
    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

    private static TelegramInitDataResult Validate(string initData, string token = SignedInitData.Token, DateTimeOffset? now = null) =>
        TelegramInitData.Validate(initData, token, now ?? SignedInitData.Now, FiveMinutes);

    [Fact]
    public void AStatementTelegramSigned_NamesTheUser()
    {
        var result = Validate(SignedInitData.Valid());

        result.IsValid.Should().BeTrue(result.Reason);
        result.User.Should().Be(new TelegramMiniAppUser(42, "tester", "Tester"));
    }

    /// <summary>
    /// A vector worked out by hand (Python's hmac over the same three fields), so the test does not only agree
    /// with its own helper.
    /// </summary>
    [Fact]
    public void TheHandWorkedVector_Verifies()
    {
        const string initData =
            "auth_date=1761130800&query_id=AAHdF6IQAAAAAN0XohDhrOrc"
            + "&user=%7B%22id%22%3A42%2C%22first_name%22%3A%22Tester%22%2C%22username%22%3A%22tester%22%7D"
            + "&hash=99378bcdc1f0df0d6592fb5a92eda1edaf7ee001bf997859ab5cfb84d45e95c6";
        var signedAt = DateTimeOffset.FromUnixTimeSeconds(1761130800);

        var result = Validate(initData, now: signedAt.AddMinutes(1));

        result.IsValid.Should().BeTrue(result.Reason);
        result.User!.Id.Should().Be(42);
    }

    [Fact]
    public void FieldOrderInTheQueryString_DoesNotMatter()
    {
        var signed = SignedInitData.Valid();
        var shuffled = string.Join('&', signed.Split('&').Reverse());

        Validate(shuffled).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AChangedField_IsRefused()
    {
        // The signature covers the user. Swapping the id for somebody else's keeps everything else intact.
        var tampered = SignedInitData.Valid().Replace("%22id%22%3A42", "%22id%22%3A43");

        var result = Validate(tampered);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("not signed");
    }

    [Fact]
    public void AStatementSignedWithAnotherBotsToken_IsRefused()
    {
        var result = Validate(SignedInitData.Valid(token: "999999:SOME-OTHER-BOT"));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("not signed");
    }

    [Fact]
    public void AStatementOlderThanTheWindow_IsRefused()
    {
        var stale = SignedInitData.Valid(authDate: SignedInitData.Now.AddMinutes(-5).AddSeconds(-1));
        var justInside = SignedInitData.Valid(authDate: SignedInitData.Now.AddMinutes(-5).AddSeconds(1));

        Validate(stale).Reason.Should().Contain("expired");
        Validate(justInside).IsValid.Should().BeTrue();
    }

    [Fact]
    public void TheWindow_IsConfigurable()
    {
        var tenMinutesOld = SignedInitData.Valid(authDate: SignedInitData.Now.AddMinutes(-10));

        TelegramInitData.Validate(tenMinutesOld, SignedInitData.Token, SignedInitData.Now, FiveMinutes).IsValid.Should().BeFalse();
        TelegramInitData.Validate(tenMinutesOld, SignedInitData.Token, SignedInitData.Now, TimeSpan.FromMinutes(15)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AStatementFromTheFuture_IsRefused_BeyondAMinuteOfSkew()
    {
        var skewed = SignedInitData.Valid(authDate: SignedInitData.Now.AddSeconds(50));
        var forged = SignedInitData.Valid(authDate: SignedInitData.Now.AddMinutes(2));

        Validate(skewed).IsValid.Should().BeTrue();
        Validate(forged).Reason.Should().Contain("future");
    }

    [Theory]
    [InlineData("")]
    [InlineData("query_id=abc&user=%7B%22id%22%3A42%7D&auth_date=1761130800")]
    [InlineData("query_id=abc&user=%7B%22id%22%3A42%7D&auth_date=1761130800&hash=")]
    [InlineData("query_id=abc&user=%7B%22id%22%3A42%7D&auth_date=1761130800&hash=not-hex")]
    public void AStatementWithoutAUsableHash_IsRefused(string initData)
    {
        var result = Validate(initData);

        result.IsValid.Should().BeFalse();
        result.User.Should().BeNull();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"first_name":"No id"}""")]
    [InlineData("""{"id":"42"}""")]
    [InlineData("""[42]""")]
    public void AStatementWhoseUserCannotBeRead_IsRefused_EvenWhenSigned(string user)
    {
        var result = Validate(SignedInitData.Valid(user: user));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("user");
    }

    [Fact]
    public void AStatementWithoutAUser_IsRefused()
    {
        var noUser = SignedInitData.Build(SignedInitData.Token, new Dictionary<string, string>
        {
            ["query_id"] = "abc",
            ["auth_date"] = SignedInitData.Now.ToUnixTimeSeconds().ToString(),
        });

        Validate(noUser).Reason.Should().Contain("user");
    }

    [Fact]
    public void ARepeatedField_IsRefused()
    {
        // Two auth_dates: one for the signature to cover and one for a lenient parser to read.
        var doubled = SignedInitData.Valid() + "&auth_date=" + SignedInitData.Now.ToUnixTimeSeconds();

        Validate(doubled).Reason.Should().Contain("malformed");
    }

    [Fact]
    public void WithoutAToken_NothingVerifies()
    {
        TelegramInitData.Validate(SignedInitData.Valid(), null, SignedInitData.Now, FiveMinutes).IsValid.Should().BeFalse();
        TelegramInitData.Validate(SignedInitData.Valid(), "", SignedInitData.Now, FiveMinutes).IsValid.Should().BeFalse();
    }
}
