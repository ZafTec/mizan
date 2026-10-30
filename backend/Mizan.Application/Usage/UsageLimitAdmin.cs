using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Ai;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;
using Mizan.Domain.Usage;

namespace Mizan.Application.Usage;

/// <summary>
/// Plans (every catalogue plan), households or users (those with an override),
/// each with its overrides and the limits they produce (docs/ADMIN.md#usage-limits).
/// </summary>
public record ListUsageLimitsQuery(string Scope) : IRequest<IReadOnlyList<UsageLimitSubjectDto>>;

/// <summary>One plan, household or user, with or without overrides, so an administrator can add the first.</summary>
public record GetUsageLimitSubjectQuery(string Scope, Guid SubjectId) : IRequest<UsageLimitSubjectDto>;

/// <summary>Creates or replaces the override for one subject and line. A null line covers every line.</summary>
public record SetUsageLimitCommand(
    string Scope,
    Guid SubjectId,
    string? Line,
    int? DailyRequests,
    long? DailyTokens,
    long? DailyCostMicros,
    long? MonthlyCostMicros,
    string? Note) : IRequest<UsageLimitSubjectDto>;

/// <summary>Removes the override for one subject and line, so it inherits again.</summary>
public record ClearUsageLimitCommand(string Scope, Guid SubjectId, string? Line) : IRequest<Unit>;

/// <summary>Parsing, naming and describing, shared by the admin handlers.</summary>
public sealed class UsageLimitSubjects
{
    /// <summary>The lines in the order the page lists them.</summary>
    public static readonly AiQuotaLine[] Lines =
        [AiQuotaLine.Personal, AiQuotaLine.Planning, AiQuotaLine.Onboarding, AiQuotaLine.TrainerClient, AiQuotaLine.Eval];

    private readonly IMizanDbContext _context;
    private readonly UsageLimitService _limits;
    private readonly IAiTierDefaults _tiers;
    private readonly IEntitlementService _entitlements;

    public UsageLimitSubjects(IMizanDbContext context, UsageLimitService limits, IAiTierDefaults tiers, IEntitlementService entitlements)
    {
        _context = context;
        _limits = limits;
        _tiers = tiers;
        _entitlements = entitlements;
    }

    public static UsageLimitScope ParseScope(string? scope) =>
        Enum.TryParse<UsageLimitScope>(scope, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new DomainValidationException("The scope is Plan, Household or User.");

    /// <summary>The line's canonical name, or null for every line ("all" or empty).</summary>
    public static string? ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || string.Equals(line, "all", StringComparison.OrdinalIgnoreCase)) return null;
        return Enum.TryParse<AiQuotaLine>(line, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed.ToString()
            : throw new DomainValidationException(
                $"The line is one of {string.Join(", ", Lines)}, or empty for every line.");
    }

    public async Task<UsageLimitSubjectDto> DescribeAsync(UsageLimitScope scope, Guid subjectId, CancellationToken ct)
    {
        var (name, detail) = await NameAsync(scope, subjectId, ct)
            ?? throw new EntityNotFoundException($"There is no {scope.ToString().ToLowerInvariant()} with id '{subjectId}'.");
        var rows = await _context.UsageLimitOverrides.AsNoTracking()
            .Where(o => o.Scope == scope && o.SubjectId == subjectId)
            .ToListAsync(ct);
        var overrides = rows.Select(r => new CachedUsageLimit(r.Scope, r.SubjectId, r.Line, r.Values)).ToList();

        var isPro = scope switch
        {
            UsageLimitScope.Plan => true,
            UsageLimitScope.Household => false,
            _ => (await _entitlements.GetAsync(subjectId, ct)).IsPro,
        };

        var effective = new List<EffectiveUsageLimitDto>();
        foreach (var line in Lines)
        {
            var composed = scope == UsageLimitScope.User
                ? (await _limits.ResolveAsync(subjectId, null, line, ct)).Limits
                : UsageLimitComposition.Compose(
                    await _tiers.ForAsync(line, isPro, ct),
                    UsageLimitService.Layers(overrides, line, (scope, subjectId)));
            effective.Add(ToDto(line, composed));
        }

        return new UsageLimitSubjectDto
        {
            Scope = scope.ToString(),
            SubjectId = subjectId,
            Name = name,
            Detail = detail,
            Tier = isPro ? "pro" : "free",
            Overrides = rows
                .OrderBy(r => r.Line is null ? -1 : Array.IndexOf(Lines, Enum.Parse<AiQuotaLine>(r.Line)))
                .Select(ToDto)
                .ToList(),
            Effective = effective,
        };
    }

