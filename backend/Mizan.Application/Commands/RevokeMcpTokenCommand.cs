using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record RevokeMcpTokenCommand : IRequest
{
    public Guid TokenId { get; init; }
}

public class RevokeMcpTokenCommandHandler : IRequestHandler<RevokeMcpTokenCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RevokeMcpTokenCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(RevokeMcpTokenCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated to revoke MCP tokens");
        }

        var token = await _context.McpTokens
            .FirstOrDefaultAsync(t => t.Id == request.TokenId && t.UserId == _currentUser.UserId, cancellationToken);

        if (token == null)
        {
            throw new InvalidOperationException("Token not found or does not belong to you");
        }

        token.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
