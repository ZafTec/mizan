using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Billing;
using Mizan.Application.Commands;
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

    /// <summary>The plans on sale and any deal checkout applies. Public: the landing page reads it.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<BillingPlanDto>>> GetPlans()
    {
        var result = await _mediator.Send(new GetBillingPlansQuery());
        return Ok(result);
    }

    [HttpGet("me")]
    public async Task<ActionResult<MySubscriptionDto>> GetMySubscription()
    {
        var result = await _mediator.Send(new GetMySubscriptionQuery());
        return Ok(result);
    }

    /// <summary>What switching to another plan charges now and next. Changes nothing.</summary>
    [HttpPost("change-plan/preview")]
    public async Task<ActionResult<PlanChangePreviewDto>> PreviewPlanChange([FromBody] PlanChangeRequest request)
    {
        var result = await _mediator.Send(new PreviewPlanChangeQuery(request.PlanId));
        return Ok(result);
    }

    [HttpPost("change-plan")]
    public async Task<ActionResult<MySubscriptionDto>> ChangePlan([FromBody] PlanChangeRequest request)
    {
        var result = await _mediator.Send(new ChangeSubscriptionPlanCommand(request.PlanId));
        return Ok(result);
    }

    /// <summary>Ends the subscription at the close of the paid period. Pro continues until then.</summary>
    [HttpPost("cancel")]
    public async Task<ActionResult<MySubscriptionDto>> Cancel()
    {
        var result = await _mediator.Send(new CancelSubscriptionCommand());
        return Ok(result);
    }

    /// <summary>Withdraws a scheduled cancellation.</summary>
    [HttpPost("resume")]
    public async Task<ActionResult<MySubscriptionDto>> Resume()
    {
        var result = await _mediator.Send(new ResumeSubscriptionCommand());
        return Ok(result);
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<BillingTransactionDto>>> GetTransactions()
    {
        var result = await _mediator.Send(new GetBillingHistoryQuery());
        return Ok(result);
    }

    /// <summary>A short-lived link to one invoice PDF. Minted per request.</summary>
    [HttpGet("transactions/{transactionId}/invoice")]
    public async Task<ActionResult<InvoiceLinkDto>> GetInvoice(string transactionId)
    {
        var result = await _mediator.Send(new GetInvoiceLinkQuery(transactionId));
        return Ok(result);
    }

    /// <summary>
    /// A fresh link to Paddle's hosted portal, for updating a card. Minted per
    /// request, never cached: the links are single-use.
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

public record PlanChangeRequest(Guid PlanId);
