using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using System.Security.Claims;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public UsersController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await _mediator.Send(new GetUserQuery(_currentUser.UserId.Value));
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }



    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateUserRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized("User not authenticated");
        }

        var command = new UpdateUserCommand(_currentUser.UserId.Value, request.Name, request.Image);
        var success = await _mediator.Send(command);

        if (!success)
        {
            return NotFound("User not found");
        }

        return NoContent();
    }
}

public record UpdateUserRequest(string? Name, string? Image);

