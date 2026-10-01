using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;
using Mizan.Domain.Entities;

namespace Mizan.Application.OAuth;

/// <summary>The token endpoint: authorization_code and refresh_token grants, public clients, PKCE required.</summary>
public record ExchangeOAuthTokenCommand(
    string? GrantType,
    string? ClientId,
    string? Code,
    string? CodeVerifier,
    string? RedirectUri,
    string? RefreshToken) : IRequest<OAuthTokenResponse>, ISkipAudit;

public record OAuthTokenResponse(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken, string Scope);

public class ExchangeOAuthTokenCommandHandler : IRequestHandler<ExchangeOAuthTokenCommand, OAuthTokenResponse>
{
    private readonly IMizanDbContext _context;
    private readonly IOAuthSettings _settings;

    public ExchangeOAuthTokenCommandHandler(IMizanDbContext context, IOAuthSettings settings)
    {
        _context = context;
        _settings = settings;
    }

    public Task<OAuthTokenResponse> Handle(ExchangeOAuthTokenCommand request, CancellationToken cancellationToken) =>
        request.GrantType switch
        {
            "authorization_code" => ExchangeCodeAsync(request, cancellationToken),
            "refresh_token" => RefreshAsync(request, cancellationToken),
            _ => throw new OAuthException("unsupported_grant_type", "Use authorization_code or refresh_token."),
        };

    private async Task<OAuthTokenResponse> ExchangeCodeAsync(ExchangeOAuthTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.CodeVerifier))
            throw new OAuthException("invalid_request", "code and code_verifier are required.");

        var codeHash = OAuthTokens.Hash(command.Code);
        var request = await _context.OAuthAuthorizationRequests
            .Include(r => r.Client)
            .FirstOrDefaultAsync(r => r.CodeHash == codeHash, cancellationToken);
        var now = DateTime.UtcNow;

        if (request is null)
            throw new OAuthException("invalid_grant", "The code is not valid.");

        if (request.Status == OAuthAuthorizationRequest.Consumed)
        {
            // A used code showing up again means someone copied it. End what it produced.
            if (request.GrantId is { } grantId) await RevokeGrantTokensAsync(grantId, now, cancellationToken);
            throw new OAuthException("invalid_grant", "The code was already used.");
        }

        if (request.Status != OAuthAuthorizationRequest.Approved || request.ExpiresAt < now)
            throw new OAuthException("invalid_grant", "The code has expired.");
        if (request.Client.ClientId != command.ClientId)
            throw new OAuthException("invalid_grant", "The code was issued to another client.");
        if (command.RedirectUri is null || !string.Equals(command.RedirectUri, request.RedirectUri, StringComparison.Ordinal))
            throw new OAuthException("invalid_grant", "redirect_uri does not match the authorization request.");
        if (!VerifyPkce(command.CodeVerifier, request.CodeChallenge))
            throw new OAuthException("invalid_grant", "The code_verifier does not match.");

        // Only one caller can flip approved to consumed, so a race has one winner.
        var claimed = await _context.OAuthAuthorizationRequests
            .Where(r => r.Id == request.Id && r.Status == OAuthAuthorizationRequest.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, OAuthAuthorizationRequest.Consumed), cancellationToken);
        if (claimed != 1)
            throw new OAuthException("invalid_grant", "The code was already used.");

        var grant = await _context.OAuthGrants.FirstOrDefaultAsync(g => g.Id == request.GrantId, cancellationToken);
        if (grant is null || grant.RevokedAt is not null)
            throw new OAuthException("invalid_grant", "The connection was removed.");

        var issued = OAuthTokens.Issue(_context, _settings, grant.Id, Guid.CreateVersion7(), request.Audience, now);
        await _context.SaveChangesAsync(cancellationToken);
        return Response(issued, grant);
    }

    private async Task<OAuthTokenResponse> RefreshAsync(ExchangeOAuthTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            throw new OAuthException("invalid_request", "refresh_token is required.");

        var hash = OAuthTokens.Hash(command.RefreshToken);
        var token = await _context.OAuthTokens
            .Include(t => t.Grant).ThenInclude(g => g.Client)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Kind == OAuthToken.KindRefresh, cancellationToken);
        var now = DateTime.UtcNow;

        if (token is null)
            throw new OAuthException("invalid_grant", "The refresh token is not valid.");
        if (token.Grant.Client.ClientId != command.ClientId)
            throw new OAuthException("invalid_grant", "The refresh token was issued to another client.");
        if (token.RevokedAt is not null || token.Grant.RevokedAt is not null)
            throw new OAuthException("invalid_grant", "The connection was removed.");

        if (token.UsedAt is not null)
        {
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            throw new OAuthException("invalid_grant", "The refresh token was already used. Connect again.");
        }

        if (token.ExpiresAt < now)
            throw new OAuthException("invalid_grant", "The refresh token has expired. Connect again.");

        var used = await _context.OAuthTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), cancellationToken);
        if (used != 1)
        {
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            throw new OAuthException("invalid_grant", "The refresh token was already used. Connect again.");
        }

        var issued = OAuthTokens.Issue(_context, _settings, token.GrantId, token.FamilyId, token.Audience, now);
        await _context.SaveChangesAsync(cancellationToken);
        return Response(issued, token.Grant);
    }

    private OAuthTokenResponse Response(OAuthTokens.Issued issued, OAuthGrant grant) =>
        new(issued.AccessToken, "Bearer", (int)_settings.AccessTokenLifetime.TotalSeconds, issued.RefreshToken,
            string.Join(' ', grant.Scopes));

    private static bool VerifyPkce(string verifier, string challenge)
    {
        if (verifier.Length is < 43 or > 128) return false;
        var computed = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(challenge));
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken) =>
        _context.OAuthTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

    private Task<int> RevokeGrantTokensAsync(Guid grantId, DateTime now, CancellationToken cancellationToken) =>
        _context.OAuthTokens
            .Where(t => t.GrantId == grantId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
}

