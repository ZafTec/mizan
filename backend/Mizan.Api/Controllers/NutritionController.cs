using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Ai;
using Mizan.Application.Commands;
using Mizan.Application.Queries;
using Mizan.Contracts.Media;
using Mizan.Contracts.Nutrition;
using Mizan.Application.Features;
using Mizan.Domain.Features;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public class NutritionController : ControllerBase
{
    private readonly IMediator _mediator;

    public NutritionController(IMediator mediator)
    {
        _mediator = mediator;
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
        using var memoryStream = new MemoryStream();
        await image.CopyToAsync(memoryStream);
        return await EstimateAsync(new EstimateFoodFromPhotoCommand(Image: memoryStream.ToArray()));
    }

    /// <summary>
    /// The same analysis for a photo at a public https address, which the API downloads (at most 4 MB, no
    /// redirects). An MCP client passes the address of a photo it holds instead of writing out its bytes.
    /// </summary>
    [HttpPost("ai/analyze-image-url")]
    [Authorize(Policy = "RequirePro")]
    [RequiresFeature(FeatureFlags.AiAssistant)]
    public Task<ActionResult<FoodAnalysisResult>> AnalyzeFoodImageUrl([FromBody] ImageUrlRequest request) =>
        EstimateAsync(new EstimateFoodFromPhotoCommand(ImageUrl: request.ImageUrl ?? string.Empty));

    /// <summary>An estimate's refused input is a 400 with its own error code, as it always was.</summary>
    private async Task<ActionResult<FoodAnalysisResult>> EstimateAsync(IRequest<FoodAnalysisResult> command)
    {
        try
        {
            return Ok(await _mediator.Send(command, HttpContext.RequestAborted));
        }
        catch (FoodEstimateInputException ex)
        {
            return BadRequest(new { errorCode = ex.ErrorCode, error = ex.Message });
        }
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
    public Task<ActionResult<FoodAnalysisResult>> AnalyzeFoodText([FromBody] AnalyzeFoodTextRequest request) =>
        EstimateAsync(new EstimateFoodFromTextCommand(request.Description));

    public const int MaxDescriptionLength = EstimateFoodFromTextCommand.MaxDescriptionLength;
}
