using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Contracts.Coaching;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ChatController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// The latest <paramref name="take"/> messages (50 by default, at most 200), or those before the message
    /// <paramref name="before"/>, oldest first. <c>hasOlder</c> says whether an earlier page remains.
    /// </summary>
    [HttpGet("{relationshipId}")]
    public async Task<ActionResult<ChatConversationDto>> GetConversation(
        Guid relationshipId, [FromQuery] Guid? before = null, [FromQuery] int take = GetChatConversationQuery.DefaultTake)
    {
        var result = await _mediator.Send(new GetChatConversationQuery(relationshipId, before, take));
        if (result == null)
        {
            return NotFound("Conversation not found");
        }
        return Ok(result);
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized("User not authenticated");
        }

        var command = new SendChatMessageCommand(request.ConversationId, _currentUser.UserId.Value, request.Content);
        var result = await _mediator.Send(command);

        return Ok(new { MessageId = result.Id });
    }
}

