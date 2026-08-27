using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Ai;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Media;

namespace Mizan.Api.Controllers;

/// <summary>
/// What the user controls about the assistant: what it may see, and what they
/// have spent. Both live here rather than under Nutrition, because neither is
/// about food - see docs/REFOCUS.md §10 and §11.
/// </summary>
[ApiController]
[Route("api/[controller]")]
// UserOrMcp, so the assistant is reachable from an MCP client as well as the
// website. The MCP token carries the user's own principal - same person, same
// consent, same quota - so this widens the client, not the access.
[Authorize(Policy = "UserOrMcp")]
public class AiController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly INutritionAiService _ai;
    private readonly ICurrentUserService _currentUser;
    private readonly IStorageService _storage;
    private readonly ILogger<AiController> _logger;

    public AiController(
        IMediator mediator,
        INutritionAiService ai,
        ICurrentUserService currentUser,
        IStorageService storage,
        ILogger<AiController> logger)
    {
        _mediator = mediator;
        _ai = ai;
        _currentUser = currentUser;
        _storage = storage;
        _logger = logger;
    }

    [HttpGet("consent")]
    public async Task<ActionResult<AiConsentDto>> GetConsent()
        => Ok(await _mediator.Send(new GetAiConsentQuery()));

    [HttpPut("consent")]
    public async Task<ActionResult<AiConsentDto>> UpdateConsent([FromBody] UpdateAiConsentCommand command)
        => Ok(await _mediator.Send(command));

    [HttpGet("usage")]
    public async Task<ActionResult<MyAiUsageDto>> GetUsage([FromQuery] int days = 14)
        => Ok(await _mediator.Send(new GetMyAiUsageQuery(days)));

    /// <summary>
    /// One turn. Not Pro-gated: free gets a small daily allowance and Pro a
    /// working one, and IAiQuotaService is what decides - a policy attribute
    /// here would be a second, drifting copy of the gating table
    /// (docs/REFOCUS.md §10).
    /// </summary>
    [HttpPost("chat")]
    public async Task<ActionResult<AiChatTurnDto>> Chat([FromBody] SendAiChatMessageCommand command)
        => Ok(await _mediator.Send(command));

    /// <summary>
    /// A turn with a photo attached. Separate from the JSON endpoint because
    /// it is multipart, not because it is a different conversation - the reply
    /// lands in the same thread.
    /// </summary>
    [HttpPost("chat/image")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<AiChatTurnDto>> ChatWithImage(
        IFormFile image,
        [FromForm] string? message,
        [FromForm] Guid? threadId,
        CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
        {
            throw new DomainValidationException("No image was uploaded.");
        }

        if (image.Length > 8_000_000)
        {
            throw new DomainValidationException("Image must be 8 MB or smaller.");
        }

        using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        // The bytes decide, not the Content-Type header (docs/REFOCUS.md §7).
        var contentType = ImageFormat.Detect(
            bytes.AsSpan(0, Math.Min(ImageFormat.HeaderBytes, bytes.Length)));

        if (contentType is null or "image/gif")
        {
            throw new DomainValidationException("That file is not a JPEG, PNG or WebP image.");
        }

        string? storedUrl = null;
        try
        {
            await using var content = new MemoryStream(bytes);
            var stored = await _storage.UploadAsync(
                new StorageUpload(
                    StorageFolder.Meals,
                    $"chat{ImageFormat.Extension(contentType)}",
                    contentType,
                    content,
                    bytes.Length),
                cancellationToken);

            storedUrl = stored.Url;
        }
        catch (Exception ex)
        {
            // Answering about the photo matters more than keeping it. The turn
            // goes ahead without a stored copy rather than failing outright.
            _logger.LogWarning(ex, "Could not store a chat photo");
        }

        return Ok(await _mediator.Send(
            new SendAiChatMessageCommand(
                threadId,
                string.IsNullOrWhiteSpace(message) ? "What is in this photo?" : message.Trim(),
                new AiImageRef(bytes, contentType),
                storedUrl),
            cancellationToken));
    }

    /// <summary>
    /// Proposals for the rest of today. A POST because it costs money and
    /// writes a ledger row - a GET invites a prefetch that quietly spends
    /// someone's allowance.
    /// </summary>
    [HttpPost("suggestions")]
    public async Task<ActionResult<MealSuggestionResult>> Suggestions()
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        return Ok(await _ai.SuggestMealsAsync(userId));
    }

    /// <summary>
    /// One turn of onboarding. Unlike chat, the model here has tools, so the
    /// response says what it actually did.
    /// </summary>
    [HttpPost("onboarding")]
    public async Task<ActionResult<AiOnboardingTurnDto>> Onboarding(
        [FromBody] SendAiOnboardingMessageCommand command)
        => Ok(await _mediator.Send(command));

    /// <summary>
    /// The setup conversation so far, so a half-finished setup resumes instead
    /// of starting over. 204 when the user has never begun one.
    /// </summary>
    [HttpGet("onboarding")]
    public async Task<ActionResult<AiChatThreadDetailDto>> GetOnboarding()
    {
        var thread = await _mediator.Send(new GetAiOnboardingThreadQuery());
        return thread is null ? NoContent() : Ok(thread);
    }

    /// <summary>The allowlist, so the UI can say what onboarding is able to do before it starts.</summary>
    [HttpGet("onboarding/tools")]
    public ActionResult<IReadOnlyList<AiToolSummary>> OnboardingTools()
        => Ok(AiToolCatalogue.Onboarding
            .Select(tool => new AiToolSummary(tool.Name, tool.Description))
            .ToList());

    /// <summary>
    /// A coach asking about one client. Read-only over the client's log, and
    /// billed to the coach (docs/REFOCUS.md §11).
    /// </summary>
    [HttpPost("clients/{clientId:guid}/ask")]
    [Authorize(Policy = "RequireTrainer")]
    public async Task<ActionResult<AiTrainerAnswerDto>> AskAboutClient(
        Guid clientId, [FromBody] AskClientRequest body)
        => Ok(await _mediator.Send(new AskAboutClientCommand(clientId, body.ThreadId, body.Message)));

    [HttpGet("threads")]
    public async Task<ActionResult<IReadOnlyList<AiChatThreadDto>>> ListThreads([FromQuery] int take = 30)
        => Ok(await _mediator.Send(new ListAiChatThreadsQuery(take)));

    [HttpGet("threads/{id:guid}")]
    public async Task<ActionResult<AiChatThreadDetailDto>> GetThread(Guid id)
        => Ok(await _mediator.Send(new GetAiChatThreadQuery(id)));

    [HttpDelete("threads/{id:guid}")]
    public async Task<IActionResult> DeleteThread(Guid id)
    {
        await _mediator.Send(new DeleteAiChatThreadCommand(id));
        return NoContent();
    }

    [HttpGet("usage/global")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<GlobalAiUsageDto>> GetGlobalUsage()
        => Ok(await _mediator.Send(new GetGlobalAiUsageQuery()));
}

public record AiToolSummary(string Name, string Description);

public record AskClientRequest(Guid? ThreadId, string Message);
