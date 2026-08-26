using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Admin;
using Mizan.Application.Common;

namespace Mizan.Api.Controllers;

/// <summary>
/// Trainer-client relationships, from the outside.
///
/// Read plus one write. An admin can see who is linked to whom and with which
/// grants, and can end a relationship when a client asks and cannot do it
/// themselves. Editing the grants is deliberately absent: those switches
/// belong to the client (docs/REFOCUS.md §11).
/// </summary>
[ApiController]
[Route("api/Admin/Relationships")]
[Authorize(Policy = "RequireAdmin")]
public class AdminRelationshipsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminRelationshipsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminRelationshipDto>>> List(
        [FromQuery] ListAdminRelationshipsQuery query)
        => Ok(await _mediator.Send(query));

    [HttpPost("{id:guid}/end")]
    public async Task<IActionResult> End(Guid id, [FromBody] EndRelationshipRequest? body)
    {
        await _mediator.Send(new EndAdminRelationshipCommand(id, body?.Reason));
        return NoContent();
    }

    public record EndRelationshipRequest(string? Reason);
}
