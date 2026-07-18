using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Application.Commands;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public sealed class SocialController : ControllerBase
{
    private readonly IMediator _mediator;
    public SocialController(IMediator mediator) => _mediator = mediator;

    [HttpGet("profile")]
    public async Task<ActionResult<SocialProfileDto>> Profile() { var p = await _mediator.Send(new GetMySocialProfileQuery()); return p is null ? NotFound() : Ok(p); }
    [HttpPost("profile")]
    public async Task<IActionResult> SaveProfile([FromBody] SaveSocialProfileCommand command) { await _mediator.Send(command); return NoContent(); }
    [HttpDelete("profile")]
    public async Task<IActionResult> DeleteProfile() { await _mediator.Send(new DeleteSocialProfileCommand()); return NoContent(); }
    [HttpPost("profile/rotate-token")]
    public async Task<ActionResult<object>> RotateToken() => Ok(new { shareToken = await _mediator.Send(new RotateSocialShareTokenCommand()) });

    [AllowAnonymous]
    [HttpGet("share/{token}")]
    [EnableRateLimiting("AnonymousSocial")]
    public async Task<ActionResult<SocialProfileDto>> Shared(string token) { var p = await _mediator.Send(new GetSharedSocialProfileQuery(token)); return p is null ? NotFound() : Ok(p); }

    [HttpPost("follows")]
    [EnableRateLimiting("SocialWrites")]
    public async Task<ActionResult<object>> Follow([FromBody] RequestFollowCommand command) => Ok(new { id = await _mediator.Send(command) });
    [HttpGet("follows")]
    public async Task<ActionResult<IReadOnlyList<FollowDto>>> Follows([FromQuery] string direction = "in", [FromQuery] string? status = null) => Ok(await _mediator.Send(new GetFollowsQuery(direction, status)));
    [HttpPost("follows/{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, [FromBody] FollowResponse request) { await _mediator.Send(new RespondToFollowCommand(id, request.Accept)); return NoContent(); }
    [HttpDelete("follows/{id:guid}")]
    public async Task<IActionResult> DeleteFollow(Guid id) { await _mediator.Send(new DeleteFollowCommand(id)); return NoContent(); }

    [HttpGet("feed")]
    public async Task<ActionResult<SocialFeedResult>> Feed([FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Ok(await _mediator.Send(new GetSocialFeedQuery(page, pageSize)));
    [HttpPost("feed")]
    [EnableRateLimiting("SocialWrites")]
    public async Task<ActionResult<object>> Publish([FromBody] PublishFeedItemCommand command) => Ok(new { id = await _mediator.Send(command) });
    [HttpDelete("feed/{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id) { await _mediator.Send(new DeleteFeedItemCommand(id)); return NoContent(); }
    [HttpPost("feed/{id:guid}/reactions")]
    [EnableRateLimiting("SocialWrites")]
    public async Task<ActionResult<object>> React(Guid id, [FromBody] ReactionRequest request) => Ok(new { id = await _mediator.Send(new AddFeedReactionCommand(id, request.Emoji)) });
    [HttpDelete("feed/{id:guid}/reactions")]
    public async Task<IActionResult> RemoveReaction(Guid id, [FromQuery] string emoji) { await _mediator.Send(new RemoveFeedReactionCommand(id, emoji)); return NoContent(); }
    [HttpPost("feed/{id:guid}/comments")]
    [EnableRateLimiting("SocialWrites")]
    public async Task<ActionResult<object>> Comment(Guid id, [FromBody] CommentRequest request) => Ok(new { id = await _mediator.Send(new AddFeedCommentCommand(id, request.Body)) });
    [HttpDelete("comments/{id:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id) { await _mediator.Send(new DeleteFeedCommentCommand(id)); return NoContent(); }
    [HttpPost("reports")]
    [EnableRateLimiting("SocialWrites")]
    public async Task<ActionResult<object>> Report([FromBody] ReportContentCommand command) => Ok(new { id = await _mediator.Send(command) });
}

public record FollowResponse(bool Accept);
public record ReactionRequest(string Emoji);
public record CommentRequest(string Body);
