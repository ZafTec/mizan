using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Prices;
using Mizan.Contracts.Prices;

namespace Mizan.Api.Controllers;

/// <summary>
/// The prices worker's side of the API (docs/PLANNING.md#service). The worker
/// has no database: it reads its sources here, posts what it scraped, and
/// reports how each run ended. Only the service key acting for nobody gets in.
/// </summary>
[ApiController]
[Route("api/internal/prices")]
[Authorize(Policy = "PricesWorker")]
public sealed class InternalPricesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalPricesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("sources")]
    public async Task<ActionResult<List<PriceSourceDto>>> Sources() =>
        Ok(await _mediator.Send(new GetWorkerPriceSourcesQuery()));

    [HttpPost("observations")]
    public async Task<ActionResult<PriceObservationReceipt>> Observations([FromBody] PriceObservationBatch batch) =>
        Ok(await _mediator.Send(new RecordPriceObservationsCommand(batch.SourceId, batch.Observations ?? [])));

    [HttpPost("sources/{id:guid}/runs")]
    public async Task<IActionResult> Run(Guid id, [FromBody] PriceSourceRunResult result)
    {
        await _mediator.Send(new RecordPriceSourceRunCommand(id, result.Status, result.Error, result.Observations, result.FinishedAt));
        return NoContent();
    }
}
