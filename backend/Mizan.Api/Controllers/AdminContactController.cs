using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Common;
using Mizan.Application.Contact;
using Mizan.Contracts.Admin;

namespace Mizan.Api.Controllers;

/// <summary>What people sent through the contact form, and the replies (docs/ADMIN.md#contact).</summary>
[ApiController]
[Route("api/admin/contact")]
[Authorize(Policy = "RequireAdmin")]
public class AdminContactController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminContactController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ContactSubmissionDto>>> List([FromQuery] ListContactSubmissionsQuery query)
        => Ok(await _mediator.Send(query));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContactSubmissionDto>> Get(Guid id)
        => Ok(await _mediator.Send(new GetContactSubmissionQuery(id)));

    [HttpPost("{id:guid}/handled")]
    public async Task<ActionResult<ContactSubmissionDto>> MarkHandled(Guid id)
        => Ok(await _mediator.Send(new MarkContactHandledCommand(id)));

    /// <summary>Emails the sender and marks the message handled. One reply per message (409 for a second).</summary>
    [HttpPost("{id:guid}/reply")]
    public async Task<ActionResult<ContactSubmissionDto>> Reply(Guid id, [FromBody] ContactReplyRequest request)
        => Ok(await _mediator.Send(new ReplyContactCommand(id, request.Body ?? string.Empty)));
}
