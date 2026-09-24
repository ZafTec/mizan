using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Billing;
using Mizan.Application.Commands;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

/// <summary>
/// The Pro catalogue: plans (Paddle prices) and discounts. Every write reaches
/// Paddle before Mizan stores it - docs/ARCHITECTURE.md#billing.
/// </summary>
[ApiController]
[Route("api/admin/billing")]
[Authorize(Policy = "RequireAdmin")]
public class AdminBillingController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminBillingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<AdminBillingCatalogDto>> GetCatalog()
    {
        var result = await _mediator.Send(new GetAdminBillingCatalogQuery());
        return Ok(result);
    }

    [HttpPost("plans")]
    public async Task<ActionResult<CreateBillingPlanResult>> CreatePlan([FromBody] CreateBillingPlanCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPut("plans/{id:guid}")]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdateBillingPlanCommand command)
    {
        if (id != command.Id) return BadRequest("Route id and body id must match.");
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>A new price for the plan. The old price is archived; its subscribers keep it.</summary>
    [HttpPost("plans/{id:guid}/price")]
    public async Task<ActionResult<CreateBillingPlanResult>> ReplacePrice(Guid id, [FromBody] ReplaceBillingPlanPriceCommand command)
    {
        if (id != command.Id) return BadRequest("Route id and body id must match.");
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPost("plans/{id:guid}/archive")]
    public async Task<IActionResult> ArchivePlan(Guid id)
    {
        await _mediator.Send(new ArchiveBillingPlanCommand(id));
        return NoContent();
    }

    /// <summary>Adopts Pro prices that exist in Paddle but not yet in Mizan.</summary>
    [HttpPost("plans/import")]
    public async Task<ActionResult<ImportBillingPlansResult>> ImportPlans()
    {
        var result = await _mediator.Send(new ImportBillingPlansCommand());
        return Ok(result);
    }

    [HttpPost("discounts")]
    public async Task<ActionResult<CreateBillingDiscountResult>> CreateDiscount([FromBody] CreateBillingDiscountCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPost("discounts/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveDiscount(Guid id)
    {
        await _mediator.Send(new ArchiveBillingDiscountCommand(id));
        return NoContent();
    }
}