/// <summary>RFC 7009. Always succeeds, so a caller learns nothing about which tokens exist.</summary>
public record RevokeOAuthTokenCommand(string? Token, string? ClientId) : IRequest<Unit>, ISkipAudit;

public class RevokeOAuthTokenCommandHandler : IRequestHandler<RevokeOAuthTokenCommand, Unit>
{
    private readonly IMizanDbContext _context;

    public RevokeOAuthTokenCommandHandler(IMizanDbContext context) => _context = context;

    public async Task<Unit> Handle(RevokeOAuthTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)) return Unit.Value;

        var hash = OAuthTokens.Hash(request.Token);
        var token = await _context.OAuthTokens
            .Include(t => t.Grant).ThenInclude(g => g.Client)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null || (request.ClientId is not null && token.Grant.Client.ClientId != request.ClientId))
            return Unit.Value;

        var now = DateTime.UtcNow;
        await _context.OAuthTokens
            .Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
        return Unit.Value;
    }
}

/// <summary>
/// Asked by the MCP server for every new access token it sees. The answer says
/// who the token is for and what it may do, as the grant stands now.
/// </summary>
public record IntrospectOAuthTokenQuery(string Token, string Audience) : IRequest<OAuthIntrospection>;

public record OAuthIntrospection
{
    public bool Active { get; init; }
    public Guid UserId { get; init; }
    public Guid GrantId { get; init; }
    public Guid ClientRowId { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; init; } = [];
    public string HouseholdMode { get; init; } = OAuthGrant.HouseholdsNone;
    public IReadOnlyList<Guid> HouseholdIds { get; init; } = [];
    public string Role { get; init; } = "user";
    public string Plan { get; init; } = "free";
    public int? MonthlyLimit { get; init; }
    public int UsedThisMonth { get; init; }

    public static OAuthIntrospection Inactive { get; } = new();
}

public class IntrospectOAuthTokenQueryHandler : IRequestHandler<IntrospectOAuthTokenQuery, OAuthIntrospection>
{
    private static readonly TimeSpan LastUsedGranularity = TimeSpan.FromMinutes(1);

    private readonly IMizanDbContext _context;
    private readonly IUserStatusService _users;

    public IntrospectOAuthTokenQueryHandler(IMizanDbContext context, IUserStatusService users)
    {
        _context = context;
        _users = users;
    }

    public async Task<OAuthIntrospection> Handle(IntrospectOAuthTokenQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || !request.Token.StartsWith(OAuthTokens.AccessPrefix, StringComparison.Ordinal))
            return OAuthIntrospection.Inactive;

