using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Notifications;
using Mizan.Contracts.Notifications;

namespace Mizan.Api.Controllers;

/// <summary>The signed-in person's reminders (docs/ARCHITECTURE.md#reminders). The bot's buttons use done, snooze and skip.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public sealed class RemindersController : ControllerBase
{
    private readonly IMediator _mediator;
    public RemindersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<ReminderListResult>> List()
        => Ok(await _mediator.Send(new ListRemindersQuery()));

    /// <summary>Creates a reminder. Send an <c>Idempotency-Key</c> so a retry does not add a second one.</summary>
    [HttpPost]
    public async Task<ActionResult<ReminderDto>> Create([FromBody] CreateReminderRequest request)
        => Ok(await _mediator.Send(new CreateReminderCommand(request)));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReminderDto>> Update(Guid id, [FromBody] UpdateReminderRequest request)
        => Ok(await _mediator.Send(new UpdateReminderCommand(id, request)));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteReminderCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/snooze")]
    public async Task<ActionResult<ReminderActionResult>> Snooze(Guid id, [FromBody] SnoozeReminderRequest? request)
        => Ok(await _mediator.Send(new SnoozeReminderCommand(id, request?.Minutes)));

    [HttpPost("{id:guid}/skip")]
    public async Task<ActionResult<ReminderActionResult>> Skip(Guid id)
        => Ok(await _mediator.Send(new SkipReminderCommand(id)));

    [HttpPost("{id:guid}/done")]
    public async Task<ActionResult<ReminderActionResult>> Done(Guid id)
        => Ok(await _mediator.Send(new CompleteReminderCommand(id)));
}
