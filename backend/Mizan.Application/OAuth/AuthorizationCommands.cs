using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;

namespace Mizan.Application.OAuth;

/// <summary>
/// The authorize endpoint. It checks the request and stores it, then the web
/// app shows the consent screen. Nothing is decided here: the user has not
/// said yes. The result is either a secret for the consent page or an error.
/// </summary>
public record StartAuthorizationCommand(
    string? ClientId,
    string? RedirectUri,
    string? ResponseType,
    string? Scope,
    string? State,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    string? Resource) : IRequest<StartAuthorizationResult>, ISkipAudit;

/// <param name="RequestSecret">Set when the request is valid. The consent page needs it.</param>
/// <param name="Error">Set when it is not.</param>
/// <param name="RedirectBackTo">
/// Set only when the client and redirect URI are known and valid, so the error
/// may go back to the client. Otherwise the error is shown to the user and
/// nothing is redirected.
/// </param>
public record StartAuthorizationResult(
    string? RequestSecret,
    string? Error,
    string? ErrorDescription,
    string? RedirectBackTo,
    string? State);

public class StartAuthorizationCommandHandler : IRequestHandler<StartAuthorizationCommand, StartAuthorizationResult>
{
    private readonly IMizanDbContext _context;
    private readonly IOAuthSettings _settings;
    private readonly OAuthClientResolver _clients;

    public StartAuthorizationCommandHandler(IMizanDbContext context, IOAuthSettings settings, OAuthClientResolver clients)
    {
        _context = context;
        _settings = settings;
        _clients = clients;
    }

    public async Task<StartAuthorizationResult> Handle(StartAuthorizationCommand request, CancellationToken cancellationToken)
    {
        var client = await _clients.ResolveAsync(request.ClientId, cancellationToken);
        if (client is null)
            return Fail("invalid_request", "Unknown client_id.", null, request.State);

        // The redirect URI is checked before anything else is allowed to redirect.
        var redirect = request.RedirectUri;
        if (string.IsNullOrWhiteSpace(redirect))
        {
            if (client.RedirectUris.Count != 1)
                return Fail("invalid_request", "redirect_uri is required.", null, request.State);
            redirect = client.RedirectUris[0];
        }
        else if (!client.RedirectUris.Any(registered => RedirectUriRules.Matches(registered, redirect)))
        {
            return Fail("invalid_request", "redirect_uri does not match a registered URI.", null, request.State);
        }

        if (request.ResponseType != "code")
            return Fail("unsupported_response_type", "Only response_type=code is supported.", redirect, request.State);

        if (string.IsNullOrWhiteSpace(request.CodeChallenge) || request.CodeChallenge.Length is < 43 or > 128)
            return Fail("invalid_request", "A PKCE code_challenge is required.", redirect, request.State);
        if (request.CodeChallengeMethod != "S256")
            return Fail("invalid_request", "code_challenge_method must be S256.", redirect, request.State);

        string audience;
        if (string.IsNullOrWhiteSpace(request.Resource) || Same(request.Resource, _settings.McpResource))
        {
            audience = OAuthToken.AudienceMcp;
        }
        else if (client.IsFirstParty && Same(request.Resource, _settings.Issuer))
        {
            audience = OAuthToken.AudienceApi;
        }
        else
        {
            return Fail("invalid_target", "Unknown resource.", redirect, request.State);
        }

        var requested = McpScopes.Parse(request.Scope)
            .Where(s => McpScopes.IsKnown(s) && (s != McpScopes.Full || client.IsFirstParty))
            .ToList();

        var secret = SecureToken.Generate();
        var now = DateTime.UtcNow;
        _context.OAuthAuthorizationRequests.Add(new OAuthAuthorizationRequest
        {
            Id = Guid.CreateVersion7(),
            RequestHash = SecureToken.Hash(secret),
            ClientId = client.Id,
            RedirectUri = redirect,
            RequestedScopes = requested,
            State = request.State is { Length: <= 512 } ? request.State : null,
            CodeChallenge = request.CodeChallenge,
            Audience = audience,
            CreatedAt = now,
            ExpiresAt = now + _settings.RequestLifetime,
        });
        await _context.SaveChangesAsync(cancellationToken);

        return new StartAuthorizationResult(secret, null, null, null, request.State);
    }

