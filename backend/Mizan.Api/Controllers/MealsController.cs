using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MealsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MealsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<FoodDiaryResult>> GetMeals([FromQuery] DateOnly? date)
    {
        var query = new GetFoodDiaryQuery
        {
            Date = date ?? DateOnly.FromDateTime(DateTime.UtcNow)
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CreateFoodDiaryEntryResult>> LogMeal([FromBody] CreateFoodDiaryEntryCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success)
            return BadRequest(result.Message);
        return CreatedAtAction(nameof(GetMeals), new { id = result.Id }, result);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult> DeleteMeal(Guid id)
    {
        var result = await _mediator.Send(new DeleteFoodDiaryEntryCommand { Id = id });
        if (!result.Success)
            return BadRequest(result.Message);
        return Ok(result);
    }
}
