using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ChatController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("{relationshipId}")]
    public async Task<ActionResult<ChatConversationDto>> GetConversation(Guid relationshipId)
    {
        var result = await _mediator.Send(new GetChatConversationQuery(relationshipId));
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

public record SendMessageRequest(Guid ConversationId, string Content);
