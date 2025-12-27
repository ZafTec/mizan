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
public class HouseholdsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public HouseholdsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HouseholdDto>> GetHousehold(Guid id)
    {
        var result = await _mediator.Send(new GetHouseholdQuery(id));
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateHousehold([FromBody] CreateHouseholdRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized("User not authenticated");
        }

        var command = new CreateHouseholdCommand(request.Name, _currentUser.UserId.Value);
        var householdId = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetHousehold), new { id = householdId }, householdId);
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized("User not authenticated");
        }

        var command = new AddHouseholdMemberCommand(id, request.Email, _currentUser.UserId.Value);
        var success = await _mediator.Send(command);

        if (!success)
        {
            return BadRequest("Failed to add member. Check if user exists, is already a member, or if you have permissions.");
        }

        return NoContent();
    }
}

public record CreateHouseholdRequest(string Name);
public record AddMemberRequest(string Email);
