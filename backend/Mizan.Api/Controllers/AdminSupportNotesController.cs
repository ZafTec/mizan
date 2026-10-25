using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Admin;
using Mizan.Contracts.Admin;

namespace Mizan.Api.Controllers;

/// <summary>Administrators' notes on a user or a household. Never served to anyone else.</summary>
[ApiController]
[Route("api/admin/support-notes")]
[Authorize(Policy = "RequireAdmin")]
public class AdminSupportNotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminSupportNotesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Newest first. subjectType is User or Household.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupportNoteDto>>> List([FromQuery] string subjectType, [FromQuery] Guid subjectId)
        => Ok(await _mediator.Send(new ListSupportNotesQuery(subjectType, subjectId)));

    [HttpPost]
    public async Task<ActionResult<SupportNoteDto>> Create([FromBody] CreateSupportNoteRequest request)
    {
        var note = await _mediator.Send(new AddSupportNoteCommand(request.SubjectType, request.SubjectId, request.Body));
        return Created($"/api/admin/support-notes/{note.Id}", note);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SupportNoteDto>> Update(Guid id, [FromBody] UpdateSupportNoteRequest request)
        => Ok(await _mediator.Send(new UpdateSupportNoteCommand(id, request.Body)));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteSupportNoteCommand(id));
        return NoContent();
    }
}
