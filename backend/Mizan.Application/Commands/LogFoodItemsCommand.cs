using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Mizan.Application.Exceptions;
using Mizan.Application.Planning;
using Mizan.Contracts.Meals;
using Mizan.Domain.Constants;

namespace Mizan.Application.Commands;

/// <summary>
/// Logs several things eaten, one after another, through the commands a single log uses
/// (<see cref="CreateFoodDiaryEntryCommand"/>, <see cref="LogPlanEntryCommand"/>). It is not one
/// transaction: the receipt says what was saved, what stopped the batch and what remains, so a
/// retry sends only the rest. Every item is checked before the first is written.
/// </summary>
/// <param name="Date">The diary day for items that do not name their own. Today when left out; a plan entry keeps its own day.</param>
/// <param name="MealType">The meal type for items that do not name their own.</param>
/// <param name="AllowPlanEntries">False where the caller may not read meal plans, so a plan entry item is refused.</param>
public sealed record LogFoodItemsCommand(
    DateOnly? Date,
    string? MealType,
    IReadOnlyList<LogFoodItem> Items,
    bool AllowPlanEntries = true) : IRequest<LogFoodReceipt>
{
    public const int MaxItems = 100;
}

public sealed class LogFoodItemsCommandHandler : IRequestHandler<LogFoodItemsCommand, LogFoodReceipt>
{
    private readonly ISender _sender;
    private readonly ILogger<LogFoodItemsCommandHandler> _logger;

