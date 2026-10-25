using System.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Ai;
using Mizan.Application.Behaviors;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;
using Mizan.Domain.Settings;

namespace Mizan.Application.Settings;

/// <summary>Every runtime setting in the sections of the admin page (docs/ADMIN.md#runtime-settings).</summary>
public record GetRuntimeSettingsQuery : IRequest<RuntimeSettingsDto>;

/// <summary>
/// Sets one runtime setting. The audit row names the key (as its entity id and in the details) and
/// stores the value, except for a secret, whose value is replaced by the redaction marker.
/// </summary>
public record SetRuntimeSettingCommand(string Key, string Value) : IRequest<RuntimeSettingDto>, IRedactedAudit
{
    /// <summary>The key again, so the audit row's entity id names the setting.</summary>
    public string Id => Key;

    // An unknown key is treated as a secret here: the command is refused, but the audit details are
    // built before anyone knows that, and the safe guess keeps a mistyped secret out of the log.
    object IRedactedAudit.AuditDetails => new
    {
        Key = RuntimeSettingDefinitions.Find(Key)?.Key ?? Key,
        Value = RuntimeSettingDefinitions.Find(Key) is { IsSecret: false } ? Value : AuditRedaction.Placeholder,
    };
}

/// <summary>Clears one runtime setting, so the configuration value applies again.</summary>
public record ClearRuntimeSettingCommand(string Key) : IRequest<RuntimeSettingDto>, IRedactedAudit
{
    /// <summary>The key again, so the audit row's entity id names the setting.</summary>
    public string Id => Key;

    object IRedactedAudit.AuditDetails => new { Key = RuntimeSettingDefinitions.Find(Key)?.Key ?? Key, Cleared = true };
}

/// <summary>Queues one message to the signed-in administrator through the outbox, with the SMTP settings as they stand.</summary>
public record SendTestEmailCommand : IRequest<SmtpTestResult>;

/// <summary>One tiny provider call on the evaluation line, reserved and settled like any other.</summary>
public record TestAiProviderCommand : IRequest<AiProviderTestResult>;

public sealed class SetRuntimeSettingCommandValidator : AbstractValidator<SetRuntimeSettingCommand>
{
    public SetRuntimeSettingCommandValidator()
    {
        // An unknown key is a 404 from the handler, not a validation message.
        RuleFor(c => c.Value).Custom((value, context) =>
        {
            var definition = RuntimeSettingDefinitions.Find(context.InstanceToValidate.Key);
            if (definition is null) return;
            var result = RuntimeSettingDefinitions.Validate(definition, value);
            if (!result.IsValid) context.AddFailure(nameof(SetRuntimeSettingCommand.Value), result.Error!);
        });
    }
}

internal static class RuntimeSettingMapping
{
    public static RuntimeSettingDefinition Require(string key) =>
        RuntimeSettingDefinitions.Find(key)
        ?? throw new EntityNotFoundException($"There is no runtime setting named '{key}'.");

    public static RuntimeSettingDto ToDto(RuntimeSettingState state)
    {
        var d = state.Definition;
        return new RuntimeSettingDto
        {
            Key = d.Key,
            Group = d.Group,
            Label = d.Label,
            Description = d.Description,
            Type = d.Type.ToString().ToLowerInvariant(),
            Format = d.Format.ToString().ToLowerInvariant(),
            IsSecret = d.IsSecret,
            AllowEmpty = d.AllowEmpty,
            AllowedValues = d.AllowedValues,
            Min = d.Type == RuntimeSettingType.Int ? d.Min ?? 0 : null,
            Max = d.Type == RuntimeSettingType.Int ? d.Max : null,
            // Belt and braces: the store already withholds a secret's value.
            Value = d.IsSecret ? null : state.Value,
            HasValue = state.HasValue,
            Source = state.Source,
            UpdatedAt = state.UpdatedAt,
            UpdatedByUserId = state.UpdatedByUserId,
        };
    }

    public static async Task<RuntimeSettingDto> CurrentAsync(
        IRuntimeSettings settings, RuntimeSettingDefinition definition, CancellationToken cancellationToken)
    {
        var all = await settings.ListAsync(cancellationToken);
        return ToDto(all.Single(s => s.Definition.Key == definition.Key));
    }
}

public sealed class GetRuntimeSettingsQueryHandler : IRequestHandler<GetRuntimeSettingsQuery, RuntimeSettingsDto>
{
    private readonly IRuntimeSettings _settings;

    public GetRuntimeSettingsQueryHandler(IRuntimeSettings settings) => _settings = settings;

