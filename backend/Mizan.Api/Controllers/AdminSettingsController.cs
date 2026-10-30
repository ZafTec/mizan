using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Settings;
using Mizan.Contracts.Admin;

namespace Mizan.Api.Controllers;

/// <summary>
/// Runtime settings: SMTP, the AI provider and its limits, the Telegram bot service, push and Paddle,
/// set in the admin area instead of the environment (docs/ADMIN.md#runtime-settings). A secret is
/// accepted here and never returned.
/// </summary>
[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = "RequireAdmin")]
public class AdminSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Every setting with its current value and source. Secrets carry only <c>hasValue</c>.</summary>
    [HttpGet]
    public async Task<ActionResult<RuntimeSettingsDto>> List(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetRuntimeSettingsQuery(), cancellationToken));

    /// <summary>Sets one value, checked against the catalog. It applies from the next use on every instance.</summary>
    [HttpPut("{key}")]
    public async Task<ActionResult<RuntimeSettingDto>> Set(
        string key, [FromBody] SetRuntimeSettingRequest request, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new SetRuntimeSettingCommand(key, request.Value), cancellationToken));

    /// <summary>Removes the database value, so configuration applies again.</summary>
    [HttpDelete("{key}")]
    public async Task<ActionResult<RuntimeSettingDto>> Clear(string key, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new ClearRuntimeSettingCommand(key), cancellationToken));

    /// <summary>Queues a test message to the signed-in administrator through the outbox.</summary>
    [HttpPost("smtp/test")]
    public async Task<ActionResult<SmtpTestResult>> TestSmtp(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new SendTestEmailCommand(), cancellationToken));

    /// <summary>One tiny provider call on the evaluation line. Reports the error class, never the provider's body.</summary>
    [HttpPost("ai/test")]
    public async Task<ActionResult<AiProviderTestResult>> TestAi(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new TestAiProviderCommand(), cancellationToken));
}
