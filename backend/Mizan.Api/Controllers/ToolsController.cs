using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Tools;
using Mizan.Contracts.Tools;

namespace Mizan.Api.Controllers;

/// <summary>
/// The task tools (docs/MCP.md#task-tools). The manifest lists what the caller may use on one
/// surface, and a call runs one tool through <see cref="ITaskToolRunner"/>, which checks the
/// caller again and fails closed. The MCP service passes both through for its connections.
/// </summary>
[ApiController]
[Route("api/tools")]
[Authorize(Policy = "UserOrMcp")]
public class ToolsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ToolsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<ToolManifestDto>> Manifest([FromQuery] string? surface, CancellationToken cancellationToken)
    {
        if (ToolFilter.ParseSurface(surface) is not { } parsed)
            return BadRequest(new { errorCode = "unknown_surface", error = "surface is mcp or assistant." });
        return Ok(await _mediator.Send(new GetToolManifestQuery(parsed), cancellationToken));
    }

    [HttpPost("{name}/call")]
    public async Task<ActionResult<ToolCallResultDto>> Call(string name, [FromBody] ToolCallRequest request, CancellationToken cancellationToken)
    {
        if (ToolFilter.ParseSurface(request.Surface) is not { } surface)
            return BadRequest(new { errorCode = "unknown_surface", error = "surface is mcp or assistant." });

        var arguments = request.Arguments ?? JsonSerializer.SerializeToElement(new { });
        var result = await _mediator.Send(new CallTaskToolCommand(name, arguments, surface), cancellationToken);
        return Ok(new ToolCallResultDto(
            result.Tool,
            result.Mode,
            result.IsError,
            result.Text,
            result.Data is null ? null : JsonSerializer.SerializeToElement(result.Data, ToolArgs.Json),
            result.ErrorCode,
            result.Needs,
            result.Confirmation));
    }

    /// <summary>The fixed lists the tools take values from: price regions, goal types, meal slots, macro modes, reminder kinds.</summary>
    [HttpGet("reference")]
    public async Task<ActionResult<ToolReferenceDto>> Reference(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetToolReferenceQuery(), cancellationToken));
}
