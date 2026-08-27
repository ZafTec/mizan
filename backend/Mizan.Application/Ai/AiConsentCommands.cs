using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Ai;

public record AiConsentDto(
    bool Enabled,
    bool ShareNutrition,
    bool ShareTraining,
    bool ShareBody,
    bool AllowWrites,
    bool WriteNutrition,
    bool WriteTraining,
    bool WriteBody,
    DateTime? UpdatedAt);

public record GetAiConsentQuery : IRequest<AiConsentDto>;

public class GetAiConsentQueryHandler : IRequestHandler<GetAiConsentQuery, AiConsentDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAiConsentQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AiConsentDto> Handle(GetAiConsentQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var stored = await _context.UserAiConsents.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        // No row is the same answer as every flag off, and the screen should
        // show it that way rather than as "not configured".
        return stored is null
            ? new AiConsentDto(false, false, false, false, false, false, false, false, null)
            : new AiConsentDto(
                stored.Enabled, stored.ShareNutrition, stored.ShareTraining, stored.ShareBody,
                stored.AllowWrites, stored.WriteNutrition, stored.WriteTraining, stored.WriteBody,
                stored.UpdatedAt);
    }
}

/// <summary>
/// Replaces the whole consent record. Not a patch: a screen with four
/// switches should send four switches, and a partial update is how one gets
/// left on by accident.
/// </summary>
public record UpdateAiConsentCommand(
    bool Enabled,
    bool ShareNutrition,
    bool ShareTraining,
    bool ShareBody,
    bool AllowWrites,
    bool WriteNutrition,
    bool WriteTraining,
    bool WriteBody) : IRequest<AiConsentDto>;

public class UpdateAiConsentCommandHandler : IRequestHandler<UpdateAiConsentCommand, AiConsentDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateAiConsentCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AiConsentDto> Handle(UpdateAiConsentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var consent = await _context.UserAiConsents
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (consent is null)
        {
            consent = new UserAiConsent { UserId = userId };
            _context.UserAiConsents.Add(consent);
        }

        consent.Enabled = request.Enabled;
        consent.ShareNutrition = request.ShareNutrition;
        consent.ShareTraining = request.ShareTraining;
        consent.ShareBody = request.ShareBody;
        consent.AllowWrites = request.AllowWrites;
        consent.WriteNutrition = request.WriteNutrition;
        consent.WriteTraining = request.WriteTraining;
        consent.WriteBody = request.WriteBody;
        consent.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new AiConsentDto(
            consent.Enabled, consent.ShareNutrition, consent.ShareTraining, consent.ShareBody,
            consent.AllowWrites, consent.WriteNutrition, consent.WriteTraining, consent.WriteBody,
            consent.UpdatedAt);
    }
}