    private static bool Same(string a, string b) =>
        string.Equals(a.TrimEnd('/'), b.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    private static StartAuthorizationResult Fail(string error, string description, string? redirect, string? state) =>
        new(null, error, description, redirect, state);
}

/// <summary>What the consent screen needs: who is asking, what for, and what the user can pick.</summary>
public record GetAuthorizationRequestQuery(string RequestSecret) : IRequest<AuthorizationRequestView>;

public record ScopeGroupView(string Group, string Title, string Description, bool HasWrite, string ReadScope, string? WriteScope);

/// <summary>The permission groups a user can give a connected app, in the order the screens show them.</summary>
public static class McpScopeCatalog
{
    public static List<ScopeGroupView> For(bool isAdmin) => McpScopes.Groups
        .Where(g => !g.Value.AdminOnly || isAdmin)
        .Select(g => g.Key switch
        {
            "ai" => new ScopeGroupView("ai", g.Value.Title, g.Value.Description, false, McpScopes.AiUse, null),
            "admin" => new ScopeGroupView("admin", g.Value.Title, g.Value.Description, false, McpScopes.Admin, null),
            _ => new ScopeGroupView(g.Key, g.Value.Title, g.Value.Description, g.Value.HasWrite, $"{g.Key}:read", g.Value.HasWrite ? $"{g.Key}:write" : null),
        })
        .ToList();
}

public record ConsentHouseholdView(Guid Id, string Name);

public record ExistingGrantView(IReadOnlyList<string> Scopes, string HouseholdMode, IReadOnlyList<Guid> HouseholdIds);

public record AuthorizationRequestView(
    string ClientName,
    string Source,
    string? VerifiedHost,
    string? LogoUri,
    string? ClientUri,
    string RedirectHost,
    string Audience,
    IReadOnlyList<string> RequestedScopes,
    IReadOnlyList<ScopeGroupView> ScopeGroups,
    IReadOnlyList<ConsentHouseholdView> Households,
    ExistingGrantView? ExistingGrant,
    bool IsFirstParty,
    DateTime ExpiresAt);

public class GetAuthorizationRequestQueryHandler : IRequestHandler<GetAuthorizationRequestQuery, AuthorizationRequestView>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAuthorizationRequestQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AuthorizationRequestView> Handle(GetAuthorizationRequestQuery query, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var request = await OAuthRequests.FindPendingAsync(_context, query.RequestSecret, cancellationToken);
        var client = request.Client;

        var isAdmin = _currentUser.IsInRole("admin");
        var groups = McpScopeCatalog.For(isAdmin);

        var households = await _context.HouseholdMembers
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Household.Name)
            .Select(m => new ConsentHouseholdView(m.HouseholdId, m.Household.Name))
            .ToListAsync(cancellationToken);

        var existing = await _context.OAuthGrants
            .Where(g => g.UserId == userId && g.ClientId == client.Id && g.RevokedAt == null)
            .Select(g => new ExistingGrantView(g.Scopes, g.HouseholdMode, g.HouseholdIds))
            .FirstOrDefaultAsync(cancellationToken);

        var redirectHost = Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out var uri)
            ? (string.IsNullOrEmpty(uri.Host) ? uri.Scheme + ":" : uri.Host)
            : request.RedirectUri;

        return new AuthorizationRequestView(
            client.Name, client.Source, client.VerifiedHost, client.LogoUri, client.ClientUri, redirectHost,
            request.Audience, request.RequestedScopes, groups, households, existing, client.IsFirstParty, request.ExpiresAt);
    }
}

/// <summary>The user's answer on the consent screen. Approving creates or replaces the grant and issues a code.</summary>
public record DecideAuthorizationCommand(
    string RequestSecret,
    bool Approve,
    IReadOnlyList<string>? Scopes,
    string? HouseholdMode,
    IReadOnlyList<Guid>? HouseholdIds) : IRequest<DecideAuthorizationResult>, IRedactedAudit
{
    object IRedactedAudit.AuditDetails => new { Approve, Scopes, HouseholdMode, HouseholdIds };
}

public record DecideAuthorizationResult(string RedirectUrl);

