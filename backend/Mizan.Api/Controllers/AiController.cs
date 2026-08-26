using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Ai;

namespace Mizan.Api.Controllers;

/// <summary>
/// What the user controls about the assistant: what it may see, and what they
/// have spent. Both live here rather than under Nutrition, because neither is
/// about food - see docs/REFOCUS.md §10 and §11.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IMediator _mediator;

    public AiController(IMediator mediator) => _mediator = mediator;

    [HttpGet("consent")]
    public async Task<ActionResult<AiConsentDto>> GetConsent()
        => Ok(await _mediator.Send(new GetAiConsentQuery()));

    [HttpPut("consent")]
    public async Task<ActionResult<AiConsentDto>> UpdateConsent([FromBody] UpdateAiConsentCommand command)
        => Ok(await _mediator.Send(command));

    [HttpGet("usage")]
    public async Task<ActionResult<MyAiUsageDto>> GetUsage([FromQuery] int days = 14)
        => Ok(await _mediator.Send(new GetMyAiUsageQuery(days)));

    [HttpGet("usage/global")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<GlobalAiUsageDto>> GetGlobalUsage()
        => Ok(await _mediator.Send(new GetGlobalAiUsageQuery()));
}