        var hash = OAuthTokens.Hash(request.Token);
        var now = DateTime.UtcNow;
        var token = await _context.OAuthTokens
            .Include(t => t.Grant).ThenInclude(g => g.Client)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Kind == OAuthToken.KindAccess, cancellationToken);

        if (token is null || token.RevokedAt is not null || token.ExpiresAt < now || token.Audience != request.Audience
            || token.Grant.RevokedAt is not null)
            return OAuthIntrospection.Inactive;

        var status = await _users.GetStatusAsync(token.Grant.UserId, cancellationToken);
        if (!status.IsAllowed) return OAuthIntrospection.Inactive;

        var grant = token.Grant;
        if (grant.LastUsedAt is null || now - grant.LastUsedAt > LastUsedGranularity)
        {
            await _context.OAuthGrants
                .Where(g => g.Id == grant.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.LastUsedAt, now), cancellationToken);
        }

        var plan = await _context.Subscriptions
            .Where(s => s.UserId == grant.UserId).Select(s => s.Plan).FirstOrDefaultAsync(cancellationToken) ?? "free";
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var used = await _context.McpUsageLogs.CountAsync(
            l => l.UserId == grant.UserId && l.Success && l.Kind != "prompt" && l.Timestamp >= monthStart, cancellationToken);

        // Admin stays tied to the account's current role, not to what it was at consent time.
        var scopes = status.Role == "admin" ? grant.Scopes : grant.Scopes.Where(s => s != McpScopes.Admin).ToList();

        return new OAuthIntrospection
        {
            Active = true,
            UserId = grant.UserId,
            GrantId = grant.Id,
            ClientRowId = grant.Client.Id,
            ClientId = grant.Client.ClientId,
            ClientName = grant.Client.Name,
            Scopes = scopes,
            HouseholdMode = grant.HouseholdMode,
            HouseholdIds = grant.HouseholdIds,
            Role = status.Role,
            Plan = plan,
            MonthlyLimit = string.Equals(plan, "free", StringComparison.OrdinalIgnoreCase)
                ? Common.McpUsagePolicy.FreeMonthlyToolCalls
                : null,
            UsedThisMonth = used,
        };
    }
}

/// <summary>
/// Who an access token for the API itself belongs to. Asked on every request a native
/// app makes, so it reads only what is needed to let the request in.
/// </summary>
public record ResolveApiAccessTokenQuery(string Token) : IRequest<ApiTokenIdentity?>;

public record ApiTokenIdentity(Guid UserId, Guid GrantId, string Role);

public class ResolveApiAccessTokenQueryHandler : IRequestHandler<ResolveApiAccessTokenQuery, ApiTokenIdentity?>
{
    private static readonly TimeSpan LastUsedGranularity = TimeSpan.FromMinutes(1);

    private readonly IMizanDbContext _context;
    private readonly IUserStatusService _users;

    public ResolveApiAccessTokenQueryHandler(IMizanDbContext context, IUserStatusService users)
    {
        _context = context;
        _users = users;
    }

    public async Task<ApiTokenIdentity?> Handle(ResolveApiAccessTokenQuery request, CancellationToken cancellationToken)
    {
        if (!request.Token.StartsWith(OAuthTokens.AccessPrefix, StringComparison.Ordinal)) return null;

        var hash = OAuthTokens.Hash(request.Token);
        var now = DateTime.UtcNow;
        var token = await _context.OAuthTokens
            .Include(t => t.Grant)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Kind == OAuthToken.KindAccess, cancellationToken);

        // Only a token issued for the API is accepted, and the authorization server issues
        // those to first-party apps alone. A token meant for the MCP server is refused here.
        if (token is null || token.RevokedAt is not null || token.ExpiresAt < now
            || token.Audience != OAuthToken.AudienceApi || token.Grant.RevokedAt is not null)
            return null;

        var status = await _users.GetStatusAsync(token.Grant.UserId, cancellationToken);
        if (!status.IsAllowed) return null;

        var grant = token.Grant;
        if (grant.LastUsedAt is null || now - grant.LastUsedAt > LastUsedGranularity)
        {
            await _context.OAuthGrants
                .Where(g => g.Id == grant.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.LastUsedAt, now), cancellationToken);
        }

        return new ApiTokenIdentity(grant.UserId, grant.Id, status.Role);
    }
}
