using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Common;
using Mizan.Application.Planning;
using Mizan.Contracts.Planning;

namespace Mizan.Api.Controllers;

/// <summary>
/// Shopping lists and their items. Household members read a household list;
/// the owner and members allowed to edit lists write to it. Generation from a
/// plan lives under the plan: POST /api/MealPlans/{id}/shopping-list.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOrMcp")]
public class ShoppingListsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ShoppingListsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ShoppingListSummaryDto>>> GetShoppingLists([FromQuery] GetShoppingListsQuery query)
        => Ok(await _mediator.Send(query));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShoppingListDetailDto>> GetShoppingList(Guid id)
    {
        var result = await _mediator.Send(new GetShoppingListQuery(id));
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>Answers with the new list's id, as before, so existing clients keep working.</summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateShoppingList([FromBody] CreateShoppingListRequest request)
    {
        var id = await _mediator.Send(new CreateShoppingListCommand(request.Name, request.HouseholdId));
        return CreatedAtAction(nameof(GetShoppingList), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateShoppingList(Guid id, [FromBody] UpdateShoppingListRequest request)
    {
        await _mediator.Send(new UpdateShoppingListCommand(id, request.Name));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteShoppingList(Guid id)
    {
        await _mediator.Send(new DeleteShoppingListCommand(id));
        return NoContent();
    }

    /// <summary>Answers with the new item's id.</summary>
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<Guid>> AddItem(Guid id, [FromBody] AddShoppingListItemRequest request)
        => Ok(await _mediator.Send(new AddShoppingListItemCommand(id, request)));

    [HttpPut("items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] UpdateShoppingListItemRequest request)
    {
        await _mediator.Send(new UpdateShoppingListItemCommand(itemId, request));
        return NoContent();
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid itemId)
    {
        await _mediator.Send(new DeleteShoppingListItemCommand(itemId));
        return NoContent();
    }

    [HttpPatch("items/{itemId:guid}/toggle")]
    public async Task<IActionResult> ToggleItem(Guid itemId, [FromBody] ToggleShoppingListItemRequest request)
    {
        await _mediator.Send(new ToggleShoppingListItemCommand(itemId, request.IsChecked));
        return NoContent();
    }

    [HttpPost("{id:guid}/clear-checked")]
    public async Task<ActionResult<ClearCheckedResult>> ClearChecked(Guid id)
        => Ok(await _mediator.Send(new ClearCheckedShoppingListItemsCommand(id)));
}
