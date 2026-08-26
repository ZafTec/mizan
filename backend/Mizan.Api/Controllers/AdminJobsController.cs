using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Admin;
using Mizan.Application.Common;

namespace Mizan.Api.Controllers;

/// <summary>
/// The background queue, from the outside.
///
/// This is the observability half of the outbox. A queue whose failures are
/// invisible is worse than the fire-and-forget call it replaced, because now
/// the failure is durable and still unseen.
/// </summary>
[ApiController]
[Route("api/Admin/Jobs")]
[Authorize(Policy = "RequireAdmin")]
public class AdminJobsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminJobsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminJobDto>>> List([FromQuery] ListAdminJobsQuery query)
        => Ok(await _mediator.Send(query));

    [HttpGet("stats")]
    public async Task<ActionResult<AdminJobStats>> Stats()
        => Ok(await _mediator.Send(new GetAdminJobStatsQuery()));

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id)
    {
        await _mediator.Send(new RetryAdminJobCommand(id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteAdminJobCommand(id));
        return NoContent();
    }
}
