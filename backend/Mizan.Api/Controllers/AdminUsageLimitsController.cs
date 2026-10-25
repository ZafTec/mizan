using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Usage;
using Mizan.Contracts.Admin;

namespace Mizan.Api.Controllers;

/// <summary>
/// AI allowance overrides by billing plan, household or user
/// (docs/ADMIN.md#usage-limits). <c>scope</c> is Plan, Household or User; a
/// <c>line</c> of none, or "all", is the row for every allowance line. The
/// limits are enforced by IAiQuotaService, nowhere else.
/// </summary>
[ApiController]
[Route("api/admin/usage-limits")]
[Authorize(Policy = "RequireAdmin")]
public class AdminUsageLimitsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminUsageLimitsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Every plan, or every household or user that has an override, with the limits in force.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsageLimitSubjectDto>>> List([FromQuery] string scope = "Plan") =>
        Ok(await _mediator.Send(new ListUsageLimitsQuery(scope)));

    [HttpGet("{scope}/{subjectId:guid}")]
    public async Task<ActionResult<UsageLimitSubjectDto>> Get(string scope, Guid subjectId) =>
        Ok(await _mediator.Send(new GetUsageLimitSubjectQuery(scope, subjectId)));

    /// <summary>Creates or replaces the override for the subject and line in the body.</summary>
    [HttpPut("{scope}/{subjectId:guid}")]
    public async Task<ActionResult<UsageLimitSubjectDto>> Set(string scope, Guid subjectId, [FromBody] SetUsageLimitRequest request) =>
        Ok(await _mediator.Send(new SetUsageLimitCommand(
            scope, subjectId, request.Line, request.DailyRequests, request.DailyTokens,
            request.DailyCostMicros, request.MonthlyCostMicros, request.Note)));

    [HttpDelete("{scope}/{subjectId:guid}")]
    public async Task<IActionResult> Clear(string scope, Guid subjectId, [FromQuery] string? line = null)
    {
        await _mediator.Send(new ClearUsageLimitCommand(scope, subjectId, line));
        return NoContent();
    }
}