    public async Task<RuntimeSettingsDto> Handle(GetRuntimeSettingsQuery request, CancellationToken cancellationToken)
    {
        var all = await _settings.ListAsync(cancellationToken);
        var groups = RuntimeSettingGroups.All
            .Select(g => new RuntimeSettingGroupDto(
                g.Id,
                g.Title,
                all.Where(s => s.Definition.Group == g.Id).Select(RuntimeSettingMapping.ToDto).ToList()))
            .ToList();
        return new RuntimeSettingsDto(groups);
    }
}

public sealed class SetRuntimeSettingCommandHandler : IRequestHandler<SetRuntimeSettingCommand, RuntimeSettingDto>
{
    private readonly IRuntimeSettings _settings;
    private readonly ICurrentUserService _user;

    public SetRuntimeSettingCommandHandler(IRuntimeSettings settings, ICurrentUserService user)
    {
        _settings = settings;
        _user = user;
    }

    public async Task<RuntimeSettingDto> Handle(SetRuntimeSettingCommand request, CancellationToken cancellationToken)
    {
        var definition = RuntimeSettingMapping.Require(request.Key);
        var checkedValue = RuntimeSettingDefinitions.Validate(definition, request.Value);
        if (!checkedValue.IsValid) throw new DomainValidationException(checkedValue.Error!);

        await _settings.SetAsync(definition.Key, checkedValue.Value!, _user.UserId, cancellationToken);
        return await RuntimeSettingMapping.CurrentAsync(_settings, definition, cancellationToken);
    }
}

public sealed class ClearRuntimeSettingCommandHandler : IRequestHandler<ClearRuntimeSettingCommand, RuntimeSettingDto>
{
    private readonly IRuntimeSettings _settings;

    public ClearRuntimeSettingCommandHandler(IRuntimeSettings settings) => _settings = settings;

    public async Task<RuntimeSettingDto> Handle(ClearRuntimeSettingCommand request, CancellationToken cancellationToken)
    {
        var definition = RuntimeSettingMapping.Require(request.Key);
        await _settings.ClearAsync(definition.Key, cancellationToken);
        return await RuntimeSettingMapping.CurrentAsync(_settings, definition, cancellationToken);
    }
}

public sealed class SendTestEmailCommandHandler : IRequestHandler<SendTestEmailCommand, SmtpTestResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly IEmailQueue _emails;

    public SendTestEmailCommandHandler(IMizanDbContext context, ICurrentUserService user, IEmailQueue emails)
    {
        _context = context;
        _user = user;
        _emails = emails;
    }

    public async Task<SmtpTestResult> Handle(SendTestEmailCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenAccessException();
        var email = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("User", userId);

        var sentAt = DateTime.UtcNow.ToString("u");
        // Through the email queue, so the message shows in the mail log like any other.
        var messageId = await _emails.QueueAsync(
            new OutgoingEmail(
                email,
                "Mizan test email",
                $"<p>This is a test message from the Mizan admin area, sent {sentAt}. The SMTP settings work.</p>",
                $"This is a test message from the Mizan admin area, sent {sentAt}. The SMTP settings work."),
            EmailKinds.SettingsTest,
            toUserId: userId,
            createdByUserId: userId,
            cancellationToken: cancellationToken);
        var jobId = _context.EmailMessages.Local.Single(m => m.Id == messageId).OutboxJobId
            ?? throw new InvalidOperationException("The test email was staged without an outbox job.");
        await _context.SaveChangesAsync(cancellationToken);

        return new SmtpTestResult(jobId, email);
    }
}

public sealed class TestAiProviderCommandHandler : IRequestHandler<TestAiProviderCommand, AiProviderTestResult>
{
    /// <summary>Enough for a reasoning model to say one word, small enough to cost nothing to speak of.</summary>
    private const int MaxOutputTokens = 128;

    private readonly IAiMeteredClient _ai;
    private readonly ICurrentUserService _user;

    public TestAiProviderCommandHandler(IAiMeteredClient ai, ICurrentUserService user)
    {
        _ai = ai;
        _user = user;
    }

    public async Task<AiProviderTestResult> Handle(TestAiProviderCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenAccessException();
        if (!await _ai.IsConfiguredAsync(cancellationToken))
        {
            return new AiProviderTestResult(false, "not_configured", null, 0);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await _ai.CompleteAsync(
                new AiMeteredCall(userId, null, AiFeatures.Eval, EstimatedTokens: 40 + MaxOutputTokens, PromptVersionId: null),
                new AiCompletionRequest
                {
                    Messages = [new AiMessage(AiRole.User, "Reply with the single word: ok")],
                    MaxOutputTokens = MaxOutputTokens,
                    Temperature = 0,
                },
                cancellationToken);
            return new AiProviderTestResult(true, null, response.Model, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (AiQuotaExceededException)
        {
            return new AiProviderTestResult(false, "quota_exceeded", null, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (AiUnavailableException ex)
        {
            return new AiProviderTestResult(false, ex.Reason, null, (int)stopwatch.ElapsedMilliseconds);
        }
    }
}
