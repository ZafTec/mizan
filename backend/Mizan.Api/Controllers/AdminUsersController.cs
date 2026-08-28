using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Admin;
using Mizan.Application.Common;

namespace Mizan.Api.Controllers;

/// <summary>
/// User administration. This lived in the BetterAuth admin plugin and in
/// Drizzle queries inside Next.js server components; v2 moves it here with the
/// rest of identity - see docs/REFOCUS.md §6.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "RequireAdmin")]
public class AdminUsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminUsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("overview")]
    public async Task<ActionResult<AdminOverviewDto>> Overview()
        => Ok(await _mediator.Send(new GetAdminOverviewQuery()));

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> ListUsers([FromQuery] ListAdminUsersQuery query)
        => Ok(await _mediator.Send(query));

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<AdminUserDetailDto>> GetUser(Guid userId)
        => Ok(await _mediator.Send(new GetAdminUserQuery(userId)));

    [HttpPost("users")]
    public async Task<ActionResult<object>> CreateUser([FromBody] CreateAdminUserCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetUser), new { userId = id }, new { id });
    }

    [HttpPatch("users/{userId:guid}")]
    public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UpdateAdminUserRequest request)
    {
        await _mediator.Send(new UpdateAdminUserCommand(
            userId, request.Role, request.Banned, request.BanReason,
            request.BanExpires, request.EmailVerified, request.NewPassword));
        return NoContent();
    }

    [HttpDelete("users/{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        await _mediator.Send(new DeleteAdminUserCommand(userId));
        return NoContent();
    }

    [HttpDelete("users/{userId:guid}/sessions")]
    public async Task<IActionResult> RevokeUserSessions(Guid userId)
    {
        await _mediator.Send(new RevokeUserSessionsCommand(userId));
        return NoContent();
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<PagedResult<AdminSessionDto>>> ListSessions([FromQuery] ListAdminSessionsQuery query)
        => Ok(await _mediator.Send(query));

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        await _mediator.Send(new RevokeAdminSessionCommand(sessionId));
        return NoContent();
    }

    public record UpdateAdminUserRequest(
        string? Role,
        bool? Banned,
        string? BanReason,
        DateTime? BanExpires,
        bool? EmailVerified,
        string? NewPassword);
}
