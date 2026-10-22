using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Common;
using Mizan.Application.Planning;
using Mizan.Contracts.Planning;

namespace Mizan.Api.Controllers;

/// <summary>
/// Plans, templates and the entries and cook sessions inside them. Routes map
/// one to one onto commands; the rules live in Application and Domain
/// (docs/PLANNING.md). The templates routes are declared before the `{id}`
/// routes so "templates" is never read as an id.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public class MealPlansController : ControllerBase
{
    private readonly IMediator _mediator;

    public MealPlansController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<PagedResult<MealPlanSummaryDto>>> GetMealPlans([FromQuery] GetMealPlansQuery query)
        => Ok(await _mediator.Send(query));

    /// <summary>The viewer's active plan for a date (today by default), or 404 when none covers it.</summary>
    [HttpGet("day")]
    public async Task<ActionResult<PlanDayViewDto>> GetPlanDay([FromQuery] DateOnly? date)
    {
        var result = await _mediator.Send(new GetPlanDayQuery(date));
        if (result is null)
            return NotFound(new { errorCode = "no_active_plan", error = "No active plan covers that date." });
        return Ok(result);
    }

    [HttpGet("templates")]
    public async Task<ActionResult<List<TemplateDto>>> GetTemplates()
        => Ok(await _mediator.Send(new GetTemplatesQuery()));

    [HttpPost("templates")]
    public async Task<ActionResult<MealPlanCreatedResult>> CreateTemplate([FromBody] CreateTemplateCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetMealPlanById), new { id = result.Id }, result);
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateRequest request)
    {
        await _mediator.Send(new UpdateTemplateCommand(id, request));
        return NoContent();
    }

    [HttpDelete("templates/{id:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        await _mediator.Send(new DeleteTemplateCommand(id));
        return NoContent();
    }

    [HttpPost("templates/{id:guid}/apply")]
    public async Task<ActionResult<MealPlanCreatedResult>> ApplyTemplate(Guid id, [FromBody] ApplyTemplateRequest request)
    {
        var result = await _mediator.Send(new ApplyTemplateCommand(id, request.StartDate, request.HouseholdId, request.Name));
        return CreatedAtAction(nameof(GetMealPlanById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MealPlanDetailDto>> GetMealPlanById(Guid id)
    {
        var result = await _mediator.Send(new GetMealPlanByIdQuery(id));
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<MealPlanCreatedResult>> CreateMealPlan([FromBody] CreateMealPlanCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetMealPlanById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMealPlan(Guid id, [FromBody] UpdateMealPlanRequest request)
    {
        await _mediator.Send(new UpdateMealPlanCommand(id, request));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMealPlan(Guid id)
    {
        await _mediator.Send(new DeleteMealPlanCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetMealPlanStatusRequest request)
    {
        await _mediator.Send(new SetMealPlanStatusCommand(id, request.Status));
        return NoContent();
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        await _mediator.Send(new AcceptPlanCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id)
    {
        await _mediator.Send(new DeclinePlanCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/entries")]
    public async Task<ActionResult<PlanEntryCreatedResult>> AddEntry(Guid id, [FromBody] PlanEntryRequest request)
    {
        var result = await _mediator.Send(new AddPlanEntryCommand(id, request));
        return CreatedAtAction(nameof(GetMealPlanById), new { id }, result);
    }

    [HttpPut("{id:guid}/entries/{entryId:guid}")]
    public async Task<IActionResult> UpdateEntry(Guid id, Guid entryId, [FromBody] PlanEntryRequest request)
    {
        await _mediator.Send(new UpdatePlanEntryCommand(id, entryId, request));
        return NoContent();
    }

    [HttpDelete("{id:guid}/entries/{entryId:guid}")]
    public async Task<IActionResult> RemoveEntry(Guid id, Guid entryId)
    {
        await _mediator.Send(new RemovePlanEntryCommand(id, entryId));
        return NoContent();
    }

    /// <summary>Writes the entry to the diary on its date and slot unless the body says otherwise.</summary>
    [HttpPost("{id:guid}/entries/{entryId:guid}/log")]
    public async Task<ActionResult<LogPlanEntryResult>> LogEntry(Guid id, Guid entryId, [FromBody] LogPlanEntryRequest? request)
        => Ok(await _mediator.Send(new LogPlanEntryCommand(id, entryId, request?.Date, request?.MealType)));

    [HttpPost("{id:guid}/prep-sessions")]
    public async Task<ActionResult<PrepSessionCreatedResult>> AddPrepSession(Guid id, [FromBody] PrepSessionRequest request)
    {
        var result = await _mediator.Send(new AddPrepSessionCommand(id, request));
        return CreatedAtAction(nameof(GetMealPlanById), new { id }, result);
    }

    [HttpPut("{id:guid}/prep-sessions/{sessionId:guid}")]
    public async Task<IActionResult> UpdatePrepSession(Guid id, Guid sessionId, [FromBody] PrepSessionRequest request)
    {
        await _mediator.Send(new UpdatePrepSessionCommand(id, sessionId, request));
        return NoContent();
    }

    [HttpDelete("{id:guid}/prep-sessions/{sessionId:guid}")]
    public async Task<IActionResult> RemovePrepSession(Guid id, Guid sessionId)
    {
        await _mediator.Send(new RemovePrepSessionCommand(id, sessionId));
        return NoContent();
    }

    /// <summary>Aggregates the plan into a shopping list: a new one, or the items of an existing one replaced.</summary>
    [HttpPost("{id:guid}/shopping-list")]
    public async Task<ActionResult<GenerateShoppingListResult>> GenerateShoppingList(Guid id, [FromBody] GenerateShoppingListRequest? request)
    {
        var result = await _mediator.Send(new GenerateShoppingListCommand(id, request?.Name, request?.ReplaceListId));
        return Created($"/api/ShoppingLists/{result.ListId}", result);
    }
}
