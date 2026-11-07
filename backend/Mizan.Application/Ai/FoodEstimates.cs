using MediatR;
using Mizan.Application.Exceptions;
using Mizan.Application.Features;
using Mizan.Application.Interfaces;
using Mizan.Application.Services;
using Mizan.Contracts.Nutrition;
using Mizan.Domain.Features;
using Mizan.Domain.Media;

namespace Mizan.Application.Ai;

/// <summary>
/// An estimate's input the API refuses with 400 and its own <see cref="ErrorCode"/>
/// (no_image, image_too_large, unsupported_image, no_description, description_too_long).
/// </summary>
public sealed class FoodEstimateInputException(string errorCode, string message) : DomainValidationException(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>
/// What is in a photo of a meal, and its macros, for the person to confirm: nothing is logged.
/// Pro only, and it spends the AI allowance. The photo comes as its bytes or as a public https
/// address the API downloads. It is kept for 30 days after the analysis
/// (docs/AI.md#logging-a-meal-from-words-or-a-photo).
/// </summary>
[RequiresFeature(FeatureFlags.AiAssistant)]
public sealed record EstimateFoodFromPhotoCommand(byte[]? Image = null, string? ImageUrl = null) : IRequest<FoodAnalysisResult>, IRedactedAudit
{
    public const int MaxBytes = 8_000_000;

    public object AuditDetails => new { Source = Image is null ? "url" : "upload", Bytes = Image?.Length };
}

/// <summary>The same estimate from a sentence: "two eggs, toast and a coffee". Pro only; nothing is logged.</summary>
[RequiresFeature(FeatureFlags.AiAssistant)]
public sealed record EstimateFoodFromTextCommand(string? Description) : IRequest<FoodAnalysisResult>
{
    public const int MaxDescriptionLength = 600;
}

public sealed class EstimateFoodHandlers :
    IRequestHandler<EstimateFoodFromPhotoCommand, FoodAnalysisResult>,
    IRequestHandler<EstimateFoodFromTextCommand, FoodAnalysisResult>
{
    private const string ProOnly = "This is a Pro feature. Upgrade to use it.";
    private const string Unsupported = "Image must be JPEG, PNG or WebP";

    private readonly ICurrentUserService _currentUser;
    private readonly IEntitlementService _entitlements;
    private readonly INutritionAiService _ai;
    private readonly IRemoteImageFetcher _images;
    private readonly AnalysisPhotos _photos;
    private readonly FoodAnalysisCatalogue _catalogue;

    public EstimateFoodHandlers(
        ICurrentUserService currentUser,
        IEntitlementService entitlements,
        INutritionAiService ai,
        IRemoteImageFetcher images,
        AnalysisPhotos photos,
        FoodAnalysisCatalogue catalogue)
    {
        _currentUser = currentUser;
        _entitlements = entitlements;
        _ai = ai;
        _images = images;
        _photos = photos;
        _catalogue = catalogue;
    }

    public async Task<FoodAnalysisResult> Handle(EstimateFoodFromPhotoCommand request, CancellationToken cancellationToken)
    {
        var userId = await ProUserAsync(cancellationToken);

        var hasUrl = !string.IsNullOrWhiteSpace(request.ImageUrl);
        if (hasUrl && request.Image is not null)
            throw new FoodEstimateInputException("no_image", "Give the photo as an address or as its bytes, not both.");

        byte[] bytes;
        string? contentType;
        if (hasUrl)
        {
            var image = await _images.FetchAsync(request.ImageUrl!.Trim(), cancellationToken);
            bytes = image.Bytes;
            contentType = image.ContentType;
        }
        else
        {
            bytes = request.Image ?? [];
            if (bytes.Length == 0) throw new FoodEstimateInputException("no_image", "No image provided");
            if (bytes.Length > EstimateFoodFromPhotoCommand.MaxBytes)
                throw new FoodEstimateInputException("image_too_large", "Image must be 8 MB or smaller");

            // The bytes decide, not a declared content type (docs/ARCHITECTURE.md#storage-caching-and-jobs).
            contentType = ImageFormat.Detect(bytes.AsSpan(0, Math.Min(ImageFormat.HeaderBytes, bytes.Length)));
        }

        if (contentType is null or "image/gif") throw new FoodEstimateInputException("unsupported_image", Unsupported);

        var result = await _catalogue.MatchAsync(userId, await _ai.AnalyzeFoodImageAsync(userId, bytes, contentType, cancellationToken), cancellationToken);

        // Stored after the analysis, not before: a photo the model could not read is not worth
        // keeping, and this way a storage outage costs the picture rather than the whole request.
        return result with
        {
            ImageUrl = await _photos.StoreAsync(userId, bytes, contentType, ImageFormat.Extension(contentType), cancellationToken),
        };
    }

    public async Task<FoodAnalysisResult> Handle(EstimateFoodFromTextCommand request, CancellationToken cancellationToken)
    {
        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length == 0) throw new FoodEstimateInputException("no_description", "Describe what you ate.");
        if (description.Length > EstimateFoodFromTextCommand.MaxDescriptionLength)
            throw new FoodEstimateInputException("description_too_long",
                $"Keep the description to {EstimateFoodFromTextCommand.MaxDescriptionLength} characters or fewer.");

        var userId = await ProUserAsync(cancellationToken);
        return await _catalogue.MatchAsync(userId, await _ai.AnalyzeFoodTextAsync(userId, description, cancellationToken), cancellationToken);
    }

    private async Task<Guid> ProUserAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        if (!(await _entitlements.GetAsync(userId, cancellationToken)).IsPro) throw new UpgradeRequiredException(ProOnly);
        return userId;
    }
}
