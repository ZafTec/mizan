using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<GetAchievementsResult>> GetAchievements([FromQuery] GetAchievementsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("streak")]
    public async Task<ActionResult<GetStreakResult>> GetStreak([FromQuery] string? streakType = "nutrition")
    {
        var result = await _mediator.Send(new GetStreakQuery { StreakType = streakType ?? "nutrition" });
        return Ok(result);
    }
}
