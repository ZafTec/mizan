using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    public async Task<ActionResult<FoodAnalysisResult>> AnalyzeFoodImage(IFormFile image)
    {
        if (image.Length == 0)
        {
            return BadRequest("No image provided");
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
