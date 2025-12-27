using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AchievementsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AchievementsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<GetAchievementsResult>> GetAchievements([FromQuery] string? category)
    {
        var result = await _mediator.Send(new GetAchievementsQuery { Category = category });
        return Ok(result);
    }

    [HttpGet("streak")]
    public async Task<ActionResult<GetStreakResult>> GetStreak()
    {
        var result = await _mediator.Send(new GetStreakQuery());
        return Ok(result);
    }

    [HttpPost("streak")]
    public async Task<ActionResult<UpdateStreakResult>> UpdateStreak([FromBody] UpdateStreakCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
