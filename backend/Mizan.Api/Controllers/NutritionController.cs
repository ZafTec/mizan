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

    public NutritionController(IMediator mediator, INutritionAiService aiService, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _aiService = aiService;
        _currentUser = currentUser;
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

    // Not Pro-gated. Free gets a small daily allowance and Pro a working one,
    // and IAiQuotaService is what decides - a policy attribute here would be a
    // second, drifting copy of the gating table (docs/REFOCUS.md §10).
    [HttpPost("ai/chat")]
    [Authorize]
    public async Task<ActionResult<AiChatResponse>> ChatWithAi([FromBody] AiChatRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var response = await _aiService.GetNutritionAdviceAsync(
            _currentUser.UserId.Value,
            request.Message);

        return Ok(new AiChatResponse { Response = response });
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
        return Ok(result);
    }
}

public record AiChatRequest
{
    public string Message { get; init; } = string.Empty;
}

public record AiChatResponse
{
    public string Response { get; init; } = string.Empty;
}
