using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Mizan.Api.Webhooks;
using Mizan.Application.Commands;
using Mizan.Application.Common;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/webhooks/paddle")]
[AllowAnonymous]
public class PaddleWebhooksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly PaddleOptions _options;
    private readonly ILogger<PaddleWebhooksController> _logger;

    public PaddleWebhooksController(
        IMediator mediator,
        IOptions<PaddleOptions> options,
        ILogger<PaddleWebhooksController> logger)
    {
        _mediator = mediator;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        var signature = Request.Headers["Paddle-Signature"].ToString();
        if (!PaddleSignatureVerifier.Verify(rawBody, signature, _options.WebhookSecret))
        {
            _logger.LogWarning("Paddle webhook rejected: invalid or missing signature");
            return Unauthorized();
        }

        await _mediator.Send(new ProcessPaddleWebhookCommand(rawBody), cancellationToken);
        return Ok();
    }
}
