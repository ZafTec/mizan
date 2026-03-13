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
[Authorize]
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
        return CreatedAtAction(nameof(LogWorkout), new { id = result.Id }, result);
    }
}
