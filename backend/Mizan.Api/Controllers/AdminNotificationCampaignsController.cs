using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Notifications;
using Mizan.Contracts.Notifications;

namespace Mizan.Api.Controllers;

/// <summary>
/// Notification campaigns and lifecycle messages (docs/ADMIN.md#notification-campaigns). A campaign is
/// saved as a draft, previewed, tested on the administrator, and started with the audience count they
/// confirmed. Every write is a command, so it lands in the audit log.
/// </summary>
[ApiController]
[Route("api/admin/notification-campaigns")]
[Authorize(Policy = "RequireAdmin")]
public class AdminNotificationCampaignsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminNotificationCampaignsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationCampaignDto>>> List() =>
        Ok(await _mediator.Send(new ListNotificationCampaignsQuery()));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationCampaignDto>> Get(Guid id) =>
        Ok(await _mediator.Send(new GetNotificationCampaignQuery(id)));

    [HttpPost]
    public async Task<ActionResult<NotificationCampaignDto>> Create([FromBody] SaveNotificationCampaignRequest request) =>
        Ok(await _mediator.Send(new CreateNotificationCampaignCommand(request)));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotificationCampaignDto>> Update(Guid id, [FromBody] SaveNotificationCampaignRequest request) =>
        Ok(await _mediator.Send(new UpdateNotificationCampaignCommand(id, request)));

    /// <summary>How many people the audience reaches now, in total and on each channel.</summary>
    [HttpPost("audience")]
    public async Task<ActionResult<CampaignAudiencePreviewDto>> Audience([FromBody] CampaignAudienceRequest request) =>
        Ok(await _mediator.Send(new PreviewCampaignAudienceQuery(request)));

    /// <summary>The message as each channel shows it, with the administrator's name filled in.</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<CampaignPreviewDto>> Preview([FromBody] CampaignPreviewRequest request) =>
        Ok(await _mediator.Send(new PreviewCampaignMessageQuery(request)));

    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<CampaignTestResultDto>> Test(Guid id) =>
        Ok(await _mediator.Send(new SendCampaignTestCommand(id)));

    /// <summary>Starts the campaign. A 409 when the audience is no longer the size confirmed.</summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<NotificationCampaignDto>> Activate(Guid id, [FromBody] ActivateNotificationCampaignRequest request) =>
        Ok(await _mediator.Send(new ActivateNotificationCampaignCommand(id, request.AudienceCount)));

    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<NotificationCampaignDto>> Pause(Guid id) =>
        Ok(await _mediator.Send(new PauseNotificationCampaignCommand(id)));

    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<NotificationCampaignDto>> Resume(Guid id) =>
        Ok(await _mediator.Send(new ResumeNotificationCampaignCommand(id)));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<NotificationCampaignDto>> Cancel(Guid id) =>
        Ok(await _mediator.Send(new CancelNotificationCampaignCommand(id)));
}
