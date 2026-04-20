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

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<CreateAchievementResult>> Create([FromBody] CreateAchievementCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<AchievementDto>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetAchievementsQuery { Page = 1, PageSize = int.MaxValue });
        var match = result.Items.FirstOrDefault(a => a.Id == id);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAchievementCommand command)
    {
        if (id != command.Id) return BadRequest("Route id and body id must match.");
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteAchievementCommand(id));
        return NoContent();
    }

    [HttpGet("analytics")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<GetAchievementAnalyticsResult>> Analytics([FromQuery] GetAchievementAnalyticsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