public class DecideAuthorizationCommandHandler : IRequestHandler<DecideAuthorizationCommand, DecideAuthorizationResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IOAuthSettings _settings;

    public DecideAuthorizationCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, IOAuthSettings settings)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<DecideAuthorizationResult> Handle(DecideAuthorizationCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var request = await OAuthRequests.FindPendingAsync(_context, command.RequestSecret, cancellationToken);
        var now = DateTime.UtcNow;

        if (!command.Approve)
        {
            request.Status = OAuthAuthorizationRequest.Denied;
            await _context.SaveChangesAsync(cancellationToken);
            return new DecideAuthorizationResult(
                BuildRedirect(request, ("error", "access_denied"), ("error_description", "The user denied the request.")));
        }

        var isAdmin = _currentUser.IsInRole("admin");
        var scopes = request.Client.IsFirstParty && request.RequestedScopes.Contains(McpScopes.Full)
            ? new List<string> { McpScopes.Full }
            : McpScopes.Normalize(command.Scopes, isAdmin);

        // A user may grant less than was asked for, never more.
        if (request.RequestedScopes.Count > 0 && !request.RequestedScopes.Contains(McpScopes.Full))
        {
            var allowed = McpScopes.Normalize(request.RequestedScopes, isAdmin);
            scopes = scopes.Where(s => allowed.Contains(s) || McpScopes.Allows(request.RequestedScopes, s)).ToList();
        }

        if (scopes.Count == 0)
            throw new DomainValidationException("Choose at least one thing the app may do, or deny the request.");

        var memberOf = await _context.HouseholdMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.HouseholdId)
            .ToListAsync(cancellationToken);

        var mode = command.HouseholdMode switch
        {
            OAuthGrant.HouseholdsAll => OAuthGrant.HouseholdsAll,
            OAuthGrant.HouseholdsSelected => OAuthGrant.HouseholdsSelected,
            _ => OAuthGrant.HouseholdsNone,
        };
        var householdIds = mode == OAuthGrant.HouseholdsSelected
            ? (command.HouseholdIds ?? []).Where(memberOf.Contains).Distinct().ToList()
            : new List<Guid>();
        if (mode == OAuthGrant.HouseholdsSelected && householdIds.Count == 0) mode = OAuthGrant.HouseholdsNone;

        // Our own app is the user's app, not a third party, so it sees every household they belong to.
        if (request.Client.IsFirstParty)
        {
            mode = OAuthGrant.HouseholdsAll;
            householdIds = new List<Guid>();
        }

        var grant = await _context.OAuthGrants
            .FirstOrDefaultAsync(g => g.UserId == userId && g.ClientId == request.ClientId && g.RevokedAt == null, cancellationToken);
        if (grant is null)
        {
            grant = new OAuthGrant { Id = Guid.CreateVersion7(), UserId = userId, ClientId = request.ClientId, CreatedAt = now };
            _context.OAuthGrants.Add(grant);
        }

        grant.Scopes = scopes;
        grant.HouseholdMode = mode;
        grant.HouseholdIds = householdIds;
        grant.UpdatedAt = now;

        var code = SecureToken.Generate();
        request.Status = OAuthAuthorizationRequest.Approved;
        request.UserId = userId;
        request.GrantId = grant.Id;
        request.CodeHash = SecureToken.Hash(code);
        request.ExpiresAt = now + _settings.CodeLifetime;
        await _context.SaveChangesAsync(cancellationToken);

        return new DecideAuthorizationResult(BuildRedirect(request, ("code", code)));
    }

    private string BuildRedirect(OAuthAuthorizationRequest request, params (string Key, string Value)[] values)
    {
        var parts = values.Select(v => (v.Key, v.Value)).ToList();
        if (request.State is not null) parts.Add(("state", request.State));
        parts.Add(("iss", _settings.Issuer));

        var query = string.Join("&", parts.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        var separator = request.RedirectUri.Contains('?') ? "&" : "?";
        return request.RedirectUri + separator + query;
    }
}

internal static class OAuthRequests
{
    public static async Task<OAuthAuthorizationRequest> FindPendingAsync(
        IMizanDbContext context, string secret, CancellationToken cancellationToken)
    {
        var hash = SecureToken.Hash(secret ?? string.Empty);
        var request = await context.OAuthAuthorizationRequests
            .Include(r => r.Client)
            .FirstOrDefaultAsync(r => r.RequestHash == hash, cancellationToken);

        if (request is null || request.Status != OAuthAuthorizationRequest.Pending || request.ExpiresAt < DateTime.UtcNow)
            throw new EntityNotFoundException("This connection request has expired. Start again from the app.");

        return request;
    }
}
