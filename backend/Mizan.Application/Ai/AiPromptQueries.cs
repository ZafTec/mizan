using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Ai;

public record AiPromptSummaryDto(
    string Key,
    string Description,
    int? PublishedVersion,
    DateTime? PublishedAt,
    int DraftCount,
    int VersionCount);

/// <summary>The console's index: every programmable surface, whether code has ever been given one or not.</summary>
public record ListAiPromptsQuery : IRequest<IReadOnlyList<AiPromptSummaryDto>>;

public class ListAiPromptsQueryHandler : IRequestHandler<ListAiPromptsQuery, IReadOnlyList<AiPromptSummaryDto>>
{
    private readonly IMizanDbContext _context;

    public ListAiPromptsQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<IReadOnlyList<AiPromptSummaryDto>> Handle(
        ListAiPromptsQuery request, CancellationToken cancellationToken)
    {
        var stored = await _context.AiPrompts.AsNoTracking()
            .Select(p => new
            {
                p.Key,
                Versions = p.Versions.Select(v => new { v.Version, v.Status, v.PublishedAt }).ToList(),
            })
            .ToListAsync(cancellationToken);

        // Keys come from code, not from the table: a surface with no row yet is
        // still a surface, and the console should offer to write its first draft.
        return AiPromptKeys.Descriptions.Select(entry =>
        {
            var match = stored.FirstOrDefault(p => p.Key == entry.Key);
            var published = match?.Versions.FirstOrDefault(v => v.Status == AiPromptStatus.Published);

            return new AiPromptSummaryDto(
                entry.Key,
                entry.Value,
                published?.Version,
                published?.PublishedAt,
                match?.Versions.Count(v => v.Status == AiPromptStatus.Draft) ?? 0,
                match?.Versions.Count ?? 0);
        }).ToList();
    }
}

public record AiPromptVersionDto(
    Guid Id,
    int Version,
    string Body,
    string SoftPolicy,
    AiPromptStatus Status,
    string? Notes,
    string? AuthorName,
    DateTime CreatedAt,
    DateTime? PublishedAt);

public record AiPromptDetailDto(
    string Key,
    string Description,
    string DefaultBody,
    string Preamble,
    IReadOnlyList<HardConstraint> HardConstraints,
    IReadOnlyList<AiPromptVersionDto> Versions);

public record GetAiPromptQuery(string Key) : IRequest<AiPromptDetailDto>;

public class GetAiPromptQueryHandler : IRequestHandler<GetAiPromptQuery, AiPromptDetailDto>
{
    private readonly IMizanDbContext _context;

    public GetAiPromptQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<AiPromptDetailDto> Handle(GetAiPromptQuery request, CancellationToken cancellationToken)
    {
        if (!AiPromptKeys.Descriptions.TryGetValue(request.Key, out var description))
        {
            throw new Exceptions.EntityNotFoundException("Prompt", request.Key);
        }

        var versions = await _context.AiPromptVersions.AsNoTracking()
            .Where(v => v.Prompt!.Key == request.Key)
            .OrderByDescending(v => v.Version)
            .Select(v => new AiPromptVersionDto(
                v.Id,
                v.Version,
                v.Body,
                v.SoftPolicy,
                v.Status,
                v.Notes,
                _context.Users.Where(u => u.Id == v.AuthorId).Select(u => u.Name).FirstOrDefault(),
                v.CreatedAt,
                v.PublishedAt))
            .ToListAsync(cancellationToken);

        return new AiPromptDetailDto(
            request.Key,
            description,
            AiPromptDefaults.Body(request.Key),
            AiHardConstraints.Preamble,
            AiHardConstraints.All,
            versions);
    }
}

public record AiEvalCaseDto(Guid Id, string Name, bool IsAdversarial, string Input, string? Context, string Assertions);

public record AiEvalRunDto(
    Guid CaseId,
    AiEvalOutcome Outcome,
    bool SchemaValid,
    string? Output,
    string? FailureReason,
    int Tokens,
    long CostMicros,
    int LatencyMs);

public record AiEvalMatrixDto(
    Guid VersionId,
    IReadOnlyList<AiEvalCaseDto> Cases,
    IReadOnlyList<AiEvalRunDto> Runs,
    bool Publishable,
    string? BlockedReason,
    long CostMicros,
    long? PublishedCostMicros);

public record GetAiEvalMatrixQuery(Guid VersionId) : IRequest<AiEvalMatrixDto>;

public class GetAiEvalMatrixQueryHandler : IRequestHandler<GetAiEvalMatrixQuery, AiEvalMatrixDto>
{
    private readonly IMizanDbContext _context;

    public GetAiEvalMatrixQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<AiEvalMatrixDto> Handle(GetAiEvalMatrixQuery request, CancellationToken cancellationToken)
    {
        var version = await _context.AiPromptVersions.AsNoTracking()
            .Where(v => v.Id == request.VersionId)
            .Select(v => new { v.Id, v.PromptId, Key = v.Prompt!.Key })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Exceptions.EntityNotFoundException("Prompt version", request.VersionId);

        var cases = await _context.AiEvalCases.AsNoTracking()
            .Where(c => c.PromptKey == version.Key)
            .OrderBy(c => c.IsAdversarial).ThenBy(c => c.Name)
            .Select(c => new AiEvalCaseDto(c.Id, c.Name, c.IsAdversarial, c.Input, c.Context, c.Assertions))
            .ToListAsync(cancellationToken);

        var runs = await _context.AiEvalRuns.AsNoTracking()
            .Where(r => r.VersionId == request.VersionId)
            .Select(r => new AiEvalRunDto(
                r.CaseId,
                r.Outcome,
                r.SchemaValid,
                r.Output,
                r.FailureReason,
                r.PromptTokens + r.CompletionTokens,
                r.CostMicros,
                r.LatencyMs))
            .ToListAsync(cancellationToken);

        // The cost of the version currently in production, so the editor can
        // show what publishing this draft would do to the bill.
        var publishedCost = await _context.AiEvalRuns.AsNoTracking()
            .Where(r => r.Version!.PromptId == version.PromptId
                && r.Version.Status == AiPromptStatus.Published)
            .SumAsync(r => (long?)r.CostMicros, cancellationToken);

        var gate = AiPublishGate.Evaluate(cases, runs);

        return new AiEvalMatrixDto(
            request.VersionId,
            cases,
            runs,
            gate.Publishable,
            gate.Reason,
            runs.Sum(r => r.CostMicros),
            publishedCost);
    }
}