    public async Task<(string Name, string? Detail)?> NameAsync(UsageLimitScope scope, Guid subjectId, CancellationToken ct)
    {
        switch (scope)
        {
            case UsageLimitScope.Plan:
                var plan = await _context.BillingPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == subjectId, ct);
                return plan is null
                    ? null
                    : (plan.Name, $"{plan.AmountCents / 100m:0.00} {plan.Currency} a {plan.Interval}{(plan.IsActive ? "" : ", archived")}");
            case UsageLimitScope.Household:
                var household = await _context.Households.AsNoTracking()
                    .Where(h => h.Id == subjectId)
                    .Select(h => new { h.Name, Members = h.Members.Count })
                    .FirstOrDefaultAsync(ct);
                return household is null
                    ? null
                    : (household.Name, household.Members == 1 ? "1 member" : $"{household.Members} members");
            default:
                var user = await _context.Users.AsNoTracking()
                    .Where(u => u.Id == subjectId)
                    .Select(u => new { u.Name, u.Email })
                    .FirstOrDefaultAsync(ct);
                return user is null ? null : (user.Name ?? user.Email, user.Email);
        }
    }

    public static UsageLimitOverrideDto ToDto(UsageLimitOverride row) => new()
    {
        Id = row.Id,
        Scope = row.Scope.ToString(),
        SubjectId = row.SubjectId,
        Line = row.Line,
        DailyRequests = row.DailyRequests,
        DailyTokens = row.DailyTokens,
        DailyCostMicros = row.DailyCostMicros,
        MonthlyCostMicros = row.MonthlyCostMicros,
        Note = row.Note,
        UpdatedAt = row.UpdatedAt,
    };

    private static EffectiveUsageLimitDto ToDto(AiQuotaLine line, ComposedUsageLimits composed) => new()
    {
        Line = line.ToString(),
        DailyRequests = new SourcedLimitDto(composed.DailyRequests.Value, composed.DailyRequests.Source.ToString()),
        DailyTokens = new SourcedLimitDto(composed.DailyTokens.Value, composed.DailyTokens.Source.ToString()),
        DailyCostMicros = new SourcedLimitDto(composed.DailyCostMicros.Value, composed.DailyCostMicros.Source.ToString()),
        MonthlyCostMicros = new SourcedLimitDto(composed.MonthlyCostMicros.Value, composed.MonthlyCostMicros.Source.ToString()),
    };
}

public class ListUsageLimitsQueryHandler : IRequestHandler<ListUsageLimitsQuery, IReadOnlyList<UsageLimitSubjectDto>>
{
    private readonly IMizanDbContext _context;
    private readonly UsageLimitSubjects _subjects;

    public ListUsageLimitsQueryHandler(IMizanDbContext context, UsageLimitSubjects subjects)
    {
        _context = context;
        _subjects = subjects;
    }

    public async Task<IReadOnlyList<UsageLimitSubjectDto>> Handle(ListUsageLimitsQuery request, CancellationToken cancellationToken)
    {
        var scope = UsageLimitSubjects.ParseScope(request.Scope);
        var ids = scope == UsageLimitScope.Plan
            ? await _context.BillingPlans.AsNoTracking()
                .OrderByDescending(p => p.IsActive).ThenBy(p => p.SortOrder).ThenBy(p => p.Name)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken)
            : await _context.UsageLimitOverrides.AsNoTracking()
                .Where(o => o.Scope == scope)
                .Select(o => o.SubjectId)
                .Distinct()
                .ToListAsync(cancellationToken);

