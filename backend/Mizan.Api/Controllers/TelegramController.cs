using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Telegram;

namespace Mizan.Api.Controllers;

/// <summary>
/// Account linking, from both ends.
///
/// Two audiences with two policies, deliberately not one. The user endpoints
/// are the person in a browser session issuing and revoking a code; the
/// service endpoints are the bot, which knows a Telegram id and needs to find
/// out whether it means anything. Neither can do the other's job: a browser
/// cannot claim a Telegram id, and the bot cannot mint a code.
/// </summary>
[ApiController]
[Route("api/Telegram")]
public class TelegramController : ControllerBase
{
    private readonly IMediator _mediator;

    public TelegramController(IMediator mediator) => _mediator = mediator;

    // ---- The user's side --------------------------------------------------

    [HttpGet("link")]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<ActionResult<TelegramLinkDto>> GetLink()
        => Ok(await _mediator.Send(new GetTelegramLinkQuery()));

    /// <summary>
    /// A fresh code and the t.me link that carries it. Five minutes, single
    /// use; asking again invalidates the last one.
    /// </summary>
    [HttpPost("link")]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<ActionResult<TelegramLinkCodeDto>> IssueCode()
        => Ok(await _mediator.Send(new IssueTelegramLinkCodeCommand()));

    [HttpDelete("link")]
    [Authorize(Policy = "UserOrMcp")]
    public async Task<IActionResult> Unlink()
    {
        await _mediator.Send(new UnlinkTelegramCommand());
        return NoContent();
    }

    // ---- The bot's side ---------------------------------------------------

    /// <summary>
    /// Who this chat is, or 404. The bot's first call on every message, and
    /// the only thing an unlinked chat can produce.
    /// </summary>
    [HttpGet("resolve/{telegramUserId:long}")]
    [Authorize(Policy = "McpService")]
    public async Task<ActionResult<ResolvedTelegramUser>> Resolve(long telegramUserId)
    {
        var resolved = await _mediator.Send(new ResolveTelegramUserQuery(telegramUserId));
        return resolved is null ? NotFound() : Ok(resolved);
    }

    [HttpPost("resolve")]
    [Authorize(Policy = "McpService")]
    public async Task<ActionResult<TelegramLinkResult>> Consume([FromBody] ConsumeTelegramLinkCommand command)
        => Ok(await _mediator.Send(command));

    /// <summary>Unlinking from the phone, for the user who no longer has the browser.</summary>
    [HttpDelete("resolve/{telegramUserId:long}")]
    [Authorize(Policy = "McpService")]
    public async Task<IActionResult> UnlinkChat(long telegramUserId)
    {
        var removed = await _mediator.Send(new UnlinkTelegramCommand(telegramUserId));
        return removed ? NoContent() : NotFound();
    }
}
