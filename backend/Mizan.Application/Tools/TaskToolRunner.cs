using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Ai;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Tools;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;

namespace Mizan.Application.Tools;

/// <summary>What one task tool call came to, for the surface to pass on.</summary>
public sealed record ToolRunResult(
    string Tool,
    string? Mode,
    bool IsError,
    string Text,
    object? Data,
    string? ErrorCode = null,
    string? Needs = null,
    ToolConfirmationDto? Confirmation = null,
    IReadOnlyList<AiCardDraft>? Cards = null,
    Guid? ConfirmedAction = null);

/// <summary>Runs task tools as the calling user. Every surface goes through it: the API's tools endpoint, MCP behind it, and the assistant.</summary>
public interface ITaskToolRunner
{
    Task<ToolRunResult> RunAsync(string tool, ToolArgs args, ToolCallContext context, CancellationToken cancellationToken = default);

    /// <summary>Carries out a previewed action by its token (<see cref="ConfirmToolActionCommand"/>).</summary>
    Task<ToolRunResult> ConfirmAsync(string token, ToolCallContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// The one bridge from a task tool to a command. It checks the mode against the caller's role,
/// surface and connection on every call (failing closed), builds the existing MediatR request and
/// sends it, so validation, feature gates and the audit log apply exactly as on the web.
/// A high-impact mode is previewed and held as a <see cref="PendingToolAction"/> instead.
/// </summary>
public sealed class TaskToolRunner : ITaskToolRunner
{
    private readonly ISender _sender;
    private readonly IMizanDbContext _context;
    private readonly ILogger<TaskToolRunner> _logger;

    public TaskToolRunner(ISender sender, IMizanDbContext context, ILogger<TaskToolRunner> logger)
    {
        _sender = sender;
        _context = context;
        _logger = logger;
    }

    public async Task<ToolRunResult> RunAsync(string name, ToolArgs args, ToolCallContext context, CancellationToken cancellationToken = default)
    {
        var tool = TaskToolCatalogue.Find(name);
        if (tool is null) return Error(name, null, "unknown_tool", $"There is no tool called '{name}'.");

        var mode = tool.ModeFor(args);
        var allowed = ToolFilter.Allowed(tool, context);
        if (mode is null)
        {
            var choices = string.Join(", ", allowed.Select(m => m.Name));
            return allowed.Count == 0
                ? Error(name, null, "not_allowed", $"This connection cannot use {name}.")
                : Error(name, null, "unknown_mode", $"Say which one with '{tool.ModeArgument}': {choices}.");
        }

        if (!allowed.Contains(mode))
        {
            var needs = ToolFilter.Refusal(mode, context) ?? mode.Area;
            return Error(name, mode.Name, "not_allowed", NotAllowed(tool, mode, needs), needs);
        }

        return mode.Confirm is not null
            ? await PreviewAsync(tool, mode, args, context, cancellationToken)
            : await ExecuteAsync(tool, mode, args, context, cancellationToken);
    }

    private async Task<ToolRunResult> ExecuteAsync(TaskTool tool, ToolMode mode, ToolArgs args, ToolCallContext context, CancellationToken cancellationToken)
    {
        object? result;
        try
        {
            var request = mode.Build(args, context);
            result = await _sender.Send(request, cancellationToken);
        }
        catch (Exception ex) when (Failure(tool, mode, ex) is { } failed)
        {
            return failed;
        }

        if (result is ToolRunResult nested) return nested;
        if (Unsuccessful(result) is { } message) return Error(tool.Name, mode.Name, "failed", message);

        var data = mode.Shape is null ? result : mode.Shape(result);
        var text = mode.Describe?.Invoke(result) ?? Serialize(data);
        return new ToolRunResult(tool.Name, mode.Name, false, text, data, Cards: mode.Cards?.Invoke(result));
    }

    private async Task<ToolRunResult> PreviewAsync(TaskTool tool, ToolMode mode, ToolArgs args, ToolCallContext context, CancellationToken cancellationToken)
    {
        JsonNode facts;
        try
        {
            facts = await FactsAsync(mode, args, context, cancellationToken);
        }
        catch (Exception ex) when (Failure(tool, mode, ex) is { } failed)
        {
            return failed;
        }

        var token = "mzc_" + SecureToken.Generate();
        var now = DateTime.UtcNow;
        var arguments = args.Root.GetRawText();
        var pending = new PendingToolAction
        {
            Id = Guid.CreateVersion7(),
            UserId = context.UserId,
            GrantId = context.Grant?.GrantId,
            Tool = tool.Name,
            Mode = mode.Name,
            ArgumentsJson = arguments,
            ArgumentsHash = SecureToken.Hash(arguments),
            FactsHash = SecureToken.Hash(facts.ToJsonString()),
            TokenHash = SecureToken.Hash(token),
            CreatedAt = now,
            ExpiresAt = now + PendingToolAction.Lifetime,
        };
        _context.PendingToolActions.Add(pending);
        await _context.SaveChangesAsync(cancellationToken);

        var how = tool.ModeArgument is { } argument
            ? $"call {tool.Name} with {argument} \"{TaskToolCatalogue.ConfirmMode}\" and this token"
            : $"call {tool.Name} with this token";
        var preview = JsonSerializer.SerializeToElement(facts);
        var text = $"Nothing is removed yet. Show the person what this removes: {facts.ToJsonString()}. "
            + $"Only when they agree, {how}: {token}. It works once and expires in {PendingToolAction.Lifetime.TotalMinutes:0} minutes.";
        return new ToolRunResult(tool.Name, mode.Name, false, text, new { preview = facts, confirmToken = token, expiresAt = pending.ExpiresAt },
            Confirmation: new ToolConfirmationDto(token, pending.ExpiresAt, preview));
    }

    public async Task<ToolRunResult> ConfirmAsync(string token, ToolCallContext context, CancellationToken cancellationToken = default)
    {
        const string confirmTool = "confirm";
        if (string.IsNullOrWhiteSpace(token)) return Error(confirmTool, null, "invalid_token", "Pass the token the preview returned.");

        var hash = SecureToken.Hash(token.Trim());
        var pending = await _context.PendingToolActions.AsNoTracking().FirstOrDefaultAsync(p => p.TokenHash == hash, cancellationToken);

        // Another person's token, or one from another connection, reads as no token at all.
        if (pending is null || pending.UserId != context.UserId || pending.GrantId != context.Grant?.GrantId)
            return Error(confirmTool, null, "invalid_token", "That confirmation is not valid. Preview the action again.");

        var tool = TaskToolCatalogue.Find(pending.Tool);
        var mode = tool?.Modes.FirstOrDefault(m => m.Name == pending.Mode);
        if (tool is null || mode?.Confirm is null || SecureToken.Hash(pending.ArgumentsJson) != pending.ArgumentsHash)
            return Error(confirmTool, null, "invalid_token", "That confirmation is not valid. Preview the action again.");

        if (pending.UsedAt is not null)
            return Error(tool.Name, mode.Name, "token_used", "That confirmation was already used. Preview the action again.");
        if (pending.ExpiresAt <= DateTime.UtcNow)
            return Error(tool.Name, mode.Name, "token_expired", "That confirmation expired. Preview the action again.");

        // The person's role and the connection may have changed since the preview.
        if (ToolFilter.Refusal(mode, context) is { } needs)
            return Error(tool.Name, mode.Name, "not_allowed", NotAllowed(tool, mode, needs), needs);

        var args = ToolArgs.Parse(pending.ArgumentsJson);
        JsonNode facts;
        try
        {
            facts = await FactsAsync(mode, args, context, cancellationToken);
        }
        catch (Exception ex) when (Failure(tool, mode, ex) is { } failed)
        {
            await ClaimAsync(pending.Id, cancellationToken);
            return failed;
        }

        // The token is spent whatever happens next, and only one caller can spend it.
        if (!await ClaimAsync(pending.Id, cancellationToken))
            return Error(tool.Name, mode.Name, "token_used", "That confirmation was already used. Preview the action again.");

        if (SecureToken.Hash(facts.ToJsonString()) != pending.FactsHash)
            return Error(tool.Name, mode.Name, "facts_changed",
                $"What this would remove has changed since the preview, so nothing was done. It now reads: {facts.ToJsonString()}. Preview it again and ask the person.");

        var done = await ExecuteAsync(tool, mode, args, context, cancellationToken);
        return done with { ConfirmedAction = pending.Id };
    }

    private async Task<JsonNode> FactsAsync(ToolMode mode, ToolArgs args, ToolCallContext context, CancellationToken cancellationToken)
    {
        var request = mode.Confirm!.BuildPreview(args, context);
        var result = await _sender.Send(request, cancellationToken);
        if (Unsuccessful(result) is { } message) throw new EntityNotFoundException(message);
        return mode.Confirm.Facts(result);
    }

    private async Task<bool> ClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.PendingToolActions
            .Where(p => p.Id == id && p.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.UsedAt, now), cancellationToken) == 1;
    }

