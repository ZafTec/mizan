using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Notifications;
using Mizan.Application.Queries;
using Mizan.Contracts.Notifications;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public sealed class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<NotificationListResult>> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetNotificationsQuery(page, pageSize)));

    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> UnreadCount()
        => Ok(new { unreadCount = await _mediator.Send(new GetUnreadNotificationCountQuery()) });

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id) { await _mediator.Send(new MarkNotificationReadCommand(id)); return NoContent(); }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll() { await _mediator.Send(new MarkAllNotificationsReadCommand()); return NoContent(); }

    /// <summary>Which channels each kind of notification uses for the signed-in person, and their quiet hours.</summary>
    [HttpGet("preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> Preferences()
        => Ok(await _mediator.Send(new GetNotificationPreferencesQuery()));

    /// <summary>Changes some kinds' channels or the quiet hours, and answers with every preference.</summary>
    [HttpPut("preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdatePreferences([FromBody] UpdateNotificationPreferencesRequest request)
        => Ok(await _mediator.Send(new UpdateNotificationPreferencesCommand(request.Kinds, request.QuietHoursStart, request.QuietHoursEnd, request.ClearQuietHours)));
}
