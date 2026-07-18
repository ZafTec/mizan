using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;

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

    [HttpPost("ai/chat")]
    [Authorize(Policy = "RequirePro")]
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

        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp"
        };
        if (!allowedTypes.Contains(image.ContentType))
        {
            return BadRequest("Image must be JPEG, PNG, or WebP");
        }

        using var memoryStream = new MemoryStream();
        await image.CopyToAsync(memoryStream);
        var imageBytes = memoryStream.ToArray();

        var result = await _aiService.AnalyzeFoodImageAsync(imageBytes);
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
