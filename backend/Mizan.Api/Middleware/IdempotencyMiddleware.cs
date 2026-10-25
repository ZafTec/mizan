using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Npgsql;

namespace Mizan.Api.Middleware;

/// <summary>
/// Makes a write safe to send twice. A client that adds an <c>Idempotency-Key</c> header gets the
/// first answer back on a retry instead of a second meal or workout. This is how an app that
/// loses signal after sending can try again without thinking about what the server saw.
///
/// The key is scoped to the user and tied to the request: the same key with a different
/// method, address or body is refused, because that is a client bug, not a retry.
/// </summary>
public sealed class IdempotencyMiddleware
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayHeader = "Idempotent-Replayed";

    private const int MaxKeyLength = 128;
    private const int MaxStoredBytes = 256 * 1024;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    /// <summary>A request that has not finished after this long lost its server, so a retry may run.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopes)
    {
        var request = context.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)
            || !request.Headers.TryGetValue(HeaderName, out var header)
            || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId))
        {
            await _next(context);
            return;
        }

        var key = header.ToString();
        if (key.Length is 0 or > MaxKeyLength || key.Any(c => c < 0x21 || c > 0x7e))
        {
            await Reject(context, StatusCodes.Status400BadRequest, "invalid_idempotency_key",
                $"{HeaderName} must be 1 to {MaxKeyLength} printable characters.");
            return;
        }

        var hash = await HashRequestAsync(request);

        var existing = await ReadAsync(scopes, userId, key, context.RequestAborted);
        if (existing is not null && existing.CompletedAt is null && DateTime.UtcNow - existing.CreatedAt > StaleAfter)
        {
            await DeleteAsync(scopes, userId, key);
            existing = null;
        }

        if (existing is not null)
        {
            if (existing.RequestHash != hash)
            {
                await Reject(context, StatusCodes.Status422UnprocessableEntity, "idempotency_key_reused",
                    "This Idempotency-Key was already used for a different request.");
                return;
            }

            if (existing.CompletedAt is null)
            {
                await Reject(context, StatusCodes.Status409Conflict, "idempotency_in_progress",
                    "The first request with this key is still running. Try again shortly.");
                return;
            }

            await ReplayAsync(context, existing);
            return;
        }

        if (!await TryClaimAsync(scopes, userId, key, hash, context.RequestAborted))
        {
            await Reject(context, StatusCodes.Status409Conflict, "idempotency_in_progress",
                "The first request with this key is still running. Try again shortly.");
            return;
        }

        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await _next(context);
        }
        catch
        {
            // Nothing was answered, so a retry should run.
            context.Response.Body = original;
            await DeleteAsync(scopes, userId, key);
            throw;
        }

        context.Response.Body = original;
        buffer.Position = 0;
        await buffer.CopyToAsync(original, context.RequestAborted);

        // A server error says nothing about what the request would do, so it is not remembered.
        // Neither is an answer too big to keep; a retry runs again.
        if (context.Response.StatusCode >= 500 || buffer.Length > MaxStoredBytes)
        {
            await DeleteAsync(scopes, userId, key);
            return;
        }

        await CompleteAsync(scopes, userId, key, context.Response.StatusCode, context.Response.ContentType, buffer.ToArray());
    }

    private static async Task<string> HashRequestAsync(HttpRequest request)
    {
        request.EnableBuffering();
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(System.Text.Encoding.UTF8.GetBytes($"{request.Method}\n{request.Path}{request.QueryString}\n"));

        var bytes = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(bytes)) > 0) sha.AppendData(bytes.AsSpan(0, read));
        request.Body.Position = 0;
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private static async Task ReplayAsync(HttpContext context, IdempotencyKey stored)
    {
        context.Response.StatusCode = stored.StatusCode ?? StatusCodes.Status200OK;
        if (stored.ContentType is not null) context.Response.ContentType = stored.ContentType;
        context.Response.Headers[ReplayHeader] = "true";
        if (stored.ResponseBody is { Length: > 0 } body) await context.Response.Body.WriteAsync(body, context.RequestAborted);
    }

    private static Task Reject(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { errorCode = code, error = message });
    }

    // Each step uses a scope of its own, so what the idempotency record does can never
    // tangle with the change tracker of the request it protects.
    private static async Task<IdempotencyKey?> ReadAsync(IServiceScopeFactory scopes, Guid userId, string key, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IMizanDbContext>();
        return await db.IdempotencyKeys.AsNoTracking().FirstOrDefaultAsync(k => k.UserId == userId && k.Key == key, ct);
    }

    private async Task<bool> TryClaimAsync(IServiceScopeFactory scopes, Guid userId, string key, string hash, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IMizanDbContext>();

        // Old keys go as new ones arrive, so no separate job is needed.
        var cutoff = DateTime.UtcNow - Retention;
        await db.IdempotencyKeys.Where(k => k.UserId == userId && k.CreatedAt < cutoff).ExecuteDeleteAsync(ct);

        db.IdempotencyKeys.Add(new IdempotencyKey { UserId = userId, Key = key, RequestHash = hash, CreatedAt = DateTime.UtcNow });
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests with one key arrived together. The other one won.
            _logger.LogInformation("Idempotency key already claimed for user {UserId}", userId);
            return false;
        }
    }

    private static async Task CompleteAsync(IServiceScopeFactory scopes, Guid userId, string key, int status, string? contentType, byte[] body)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IMizanDbContext>();
        await db.IdempotencyKeys.Where(k => k.UserId == userId && k.Key == key).ExecuteUpdateAsync(s => s
            .SetProperty(k => k.StatusCode, status)
            .SetProperty(k => k.ContentType, contentType)
            .SetProperty(k => k.ResponseBody, body)
            .SetProperty(k => k.CompletedAt, DateTime.UtcNow));
    }

    private static async Task DeleteAsync(IServiceScopeFactory scopes, Guid userId, string key)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IMizanDbContext>();
        await db.IdempotencyKeys.Where(k => k.UserId == userId && k.Key == key).ExecuteDeleteAsync();
    }
}
