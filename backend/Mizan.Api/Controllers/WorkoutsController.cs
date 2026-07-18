using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public class WorkoutsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public WorkoutsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<WorkoutSummaryDto>>> GetWorkouts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();

        var result = await _mediator.Send(new GetWorkoutsQuery
        {
            UserId = _currentUser.UserId.Value,
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortOrder = sortOrder,
        });

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<LogWorkoutResult>> LogWorkout([FromBody] LogWorkoutCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetWorkout), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkoutSummaryDto>> GetWorkout(Guid id)
    {
        var result = await _mediator.Send(new GetWorkoutByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateWorkout(Guid id, [FromBody] UpdateWorkoutCommand command)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "id_mismatch", error = "Route and body IDs must match" });
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWorkout(Guid id)
    {
        await _mediator.Send(new DeleteWorkoutCommand(id));
        return NoContent();
    }

    [HttpGet("stats")]
    public async Task<ActionResult<WorkoutStatsDto>> Stats([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
        => Ok(await _mediator.Send(new GetWorkoutStatsQuery(from, to)));

    [HttpGet("draft")]
    public async Task<ActionResult<WorkoutDraftDto>> Draft()
    {
        var draft = await _mediator.Send(new GetWorkoutDraftQuery());
        return draft is null ? NotFound() : Ok(draft);
    }

    [HttpPut("draft")]
    public async Task<IActionResult> SaveDraft([FromBody] SaveWorkoutDraftCommand command)
    {
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("draft")]
    public async Task<IActionResult> DeleteDraft()
    {
        await _mediator.Send(new DeleteWorkoutDraftCommand());
        return NoContent();
    }
}
