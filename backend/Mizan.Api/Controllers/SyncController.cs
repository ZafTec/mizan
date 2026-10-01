using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

/// <summary>
/// How an app that keeps a copy of the user's data stays current without downloading all of it again.
/// See docs/ARCHITECTURE.md#native-clients.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SyncController : ControllerBase
{
    private readonly IMediator _mediator;
    public SyncController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Changes since a moment. Leave <c>since</c> out for the first sync, then send back the
    /// <c>nextSince</c> each answer gives, and keep asking while <c>hasMore</c> is true.
    /// </summary>
    [HttpGet("changes")]
    public async Task<ActionResult<SyncChangesResult>> Changes([FromQuery] DateTime? since, [FromQuery] int limit = 200)
        => Ok(await _mediator.Send(new GetSyncChangesQuery(since, limit)));
}
