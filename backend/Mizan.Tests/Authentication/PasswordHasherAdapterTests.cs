using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Identity;
using Xunit;

namespace Mizan.Tests.Authentication;

public class PasswordHasherAdapterTests
{
    private readonly PasswordHasherAdapter _hasher = new();

    [Theory]
    [InlineData(LegacyPasswordFixtures.Password, LegacyPasswordFixtures.Hash)]
    [InlineData("Passw\u212brd\uff11\uff12\uff13", LegacyPasswordFixtures.UnicodeHash)]
    [InlineData("Passw\u00c5rd123", LegacyPasswordFixtures.UnicodeHash)]
    public void KnownBetterAuthHashesMatchAndRequireRehash(string password, string hash)
    {
        _hasher.Verify(hash, password, out var needsRehash).Should().BeTrue();
        needsRehash.Should().BeTrue();
    }

    [Fact]
    public void WrongLegacyPasswordDoesNotMatchOrRequestRehash()
    {
        _hasher.Verify(LegacyPasswordFixtures.Hash, "wrong-password", out var needsRehash).Should().BeFalse();
        needsRehash.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(MalformedHashes))]
    public void MalformedOrUntaggedLegacyHashesFailCleanly(string? hash)
    {
        _hasher.Verify(hash!, LegacyPasswordFixtures.Password, out var needsRehash).Should().BeFalse();
        needsRehash.Should().BeFalse();
    }

    public static IEnumerable<object?[]> MalformedHashes()
    {
        yield return [null];
        yield return [""];
        yield return ["not-base64"];
        yield return ["AA=="];
        yield return ["AQ=="];
        yield return ["betterauth-scrypt$"];
        yield return [LegacyPasswordFixtures.Hash["betterauth-scrypt$".Length..]];
        yield return [LegacyPasswordFixtures.Hash[..^1]];
        yield return [LegacyPasswordFixtures.Hash + ":extra"];
        yield return [LegacyPasswordFixtures.Hash[..^1] + "g"];
        yield return [LegacyPasswordFixtures.Hash.Replace("000102", "00010A", StringComparison.Ordinal)];
        yield return [LegacyPasswordFixtures.Hash.Replace(":", "$", StringComparison.Ordinal)];
    }

    [Fact]
    public void CurrentIdentityHashesKeepWorkingWithoutRehash()
    {
        var hash = _hasher.Hash("current-password");

        _hasher.Verify(hash, "current-password", out var needsRehash).Should().BeTrue();
        needsRehash.Should().BeFalse();
        _hasher.Verify(hash, "wrong-password").Should().BeFalse();
    }

    [Fact]
    public void OlderIdentityHashesAlsoRequestRehash()
    {
        var oldHasher = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2,
        }));
        var hash = oldHasher.HashPassword(new User(), "current-password");

        _hasher.Verify(hash, "current-password", out var needsRehash).Should().BeTrue();
        needsRehash.Should().BeTrue();
    }
}

internal static class LegacyPasswordFixtures
{
    // Synthetic vectors generated with Node's scrypt and verified independently
    // by the pinned @better-auth/utils 0.4.2 password.node.mjs implementation.
    internal const string Password = "password";
    internal const string Hash = "betterauth-scrypt$000102030405060708090a0b0c0d0e0f:"
        + "3e0919938b5e2b36382eb5a81c23ab15482d02c6a9e295451ff69530660a61b2ca15c113ee385e40a1a76b9345e7d3d519fe53b60d2786758802d888ceb009d1";
    internal const string UnicodeHash = "betterauth-scrypt$000102030405060708090a0b0c0d0e0f:"
        + "6622b7d13b8c93726f5235a7d7e1b35774ecdaec89c7d18755079b96368c18380a695c629dcf517a3166a016c87ac21cd8adb6d90de11b3521f6b53bb1c8e80d";
}
