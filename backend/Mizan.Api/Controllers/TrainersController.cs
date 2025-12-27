using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TrainersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<TrainersController> _logger;

    public TrainersController(IMediator mediator, ICurrentUserService currentUser, ILogger<TrainersController> logger)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpPost("request")]
    public async Task<IActionResult> SendRequest([FromBody] SendTrainerRequestRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized("User not authenticated");
        }

        var command = new SendTrainerRequestCommand(_currentUser.UserId.Value, request.TrainerId);
        var id = await _mediator.Send(command);

        _logger.LogInformation("Client {ClientId} sent trainer request to {TrainerId}", _currentUser.UserId.Value, request.TrainerId);

        return Ok(new { RelationshipId = id });
    }

    [HttpPost("respond")]
    public async Task<IActionResult> Respond([FromBody] RespondRequest request)
    {
        var command = new RespondToTrainerRequestCommand(
            request.RelationshipId,
            request.Accept,
            request.CanViewNutrition,
            request.CanViewWorkouts,
            request.CanViewMeasurements,
            request.CanMessage
        );
        var success = await _mediator.Send(command);

        if (!success)
        {
            return NotFound("Relationship not found");
        }

        _logger.LogInformation("Trainer {TrainerId} responded to request {RelationshipId}: {Accepted}",
            _currentUser.UserId, request.RelationshipId, request.Accept);

        return NoContent();
    }

    [HttpGet("clients")]
    public async Task<IActionResult> GetClients()
    {
        try
        {
            var query = new GetTrainerClientsQuery();
            var clients = await _mediator.Send(query);

            _logger.LogInformation("Trainer {TrainerId} retrieved {Count} clients", _currentUser.UserId, clients.Count);

            return Ok(clients);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access to trainer clients: {Message}", ex.Message);
            return Unauthorized(ex.Message);
        }
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetPendingRequests()
    {
        try
        {
            var query = new GetTrainerPendingRequestsQuery();
            var requests = await _mediator.Send(query);

            _logger.LogInformation("Trainer {TrainerId} retrieved {Count} pending requests", _currentUser.UserId, requests.Count);

            return Ok(requests);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access to pending requests: {Message}", ex.Message);
            return Unauthorized(ex.Message);
        }
    }

    [HttpGet("clients/{clientId}/nutrition")]
    public async Task<IActionResult> GetClientNutrition(Guid clientId, [FromQuery] DateTime? date = null)
    {
        try
        {
            var query = new GetClientNutritionQuery(clientId, date);
            var nutrition = await _mediator.Send(query);

            if (nutrition == null)
            {
                return NotFound("No nutrition data found");
            }

            _logger.LogInformation("Trainer {TrainerId} accessed nutrition for client {ClientId} on {Date}",
                _currentUser.UserId, clientId, nutrition.Date);

            return Ok(nutrition);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access to client nutrition: Trainer {TrainerId}, Client {ClientId}: {Message}",
                _currentUser.UserId, clientId, ex.Message);
            return Unauthorized(ex.Message);
        }
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableTrainers()
    {
        var query = new GetAvailableTrainersQuery();
        var trainers = await _mediator.Send(query);

        _logger.LogInformation("User {UserId} retrieved {Count} available trainers", _currentUser.UserId, trainers.Count);

        return Ok(trainers);
    }

    [HttpGet("my-trainer")]
    public async Task<IActionResult> GetMyTrainer()
    {
        try
        {
            var query = new GetMyTrainerQuery();
            var trainer = await _mediator.Send(query);

            if (trainer == null)
            {
                return NotFound("No active trainer relationship found");
            }

            _logger.LogInformation("Client {ClientId} retrieved their trainer {TrainerId}", _currentUser.UserId, trainer.TrainerId);

            return Ok(trainer);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access to my trainer: {Message}", ex.Message);
            return Unauthorized(ex.Message);
        }
    }

    [HttpGet("my-requests")]
    public async Task<IActionResult> GetMyTrainerRequests()
    {
        try
        {
            var query = new GetMyTrainerRequestsQuery();
            var requests = await _mediator.Send(query);

            _logger.LogInformation("Client {ClientId} retrieved {Count} trainer requests", _currentUser.UserId, requests.Count);

            return Ok(requests);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access to my trainer requests: {Message}", ex.Message);
            return Unauthorized(ex.Message);
        }
    }
}

public record SendTrainerRequestRequest(Guid TrainerId);
public record RespondRequest(
    Guid RelationshipId,
    bool Accept,
    bool? CanViewNutrition = null,
    bool? CanViewWorkouts = null,
    bool? CanViewMeasurements = null,
    bool? CanMessage = null
);
