using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUserService;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUserService.UserId?.ToString() ?? "anonymous";

        _logger.LogInformation(
            "Executing {RequestName} for user {UserId}",
            requestName,
            userId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            stopwatch.Stop();
            _logger.LogInformation(
                "Executed {RequestName} successfully for user {UserId} in {ElapsedMs}ms",
                requestName,
                userId,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Error executing {RequestName} for user {UserId} after {ElapsedMs}ms - {ErrorType}: {ErrorMessage}",
                requestName,
                userId,
                stopwatch.ElapsedMilliseconds,
                ex.GetType().Name,
                ex.Message);
            throw;
        }
    }
}
