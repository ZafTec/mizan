using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Prices;
using Mizan.Contracts.Prices;

namespace Mizan.Api.Controllers;

/// <summary>The regions a person can pick under settings. A food's price and its reports live on the food: /api/Foods/{id}/price.</summary>
[ApiController]
[Route("api/Prices")]
[Authorize(Policy = "UserOrMcp")]
public sealed class PricesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PricesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("regions")]
    public async Task<ActionResult<List<PriceRegionDto>>> Regions() =>
        Ok(await _mediator.Send(new GetPriceRegionsQuery()));
}
