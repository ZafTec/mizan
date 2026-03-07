using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

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

    public GetClientNutritionQueryHandler(IMizanDbContext context, ITrainerAuthorizationService trainerAuthorization)
    {
        _context = context;
        _trainerAuthorization = trainerAuthorization;
    }

    public async Task<ClientNutritionDto?> Handle(GetClientNutritionQuery request, CancellationToken cancellationToken)
    {
        var relationship = await _trainerAuthorization.GetRelationshipForCurrentTrainerAndClientAsync(
            request.ClientId,
            requireActive: true,
            cancellationToken);

        if (!relationship.CanViewNutrition)
        {
            throw new ForbiddenAccessException("No permission to view client nutrition data");
        }

        // Get food diary entries for the specified date (or today if not specified)
        var targetDate = request.Date ?? DateTime.UtcNow.Date;
        var nextDate = targetDate.AddDays(1);

        var foodEntries = await _context.FoodDiaryEntries
            .Include(fl => fl.Food)
            .Include(fl => fl.Recipe)
            .Where(fl => fl.UserId == request.ClientId && fl.LoggedAt >= targetDate && fl.LoggedAt < nextDate)
            .OrderBy(fl => fl.LoggedAt)
            .ToListAsync(cancellationToken);

        var entries = foodEntries.Select(fl => new FoodLogEntryDto(
            fl.Id,
            fl.Food?.Name ?? fl.Recipe?.Title ?? "Unknown",
            fl.Servings,
            fl.Food?.ServingUnit ?? "serving",
            fl.Calories ?? 0,
            fl.ProteinGrams ?? 0,
            fl.CarbsGrams ?? 0,
            fl.FatGrams ?? 0,
            fl.LoggedAt
        )).ToList();

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