    private ToolRunResult? Failure(TaskTool tool, ToolMode mode, Exception ex)
    {
        switch (ex)
        {
            case ValidationException validation:
                return Error(tool.Name, mode.Name, "validation_failed", string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
            case EntityNotFoundException:
                return Error(tool.Name, mode.Name, "not_found", ex.Message);
            case UpgradeRequiredException:
                return Error(tool.Name, mode.Name, "upgrade_required", ex.Message);
            case FeatureDisabledException:
                return Error(tool.Name, mode.Name, "feature_disabled", ex.Message);
            case ForbiddenAccessException or UnauthorizedAccessException:
                return Error(tool.Name, mode.Name, "forbidden", ex.Message);
            case AiQuotaExceededException:
                return Error(tool.Name, mode.Name, "ai_quota_exceeded", ex.Message);
            case AiUnavailableException:
                return Error(tool.Name, mode.Name, "ai_unavailable", ex.Message);
            case PlanNeedsFoodsException:
                return Error(tool.Name, mode.Name, PlanNeedsFoodsException.Code, ex.Message);
            case ConflictException:
                return Error(tool.Name, mode.Name, "conflict", ex.Message);
            case DomainException:
                return Error(tool.Name, mode.Name, "invalid", ex.Message);
            case OperationCanceledException:
                return null;
            default:
                // Ours, not the caller's: logged, and the caller is told plainly without a stack trace.
                _logger.LogError(ex, "Task tool {Tool} ({Mode}) failed", tool.Name, mode.Name);
                return Error(tool.Name, mode.Name, "failed", "That could not be completed.");
        }
    }

    /// <summary>A result that reports its own failure (<c>Success: false</c>) the way the controllers turn into a 4xx.</summary>
    private static string? Unsuccessful(object? result)
    {
        if (result is null) return null;
        var type = result.GetType();
        if (type.GetProperty("Success", BindingFlags.Public | BindingFlags.Instance) is not { } success
            || success.PropertyType != typeof(bool)
            || (bool)success.GetValue(result)!) return null;
        foreach (var name in new[] { "Error", "Message", "ErrorMessage" })
        {
            if (type.GetProperty(name)?.GetValue(result) is string { Length: > 0 } text) return text;
        }
        return "That could not be completed.";
    }

    private static string NotAllowed(TaskTool tool, ToolMode mode, string needs) =>
        needs.StartsWith("role:", StringComparison.Ordinal)
            ? $"{tool.Name} ({mode.Name}) is for {needs["role:".Length..]}s only."
            : needs.StartsWith("surface:", StringComparison.Ordinal)
                ? $"{tool.Name} ({mode.Name}) is not offered here."
                : $"This connection is not allowed to use {tool.Name} ({mode.Name}). It needs '{needs}'. The person can change that in Mizan under More, Connected apps.";

    public static string Serialize(object? data) =>
        data is null ? "null" : JsonSerializer.Serialize(data, ToolArgs.Json);

    private static ToolRunResult Error(string tool, string? mode, string code, string message, string? needs = null) =>
        new(tool, mode, true, message, null, code, needs);
}
