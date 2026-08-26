using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Media;

namespace Mizan.Api.Controllers;

/// <summary>
/// The one door images come through. The browser never talks to the object
/// store directly and never holds a storage credential - docs/REFOCUS.md §7.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UploadsController : ControllerBase
{
    private const long MaxRequestBytes = 6 * 1024 * 1024;

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

    public record UploadedImageDto(string Key, string Url);
}
