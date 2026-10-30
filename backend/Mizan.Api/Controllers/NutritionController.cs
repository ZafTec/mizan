using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using Mizan.Contracts.Media;
using Mizan.Contracts.Nutrition;
using Mizan.Domain.Media;
using Mizan.Application.Features;
using Mizan.Domain.Features;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public class NutritionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly INutritionAiService _aiService;
    private readonly ICurrentUserService _currentUser;
    private readonly Mizan.Application.Ai.AnalysisPhotos _photos;
    private readonly IRemoteImageFetcher _images;
    private readonly Mizan.Application.Services.FoodAnalysisCatalogue _catalogue;

    public NutritionController(
        IMediator mediator,
        INutritionAiService aiService,
        ICurrentUserService currentUser,
        Mizan.Application.Ai.AnalysisPhotos photos,
        IRemoteImageFetcher images,
        Mizan.Application.Services.FoodAnalysisCatalogue catalogue)
    {
        _catalogue = catalogue;
        _mediator = mediator;
        _aiService = aiService;
        _currentUser = currentUser;
        _photos = photos;
        _images = images;
    }

    [HttpPost("log")]
    public async Task<ActionResult<LogFoodResult>> LogFood([FromBody] LogFoodCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpGet("daily")]
    public async Task<ActionResult<DailyNutritionResult>> GetDailyNutrition([FromQuery] DateOnly? date)
    {
        var query = new GetDailyNutritionQuery
        {
            Date = date ?? DateOnly.FromDateTime(DateTime.UtcNow)
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("ai/analyze-image")]
    [Authorize(Policy = "RequirePro")]
    [RequiresFeature(FeatureFlags.AiAssistant)]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<FoodAnalysisResult>> AnalyzeFoodImage(IFormFile image)
    {
        if (image.Length == 0)
        {
            return BadRequest(new { errorCode = "no_image", error = "No image provided" });
        }

        if (image.Length > 8_000_000)
        {
            return BadRequest(new { errorCode = "image_too_large", error = "Image must be 8 MB or smaller" });
        }

        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        using var memoryStream = new MemoryStream();
        await image.CopyToAsync(memoryStream);
        var imageBytes = memoryStream.ToArray();

        // The bytes decide, not the Content-Type header - same rule the upload
        // endpoint applies (docs/ARCHITECTURE.md#storage-caching-and-jobs).
        var contentType = ImageFormat.Detect(imageBytes.AsSpan(0, Math.Min(ImageFormat.HeaderBytes, imageBytes.Length)));
        if (contentType is null or "image/gif")
        {
            return BadRequest(new { errorCode = "unsupported_image", error = "Image must be JPEG, PNG or WebP" });
        }

        return Ok(await AnalyzeAsync(_currentUser.UserId.Value, imageBytes, contentType));
    }

    /// <summary>
    /// The same analysis for a photo at a public https address, which the API downloads (at most 4 MB, no
    /// redirects). An MCP client passes the address of a photo it holds instead of writing out its bytes.
    /// </summary>
    [HttpPost("ai/analyze-image-url")]
    [Authorize(Policy = "RequirePro")]
    [RequiresFeature(FeatureFlags.AiAssistant)]
    public async Task<ActionResult<FoodAnalysisResult>> AnalyzeFoodImageUrl([FromBody] ImageUrlRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var image = await _images.FetchAsync(request.ImageUrl, cancellationToken);
        if (image.ContentType == "image/gif")
        {
            return BadRequest(new { errorCode = "unsupported_image", error = "Image must be JPEG, PNG or WebP" });
        }

        return Ok(await AnalyzeAsync(_currentUser.UserId.Value, image.Bytes, image.ContentType));
    }

    private async Task<FoodAnalysisResult> AnalyzeAsync(Guid userId, byte[] imageBytes, string contentType)
    {
        var result = await _catalogue.MatchAsync(userId, await _aiService.AnalyzeFoodImageAsync(userId, imageBytes, contentType), HttpContext.RequestAborted);

        // Stored after the analysis, not before: a photo the model could not
        // read is not worth keeping, and this way a storage outage costs the
        // picture rather than the whole request.
        return result with
        {
            ImageUrl = await _photos.StoreAsync(userId, imageBytes, contentType, ImageFormat.Extension(contentType), HttpContext.RequestAborted),
        };
    }

    /// <summary>
    /// Deletes a photo the person sent for an analysis, before its removal 30 days on
    /// (docs/AI.md#logging-a-meal-from-words-or-a-photo). 404 when it is not theirs or is already gone.
    /// </summary>
    [HttpDelete("ai/photo")]
    public async Task<IActionResult> DeleteAnalysisPhoto([FromQuery] string imageUrl, CancellationToken cancellationToken)
    {
        await _mediator.Send(new Mizan.Application.Ai.DeleteAnalysisPhotoCommand(imageUrl), cancellationToken);
        return NoContent();
    }

    /// <summary>A sentence becomes a proposal, the way a photo does. Nothing is logged until the person confirms it.</summary>
    [HttpPost("ai/analyze-text")]
    [Authorize(Policy = "RequirePro")]
    [RequiresFeature(FeatureFlags.AiAssistant)]
    public async Task<ActionResult<FoodAnalysisResult>> AnalyzeFoodText([FromBody] AnalyzeFoodTextRequest request)
    {
        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length == 0)
        {
            return BadRequest(new { errorCode = "no_description", error = "Describe what you ate." });
        }

        if (description.Length > MaxDescriptionLength)
        {
            return BadRequest(new { errorCode = "description_too_long", error = $"Keep the description to {MaxDescriptionLength} characters or fewer." });
        }

        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var userId = _currentUser.UserId.Value;
        return Ok(await _catalogue.MatchAsync(userId, await _aiService.AnalyzeFoodTextAsync(userId, description), HttpContext.RequestAborted));
    }

    public const int MaxDescriptionLength = 600;
}
