using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
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

    public UploadsController(IStorageService storage) => _storage = storage;

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

        // Only an administrator curates exercise media, and models have their own door.
        if (folder == StorageFolder.Models || (folder == StorageFolder.Exercises && !User.IsInRole("admin")))
        {
            throw new DomainValidationException("That folder is not open to uploads.");
        }

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
