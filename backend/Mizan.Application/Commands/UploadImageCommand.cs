using MediatR;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Media;
using Mizan.Domain.Media;

namespace Mizan.Application.Commands;

/// <summary>
/// Stores an image, given as its bytes or as a public https address the API downloads, and returns
/// its key and address (docs/ARCHITECTURE.md#storage-caching-and-jobs). The bytes decide the type.
/// Exercise media and achievement badges are for administrators; models have their own door.
/// </summary>
public sealed record UploadImageCommand(StorageFolder Folder, string? FileName = null, byte[]? Bytes = null, string? ImageUrl = null)
    : IRequest<UploadedImageResult>, IRedactedAudit
{
    public object AuditDetails => new { Folder, Source = Bytes is null ? "url" : "upload", Size = Bytes?.Length };
}

public sealed class UploadImageCommandHandler : IRequestHandler<UploadImageCommand, UploadedImageResult>
{
    private readonly IStorageService _storage;
    private readonly IRemoteImageFetcher _images;
    private readonly ICurrentUserService _currentUser;

    public UploadImageCommandHandler(IStorageService storage, IRemoteImageFetcher images, ICurrentUserService currentUser)
    {
        _storage = storage;
        _images = images;
        _currentUser = currentUser;
    }

    public async Task<UploadedImageResult> Handle(UploadImageCommand request, CancellationToken cancellationToken)
    {
        var hasUrl = !string.IsNullOrWhiteSpace(request.ImageUrl);
        if (!hasUrl && (request.Bytes is null || request.Bytes.Length == 0))
        {
            throw new DomainValidationException("No file was uploaded.");
        }

        if (hasUrl && request.Bytes is not null)
        {
            throw new DomainValidationException("Give the image as an address or as its bytes, not both.");
        }

        var curated = request.Folder is StorageFolder.Exercises or StorageFolder.Achievements;
        if (request.Folder == StorageFolder.Models || (curated && !_currentUser.IsInRole("admin")))
        {
            throw new DomainValidationException("That folder is not open to uploads.");
        }

        byte[] bytes;
        string contentType;
        string fileName;
        if (hasUrl)
        {
            var image = await _images.FetchAsync(request.ImageUrl!.Trim(), cancellationToken);
            bytes = image.Bytes;
            contentType = image.ContentType;
            fileName = $"upload{ImageFormat.Extension(image.ContentType)}";
        }
        else
        {
            bytes = request.Bytes!;
            contentType = ImageFormat.Detect(bytes.AsSpan(0, Math.Min(ImageFormat.HeaderBytes, bytes.Length)))
                ?? throw new DomainValidationException("That file is not a JPEG, PNG, WebP or GIF image.");
            fileName = string.IsNullOrWhiteSpace(request.FileName) ? $"upload{ImageFormat.Extension(contentType)}" : request.FileName;
        }

        await using var content = new MemoryStream(bytes);
        var stored = await _storage.UploadAsync(new StorageUpload(request.Folder, fileName, contentType, content, bytes.Length), cancellationToken);
        return new UploadedImageResult(stored.Key, stored.Url);
    }
}
