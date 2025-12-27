using FluentValidation;
using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateMealPlanCommand : IRequest<CreateMealPlanResult>
{
    public string? Name { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public Guid? HouseholdId { get; init; }
    public List<MealPlanRecipeDto> Recipes { get; init; } = new();
}

public record MealPlanRecipeDto
{
    public Guid RecipeId { get; init; }
    public DateOnly Date { get; init; }
    public string MealType { get; init; } = "dinner";
    public decimal Servings { get; init; } = 1;
}

public record CreateMealPlanResult
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public int RecipeCount { get; init; }
}

public class CreateMealPlanCommandValidator : AbstractValidator<CreateMealPlanCommand>
{
    public CreateMealPlanCommandValidator()
    {
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty()
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be on or after start date");
        RuleFor(x => x.Name).MaximumLength(255);
        RuleForEach(x => x.Recipes).ChildRules(recipe =>
        {
            recipe.RuleFor(r => r.RecipeId).NotEmpty();
            recipe.RuleFor(r => r.MealType).NotEmpty()
                .Must(m => new[] { "breakfast", "lunch", "dinner", "snack" }.Contains(m.ToLower()))
                .WithMessage("Meal type must be breakfast, lunch, dinner, or snack");
            recipe.RuleFor(r => r.Servings).GreaterThan(0);
        });
    }
}

public class CreateMealPlanCommandHandler : IRequestHandler<CreateMealPlanCommand, CreateMealPlanResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateMealPlanCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateMealPlanResult> Handle(CreateMealPlanCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var mealPlan = new MealPlan
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            HouseholdId = request.HouseholdId,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var recipeDto in request.Recipes)
        {
            mealPlan.MealPlanRecipes.Add(new MealPlanRecipe
            {
                Id = Guid.NewGuid(),
                MealPlanId = mealPlan.Id,
                RecipeId = recipeDto.RecipeId,
                Date = recipeDto.Date,
                MealType = recipeDto.MealType.ToLower(),
                Servings = recipeDto.Servings
            });
        }

        _context.MealPlans.Add(mealPlan);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateMealPlanResult
        {
            Id = mealPlan.Id,
            Name = mealPlan.Name,
            RecipeCount = mealPlan.MealPlanRecipes.Count
        };
    }
}
