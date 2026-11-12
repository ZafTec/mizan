using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mizan.Infrastructure.Ai;
using Xunit;

namespace Mizan.Tests.Infrastructure;

/// <summary>
/// The end-user token a gateway reads on every call: ES256, verifiable with the published key,
/// naming the person only by their opaque id (docs/AI.md#behind-a-gateway).
/// </summary>
public class AiEndUserTokensTests
{
    private static readonly string SigningKey = NewKeyPem();

    [Fact]
    public async Task AToken_VerifiesWithThePublishedKey_AndCarriesOnlyTheRegisteredClaims()
    {
        var time = new ManualTime(DateTimeOffset.Parse("2030-05-06T07:08:09Z"));
        using var tokens = Tokens(Options(), time);
        var userId = Guid.NewGuid();

        var token = await tokens.TokenForAsync(userId);

        var parts = token.Split('.');
        parts.Should().HaveCount(3);
        var header = JsonDocument.Parse(Decode(parts[0])).RootElement;
        header.GetProperty("alg").GetString().Should().Be("ES256");
        header.GetProperty("kid").GetString().Should().Be("key-2030");

        var jwk = (await tokens.GetKeysAsync()).Keys.Should().ContainSingle().Which;
        jwk.Kid.Should().Be("key-2030");
        using var publicKey = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Decode(jwk.X), Y = Decode(jwk.Y) },
        });
        publicKey.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256)
            .Should().BeTrue();

        var claims = Claims(token);
        claims.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("iss", "aud", "sub", "iat", "exp");
        claims.GetProperty("iss").GetString().Should().Be("https://mizan.test");
        claims.GetProperty("aud").GetString().Should().Be("app-test");
        claims.GetProperty("sub").GetString().Should().Be(userId.ToString("D"));
        claims.GetProperty("iat").GetInt64().Should().Be(time.GetUtcNow().ToUnixTimeSeconds());
        (claims.GetProperty("exp").GetInt64() - claims.GetProperty("iat").GetInt64()).Should().Be(600);
        Encoding.UTF8.GetString(Decode(parts[1])).Should().NotContain("@", "no email goes in the token");
    }

    [Fact]
    public async Task AToken_IsReused_UntilAMinuteBeforeItExpires()
    {
        var time = new ManualTime(DateTimeOffset.Parse("2030-05-06T07:08:09Z"));
        using var tokens = Tokens(Options(), time);
        var userId = Guid.NewGuid();

        var first = await tokens.TokenForAsync(userId);
        time.Advance(TimeSpan.FromSeconds(539));
        (await tokens.TokenForAsync(userId)).Should().Be(first);
        (await tokens.TokenForAsync(Guid.NewGuid())).Should().NotBe(first, "each person has their own");

        time.Advance(TimeSpan.FromSeconds(1));
        (await tokens.TokenForAsync(userId)).Should().NotBe(first);
    }

    [Fact]
    public async Task ThePublishedKeys_HaveNoPrivatePart_AndKeepThePreviousKeyDuringARotation()
    {
        using var previous = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = Options();
        options.PreviousKeyId = "key-2029";
        options.PreviousPublicKey = previous.ExportSubjectPublicKeyInfoPem();
        using var tokens = Tokens(options);

        var keys = await tokens.GetKeysAsync();
        var json = JsonSerializer.Serialize(keys);

        keys.Keys.Select(k => k.Kid).Should().Equal("key-2030", "key-2029");
        keys.Keys.Should().OnlyContain(k => k.Kty == "EC" && k.Crv == "P-256" && k.Alg == "ES256" && k.Use == "sig");
        JsonDocument.Parse(json).RootElement.GetProperty("keys").EnumerateArray()
            .Should().OnlyContain(key => key.EnumerateObject().All(p => p.Name != "d"));
    }

    [Fact]
    public async Task Disabled_SendsNoHeader_AndPublishesNoKey()
    {
        using var tokens = Tokens(new AiEndUserOptions());

        (await tokens.IsEnabledAsync()).Should().BeFalse();
        (await tokens.HeadersForAsync(Guid.NewGuid())).Should().BeEmpty();
        (await tokens.GetKeysAsync()).Keys.Should().BeEmpty();
    }

    public static TheoryData<string, Action<AiEndUserOptions>, bool> Configurations => new()
    {
        { "nothing set", o => { o.Header = o.Issuer = o.Audience = o.KeyId = o.PrivateKey = string.Empty; }, true },
        { "everything set", _ => { }, true },
        { "the key on one line, as an env file holds it", o => o.PrivateKey = o.PrivateKey.Trim().ReplaceLineEndings("\\n"), true },
        { "the key missing", o => o.PrivateKey = string.Empty, false },
        { "only the header", o => { o.Issuer = o.Audience = o.KeyId = o.PrivateKey = string.Empty; }, false },
        { "a key that is not PEM", o => o.PrivateKey = "not a key", false },
        { "a P-384 key", o => o.PrivateKey = ECDsa.Create(ECCurve.NamedCurves.nistP384).ExportECPrivateKeyPem(), false },
        { "a public key where the private one goes", o => o.PrivateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfoPem(), false },
        { "a lifetime over an hour", o => o.LifetimeSeconds = 3601, false },
        { "a header name with a space", o => o.Header = "X End User", false },
        { "a previous key without its id", o => o.PreviousPublicKey = ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfoPem(), false },
    };

    [Theory]
    [MemberData(nameof(Configurations))]
    public void TheTokensStayOff_ForAPartOrAWrongConfiguration(string story, Action<AiEndUserOptions> change, bool valid)
    {
        var options = Options();
        change(options);

        var result = new AiEndUserOptionsValidator().Validate(null, options);

        result.Succeeded.Should().Be(valid, story);
        if (!valid) result.FailureMessage.Should().NotContain("PRIVATE KEY", "the key is never quoted");
    }

    internal static AiEndUserOptions Options() => new()
    {
        Header = "X-End-User",
        Issuer = "https://mizan.test",
        Audience = "app-test",
        KeyId = "key-2030",
        PrivateKey = SigningKey,
    };

    internal static AiEndUserTokens Tokens(AiEndUserOptions options, TimeProvider? time = null) =>
        new(new FixedRuntimeOptions<AiEndUserOptions>(options), time ?? TimeProvider.System, NullLogger<AiEndUserTokens>.Instance);

    internal static JsonElement Claims(string token) =>
        JsonDocument.Parse(Decode(token.Split('.')[1])).RootElement.Clone();

    private static byte[] Decode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }

    private static string NewKeyPem()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key.ExportPkcs8PrivateKeyPem();
    }

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
