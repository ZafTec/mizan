using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Common;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoodsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FoodsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FoodDto>> GetFood(Guid id)
    {
        var result = await _mediator.Send(new GetFoodByIdQuery(id));

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<FoodDto>>> SearchFoods([FromQuery] SearchFoodsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<CreateFoodResult>> CreateFood([FromBody] CreateFoodCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetFood), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<UpdateFoodResult>> UpdateFood(Guid id, [FromBody] UpdateFoodCommand command)
    {
        if (id != command.Id)
            return BadRequest("ID mismatch");

        var result = await _mediator.Send(command);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<DeleteFoodResult>> DeleteFood(Guid id)
    {
        var result = await _mediator.Send(new DeleteFoodCommand { Id = id });

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }
}
