using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Common;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class McpTokensController : ControllerBase
{
    private readonly IMediator _mediator;

    public McpTokensController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CreateMcpTokenResult>> CreateToken([FromBody] CreateMcpTokenCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetMyTokens), null, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<McpTokenDto>>> GetMyTokens([FromQuery] GetMcpTokensQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult> RevokeToken(Guid id)
    {
        try
        {
            await _mediator.Send(new RevokeMcpTokenCommand { TokenId = id });
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("validate")]
    [AllowAnonymous] // MCP server calls this endpoint
    public async Task<ActionResult<ValidateTokenResult>> ValidateToken([FromBody] ValidateTokenCommand command)
    {
        var result = await _mediator.Send(command);

        if (!result.IsValid)
        {
            return Unauthorized(new { error = "Invalid or expired token" });
        }

        return Ok(result);
    }

    [HttpPost("usage")]
    [Authorize]
    public async Task<ActionResult> LogUsage([FromBody] LogMcpUsageCommand command)
    {
        await _mediator.Send(command);
        return Ok();
    }

    [HttpGet("analytics")]
    [Authorize]
    public async Task<ActionResult<McpUsageAnalyticsResult>> GetAnalytics([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var result = await _mediator.Send(new GetMcpUsageAnalyticsQuery
        {
            StartDate = startDate,
            EndDate = endDate
        });
        return Ok(result);
    }
}
