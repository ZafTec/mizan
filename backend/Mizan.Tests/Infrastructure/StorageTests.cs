using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Domain.Media;
using Mizan.Infrastructure.Storage;
using Xunit;

namespace Mizan.Tests.Infrastructure;

public class ImageFormatTests
{
    [Theory]
    [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })]
    [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    public void Detect_RecognisesSignatures(string expected, byte[] header)
    {
        ImageFormat.Detect(header).Should().Be(expected);
    }

    [Fact]
    public void Detect_RecognisesGifAndWebp()
    {
        ImageFormat.Detect(Encoding.ASCII.GetBytes("GIF89a...")).Should().Be("image/gif");

        var webp = new List<byte>();
        webp.AddRange(Encoding.ASCII.GetBytes("RIFF"));
        webp.AddRange(new byte[] { 1, 2, 3, 4 });
        webp.AddRange(Encoding.ASCII.GetBytes("WEBP"));
        ImageFormat.Detect(webp.ToArray()).Should().Be("image/webp");
    }

    /// <summary>
    /// The point of sniffing: a file the browser labels image/png is still
    /// rejected when its bytes say otherwise.
    /// </summary>
    [Fact]
    public void Detect_RejectsThingsThatAreNotImages()
    {
        ImageFormat.Detect(Encoding.ASCII.GetBytes("<?php exec($_GET")).Should().BeNull();
        ImageFormat.Detect(Encoding.ASCII.GetBytes("RIFF____AVI ")).Should().BeNull();
        ImageFormat.Detect(ReadOnlySpan<byte>.Empty).Should().BeNull();
        ImageFormat.Detect(new byte[] { 0xFF, 0xD8 }).Should().BeNull();
    }
}

public class StorageKeyTests
{
    private static readonly DateTime March = new(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_PutsTheFolderAndDateInFront()
    {
        StorageKey.Build(StorageFolder.Avatars, "me.png", March)
            .Should().MatchRegex(@"^avatars/2026/03/[0-9a-f]{32}\.png$");
    }

    /// <summary>
    /// A file name is caller input. It contributes an extension from a fixed
    /// list and nothing else - no directory, no traversal, no original name.
    /// </summary>
    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("shell.php")]
    [InlineData("no-extension")]
    [InlineData("trick.png.exe")]
    public void Build_NeverLetsAFileNameSteerTheKey(string fileName)
    {
        var key = StorageKey.Build(StorageFolder.Recipes, fileName, March);

        key.Should().StartWith("recipes/2026/03/");
        key.Should().NotContain("..");
        key.Split('/').Should().HaveCount(4);
        Path.GetExtension(key).Should().Be(".bin");
    }

    [Fact]
    public void Build_KeepsAKnownImageExtension()
    {
        Path.GetExtension(StorageKey.Build(StorageFolder.Recipes, "a.JPEG", March)).Should().Be(".jpeg");
        Path.GetExtension(StorageKey.Build(StorageFolder.Recipes, "a.webp", March)).Should().Be(".webp");
    }

    [Theory]
    [InlineData("avatars/2026/03/abc.png", true)]
    [InlineData("recipes/2026/03/abc.png", true)]
    [InlineData("etc/passwd", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOurs_OnlyAcceptsKnownFolders(string? key, bool expected)
    {
        StorageKey.IsOurs(key).Should().Be(expected);
    }
}

public class StorageUrlTests
{
    private static S3StorageService Create(string? publicBase = "https://media.example.test/assets/mizan", bool forcePathStyle = true)
        => new(Options.Create(new StorageOptions
        {
            ServiceUrl = "https://s3.example.test/storage",
            PublicBaseUrl = publicBase,
            Bucket = "mizan",
            AccessKeyId = "test-access-key",
            SecretAccessKey = "test-secret-key",
            Region = "us-east-1",
            ForcePathStyle = forcePathStyle,
        }), NullLogger<S3StorageService>.Instance);

    [Theory]
    [InlineData("https://media.example.test")]
    [InlineData("https://media.example.test/mizan/")]
    [InlineData("https://media.example.test/public/images/current")]
    public async Task TryGetKey_RoundTripsTheCompletePublicBasePath(string publicBase)
    {
        using var storage = Create(publicBase);
        const string key = "avatars/2026/09/avatar.png";

        storage.TryGetKey(await storage.GetUrlAsync(key)).Should().Be(key);
    }

    [Theory]
    [InlineData("https://media.example.test/assets/mizan/recipes/2026/09/photo.png")]
    [InlineData("https://MEDIA.example.test:443/assets/mizan/recipes/2026/09/photo.png")]
    [InlineData("https://s3.example.test/storage/mizan/recipes/2026/09/photo.png?X-Amz-Signature=test&X-Amz-Expires=3600")]
    public void TryGetKey_AcceptsPublicAndSignedServiceUrls(string url)
    {
        using var storage = Create();

        storage.TryGetKey(url).Should().Be("recipes/2026/09/photo.png");
    }

    [Theory]
    [InlineData("https://foreign.example.test/assets/mizan/avatars/photo.png")]
    [InlineData("https://media.example.test.foreign.example/assets/mizan/avatars/photo.png")]
    [InlineData("http://media.example.test/assets/mizan/avatars/photo.png")]
    [InlineData("https://media.example.test:8443/assets/mizan/avatars/photo.png")]
    [InlineData("https://user@media.example.test/assets/mizan/avatars/photo.png")]
    [InlineData("https://media.example.test/assets/mizan-other/avatars/photo.png")]
    [InlineData("https://media.example.test/avatars/photo.png")]
    [InlineData("https://s3.example.test/storage/another-bucket/avatars/photo.png")]
    [InlineData("https://s3.example.test/storage/mizan-other/avatars/photo.png")]
    [InlineData("https://s3.example.test/storage/avatars/photo.png")]
    [InlineData("https://s3.example.test/mizan/avatars/photo.png")]
    [InlineData("https://media.example.test/assets/mizan/legacy-media/shared.png")]
    [InlineData("https://media.example.test/assets/mizan/avatars/%2e%2e%2fprivate.png")]
    [InlineData("https://media.example.test/assets/mizan/avatars/photo%5cname.png")]
    [InlineData("https://media.example.test/assets/mizan/avatars/photo%00.png")]
    [InlineData("avatars/photo.png")]
    [InlineData(null)]
    public void TryGetKey_RejectsForeignOriginsAndObjectsOutsideConfiguredBoundaries(string? url)
    {
        using var storage = Create();

        storage.TryGetKey(url).Should().BeNull();
    }

    [Theory]
    [InlineData("https://mizan.s3.example.test/storage/meals/2026/09/photo.png?X-Amz-Signature=test", "meals/2026/09/photo.png")]
    [InlineData("https://another.s3.example.test/storage/meals/2026/09/photo.png", null)]
    [InlineData("https://s3.example.test/storage/mizan/meals/2026/09/photo.png", null)]
    public void TryGetKey_UsesTheBucketHostForVirtualHostedServiceUrls(string url, string? expected)
    {
        using var storage = Create(publicBase: null, forcePathStyle: false);

        storage.TryGetKey(url).Should().Be(expected);
    }
}
