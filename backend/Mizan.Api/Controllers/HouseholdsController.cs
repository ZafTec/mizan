using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HouseholdsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public HouseholdsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    // ---------- User endpoints ----------

    [HttpGet("mine")]
    public async Task<ActionResult<GetMyHouseholdsResult>> GetMine()
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var result = await _mediator.Send(new GetMyHouseholdsQuery(_currentUser.UserId.Value));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HouseholdDto>> GetHousehold(Guid id)
    {
        var result = await _mediator.Send(new GetHouseholdQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateHousehold([FromBody] CreateHouseholdRequest request)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var householdId = await _mediator.Send(new CreateHouseholdCommand(request.Name, _currentUser.UserId.Value));
        return CreatedAtAction(nameof(GetHousehold), new { id = householdId }, householdId);
    }

    // ---------- Active household ----------

    [HttpPut("active")]
    public async Task<ActionResult<SetActiveHouseholdResult>> SetActive([FromBody] SetActiveHouseholdRequest request)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var result = await _mediator.Send(new SetActiveHouseholdCommand(_currentUser.UserId.Value, request.HouseholdId));
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ---------- Invitations ----------

    [HttpGet("{id:guid}/invitations")]
    public async Task<ActionResult<List<HouseholdInvitationAdminDto>>> GetInvitations(Guid id)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var result = await _mediator.Send(new GetHouseholdInvitationsQuery(id, _currentUser.UserId.Value));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/invitations")]
    public async Task<ActionResult<InviteHouseholdMemberResult>> Invite(Guid id, [FromBody] InviteMemberRequest request)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var result = await _mediator.Send(new InviteHouseholdMemberCommand(
            id,
            request.Email,
            string.IsNullOrWhiteSpace(request.Role) ? "member" : request.Role,
            _currentUser.UserId.Value));
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("invitations/{invitationId:guid}/respond")]
    public async Task<ActionResult<RespondToHouseholdInvitationResult>> Respond(Guid invitationId, [FromBody] RespondInvitationRequest request)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var result = await _mediator.Send(new RespondToHouseholdInvitationCommand(
            invitationId,
            _currentUser.UserId.Value,
            request.Action));
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ---------- Member management ----------

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<ActionResult<RemoveHouseholdMemberResult>> RemoveMember(Guid id, Guid userId)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var result = await _mediator.Send(new RemoveHouseholdMemberCommand(id, userId, _currentUser.UserId.Value));
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<ActionResult<LeaveHouseholdResult>> Leave(Guid id)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var result = await _mediator.Send(new LeaveHouseholdCommand(id, _currentUser.UserId.Value));
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ---------- Admin endpoints ----------

    [HttpGet("admin/all")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<PagedResult<AdminHouseholdSummary>>> AdminListAll([FromQuery] GetAllHouseholdsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpDelete("admin/{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<AdminDeleteHouseholdResult>> AdminDelete(Guid id)
    {
        var result = await _mediator.Send(new AdminDeleteHouseholdCommand(id));
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpDelete("admin/{id:guid}/members/{userId:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<RemoveHouseholdMemberResult>> AdminRemoveMember(Guid id, Guid userId)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        // Admins implicitly have remove rights even when not a household member;
        // RemoveHouseholdMemberCommand gates on household role, so we fake
        // admin-level membership at the controller by invoking a dedicated
        // flow, simpler: just require an explicit admin-only path that
        // bypasses the household-role check.
        var result = await _mediator.Send(new AdminRemoveHouseholdMemberCommand(id, userId));
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}

public record CreateHouseholdRequest(string Name);
public record SetActiveHouseholdRequest(Guid? HouseholdId);
public record InviteMemberRequest(string Email, string? Role);
public record RespondInvitationRequest(string Action);
