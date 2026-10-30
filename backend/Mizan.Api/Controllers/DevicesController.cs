using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;

namespace Mizan.Api.Controllers;

/// <summary>The phones that receive push notifications for the signed-in user.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DevicesController : ControllerBase
{
    private readonly IMediator _mediator;
    public DevicesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> List() => Ok(await _mediator.Send(new ListDevicesQuery()));

    [HttpPost]
    public async Task<ActionResult<DeviceDto>> Register([FromBody] RegisterDeviceCommand command) => Ok(await _mediator.Send(command));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Unregister(Guid id)
    {
        await _mediator.Send(new UnregisterDeviceCommand(id));
        return NoContent();
    }
}
