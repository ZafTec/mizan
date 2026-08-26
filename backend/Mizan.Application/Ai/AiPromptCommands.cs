using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Ai;

/// <summary>
/// Starts a new draft for a key, seeded from whatever is published (or from
/// the built-in default when nothing is). Versions are immutable once
/// published, so editing production means branching from it.
/// </summary>
public record CreateAiPromptDraftCommand(string Key, string? Body, string? SoftPolicy, string? Notes)
    : IRequest<AiPromptVersionDto>;

public class CreateAiPromptDraftCommandValidator : AbstractValidator<CreateAiPromptDraftCommand>
{
    public CreateAiPromptDraftCommandValidator()
    {
        RuleFor(c => c.Key).NotEmpty()
            .Must(AiPromptKeys.Descriptions.ContainsKey).WithMessage("Unknown prompt key.");
        RuleFor(c => c.Body).MaximumLength(20_000);
        RuleFor(c => c.Notes).MaximumLength(500);
        RuleFor(c => c.SoftPolicy!).Must(BeJson)
            .When(c => !string.IsNullOrWhiteSpace(c.SoftPolicy))
            .WithMessage("Soft policy must be a JSON object.");
    }

    internal static bool BeJson(string value)
    {
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(value);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}

public class CreateAiPromptDraftCommandHandler
    : IRequestHandler<CreateAiPromptDraftCommand, AiPromptVersionDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateAiPromptDraftCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AiPromptVersionDto> Handle(
        CreateAiPromptDraftCommand request, CancellationToken cancellationToken)
    {
        var prompt = await _context.AiPrompts
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.Key == request.Key, cancellationToken);

        if (prompt is null)
        {
            prompt = new AiPrompt
            {
                Id = Guid.CreateVersion7(),
                Key = request.Key,
                Description = AiPromptKeys.Descriptions[request.Key],
                CreatedAt = DateTime.UtcNow,
            };
            _context.AiPrompts.Add(prompt);
        }

        var current = prompt.Versions.FirstOrDefault(v => v.Status == AiPromptStatus.Published);

        var version = new AiPromptVersion
        {
            Id = Guid.CreateVersion7(),
            PromptId = prompt.Id,
            Version = prompt.Versions.Count == 0 ? 1 : prompt.Versions.Max(v => v.Version) + 1,
            Body = Trimmed(request.Body) ?? current?.Body ?? AiPromptDefaults.Body(request.Key),
            SoftPolicy = Trimmed(request.SoftPolicy) ?? current?.SoftPolicy ?? "{}",
            Status = AiPromptStatus.Draft,
            AuthorId = _currentUser.UserId,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
        };

        _context.AiPromptVersions.Add(version);
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(version, null);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static AiPromptVersionDto ToDto(AiPromptVersion version, string? authorName) =>
        new(version.Id, version.Version, version.Body, version.SoftPolicy, version.Status,
            version.Notes, authorName, version.CreatedAt, version.PublishedAt);
}

/// <summary>Edits a draft. A published or archived version is history and is never rewritten.</summary>
public record UpdateAiPromptDraftCommand(Guid Id, string Body, string SoftPolicy, string? Notes)
    : IRequest<AiPromptVersionDto>;

public class UpdateAiPromptDraftCommandValidator : AbstractValidator<UpdateAiPromptDraftCommand>
{
    public UpdateAiPromptDraftCommandValidator()
    {
        RuleFor(c => c.Body).NotEmpty().MaximumLength(20_000);
        RuleFor(c => c.Notes).MaximumLength(500);
        RuleFor(c => c.SoftPolicy).NotEmpty()
            .Must(CreateAiPromptDraftCommandValidator.BeJson)
            .WithMessage("Soft policy must be a JSON object.");
    }
}

