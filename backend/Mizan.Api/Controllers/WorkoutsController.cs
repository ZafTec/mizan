using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkoutsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkoutsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<LogWorkoutResult>> LogWorkout([FromBody] LogWorkoutCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(LogWorkout), new { id = result.Id }, result);
    }
}
