using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Usage;
using Mizan.Contracts.Admin;

namespace Mizan.Api.Controllers;

/// <summary>
/// Platform AI usage from the ledger: requests, tokens, cost and refusals per
/// household, user, plan or feature (docs/ADMIN.md#usage). Dates are UTC and
/// inclusive.
/// </summary>
[ApiController]
[Route("api/admin/usage")]
[Authorize(Policy = "RequireAdmin")]
public class AdminUsageController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminUsageController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<PlatformUsageDto>> Get([FromQuery] GetPlatformUsageQuery query) =>
        Ok(await _mediator.Send(query));

    /// <summary>The same table as RFC 4180 CSV in UTF-8, a header row then one row per group, written as it is read.</summary>
    [HttpGet("export")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/csv")]
    public async Task Export([FromQuery] GetPlatformUsageQuery query, CancellationToken cancellationToken)
    {
        var usage = await _mediator.Send(query, cancellationToken);

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition =
            $"attachment; filename=\"mizan-usage-{usage.GroupBy}-{usage.From:yyyy-MM-dd}-{usage.To:yyyy-MM-dd}.csv\"";

        await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        foreach (var line in PlatformUsageCsv.Lines(usage))
        {
            await writer.WriteAsync(line);
            await writer.WriteAsync("\r\n");
        }

        await writer.FlushAsync(cancellationToken);
    }
}
