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
public class ShoppingListsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ShoppingListsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ShoppingListDto>> GetShoppingList(Guid id)
    {
        var result = await _mediator.Send(new GetShoppingListQuery(id));
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ShoppingListSummaryDto>>> GetShoppingLists([FromQuery] GetShoppingListsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateShoppingList([FromBody] CreateShoppingListRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized("User not authenticated");
        }

        var command = new CreateShoppingListCommand(request.Name, _currentUser.UserId.Value, request.HouseholdId);
        var listId = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetShoppingList), new { id = listId }, listId);
    }

    [HttpPost("{id}/items")]
    public async Task<ActionResult<Guid>> AddItem(Guid id, [FromBody] AddShoppingListItemRequest request)
    {
        var command = new AddShoppingListItemCommand(id, request.ItemName, request.Amount, request.Unit, request.Category);
        var itemId = await _mediator.Send(command);

        if (itemId == null)
        {
            return NotFound("Shopping list not found");
        }

        return Ok(itemId);
    }

    [HttpPatch("items/{itemId}/toggle")]
    public async Task<IActionResult> ToggleItem(Guid itemId, [FromBody] ToggleItemRequest request)
    {
        var command = new ToggleShoppingListItemCommand(itemId, request.IsChecked);
        var success = await _mediator.Send(command);

        if (!success)
        {
            return NotFound("Item not found");
        }

        return NoContent();
    }
}

public record CreateShoppingListRequest(string Name, Guid? HouseholdId);
public record AddShoppingListItemRequest(string ItemName, decimal? Amount, string? Unit, string? Category);
public record ToggleItemRequest(bool IsChecked);
