using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Ai;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// The single answer to "may this principal read this axis of this subject".
///
/// Two rules, and the second is the one that matters:
///
/// 1. Your own data is yours to read in the product. Sending it to a model is
///    a separate decision, and it defaults to no.
/// 2. A trainer reading a client is governed by the intersection of two
///    independent grants: what the client granted that trainer, and what the
///    client consented to for AI. Neither alone is sufficient - a client who
///    shares workouts with their coach but wants no AI involvement gets
///    exactly that. See docs/REFOCUS.md §11.
/// </summary>
public class DataAccessPolicy : IDataAccessPolicy
{
    private static readonly DataAxis[] AllAxes =
        [DataAxis.Nutrition, DataAxis.Training, DataAxis.Body];

    private readonly IMizanDbContext _context;

    public DataAccessPolicy(IMizanDbContext context) => _context = context;

    public async Task<bool> CanReadAsync(
        Guid principalId,
        Guid subjectId,
        DataAxis axis,
        AccessPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var axes = await ReadableAxesAsync(principalId, subjectId, purpose, cancellationToken);
        return axes.Contains(axis);
    }

    public async Task<IReadOnlySet<DataAxis>> ReadableAxesAsync(
        Guid principalId,
        Guid subjectId,
        AccessPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        // The subject's AI consent gates every AI read of their data, whoever
        // is asking. Withholding, not instructing: a disabled axis is simply
        // never returned.
        var consent = purpose == AccessPurpose.AiContext
            ? await ConsentAsync(subjectId, cancellationToken)
            : null;

        if (principalId == subjectId)
        {
            return Freeze(AllAxes.Where(axis => consent is null || consent.Allows(axis)));
        }

        var grants = await GrantsAsync(principalId, subjectId, cancellationToken);
        if (grants is null) return Freeze([]);

        return Freeze(AllAxes.Where(axis =>
            grants.Grants(axis) && (consent is null || consent.Allows(axis))));
    }

    private async Task<UserAiConsent> ConsentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stored = await _context.UserAiConsents.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        // No row means never asked, which means no.
        return stored ?? UserAiConsent.None(userId);
    }

    /// <summary>
    /// Deliberately uncached. This is one indexed point lookup per request, and
    /// caching a permission would mean a revoked grant kept working for the
    /// length of the TTL. That is the wrong trade for a saving this small.
    /// </summary>
    private async Task<TrainerGrants?> GrantsAsync(
        Guid trainerId, Guid clientId, CancellationToken cancellationToken)
    {
        return await _context.TrainerClientRelationships.AsNoTracking()
            .Where(r => r.TrainerId == trainerId && r.ClientId == clientId && r.Status == "active")
            .Select(r => new TrainerGrants(r.CanViewNutrition, r.CanViewWorkouts, r.CanViewMeasurements))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IReadOnlySet<DataAxis> Freeze(IEnumerable<DataAxis> axes) =>
        axes.ToHashSet();

    private sealed record TrainerGrants(bool Nutrition, bool Training, bool Body)
    {
        public bool Grants(DataAxis axis) => axis switch
        {
            DataAxis.Nutrition => Nutrition,
            DataAxis.Training => Training,
            DataAxis.Body => Body,
            _ => false,
        };
    }
}
