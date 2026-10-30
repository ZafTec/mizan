using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// The model's answer in the <c>meal_plan_v1</c> schema. It names foods and recipes and their
/// amounts; it carries no nutrition and no prices, which code computes.
/// </summary>
public sealed record MealPlanDraftV1(string? Name, List<MealPlanDayV1>? Days, List<MealPlanPrepV1>? PrepSessions, string? Note);

public sealed record MealPlanDayV1(int DayIndex, List<MealPlanEntryV1>? Entries);

public sealed record MealPlanEntryV1(
    string? Slot,
    string? Kind,
    string? FoodName,
    string? RecipeId,
    decimal? Grams,
    decimal? Servings,
    string? Note,
    int? PrepSessionIndex);

public sealed record MealPlanPrepV1(int DayIndex, string? Title, string? Note);

/// <summary>
/// Turns a model draft into plan entries. A recipe entry must name one of the person's recipes
/// by id; a food entry must name a catalogue food the resolver found, with grams. Anything else
/// is kept as a text entry whose note says what to review, and is listed as unresolved, so
/// nothing the model wrote is silently dropped or silently trusted.
/// </summary>
public static class MealPlanDraftReader
{
    public const int MaxEntriesPerDay = 10;
    public const decimal MaxGrams = 2000m;
    public const decimal MaxServings = 20m;
    public const decimal MinServings = 0.25m;

    public const string NotARecipeOfYours = "That recipe is not one of yours.";
    public const string NotInCatalogue = "Not found in the food catalogue.";
    public const string NoGrams = "No amount in grams.";
    public const string WrittenAsText = "Written as a note.";

    public sealed record Result(
        List<PlanEntryRequest> Entries,
        List<PrepSessionRequest> PrepSessions,
        List<UnresolvedPlanLineDto> Unresolved);

    /// <param name="foodsByName">Resolved foods keyed by <see cref="FoodNameMatch.Normalize"/> of the written name.</param>
    public static Result Read(
        MealPlanDraftV1 draft,
        int durationDays,
        IReadOnlyDictionary<Guid, string> recipes,
        IReadOnlyDictionary<string, Food> foodsByName)
    {
        var sessions = new List<PrepSessionRequest>();
        var sessionIndex = new Dictionary<int, int>();
        foreach (var (prep, index) in (draft.PrepSessions ?? []).Select((p, i) => (p, i)))
        {
            if (prep.DayIndex < 0 || prep.DayIndex >= durationDays || string.IsNullOrWhiteSpace(prep.Title)) continue;
            sessionIndex[index] = sessions.Count;
            sessions.Add(new PrepSessionRequest
            {
                DayIndex = prep.DayIndex,
                Title = Cut(prep.Title.Trim(), 255),
                Notes = string.IsNullOrWhiteSpace(prep.Note) ? null : Cut(prep.Note.Trim(), 2000),
            });
        }

        var entries = new List<PlanEntryRequest>();
        var unresolved = new List<UnresolvedPlanLineDto>();
        var perDay = new Dictionary<int, int>();
        var perSlot = new Dictionary<(int, string), int>();

        foreach (var day in (draft.Days ?? []).Where(d => d.DayIndex >= 0 && d.DayIndex < durationDays))
        {
            foreach (var line in day.Entries ?? [])
            {
                var count = perDay.GetValueOrDefault(day.DayIndex);
                if (count >= MaxEntriesPerDay) break;

                var slot = PlanSlots.IsValid(line.Slot) ? PlanSlots.Normalize(line.Slot) : Domain.Constants.MealTypes.Snack;
                var order = perSlot.GetValueOrDefault((day.DayIndex, slot));
                int? session = line.PrepSessionIndex is { } p && sessionIndex.TryGetValue(p, out var mapped) ? mapped : null;
                var note = string.IsNullOrWhiteSpace(line.Note) ? null : line.Note.Trim();

                var entry = Entry(line, day.DayIndex, slot, order, session, note, recipes, foodsByName, out var problem);
                if (entry is null) continue;
                if (problem is not null)
                    unresolved.Add(new UnresolvedPlanLineDto(day.DayIndex, slot, entry.Text!, problem));

                entries.Add(entry);
                perDay[day.DayIndex] = count + 1;
                perSlot[(day.DayIndex, slot)] = order + 1;
            }
        }

        return new Result(entries, sessions, unresolved);
    }

    private static PlanEntryRequest? Entry(
        MealPlanEntryV1 line,
        int dayIndex,
        string slot,
        int order,
        int? session,
        string? note,
        IReadOnlyDictionary<Guid, string> recipes,
        IReadOnlyDictionary<string, Food> foodsByName,
        out string? problem)
    {
        problem = null;
        var kind = (line.Kind ?? "food").Trim().ToLowerInvariant();
        var name = string.IsNullOrWhiteSpace(line.FoodName) ? null : line.FoodName.Trim();

        if (kind == "recipe")
        {
            if (Guid.TryParse(line.RecipeId, out var recipeId) && recipes.ContainsKey(recipeId))
            {
                return new PlanEntryRequest
                {
                    DayIndex = dayIndex,
                    Slot = slot,
                    Kind = nameof(PlanEntryKind.Recipe),
                    RecipeId = recipeId,
                    Servings = Math.Clamp(Math.Round(line.Servings ?? 1m, 2), MinServings, MaxServings),
                    Notes = note is null ? null : Cut(note, 1000),
                    SortOrder = order,
                    PrepSessionIndex = session,
                };
            }
            problem = NotARecipeOfYours;
            return Text(name ?? "A recipe", dayIndex, slot, order, session, note, problem);
        }

        if (kind == "text")
        {
            var text = name ?? note;
            if (text is null) return null;
            problem = WrittenAsText;
            return Text(text, dayIndex, slot, order, session, name is null ? null : note, problem);
        }

        if (name is null) return null;
        if (!foodsByName.TryGetValue(FoodNameMatch.Normalize(name), out var food))
        {
            problem = NotInCatalogue;
            return Text(name, dayIndex, slot, order, session, note, problem);
        }
        if (line.Grams is not { } grams || grams <= 0)
        {
            problem = NoGrams;
            return Text(food.Name, dayIndex, slot, order, session, note, problem);
        }

        return new PlanEntryRequest
        {
            DayIndex = dayIndex,
            Slot = slot,
            Kind = nameof(PlanEntryKind.Food),
            FoodId = food.Id,
            Grams = Math.Min(Math.Round(grams, 0), MaxGrams),
            Notes = note is null ? null : Cut(note, 1000),
            SortOrder = order,
            PrepSessionIndex = session,
        };
    }

    /// <summary>A line kept for the person to review: its note says what to check.</summary>
    private static PlanEntryRequest Text(string text, int dayIndex, string slot, int order, int? session, string? note, string problem) => new()
    {
        DayIndex = dayIndex,
        Slot = slot,
        Kind = nameof(PlanEntryKind.Text),
        Text = Cut(text, 255),
        Notes = Cut(note is null ? $"Review: {problem}" : $"{note} Review: {problem}", 1000),
        SortOrder = order,
        PrepSessionIndex = session,
    };

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
