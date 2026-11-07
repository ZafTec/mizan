using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
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
    private readonly IMediator _mediator;

    public UploadsController(IStorageService storage, IMediator mediator)
    {
        _storage = storage;
        _mediator = mediator;
    }

    [HttpPost("image")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<ActionResult<UploadedImageDto>> UploadImage(
        IFormFile file,
        [FromQuery] StorageFolder folder = StorageFolder.Recipes,
        CancellationToken cancellationToken = default)
    {
        byte[]? bytes = null;
        if (file is { Length: > 0 })
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }

        var stored = await _mediator.Send(new UploadImageCommand(folder, file?.FileName, bytes ?? []), cancellationToken);
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
        var stored = await _mediator.Send(new UploadImageCommand(folder, ImageUrl: request.ImageUrl), cancellationToken);
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
