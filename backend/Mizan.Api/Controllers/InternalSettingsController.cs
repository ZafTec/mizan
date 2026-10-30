using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Settings;
using Mizan.Contracts.Prices;
using Mizan.Contracts.Telegram;

namespace Mizan.Api.Controllers;

/// <summary>
/// The runtime settings the bot service and the prices worker need, read over the internal network
/// (docs/ADMIN.md#runtime-settings). They hold no database or key ring of their own. Only the service
/// key acting for nobody gets in, since the bot's token and webhook secret are in the answer.
/// </summary>
[ApiController]
[Route("api/internal/settings")]
[Authorize(Policy = "InternalService")]
public sealed class InternalSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalSettingsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("telegram-bot")]
    public async Task<ActionResult<TelegramBotSettings>> TelegramBot() =>
        Ok(await _mediator.Send(new GetTelegramBotSettingsQuery()));

    [HttpGet("prices")]
    public async Task<ActionResult<PriceCrawlSettings>> Prices() =>
        Ok(await _mediator.Send(new GetPriceCrawlSettingsQuery()));
}
