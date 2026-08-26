using System.Text;
using FluentAssertions;
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
