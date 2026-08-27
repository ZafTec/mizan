using System.Globalization;
using System.Text.Json;
using Mizan.Application.Exceptions;

namespace Mizan.Application.Ai.Tools;

/// <summary>
/// Reading arguments a language model produced. Every failure names the
/// argument, because the model reads the error and retries - a message it
/// cannot act on costs another round trip for nothing.
/// </summary>
public static class AiToolArgs
{
    public static string RequiredString(JsonElement args, string field)
    {
        var value = OptionalString(args, field);
        return string.IsNullOrWhiteSpace(value)
            ? throw new DomainValidationException($"'{field}' is required.")
            : value;
    }

    public static string? OptionalString(JsonElement args, string field) =>
        args.ValueKind == JsonValueKind.Object
        && args.TryGetProperty(field, out var element)
        && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    public static decimal? OptionalDecimal(JsonElement args, string field)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(field, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.Null => null,
            // Models routinely quote numbers. Rejecting that is pedantry that
            // costs a retry; refusing to guess at nonsense is not.
            JsonValueKind.String when decimal.TryParse(
                element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new DomainValidationException($"'{field}' must be a number."),
        };
    }

    public static int? OptionalInt(JsonElement args, string field) =>
        OptionalDecimal(args, field) is { } value ? (int)Math.Round(value) : null;

    public static DateOnly? OptionalDate(JsonElement args, string field)
    {
        var raw = OptionalString(args, field);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        return DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out var date)
            ? date
            : throw new DomainValidationException($"'{field}' must be a date in YYYY-MM-DD form.");
    }

    public static decimal RequiredDecimal(JsonElement args, string field) =>
        OptionalDecimal(args, field)
        ?? throw new DomainValidationException($"'{field}' is required.");

    public static Guid RequiredGuid(JsonElement args, string field)
    {
        var raw = RequiredString(args, field);
        return Guid.TryParse(raw, out var id)
            ? id
            // A model that invents an id is told so plainly; the alternative is
            // a lookup failure it cannot interpret.
            : throw new DomainValidationException($"'{field}' must be an id returned by a search tool.");
    }

    /// <summary>
    /// The one nested shape in the catalogue. Workouts are exercises of sets,
    /// and flattening that into scalars would make the model's job harder than
    /// reading it back out here.
    /// </summary>
    public static class Workouts
    {
        public static List<Contracts.Workouts.WorkoutExerciseDto> Exercises(JsonElement args)
        {
            if (args.ValueKind != JsonValueKind.Object
                || !args.TryGetProperty("exercises", out var list)
                || list.ValueKind != JsonValueKind.Array)
            {
                throw new DomainValidationException("'exercises' is required and must be a list.");
            }

            var exercises = list.EnumerateArray().Select(element => new Contracts.Workouts.WorkoutExerciseDto
            {
                ExerciseId = RequiredGuid(element, "exerciseId"),
                Notes = OptionalString(element, "notes"),
                Sets = Sets(element),
            }).ToList();

            return exercises.Count > 0
                ? exercises
                : throw new DomainValidationException("'exercises' must contain at least one exercise.");
        }

        private static List<Contracts.Workouts.ExerciseSetDto> Sets(JsonElement exercise)
        {
            if (!exercise.TryGetProperty("sets", out var sets) || sets.ValueKind != JsonValueKind.Array)
            {
                throw new DomainValidationException("Each exercise needs a 'sets' list.");
            }

            return sets.EnumerateArray().Select(set => new Contracts.Workouts.ExerciseSetDto
            {
                Reps = OptionalInt(set, "reps"),
                WeightKg = OptionalDecimal(set, "weightKg"),
                DurationSeconds = OptionalInt(set, "durationSeconds"),
                DistanceMeters = OptionalDecimal(set, "distanceMeters"),
            }).ToList();
        }
    }
}
