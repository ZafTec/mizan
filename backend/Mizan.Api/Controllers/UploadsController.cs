using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Media;
using Mizan.Domain.Media;

namespace Mizan.Api.Controllers;

/// <summary>
/// The one door images come through. The browser never talks to the object
/// store directly and never holds a storage credential - docs/ARCHITECTURE.md#storage-caching-and-jobs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public class UploadsController : ControllerBase
{
    private const long MaxRequestBytes = 6 * 1024 * 1024;
    private const long MaxModelRequestBytes = 32 * 1024 * 1024;

    private readonly IStorageService _storage;
    private readonly IRemoteImageFetcher _images;

    public UploadsController(IStorageService storage, IRemoteImageFetcher images)
    {
        _storage = storage;
        _images = images;
    }

    [HttpPost("image")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<ActionResult<UploadedImageDto>> UploadImage(
        IFormFile file,
        [FromQuery] StorageFolder folder = StorageFolder.Recipes,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainValidationException("No file was uploaded.");
        }

        EnsureOpen(folder);

        await using var content = file.OpenReadStream();

        var header = new byte[ImageFormat.HeaderBytes];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var contentType = ImageFormat.Detect(header.AsSpan(0, read))
            ?? throw new DomainValidationException("That file is not a JPEG, PNG, WebP or GIF image.");

        content.Position = 0;

        var stored = await _storage.UploadAsync(
            new StorageUpload(folder, file.FileName, contentType, content, file.Length),
            cancellationToken);

        return Ok(new UploadedImageDto(stored.Key, stored.Url));
    }

    /// <summary>
    /// An image at a public https address, downloaded by the API (at most 4 MB, no redirects) and stored like an
    /// uploaded one. An MCP client passes the address of a photo it holds instead of writing out its bytes.
    /// </summary>
    [HttpPost("image-url")]
    public async Task<ActionResult<UploadedImageDto>> UploadImageUrl(
        [FromBody] ImageUrlRequest request,
        [FromQuery] StorageFolder folder = StorageFolder.Recipes,
        CancellationToken cancellationToken = default)
    {
        EnsureOpen(folder);

        var image = await _images.FetchAsync(request.ImageUrl, cancellationToken);
        await using var content = new MemoryStream(image.Bytes);
        var stored = await _storage.UploadAsync(
            new StorageUpload(folder, $"upload{ImageFormat.Extension(image.ContentType)}", image.ContentType, content, image.Bytes.Length),
            cancellationToken);

        return Ok(new UploadedImageDto(stored.Key, stored.Url));
    }

    /// <summary>Only an administrator curates exercise media and achievement badges, and models have their own door.</summary>
    private void EnsureOpen(StorageFolder folder)
    {
        var curated = folder is StorageFolder.Exercises or StorageFolder.Achievements;
        if (folder == StorageFolder.Models || (curated && !User.IsInRole("admin")))
        {
            throw new DomainValidationException("That folder is not open to uploads.");
        }
    }

    /// <summary>A rigged 3D figure for the app, authored in Blender and exported as .glb. Administrator only.</summary>
    [HttpPost("model")]
    [Authorize(Policy = "RequireAdmin")]
    [RequestSizeLimit(MaxModelRequestBytes)]
    public async Task<ActionResult<UploadedImageDto>> UploadModel(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainValidationException("No file was uploaded.");
        }

        await using var content = file.OpenReadStream();

        var header = new byte[ModelFormat.HeaderBytes];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var contentType = ModelFormat.Detect(header.AsSpan(0, read))
            ?? throw new DomainValidationException("That file is not a glTF 2 binary (.glb) model.");

        content.Position = 0;

        var stored = await _storage.UploadAsync(
            new StorageUpload(StorageFolder.Models, file.FileName, contentType, content, file.Length),
            cancellationToken);

        return Ok(new UploadedImageDto(stored.Key, stored.Url));
    }

    public record UploadedImageDto(string Key, string Url);
}
