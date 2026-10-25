using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Contact;
using Mizan.Contracts.Admin;

namespace Mizan.Api.Controllers;

/// <summary>
/// The public contact form (docs/ADMIN.md#contact). Signed out is fine; when
/// the session cookie is present the message is linked to that account.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class ContactController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContactController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [ProducesResponseType(typeof(ContactAcceptedDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ContactAcceptedDto>> Submit([FromBody] ContactRequest request)
    {
        var result = await _mediator.Send(new SubmitContactCommand(
            request.Name ?? string.Empty,
            request.Email ?? string.Empty,
            request.Subject ?? string.Empty,
            request.Message ?? string.Empty,
            request.Website));
        return Accepted(result);
    }
}
