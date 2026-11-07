using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Common;
using Mizan.Application.Mail;
using Mizan.Contracts.Admin;

namespace Mizan.Api.Controllers;

/// <summary>
/// The email log and announcements (docs/ADMIN.md#mail). Every email the system
/// sends is a row here from the moment it is queued; the body is not kept.
/// </summary>
[ApiController]
[Route("api/admin/mail")]
[Authorize(Policy = "RequireAdmin")]
public class AdminMailController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminMailController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<PagedResult<EmailMessageDto>>> List([FromQuery] ListEmailMessagesQuery query)
        => Ok(await _mediator.Send(query));

    [HttpGet("stats")]
    public async Task<ActionResult<MailStatsDto>> Stats()
        => Ok(await _mediator.Send(new GetMailStatsQuery()));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmailMessageDto>> Get(Guid id)
        => Ok(await _mediator.Send(new GetEmailMessageQuery(id)));

    /// <summary>Queues a message that did not go out once more. Refused (409) for one that was sent.</summary>
    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<EmailMessageDto>> Retry(Guid id)
        => Ok(await _mediator.Send(new RetryEmailMessageCommand(id)));

    /// <summary>How many people an audience reaches, for the confirmation step before sending.</summary>
    [HttpGet("announcements/audience")]
    public async Task<ActionResult<AnnouncementAudienceDto>> Audience([FromQuery] string audience)
        => Ok(await _mediator.Send(new GetAnnouncementAudienceQuery(audience)));

    /// <summary>One email per recipient. An announcement to everyone is limited to one an hour (429).</summary>
    [HttpPost("announcements")]
    public async Task<ActionResult<AnnouncementResultDto>> Announce([FromBody] SendAnnouncementRequest request)
        => Ok(await _mediator.Send(new SendAnnouncementCommand(
            request.Subject, request.Body, request.Audience, request.AnnouncementId)));
}
