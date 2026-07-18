using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/admin/social")]
[Authorize(Policy = "RequireAdmin")]
public sealed class SocialAdminController : ControllerBase
{
    private readonly IMediator _mediator;
    public SocialAdminController(IMediator mediator) => _mediator = mediator;

    [HttpGet("analytics")]
    public async Task<ActionResult<SocialAnalyticsDto>> Analytics() => Ok(await _mediator.Send(new GetSocialAnalyticsQuery()));

    [HttpGet("reports")]
    public async Task<IActionResult> Reports([FromQuery] string status = "Open", [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetContentReportsQuery(status, page, pageSize));
        return Ok(new { result.Items, result.TotalCount, page, pageSize });
    }

    [HttpPost("reports/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveReportRequest request)
    {
        await _mediator.Send(new ResolveContentReportCommand(id, request.Action, request.Note));
        return NoContent();
    }
}

public record ResolveReportRequest(string Action, string? Note);
