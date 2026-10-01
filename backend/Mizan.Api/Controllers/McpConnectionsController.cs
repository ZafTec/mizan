using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.OAuth;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

/// <summary>
/// The apps a user connected over OAuth. Signed-in users manage their own;
/// the MCP service reports each call it served.
/// </summary>
[ApiController]
[Route("api/McpConnections")]
public class McpConnectionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public McpConnectionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<McpConnectionDto>>> List() =>
        Ok(await _mediator.Send(new ListMcpConnectionsQuery()));

    /// <summary>The permission groups the consent screen and the edit dialog offer.</summary>
    [HttpGet("scopes")]
    [Authorize]
    public ActionResult<List<ScopeGroupView>> Scopes() => Ok(McpScopeCatalog.For(User.IsInRole("admin")));

    [HttpPatch("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMcpConnectionBody body)
    {
        await _mediator.Send(new UpdateMcpConnectionCommand(id, body.Scopes, body.HouseholdMode, body.HouseholdIds));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Revoke(Guid id)
    {
        await _mediator.Send(new RevokeMcpConnectionCommand(id));
        return NoContent();
    }

    [HttpGet("analytics")]
    [Authorize]
    public async Task<ActionResult<McpUsageAnalyticsResult>> Analytics(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] Guid? connectionId) =>
        Ok(await _mediator.Send(new GetMcpUsageAnalyticsQuery
        {
            StartDate = startDate, EndDate = endDate, GrantId = connectionId,
        }));

    /// <summary>Reported by the MCP service for each call it served on a connection's behalf.</summary>
    [HttpPost("usage")]
    [Authorize(Policy = "McpService")]
    public async Task<IActionResult> LogUsage([FromBody] Mizan.Application.Commands.LogMcpUsageCommand command)
    {
        await _mediator.Send(command);
        return Ok();
    }

    public sealed record UpdateMcpConnectionBody(List<string>? Scopes, string? HouseholdMode, List<Guid>? HouseholdIds);
}
