using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExercisesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExercisesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<GetExercisesResult>> GetExercises([FromQuery] GetExercisesQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<ActionResult<CreateExerciseResult>> CreateExercise([FromBody] CreateExerciseCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetExercises), result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExerciseCommand command)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "id_mismatch", error = "Route and body IDs must match" });
        await _mediator.Send(command); return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<IActionResult> Delete(Guid id) { await _mediator.Send(new DeleteExerciseCommand(id)); return NoContent(); }

    [HttpPost("{id:guid}/promote")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Promote(Guid id) { await _mediator.Send(new PromoteExerciseCommand(id)); return NoContent(); }
}
