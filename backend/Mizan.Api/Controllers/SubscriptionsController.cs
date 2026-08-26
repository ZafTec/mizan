using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public class SubscriptionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubscriptionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("me")]
    public async Task<ActionResult<MySubscriptionDto>> GetMySubscription()
    {
        var result = await _mediator.Send(new GetMySubscriptionQuery());
        return Ok(result);
    }

    /// <summary>
    /// A fresh link to Paddle's hosted portal - cancel, change plan, update a
    /// card. Minted per request, never cached: the links are single-use.
    /// </summary>
    [HttpPost("portal")]
    public async Task<ActionResult<BillingPortalSessionDto>> GetBillingPortal()
    {
        var result = await _mediator.Send(new GetBillingPortalSessionQuery());
        if (result is null)
        {
            return StatusCode(502, new { errorCode = "paddle_unavailable", error = "Could not reach Paddle. Try again in a moment." });
        }

        return Ok(result);
    }
}
