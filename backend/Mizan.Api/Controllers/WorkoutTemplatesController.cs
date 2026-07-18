using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public sealed class WorkoutTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;
    public WorkoutTemplatesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkoutTemplateDto>>> Get()
        => Ok(await _mediator.Send(new GetWorkoutTemplatesQuery()));

    [HttpPost]
    public async Task<ActionResult<object>> Save([FromBody] SaveWorkoutTemplateCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveWorkoutTemplateCommand command)
    {
        if (command.Id != id) return BadRequest(new { errorCode = "id_mismatch", error = "Route and body IDs must match" });
        await _mediator.Send(command); return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) { await _mediator.Send(new DeleteWorkoutTemplateCommand(id)); return NoContent(); }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<object>> Duplicate(Guid id) => Ok(new { id = await _mediator.Send(new DuplicateWorkoutTemplateCommand(id)) });

    [HttpGet("{id:guid}/next-session")]
    public async Task<ActionResult<NextSessionDto>> Next(Guid id)
    {
        var result = await _mediator.Send(new GetNextTemplateSessionQuery(id));
        return result is null ? NotFound() : Ok(result);
    }
}
