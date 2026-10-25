using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Features;
using Mizan.Contracts.Admin;

namespace Mizan.Api.Controllers;

/// <summary>
/// Feature flags with per-household overrides (docs/ADMIN.md#feature-flags).
/// The keys are the catalog's; any other key is a 404. Every write is a command,
/// so it lands in the audit log.
/// </summary>
[ApiController]
[Route("api/admin/feature-flags")]
[Authorize(Policy = "RequireAdmin")]
public class AdminFeatureFlagsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminFeatureFlagsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FeatureFlagDto>>> List() =>
        Ok(await _mediator.Send(new ListFeatureFlagsQuery()));

    /// <summary>Sets the default for every household without an override.</summary>
    [HttpPut("{key}")]
    public async Task<ActionResult<FeatureFlagDto>> SetDefault(string key, [FromBody] SetFeatureFlagRequest request) =>
        Ok(await _mediator.Send(new SetFeatureFlagDefaultCommand(key, request.Enabled)));

    [HttpGet("{key}/overrides")]
    public async Task<ActionResult<IReadOnlyList<FeatureFlagOverrideDto>>> Overrides(string key) =>
        Ok(await _mediator.Send(new GetFeatureFlagOverridesQuery(key)));

    [HttpPut("{key}/overrides/{householdId:guid}")]
    public async Task<ActionResult<FeatureFlagOverrideDto>> SetOverride(string key, Guid householdId, [FromBody] SetFeatureFlagRequest request) =>
        Ok(await _mediator.Send(new SetFeatureFlagOverrideCommand(key, householdId, request.Enabled)));

    [HttpDelete("{key}/overrides/{householdId:guid}")]
    public async Task<IActionResult> ClearOverride(string key, Guid householdId)
    {
        await _mediator.Send(new ClearFeatureFlagOverrideCommand(key, householdId));
        return NoContent();
    }
}
