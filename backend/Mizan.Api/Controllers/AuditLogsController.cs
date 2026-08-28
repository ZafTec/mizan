using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Common;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAdmin")]
public class AuditLogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLogs([FromQuery] GetAuditLogsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>The distinct actions and entity types present, for the filter dropdowns.</summary>
    [HttpGet("facets")]
    public async Task<ActionResult<AuditLogFacetsDto>> GetFacets()
        => Ok(await _mediator.Send(new GetAuditLogFacetsQuery()));

    /// <summary>
    /// The current filter as a CSV. Capped, because an audit log grows without
    /// limit and an uncapped export is a way to take the server down from a
    /// browser tab.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] GetAuditLogsQuery query)
    {
        const int maxRows = 10_000;
        var result = await _mediator.Send(query with { Page = 1, PageSize = maxRows });

        var csv = new StringBuilder();
        csv.AppendLine("timestamp,actor,action,entityType,entityId,ipAddress,details");

        foreach (var row in result.Items)
        {
            csv.Append(row.Timestamp.ToString("O")).Append(',')
                .Append(Csv(row.UserEmail)).Append(',')
                .Append(Csv(row.Action)).Append(',')
                .Append(Csv(row.EntityType)).Append(',')
                .Append(Csv(row.EntityId)).Append(',')
                .Append(Csv(row.IpAddress)).Append(',')
                .Append(Csv(row.Details)).AppendLine();
        }

        var name = $"audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", name);
    }

    /// <summary>
    /// Quotes every field rather than only the ones that need it. A leading
    /// =, +, - or @ is also prefixed with a tab: spreadsheet software treats
    /// those as formulas, and an audit log is exactly where an attacker would
    /// plant one.
    /// </summary>
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        var safe = value[0] is '=' or '+' or '-' or '@' ? "\t" + value : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
