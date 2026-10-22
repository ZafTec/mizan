using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Common;
using Mizan.Application.Prices;
using Mizan.Application.Queries;
using Mizan.Contracts.Prices;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoodsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FoodsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "OptionalUserOrMcp")]
    public async Task<ActionResult<FoodDto>> GetFood(Guid id)
    {
        var result = await _mediator.Send(new GetFoodByIdQuery(id));

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("search")]
    [Authorize(Policy = "OptionalUserOrMcp")]
    public async Task<ActionResult<PagedResult<FoodDto>>> SearchFoods([FromQuery] SearchFoodsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    // Any signed-in user may add a food; it is private to them unless they are an
    // admin curating the shared catalogue. Ownership is enforced in the handler.
    // UserOrMcp, not bare [Authorize]: the default policy accepts JWT only, and
    // these endpoints must stay reachable by the MCP server's API key.
    [HttpPost]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<ActionResult<CreateFoodResult>> CreateFood([FromBody] CreateFoodCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetFood), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<ActionResult<UpdateFoodResult>> UpdateFood(Guid id, [FromBody] UpdateFoodCommand command)
    {
        if (id != command.Id)
            return BadRequest(new { errorCode = "id_mismatch", error = "Route and body IDs must match" });

        var result = await _mediator.Send(command);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<ActionResult<DeleteFoodResult>> DeleteFood(Guid id)
    {
        var result = await _mediator.Send(new DeleteFoodCommand { Id = id });

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>The price in force for the viewer's region. <c>priced</c> is false when there is none.</summary>
    [HttpGet("{id:guid}/price")]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<ActionResult<FoodPriceQuoteDto>> GetPrice(Guid id) =>
        Ok(await _mediator.Send(new GetFoodPriceQuery(id)));

    /// <summary>Report a wrong price. Send an <c>Idempotency-Key</c> to make a retry safe.</summary>
    [HttpPost("{id:guid}/price-reports")]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<ActionResult<PriceReportCreatedResult>> ReportPrice(Guid id, [FromBody] CreatePriceReportRequest request)
    {
        var reportId = await _mediator.Send(new CreatePriceReportCommand(id, request.SuggestedPrice, request.Quantity, request.Unit ?? string.Empty, request.Note));
        return StatusCode(StatusCodes.Status201Created, new PriceReportCreatedResult(reportId));
    }
}
