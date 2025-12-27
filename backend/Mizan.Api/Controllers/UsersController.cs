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
[Authorize]
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

    [HttpGet("me/debug")]
    public async Task<ActionResult<AuthDebugDto>> GetMeDebug()
    {
        var user = HttpContext.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;

        if (!isAuthenticated)
        {
            return Unauthorized(new AuthDebugDto
            {
                IsAuthenticated = false,
                Error = "No authentication token provided or token is invalid"
            });
        }

        var claims = user!.Claims.Select(c => new ClaimDto
        {
            Type = c.Type,
            Value = c.Value
        }).ToList();

        var userId = _currentUser.UserId;
        var email = _currentUser.Email;

        UserDto? userDetails = null;
        if (userId.HasValue)
        {
            userDetails = await _mediator.Send(new GetUserQuery(userId.Value));
        }

        return Ok(new AuthDebugDto
        {
            IsAuthenticated = true,
            UserId = userId,
            Email = email,
            Claims = claims,
            User = userDetails,
            AuthenticationType = user.Identity?.AuthenticationType
        });
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

public class AuthDebugDto
{
    public bool IsAuthenticated { get; set; }
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public List<ClaimDto>? Claims { get; set; }
    public UserDto? User { get; set; }
    public string? AuthenticationType { get; set; }
    public string? Error { get; set; }
}

public class ClaimDto
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
