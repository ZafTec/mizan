using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MealPlansController : ControllerBase
{
    private readonly IMediator _mediator;

    public MealPlansController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<GetMealPlansResult>> GetMealPlans([FromQuery] GetMealPlansQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MealPlanDetailDto>> GetMealPlanById(Guid id)
    {
        var result = await _mediator.Send(new GetMealPlanByIdQuery(id));
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CreateMealPlanResult>> CreateMealPlan([FromBody] CreateMealPlanCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetMealPlanById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/recipes")]
    public async Task<ActionResult<AddRecipeToMealPlanResult>> AddRecipeToMealPlan(
        Guid id,
        [FromBody] AddRecipeToMealPlanRequest request)
    {
        var command = new AddRecipeToMealPlanCommand
        {
            MealPlanId = id,
            RecipeId = request.RecipeId,
            Date = request.Date,
            MealType = request.MealType,
            Servings = request.Servings
        };
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<DeleteMealPlanResult>> DeleteMealPlan(Guid id)
    {
        var result = await _mediator.Send(new DeleteMealPlanCommand(id));
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }
}

public record AddRecipeToMealPlanRequest
{
    public Guid RecipeId { get; init; }
    public DateOnly Date { get; init; }
    public string MealType { get; init; } = "dinner";
    public decimal Servings { get; init; } = 1;
}