    public LogFoodItemsCommandHandler(ISender sender, ILogger<LogFoodItemsCommandHandler> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task<LogFoodReceipt> Handle(LogFoodItemsCommand request, CancellationToken cancellationToken)
    {
        var items = request.Items ?? [];
        if (items.Count == 0) throw new DomainValidationException("Give at least one item to log.");
        if (items.Count > LogFoodItemsCommand.MaxItems)
            throw new DomainValidationException($"Log at most {LogFoodItemsCommand.MaxItems} items at once.");
        if (request.MealType is { } batchType && !string.IsNullOrWhiteSpace(batchType) && !MealTypes.IsValid(batchType))
            throw new DomainValidationException($"mealType must be one of {string.Join(", ", MealTypes.All)}.");

        for (var index = 0; index < items.Count; index++)
        {
            Check(request, items[index], index);
        }

        var completed = new List<LoggedFoodItem>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var kind = KindOf(item);
            try
            {
                completed.Add(await LogAsync(request, item, index, kind, cancellationToken));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // A rule or a missing record refused the item before anything was saved. Anything else
                // may have failed after the save, so the caller must look before sending it again.
                var refused = error is DomainException or ValidationException;
                if (!refused) _logger.LogError(error, "Logging item {Index} of a batch failed", index);
                var message = error switch
                {
                    ValidationException validation => string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)),
                    DomainException => error.Message,
                    _ => "That could not be completed.",
                };
                var remaining = Enumerable.Range(index + (refused ? 0 : 1), items.Count - index - (refused ? 0 : 1)).ToList();
                return new LogFoodReceipt(
                    false,
                    completed,
                    new FailedFoodItem(index, kind, message, !refused),
                    remaining,
                    refused
                        ? "The completed items are saved. Fix the failed item and send it with the remaining ones; do not send the completed ones again."
                        : "The failed item's save is unconfirmed: check the diary before sending it again. Send only the remaining items; do not send the completed ones again.");
            }
        }

        return new LogFoodReceipt(true, completed, null, [], null);
    }

    private static void Check(LogFoodItemsCommand request, LogFoodItem item, int index)
    {
        if (item is null) throw new DomainValidationException($"items[{index}] is empty.");
        var sources = new[] { item.FoodId.HasValue, item.RecipeId.HasValue, item.PlanEntryId.HasValue, !string.IsNullOrWhiteSpace(item.Name) }
            .Count(given => given);
        if (sources != 1)
            throw new DomainValidationException($"items[{index}] needs exactly one of foodId, recipeId, planEntryId, or a name with its nutrition.");
        if (item.FoodId == Guid.Empty || item.RecipeId == Guid.Empty || item.PlanEntryId == Guid.Empty)
            throw new DomainValidationException($"items[{index}] has an empty id.");
        if (item.PlanEntryId.HasValue)
        {
            if (!request.AllowPlanEntries)
                throw new ForbiddenAccessException($"items[{index}] is a meal plan entry, which this connection may not read.");
            if (item.PlanId is null || item.PlanId == Guid.Empty)
                throw new DomainValidationException($"items[{index}] needs the planId of its plan entry.");
        }

        if (item.Servings is <= 0) throw new DomainValidationException($"items[{index}].servings must be more than zero.");
        if (item.MealType is { } type && !string.IsNullOrWhiteSpace(type) && !MealTypes.IsValid(type))
            throw new DomainValidationException($"items[{index}].mealType must be one of {string.Join(", ", MealTypes.All)}.");
        if (new[] { item.Calories, item.ProteinGrams, item.CarbsGrams, item.FatGrams, item.FiberGrams,
                item.CaloriesPer100g, item.ProteinPer100g, item.CarbsPer100g, item.FatPer100g, item.FiberPer100g }.Any(v => v < 0))
            throw new DomainValidationException($"items[{index}] nutrition values cannot be negative.");
        if (!string.IsNullOrWhiteSpace(item.Name) && item.Calories is null && item.CaloriesPer100g is null)
            throw new DomainValidationException($"items[{index}] needs calories, or amountGrams with caloriesPer100g.");
    }

    private static string KindOf(LogFoodItem item) =>
        item.FoodId.HasValue ? "food" : item.RecipeId.HasValue ? "recipe" : item.PlanEntryId.HasValue ? "plan_entry" : "manual";

    private async Task<LoggedFoodItem> LogAsync(LogFoodItemsCommand request, LogFoodItem item, int index, string kind, CancellationToken cancellationToken)
    {
        var mealType = string.IsNullOrWhiteSpace(item.MealType) ? request.MealType : item.MealType;
        if (kind == "plan_entry")
        {
            var logged = await _sender.Send(new LogPlanEntryCommand(
                item.PlanId!.Value,
                item.PlanEntryId!.Value,
                item.Date ?? request.Date,
                string.IsNullOrWhiteSpace(mealType) ? null : mealType.Trim().ToUpperInvariant()), cancellationToken);
            return new LoggedFoodItem(index, kind, logged.Date, logged.MealType, logged.DiaryEntryIds, null, []);
        }

        var per100g = item.CaloriesPer100g is { } calories
            ? new NutritionPer100g
            {
                Calories = calories,
                ProteinGrams = item.ProteinPer100g ?? 0,
                CarbsGrams = item.CarbsPer100g ?? 0,
                FatGrams = item.FatPer100g ?? 0,
                FiberGrams = item.FiberPer100g,
            }
            : null;
        var date = item.Date ?? request.Date;
        var type = MealTypes.Normalize(mealType);
        var result = await _sender.Send(new CreateFoodDiaryEntryCommand
        {
            FoodId = item.FoodId,
            RecipeId = item.RecipeId,
            Name = item.Name?.Trim() ?? string.Empty,
            EntryDate = date,
            LoggedAt = item.LoggedAt,
            MealType = type,
            Servings = item.Servings ?? 1,
            AmountGrams = item.AmountGrams,
            Calories = item.Calories,
            ProteinGrams = item.ProteinGrams,
            CarbsGrams = item.CarbsGrams,
            FatGrams = item.FatGrams,
            FiberGrams = item.FiberGrams,
            Per100g = per100g,
        }, cancellationToken);
        if (!result.Success) throw new DomainValidationException(result.Message ?? "That item could not be logged.");

        var entryIds = result.EntryIds.Count > 0 ? result.EntryIds : [result.Id];
        return new LoggedFoodItem(index, kind, date, type, entryIds, result.GroupId, result.Warnings);
    }
}