        var subjects = new List<UsageLimitSubjectDto>();
        foreach (var id in ids)
        {
            // An override can outlive its subject (a deleted account or household); it is listed no more.
            if (await _subjects.NameAsync(scope, id, cancellationToken) is null) continue;
            subjects.Add(await _subjects.DescribeAsync(scope, id, cancellationToken));
        }

        return scope == UsageLimitScope.Plan ? subjects : subjects.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public class GetUsageLimitSubjectQueryHandler : IRequestHandler<GetUsageLimitSubjectQuery, UsageLimitSubjectDto>
{
    private readonly UsageLimitSubjects _subjects;

    public GetUsageLimitSubjectQueryHandler(UsageLimitSubjects subjects) => _subjects = subjects;

    public Task<UsageLimitSubjectDto> Handle(GetUsageLimitSubjectQuery request, CancellationToken cancellationToken) =>
        _subjects.DescribeAsync(UsageLimitSubjects.ParseScope(request.Scope), request.SubjectId, cancellationToken);
}

public class SetUsageLimitCommandHandler : IRequestHandler<SetUsageLimitCommand, UsageLimitSubjectDto>
{
    private readonly IMizanDbContext _context;
    private readonly UsageLimitSubjects _subjects;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public SetUsageLimitCommandHandler(IMizanDbContext context, UsageLimitSubjects subjects, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _subjects = subjects;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<UsageLimitSubjectDto> Handle(SetUsageLimitCommand request, CancellationToken cancellationToken)
    {
        var scope = UsageLimitSubjects.ParseScope(request.Scope);
        var line = UsageLimitSubjects.ParseLine(request.Line);
        if (await _subjects.NameAsync(scope, request.SubjectId, cancellationToken) is null)
        {
            throw new EntityNotFoundException($"There is no {scope.ToString().ToLowerInvariant()} with id '{request.SubjectId}'.");
        }

        var row = await _context.UsageLimitOverrides
            .FirstOrDefaultAsync(o => o.Scope == scope && o.SubjectId == request.SubjectId && o.Line == line, cancellationToken);
        if (row is null)
        {
            row = new UsageLimitOverride { Id = Guid.CreateVersion7(), Scope = scope, SubjectId = request.SubjectId, Line = line };
            _context.UsageLimitOverrides.Add(row);
        }

        row.DailyRequests = request.DailyRequests;
        row.DailyTokens = request.DailyTokens;
        row.DailyCostMicros = request.DailyCostMicros;
        row.MonthlyCostMicros = request.MonthlyCostMicros;
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedByUserId = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.UsageLimits, cancellationToken);

        return await _subjects.DescribeAsync(scope, request.SubjectId, cancellationToken);
    }
}

public class SetUsageLimitCommandValidator : AbstractValidator<SetUsageLimitCommand>
{
    public SetUsageLimitCommandValidator()
    {
        RuleFor(c => c.DailyRequests).GreaterThanOrEqualTo(0);
        RuleFor(c => c.DailyTokens).GreaterThanOrEqualTo(0);
        RuleFor(c => c.DailyCostMicros).GreaterThanOrEqualTo(0);
        RuleFor(c => c.MonthlyCostMicros).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Note).MaximumLength(500);
        RuleFor(c => c)
            .Must(c => c.DailyRequests is not null || c.DailyTokens is not null || c.DailyCostMicros is not null || c.MonthlyCostMicros is not null)
            .WithName("Limits")
            .WithMessage("Set at least one cap. To inherit every cap, remove the override.");
    }
}

public class ClearUsageLimitCommandHandler : IRequestHandler<ClearUsageLimitCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public ClearUsageLimitCommandHandler(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Unit> Handle(ClearUsageLimitCommand request, CancellationToken cancellationToken)
    {
        var scope = UsageLimitSubjects.ParseScope(request.Scope);
        var line = UsageLimitSubjects.ParseLine(request.Line);
        var removed = await _context.UsageLimitOverrides
            .Where(o => o.Scope == scope && o.SubjectId == request.SubjectId && o.Line == line)
            .ExecuteDeleteAsync(cancellationToken);
        if (removed == 0) throw new EntityNotFoundException("There is no such override.");

        await _cache.RemoveByTagAsync(CacheTags.UsageLimits, cancellationToken);
        return Unit.Value;
    }
}
