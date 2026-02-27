using FluentValidation;
using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Application.Services;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateFoodDiaryEntryCommand : IRequest<CreateFoodDiaryEntryResult>
{
    public Guid? FoodId { get; init; }
    public Guid? RecipeId { get; init; }
    public DateOnly? EntryDate { get; init; }
    public string MealType { get; init; } = "SNACK";
    public decimal Servings { get; init; } = 1;
    public decimal? Calories { get; init; }
    public decimal? ProteinGrams { get; init; }
    public decimal? CarbsGrams { get; init; }
    public decimal? FatGrams { get; init; }
    public decimal? FiberGrams { get; init; }
    public string Name { get; init; } = string.Empty;
}

public record CreateFoodDiaryEntryResult
{
    public Guid Id { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public class CreateFoodDiaryEntryCommandValidator : AbstractValidator<CreateFoodDiaryEntryCommand>
{
    public CreateFoodDiaryEntryCommandValidator()
    {
        RuleFor(x => x.MealType).NotEmpty()
            .Must(m => new[] { "MEAL", "SNACK", "DRINK" }.Contains(m.ToUpper()))
            .WithMessage("Meal type must be MEAL, SNACK, or DRINK");
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => x.FoodId.HasValue || x.RecipeId.HasValue || !string.IsNullOrWhiteSpace(x.Name))
            .WithName("FoodId")
            .WithMessage("Either foodId, recipeId, or a meal name must be provided");
        RuleFor(x => x.Calories).GreaterThanOrEqualTo(0).When(x => x.Calories.HasValue);
        RuleFor(x => x.ProteinGrams).GreaterThanOrEqualTo(0).When(x => x.ProteinGrams.HasValue);
        RuleFor(x => x.CarbsGrams).GreaterThanOrEqualTo(0).When(x => x.CarbsGrams.HasValue);
        RuleFor(x => x.FatGrams).GreaterThanOrEqualTo(0).When(x => x.FatGrams.HasValue);
        RuleFor(x => x.FiberGrams).GreaterThanOrEqualTo(0).When(x => x.FiberGrams.HasValue);
    }
}

public class CreateFoodDiaryEntryCommandHandler : IRequestHandler<CreateFoodDiaryEntryCommand, CreateFoodDiaryEntryResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateFoodDiaryEntryCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateFoodDiaryEntryResult> Handle(CreateFoodDiaryEntryCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new CreateFoodDiaryEntryResult
            {
                Success = false,
                Message = "User not authenticated"
            };
        }

        var entry = new FoodDiaryEntry
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            FoodId = request.FoodId,
            RecipeId = request.RecipeId,
            EntryDate = request.EntryDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = request.MealType.ToUpper(),
            Servings = request.Servings,
            Calories = request.Calories,
            ProteinGrams = request.ProteinGrams,
            CarbsGrams = request.CarbsGrams,
            FatGrams = request.FatGrams,
            FiberGrams = request.FiberGrams,
            Name = request.Name,
            LoggedAt = DateTime.UtcNow
        };

        _context.FoodDiaryEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);

        var warnings = NutritionHints.CheckConsistency(
            request.Calories,
            request.ProteinGrams,
            request.CarbsGrams,
            request.FatGrams,
            request.FiberGrams);

        return new CreateFoodDiaryEntryResult
        {
            Id = entry.Id,
            Success = true,
            Message = "Entry logged successfully",
            Warnings = warnings
        };
    }
}
