using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;

namespace Mizan.Api.Controllers;

/// <summary>
/// The prompt console's backend. Prompts are product surface, not a
/// deploy-only constant, so an admin edits, evaluates and rolls one back
/// without a release (docs/REFOCUS.md §12).
///
/// Everything here is admin-only, and everything that changes what production
/// says goes through a command, so the audit log records who moved it.
/// </summary>
[ApiController]
[Route("api/Admin/Ai/Prompts")]
[Authorize(Policy = "RequireAdmin")]
public class AdminAiPromptsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminAiPromptsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AiPromptSummaryDto>>> List()
        => Ok(await _mediator.Send(new ListAiPromptsQuery()));

    [HttpGet("{key}")]
    public async Task<ActionResult<AiPromptDetailDto>> Get(string key)
        => Ok(await _mediator.Send(new GetAiPromptQuery(key)));

    [HttpPost("{key}/drafts")]
    public async Task<ActionResult<AiPromptVersionDto>> CreateDraft(
        string key, [FromBody] CreateDraftRequest body)
        => Ok(await _mediator.Send(
            new CreateAiPromptDraftCommand(key, body.Body, body.SoftPolicy, body.Notes)));

    [HttpPut("versions/{id:guid}")]
    public async Task<ActionResult<AiPromptVersionDto>> UpdateDraft(
        Guid id, [FromBody] UpdateDraftRequest body)
        => Ok(await _mediator.Send(
            new UpdateAiPromptDraftCommand(id, body.Body, body.SoftPolicy, body.Notes)));

    [HttpGet("versions/{id:guid}/evals")]
    public async Task<ActionResult<AiEvalMatrixDto>> GetEvals(Guid id)
        => Ok(await _mediator.Send(new GetAiEvalMatrixQuery(id)));

    [HttpPost("versions/{id:guid}/evals")]
    public async Task<ActionResult<EvalSummary>> RunEvals(Guid id)
        => Ok(await _mediator.Send(new RunAiPromptEvalsCommand(id)));

    /// <summary>Also the rollback: publishing an archived version moves the pointer back.</summary>
    [HttpPost("versions/{id:guid}/publish")]
    public async Task<ActionResult<AiPromptVersionDto>> Publish(Guid id)
        => Ok(await _mediator.Send(new PublishAiPromptVersionCommand(id)));

    public record CreateDraftRequest(string? Body, string? SoftPolicy, string? Notes);

    public record UpdateDraftRequest(string Body, string SoftPolicy, string? Notes);
}
