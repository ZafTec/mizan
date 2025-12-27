using MediatR;
using Microsoft.EntityFrameworkCore;
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
    private readonly ICurrentUserService _currentUser;

    public GetClientNutritionQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ClientNutritionDto?> Handle(GetClientNutritionQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var trainerId = _currentUser.UserId.Value;

        // Verify trainer-client relationship and permissions
        var relationship = await _context.TrainerClientRelationships
            .FirstOrDefaultAsync(r =>
                r.TrainerId == trainerId &&
                r.ClientId == request.ClientId &&
                r.Status == "active",
                cancellationToken);

        if (relationship == null)
        {
            throw new UnauthorizedAccessException("No active relationship with this client");
        }

        if (!relationship.CanViewNutrition)
        {
            throw new UnauthorizedAccessException("No permission to view client nutrition data");
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
