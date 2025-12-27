using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record LogFoodCommand : IRequest<LogFoodResult>
{
    public Guid? FoodId { get; init; }
    public Guid? RecipeId { get; init; }
    public DateOnly EntryDate { get; init; }
    public string MealType { get; init; } = "snack";
    public decimal Servings { get; init; } = 1;
}

public record LogFoodResult
{
    public Guid Id { get; init; }
    public decimal Calories { get; init; }
    public decimal ProteinGrams { get; init; }
    public decimal CarbsGrams { get; init; }
    public decimal FatGrams { get; init; }
    public string Message { get; init; } = string.Empty;
}

public class LogFoodCommandValidator : AbstractValidator<LogFoodCommand>
{
    public LogFoodCommandValidator()
    {
        RuleFor(x => x.Servings).GreaterThan(0).WithMessage("Servings must be greater than 0");
        RuleFor(x => x.MealType).Must(x => new[] { "breakfast", "lunch", "dinner", "snack" }.Contains(x))
            .WithMessage("Meal type must be breakfast, lunch, dinner, or snack");
        RuleFor(x => x).Must(x => x.FoodId.HasValue || x.RecipeId.HasValue)
            .WithMessage("Either FoodId or RecipeId must be provided");
    }
}

public class LogFoodCommandHandler : IRequestHandler<LogFoodCommand, LogFoodResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public LogFoodCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<LogFoodResult> Handle(LogFoodCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        decimal calories = 0;
        decimal protein = 0, carbs = 0, fat = 0;
        string itemName = "";

        if (request.FoodId.HasValue)
        {
            var food = await _context.Foods.FindAsync(new object[] { request.FoodId.Value }, cancellationToken);
            if (food != null)
            {
                calories = food.CaloriesPer100g * (food.ServingSize / 100m) * request.Servings;
                protein = food.ProteinPer100g * (food.ServingSize / 100m) * request.Servings;
                carbs = food.CarbsPer100g * (food.ServingSize / 100m) * request.Servings;
                fat = food.FatPer100g * (food.ServingSize / 100m) * request.Servings;
                itemName = food.Name;
            }
        }
        else if (request.RecipeId.HasValue)
        {
            var recipe = await _context.Recipes
                .Include(r => r.Nutrition)
                .FirstOrDefaultAsync(r => r.Id == request.RecipeId.Value, cancellationToken);

            if (recipe?.Nutrition != null)
            {
                calories = (recipe.Nutrition.CaloriesPerServing ?? 0) * request.Servings;
                protein = (recipe.Nutrition.ProteinGrams ?? 0) * request.Servings;
                carbs = (recipe.Nutrition.CarbsGrams ?? 0) * request.Servings;
                fat = (recipe.Nutrition.FatGrams ?? 0) * request.Servings;
                itemName = recipe.Title;
            }
        }

        var entry = new FoodDiaryEntry
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            FoodId = request.FoodId,
            RecipeId = request.RecipeId,
            EntryDate = request.EntryDate,
            MealType = request.MealType,
            Servings = request.Servings,
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            LoggedAt = DateTime.UtcNow
        };

        _context.FoodDiaryEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);

        return new LogFoodResult
        {
            Id = entry.Id,
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            Message = $"Logged {request.Servings} serving(s) of {itemName} ({calories} kcal)"
        };
    }
}