public class UpdateAiPromptDraftCommandHandler
    : IRequestHandler<UpdateAiPromptDraftCommand, AiPromptVersionDto>
{
    private readonly IMizanDbContext _context;

    public UpdateAiPromptDraftCommandHandler(IMizanDbContext context) => _context = context;

    public async Task<AiPromptVersionDto> Handle(
        UpdateAiPromptDraftCommand request, CancellationToken cancellationToken)
    {
        var version = await _context.AiPromptVersions
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Prompt version", request.Id);

        if (version.Status != AiPromptStatus.Draft)
        {
            throw new DomainValidationException(
                "Only a draft can be edited. Branch a new draft from this version instead.");
        }

        version.Body = request.Body.Trim();
        version.SoftPolicy = request.SoftPolicy.Trim();
        version.Notes = request.Notes;

        // Editing invalidates whatever the old text proved.
        await _context.AiEvalRuns
            .Where(r => r.VersionId == version.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return CreateAiPromptDraftCommandHandler.ToDto(version, null);
    }
}

/// <summary>Runs the suite against a draft. The only way to earn a publish button.</summary>
public record RunAiPromptEvalsCommand(Guid Id) : IRequest<EvalSummary>;

public class RunAiPromptEvalsCommandHandler : IRequestHandler<RunAiPromptEvalsCommand, EvalSummary>
{
    private readonly IAiEvalRunner _runner;
    private readonly ICurrentUserService _currentUser;

    public RunAiPromptEvalsCommandHandler(IAiEvalRunner runner, ICurrentUserService currentUser)
    {
        _runner = runner;
        _currentUser = currentUser;
    }

    public Task<EvalSummary> Handle(RunAiPromptEvalsCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        return _runner.RunAsync(request.Id, adminId, cancellationToken);
    }
}

/// <summary>
/// Promotes a draft, archiving whatever it replaces. Refuses when the
/// adversarial set has not been beaten - the gate is here and not only in the
/// console, because a console is a suggestion and a handler is a rule.
/// </summary>
public record PublishAiPromptVersionCommand(Guid Id) : IRequest<AiPromptVersionDto>;

public class PublishAiPromptVersionCommandHandler
    : IRequestHandler<PublishAiPromptVersionCommand, AiPromptVersionDto>
{
    private readonly IMizanDbContext _context;
    private readonly IMediator _mediator;

    public PublishAiPromptVersionCommandHandler(IMizanDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<AiPromptVersionDto> Handle(
        PublishAiPromptVersionCommand request, CancellationToken cancellationToken)
    {
        var version = await _context.AiPromptVersions
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Prompt version", request.Id);

        if (version.Status == AiPromptStatus.Published)
        {
            return CreateAiPromptDraftCommandHandler.ToDto(version, null);
        }

        // An archived version was published before, which means it cleared this
        // same gate. Re-running the suite to move a pointer back is exactly the
        // friction that stops people rolling back when they should.
        if (version.Status == AiPromptStatus.Draft)
        {
            var matrix = await _mediator.Send(new GetAiEvalMatrixQuery(version.Id), cancellationToken);
            if (!matrix.Publishable)
            {
                throw new DomainValidationException(matrix.BlockedReason ?? "This draft has not passed its evals.");
            }
        }

        await SwapAsync(version, cancellationToken);

        return CreateAiPromptDraftCommandHandler.ToDto(version, null);
    }

    private async Task SwapAsync(AiPromptVersion version, CancellationToken cancellationToken)
    {
        var incumbent = await _context.AiPromptVersions
            .FirstOrDefaultAsync(
                v => v.PromptId == version.PromptId && v.Status == AiPromptStatus.Published,
                cancellationToken);

        if (incumbent is not null)
        {
            // Archived first and saved, because the database enforces one
            // published version per prompt with a filtered unique index.
            incumbent.Status = AiPromptStatus.Archived;
            await _context.SaveChangesAsync(cancellationToken);
        }

        version.Status = AiPromptStatus.Published;
        version.PublishedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
