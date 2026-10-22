using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Common;
using Mizan.Application.Prices;
using Mizan.Contracts.Prices;

namespace Mizan.Api.Controllers;

/// <summary>
/// /admin/prices: regions, sources and Run now, the alias review queue, manual
/// prices, and the price reports people send (docs/PLANNING.md#prices).
/// </summary>
[ApiController]
[Route("api/admin/prices")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminPricesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminPricesController(IMediator mediator) => _mediator = mediator;

    // ---- Regions ----------------------------------------------------------

    [HttpGet("regions")]
    public async Task<ActionResult<List<PriceRegionDto>>> Regions() =>
        Ok(await _mediator.Send(new GetPriceRegionsQuery(IncludeDisabled: true)));

    [HttpPost("regions")]
    public async Task<ActionResult<PriceRegionDto>> CreateRegion([FromBody] CreatePriceRegionRequest request)
    {
        var region = await _mediator.Send(new CreatePriceRegionCommand(request.Code?.Trim().ToUpperInvariant() ?? string.Empty, request.Name, request.Currency?.Trim().ToUpperInvariant() ?? string.Empty, request.Enabled));
        return StatusCode(StatusCodes.Status201Created, region);
    }

    [HttpPut("regions/{code}")]
    public async Task<ActionResult<PriceRegionDto>> UpdateRegion(string code, [FromBody] UpdatePriceRegionRequest request) =>
        Ok(await _mediator.Send(new UpdatePriceRegionCommand(code.Trim().ToUpperInvariant(), request.Name, request.Currency?.Trim().ToUpperInvariant() ?? string.Empty, request.Enabled)));

    // ---- Sources ----------------------------------------------------------

    [HttpGet("sources")]
    public async Task<ActionResult<List<PriceSourceAdminDto>>> Sources([FromQuery] string? regionCode = null) =>
        Ok(await _mediator.Send(new GetPriceSourcesQuery(regionCode)));

    [HttpPost("sources")]
    public async Task<ActionResult<PriceSourceCreatedResult>> CreateSource([FromBody] SavePriceSourceRequest request)
    {
        var id = await _mediator.Send(ToCommand(null, request));
        return StatusCode(StatusCodes.Status201Created, new PriceSourceCreatedResult(id));
    }

    [HttpPut("sources/{id:guid}")]
    public async Task<IActionResult> UpdateSource(Guid id, [FromBody] SavePriceSourceRequest request)
    {
        await _mediator.Send(ToCommand(id, request));
        return NoContent();
    }

    [HttpDelete("sources/{id:guid}")]
    public async Task<IActionResult> DeleteSource(Guid id)
    {
        await _mediator.Send(new DeletePriceSourceCommand(id));
        return NoContent();
    }

    /// <summary>Run now. 409 when the API has no <c>Prices:WorkerUrl</c>; 502 when the worker does not answer.</summary>
    [HttpPost("sources/{id:guid}/run")]
    public async Task<ActionResult<PriceSourceRunStartedDto>> RunSource(Guid id) =>
        StatusCode(StatusCodes.Status202Accepted, await _mediator.Send(new RunPriceSourceCommand(id)));

    // ---- Aliases and the review queue -------------------------------------

    /// <summary><c>queue=true</c> lists the products waiting for review: no food, not ignored.</summary>
    [HttpGet("aliases")]
    public async Task<ActionResult<PagedResult<PriceSourceAliasDto>>> Aliases(
        [FromQuery] Guid? sourceId = null, [FromQuery] bool queue = false, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await _mediator.Send(new GetPriceAliasesQuery(sourceId, queue, search, page, pageSize)));

    [HttpPut("aliases/{id:guid}")]
    public async Task<IActionResult> UpdateAlias(Guid id, [FromBody] UpdatePriceAliasRequest request)
    {
        await _mediator.Send(new UpdatePriceAliasCommand(id, request.FoodId, request.Quantity, request.Unit, request.Ignored));
        return NoContent();
    }

    // ---- Manual prices ----------------------------------------------------

    [HttpPost("prices")]
    public async Task<ActionResult<FoodPriceCreatedResult>> CreatePrice([FromBody] CreateManualPriceRequest request)
    {
        var id = await _mediator.Send(new CreateManualPriceCommand(
            request.FoodId, request.RegionCode, request.Price, request.Quantity, request.Unit, request.Confidence, request.ObservedAt));
        return StatusCode(StatusCodes.Status201Created, new FoodPriceCreatedResult(id));
    }

    /// <summary>Writes the Addis Ababa staples as Estimated prices for the catalogue foods that match. Safe to repeat.</summary>
    [HttpPost("seed-staples")]
    public async Task<ActionResult<SeedStaplePricesResult>> SeedStaples() =>
        Ok(await _mediator.Send(new SeedStaplePricesCommand()));

    // ---- Reports ----------------------------------------------------------

    [HttpGet("reports")]
    public async Task<ActionResult<PagedResult<PriceReportDto>>> Reports(
        [FromQuery] string? status = "Open", [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await _mediator.Send(new GetPriceReportsQuery(status, page, pageSize)));

    [HttpPost("reports/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, [FromBody] ResolvePriceReportRequest? request)
    {
        await _mediator.Send(new ResolvePriceReportCommand(id, Accept: true, request?.Note));
        return NoContent();
    }

    [HttpPost("reports/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ResolvePriceReportRequest? request)
    {
        await _mediator.Send(new ResolvePriceReportCommand(id, Accept: false, request?.Note));
        return NoContent();
    }

    private static SavePriceSourceCommand ToCommand(Guid? id, SavePriceSourceRequest request) => new(
        id, request.Name ?? string.Empty, request.Kind ?? string.Empty, request.RegionCode ?? string.Empty,
        request.Url, request.ScraperKey, request.Schedule, request.Enabled);
}
