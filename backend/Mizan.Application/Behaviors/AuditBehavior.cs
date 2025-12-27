using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Behaviors;

public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    public AuditBehavior(
        IMizanDbContext context,
        ICurrentUserService currentUserService,
        ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Only audit commands (convention: ends with "Command")
        if (!requestName.EndsWith("Command"))
        {
            return await next();
        }

        var response = await next();

        try
        {
            var userId = _currentUserService.UserId;
            var ipAddress = _currentUserService.IpAddress;
            
            // Try to extract an ID from the request or response if possible
            string entityId = string.Empty;
            var idProperty = typeof(TRequest).GetProperty("Id") ?? typeof(TRequest).GetProperty("recipeId") ?? typeof(TRequest).GetProperty("foodId");
            if (idProperty != null)
            {
                entityId = idProperty.GetValue(request)?.ToString() ?? string.Empty;
            }
            
            if (string.IsNullOrEmpty(entityId) && response != null)
            {
                var responseIdProperty = response.GetType().GetProperty("Id");
                if (responseIdProperty != null)
                {
                    entityId = responseIdProperty.GetValue(response)?.ToString() ?? string.Empty;
                }
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = requestName,
                EntityType = requestName.Replace("Command", ""),
                EntityId = entityId,
                Details = JsonSerializer.Serialize(request),
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Audit log created for {Action} by {User}", requestName, userId?.ToString() ?? "Anonymous");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audit log for {RequestName}", requestName);
        }

        return response;
    }
}
