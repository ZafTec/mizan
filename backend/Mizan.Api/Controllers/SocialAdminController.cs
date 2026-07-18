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
    public async Task<ActionResult<ContentReportListResult>> Reports([FromQuery] string status = "Open", [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetContentReportsQuery(status, page, pageSize)));

    [HttpPost("reports/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveReportRequest request)
    {
        await _mediator.Send(new ResolveContentReportCommand(id, request.Action, request.Note));
        return NoContent();
    }
}

public record ResolveReportRequest(string Action, string? Note);
