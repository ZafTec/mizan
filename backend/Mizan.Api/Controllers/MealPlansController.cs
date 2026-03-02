using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Common;
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
    public async Task<ActionResult<PagedResult<MealPlanDto>>> GetMealPlans([FromQuery] GetMealPlansQuery query)
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UpdateMealPlanResult>> UpdateMealPlan(
        Guid id,
        [FromBody] UpdateMealPlanRequest request)
    {
        var command = new UpdateMealPlanCommand(id, request.Name, request.StartDate, request.EndDate);
        var result = await _mediator.Send(command);
        if (!result.Success)
            return BadRequest(result);
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

    [HttpDelete("{id:guid}/recipes/{recipeId:guid}")]
    public async Task<ActionResult<RemoveRecipeFromMealPlanResult>> RemoveRecipeFromMealPlan(Guid id, Guid recipeId)
    {
        var result = await _mediator.Send(new RemoveRecipeFromMealPlanCommand(id, recipeId));
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id:guid}/recipes/{recipeId:guid}")]
    public async Task<ActionResult<UpdateMealPlanRecipeResult>> UpdateMealPlanRecipe(
        Guid id,
        Guid recipeId,
        [FromBody] UpdateMealPlanRecipeRequest request)
    {
        var command = new UpdateMealPlanRecipeCommand(id, recipeId, request.Date, request.MealType, request.Servings);
        var result = await _mediator.Send(command);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}

public record UpdateMealPlanRequest
{
    public string Name { get; init; } = "";
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
}

public record AddRecipeToMealPlanRequest
{
    public Guid RecipeId { get; init; }
    public DateOnly Date { get; init; }
    public string MealType { get; init; } = "dinner";
    public decimal Servings { get; init; } = 1;
}

public record UpdateMealPlanRecipeRequest
{
    public DateOnly Date { get; init; }
    public string MealType { get; init; } = "dinner";
    public decimal Servings { get; init; } = 1;
}
