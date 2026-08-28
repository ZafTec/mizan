using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using Mizan.Domain.Media;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public class NutritionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly INutritionAiService _aiService;
    private readonly ICurrentUserService _currentUser;
    private readonly IStorageService _storage;
    private readonly ILogger<NutritionController> _logger;

    public NutritionController(
        IMediator mediator,
        INutritionAiService aiService,
        ICurrentUserService currentUser,
        IStorageService storage,
        ILogger<NutritionController> logger)
    {
        _mediator = mediator;
        _aiService = aiService;
        _currentUser = currentUser;
        _storage = storage;
        _logger = logger;
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
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<FoodAnalysisResult>> AnalyzeFoodImage(IFormFile image)
    {
        if (image.Length == 0)
        {
            return BadRequest("No image provided");
        }

        if (image.Length > 8_000_000)
        {
            return BadRequest("Image must be 8 MB or smaller");
        }

        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        using var memoryStream = new MemoryStream();
        await image.CopyToAsync(memoryStream);
        var imageBytes = memoryStream.ToArray();

        // The bytes decide, not the Content-Type header - same rule the upload
        // endpoint applies (docs/REFOCUS.md §7).
        var contentType = ImageFormat.Detect(imageBytes.AsSpan(0, Math.Min(ImageFormat.HeaderBytes, imageBytes.Length)));
        if (contentType is null or "image/gif")
        {
            return BadRequest("Image must be JPEG, PNG or WebP");
        }

        var result = await _aiService.AnalyzeFoodImageAsync(
            _currentUser.UserId.Value, imageBytes, contentType);

        // Stored after the analysis, not before: a photo the model could not
        // read is not worth keeping, and this way a storage outage costs the
        // picture rather than the whole request.
        return Ok(result with { ImageUrl = await StoreAsync(imageBytes, contentType) });
    }

    private async Task<string?> StoreAsync(byte[] bytes, string contentType)
    {
        try
        {
            await using var stream = new MemoryStream(bytes);
            var stored = await _storage.UploadAsync(new StorageUpload(
                StorageFolder.Meals,
                $"meal{ImageFormat.Extension(contentType)}",
                contentType,
                stream,
                bytes.Length));

            return stored.Url;
        }
        catch (Exception ex)
        {
            // The analysis is the answer; the photo is a record of it. Losing
            // the second must not fail the first.
            _logger.LogWarning(ex, "Could not store an analysed meal photo");
            return null;
        }
    }
}
