using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.OAuth;
using Mizan.Domain.Entities;

namespace Mizan.Api.Controllers;

/// <summary>
/// State for long-running MCP calls. Only the MCP service calls this, on behalf
/// of a user, and a task is visible only to the user who started it.
/// </summary>
[ApiController]
[Route("api/McpTasks")]
[Authorize(Policy = "McpService")]
public class McpTasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public McpTasksController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<McpTaskDto>> Create() =>
        StatusCode(StatusCodes.Status201Created, await _mediator.Send(new CreateMcpTaskCommand(null)));

    [HttpGet("{id}")]
    public async Task<ActionResult<McpTaskDto>> Get(string id) =>
        await _mediator.Send(new GetMcpTaskQuery(id)) is { } task ? Ok(task) : NotFound();

    [HttpPost("{id}/complete")]
    public Task<IActionResult> Complete(string id, [FromBody] JsonElement result) =>
        Finish(id, McpTask.Completed, result);

    [HttpPost("{id}/fail")]
    public Task<IActionResult> Fail(string id, [FromBody] JsonElement error) =>
        Finish(id, McpTask.Failed, error);

    [HttpPost("{id}/cancel")]
    public Task<IActionResult> Cancel(string id) => Finish(id, McpTask.Cancelled, null);

    private async Task<IActionResult> Finish(string id, string outcome, JsonElement? json) =>
        await _mediator.Send(new FinishMcpTaskCommand(id, outcome, json?.GetRawText())) ? NoContent() : Conflict(new { error = "The task is finished or does not exist." });
}
