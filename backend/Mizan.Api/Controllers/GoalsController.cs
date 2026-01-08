using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GoalsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GoalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<UserGoalDto?>> GetCurrentGoal()
    {
        var result = await _mediator.Send(new GetUserGoalQuery());
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CreateUserGoalResult>> CreateGoal([FromBody] CreateUserGoalCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success)
            return BadRequest(result.Message);
        return CreatedAtAction(nameof(GetCurrentGoal), new { id = result.Id }, result);
    }

    [HttpPost("progress")]
    public async Task<ActionResult<RecordGoalProgressResult>> RecordProgress([FromBody] RecordGoalProgressCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success)
            return BadRequest(result.Message);
        return Ok(result);
    }

    [HttpGet("progress")]
    public async Task<ActionResult<GoalProgressHistoryDto>> GetProgressHistory([FromQuery] int days = 30)
    {
        var result = await _mediator.Send(new GetGoalProgressHistoryQuery { Days = days });
        return Ok(result);
    }
}

