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
public class BodyMeasurementsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public BodyMeasurementsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BodyMeasurementDto>>> GetMyMeasurements(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await _mediator.Send(new GetBodyMeasurementsQuery
        {
            UserId = _currentUser.UserId.Value,
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortOrder = sortOrder
        });
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> LogMeasurement([FromBody] LogMeasurementRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized("User not authenticated");
        }

        var command = new LogBodyMeasurementCommand(
            _currentUser.UserId.Value,
            request.Date ?? DateTime.UtcNow,
            request.WeightKg,
            request.BodyFatPercentage,
            request.MuscleMassKg,
            request.WaistCm,
            request.HipsCm,
            request.ChestCm,
            request.ArmsCm,
            request.ThighsCm,
            request.Notes
        );

        var id = await _mediator.Send(command);
        return Ok(id);
    }
}

public record LogMeasurementRequest(
    DateTime? Date,
    decimal? WeightKg,
    decimal? BodyFatPercentage,
    decimal? MuscleMassKg,
    decimal? WaistCm,
    decimal? HipsCm,
    decimal? ChestCm,
    decimal? ArmsCm,
    decimal? ThighsCm,
    string? Notes
);
