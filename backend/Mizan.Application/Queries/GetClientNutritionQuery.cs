using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Ai;

namespace Mizan.Application.Queries;

public record GetClientNutritionQuery(Guid ClientId, DateTime? Date = null) : IRequest<ClientNutritionDto?>;

public record ClientNutritionDto(
    Guid ClientId,
    DateTime Date,
    List<FoodLogEntryDto> FoodLogs,
    NutritionSummaryDto Summary
);

public record FoodLogEntryDto(
    Guid Id,
    string FoodName,
    decimal ServingSize,
    string? ServingUnit,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    DateTime LoggedAt
);

public record NutritionSummaryDto(
    decimal TotalCalories,
    decimal TotalProtein,
    decimal TotalCarbs,
    decimal TotalFat
);

public class GetClientNutritionQueryHandler : IRequestHandler<GetClientNutritionQuery, ClientNutritionDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ITrainerAuthorizationService _trainerAuthorization;
    private readonly IDataAccessPolicy _policy;
    private readonly ICurrentUserService _currentUser;

    public GetClientNutritionQueryHandler(
        IMizanDbContext context,
        ITrainerAuthorizationService trainerAuthorization,
        IDataAccessPolicy policy,
        ICurrentUserService currentUser)
    {
        _context = context;
        _trainerAuthorization = trainerAuthorization;
        _policy = policy;
        _currentUser = currentUser;
    }

    public async Task<ClientNutritionDto?> Handle(GetClientNutritionQuery request, CancellationToken cancellationToken)
    {
        var relationship = await _trainerAuthorization.GetRelationshipForCurrentTrainerAndClientAsync(
            request.ClientId,
            requireActive: true,
            cancellationToken);

        // Through the policy rather than reading the flag here: one place to
        // audit, and the reason CanViewMeasurements was missed for so long
        // (docs/AI.md#consent).
        await ClientReadAccess.EnsureAsync(_policy, _currentUser, relationship.TrainerId, request.ClientId,
            DataAxis.Nutrition, "No permission to view client nutrition data", cancellationToken);

        // Get food diary entries for the specified date (or today if not specified)
        var day = DateOnly.FromDateTime(request.Date ?? DateTime.UtcNow);
        var targetDate = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Projected in the query: the trainer's view reads names, not tracked food and recipe entities.
        var entries = await _context.FoodDiaryEntries.AsNoTracking()
            .Where(fl => fl.UserId == request.ClientId && fl.EntryDate == day)
            .OrderBy(fl => fl.LoggedAt)
            .Select(fl => new FoodLogEntryDto(
                fl.Id,
                fl.Food != null ? fl.Food.Name : fl.Recipe != null ? fl.Recipe.Title : "Unknown",
                fl.Servings,
                fl.Food != null ? fl.Food.ServingUnit : "serving",
                fl.Calories ?? 0,
                fl.ProteinGrams ?? 0,
                fl.CarbsGrams ?? 0,
                fl.FatGrams ?? 0,
                fl.LoggedAt))
            .ToListAsync(cancellationToken);

        var summary = new NutritionSummaryDto(
            entries.Sum(e => e.Calories),
            entries.Sum(e => e.Protein),
            entries.Sum(e => e.Carbs),
            entries.Sum(e => e.Fat)
        );

        return new ClientNutritionDto(
            request.ClientId,
            targetDate,
            entries,
            summary
        );
    }
}
