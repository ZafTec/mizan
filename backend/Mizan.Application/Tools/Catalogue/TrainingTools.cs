using System.Text.Json.Nodes;
using Mizan.Application.Commands;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Queries;
using Mizan.Contracts.Mcp;
using Mizan.Contracts.Workouts;

namespace Mizan.Application.Tools.Catalogue;

/// <summary>Training: workouts logged, the draft in progress, templates and their next session, and exercises.</summary>
public static class TrainingTools
{
    private static readonly JsonObject SetSchema = ToolSchema.Object(
        "One set; give what was done.",
        new JsonObject
        {
            ["reps"] = ToolSchema.Integer("Repetitions."),
            ["weightKg"] = ToolSchema.Number("Load in kg."),
            ["durationSeconds"] = ToolSchema.Integer("Time, seconds."),
            ["distanceMeters"] = ToolSchema.Number("Distance, metres."),
            ["completed"] = ToolSchema.Boolean("False for a set planned but not done; true by default."),
            ["completedAt"] = ToolSchema.String("When it was done, ISO 8601."),
        });

    public static IReadOnlyList<TaskTool> All { get; } =
    [
        new TaskTool
        {
            Name = "get_training",
            Title = "Training history, stats and templates",
            Description = "Read the person's training: logged workouts with their sets, one workout, stats over a range, the workout in progress, "
                + "their workout templates and a template's next session, or the exercise catalogue (for the ids log_workout and templates use).",
            Kind = ToolKind.Read,
            ModeArgument = "view",
            DefaultMode = "history",
            Properties = new JsonObject
            {
                ["workoutId"] = ToolSchema.Id("history: one workout by id, with every set, instead of the list."),
                ["from"] = ToolSchema.Date("history, stats: first day, YYYY-MM-DD."),
                ["to"] = ToolSchema.Date("history, stats: last day, YYYY-MM-DD."),
                ["templateId"] = ToolSchema.Id("next_session: the template."),
                ["query"] = ToolSchema.String("exercises: part of the name."),
                ["muscleGroup"] = ToolSchema.String("exercises: only this muscle group."),
                ["category"] = ToolSchema.String("exercises: only this category."),
                ["page"] = ToolSchema.Integer("history, exercises: page, from 1."),
                ["pageSize"] = ToolSchema.Integer("history, exercises: results per page, 20 by default (at most 100)."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "history",
                    Summary = "Workouts logged, newest first, with their sets; or one by workoutId. The default.",
                    Area = McpScopes.TrainingRead,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(GetWorkoutsQuery), typeof(GetWorkoutByIdQuery)],
                    Replaces = ["list_workouts", "get_workout"],
                    NotFound = "There is no such workout.",
                    Build = (args, context) => args.OptionalGuid("workoutId") is { } workoutId
                        ? new GetWorkoutByIdQuery(workoutId)
                        : new GetWorkoutsQuery
                        {
                            UserId = context.UserId,
                            From = args.OptionalDate("from"),
                            To = args.OptionalDate("to"),
                            Page = Math.Max(1, args.OptionalInt("page") ?? 1),
                            PageSize = Math.Clamp(args.OptionalInt("pageSize") ?? (context.Surface == ToolSurface.Assistant ? 10 : 20), 1, 100),
                        },
                    Describe = result => result switch
                    {
                        PagedResult<WorkoutSummaryDto> { Items.Count: > 0 } workouts => string.Join("\n", workouts.Items.Select(WorkoutLine)),
                        PagedResult<WorkoutSummaryDto> => "No workouts logged in that range.",
                        WorkoutSummaryDto workout => WorkoutText(workout),
                        null => "There is no such workout.",
                        _ => TaskToolRunner.Serialize(result),
                    },
                },
                new ToolMode
                {
                    Name = "stats",
                    Summary = "Trends between from and to: workouts and sets a week, volume, heaviest lifts, personal records, muscle-group coverage.",
                    Area = McpScopes.TrainingRead,
                    Requests = [typeof(GetWorkoutStatsQuery)],
                    Replaces = ["get_workout_stats"],
                    Build = (args, _) => new GetWorkoutStatsQuery(args.OptionalDate("from"), args.OptionalDate("to")),
                },
                new ToolMode
                {
                    Name = "draft",
                    Summary = "The workout in progress that the web log keeps, or none.",
                    Area = McpScopes.TrainingRead,
                    Requests = [typeof(GetWorkoutDraftQuery)],
                    Replaces = ["get_workout_draft"],
                    Build = (_, _) => new GetWorkoutDraftQuery(),
                    Shape = result => result ?? new { draft = (object?)null, message = "No workout draft is saved." },
                },
                new ToolMode
                {
                    Name = "templates",
                    Summary = "The person's workout templates and the built-in ones, with their exercises and targets.",
                    Area = McpScopes.TrainingRead,
                    Requests = [typeof(GetWorkoutTemplatesQuery)],
                    Replaces = ["list_workout_templates"],
                    Build = (_, _) => new GetWorkoutTemplatesQuery(),
                },
                new ToolMode
                {
                    Name = "next_session",
                    Summary = "A template's next session (templateId): each exercise with the weights progression sets from the last workout logged with it.",
                    Area = McpScopes.TrainingRead,
                    Requests = [typeof(GetNextTemplateSessionQuery)],
                    Replaces = ["get_next_workout_session"],
                    Build = (args, _) => new GetNextTemplateSessionQuery(args.RequiredGuid("templateId")),
                    NotFound = "There is no such workout template.",
                },
                new ToolMode
                {
                    Name = "exercises",
                    Summary = "The exercise catalogue and the person's own exercises, by query, muscleGroup or category; ids for log_workout. Never guess an id.",
                    Area = McpScopes.TrainingRead,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(GetExercisesQuery)],
                    Replaces = ["list_exercises"],
                    Build = (args, context) => new GetExercisesQuery
                    {
                        SearchTerm = args.OptionalString("query"),
                        MuscleGroup = args.OptionalString("muscleGroup"),
                        Category = args.OptionalString("category"),
                        Page = Math.Max(1, args.OptionalInt("page") ?? 1),
                        PageSize = context.Surface == ToolSurface.Assistant ? 10 : Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 100),
                    },
                    Describe = result => result is GetExercisesResult exercises && exercises.Items.Count > 0
                        ? string.Join("\n", exercises.Items.Select(e => $"{e.Id} — {e.Name}"))
                        : "No exercises matched that.",
                },
            ],
        },
        new TaskTool
        {
            Name = "log_workout",
            Title = "Log, change or draft a workout",
            Description = "Log a training session, change a logged one, or save the workout in progress that the web log resumes. "
                + "Each exercise is given by exerciseId or by exerciseName; a name must match one exercise the person can see, or nothing is saved and the error lists the candidates.",
            Kind = ToolKind.Write,
            ModeArgument = "action",
            DefaultMode = "log",
            Required = ["exercises"],
            Properties = new JsonObject
            {
                ["workoutId"] = ToolSchema.Id("update: the workout to change."),
                ["name"] = ToolSchema.String("The workout's name."),
                ["date"] = ToolSchema.Date("YYYY-MM-DD; today by default (update keeps the workout's day)."),
                ["durationMinutes"] = ToolSchema.Integer("How long it took."),
                ["bodyweightKg"] = ToolSchema.Number("Bodyweight that day, kg."),
                ["caloriesBurned"] = ToolSchema.Integer("Calories burned, when a device measured them."),
                ["notes"] = ToolSchema.String("A note."),
                ["templateId"] = ToolSchema.Id("The template it followed, so its next session progresses from it."),
                ["exercises"] = ToolSchema.Array("The exercises in order. update replaces every exercise with these.", ToolSchema.Object(
                    "One exercise: exerciseId, or exerciseName, with its sets.",
                    new JsonObject
                    {
                        ["exerciseId"] = ToolSchema.Id("From get_training view exercises."),
                        ["exerciseName"] = ToolSchema.String("The exercise's name, when the id is not known."),
                        ["notes"] = ToolSchema.String("A note on this exercise."),
                        ["supersetWithNext"] = ToolSchema.Boolean("Done as a superset with the next exercise."),
                        ["sets"] = ToolSchema.Array("The sets.", SetSchema),
                    },
                    "sets")),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "log",
                    Summary = "Log a new workout. The default.",
                    Area = McpScopes.TrainingWrite,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(SaveNamedWorkoutCommand)],
                    Replaces = ["log_workout"],
                    Build = (args, _) => Workout(args, NamedWorkoutAction.Log),
                    Describe = result => result is NamedWorkoutResult { Logged: { } workout }
                        ? $"Logged {workout.TotalExercises} exercise(s), {workout.TotalSets} set(s)."
                        : "Logged that workout.",
                },
                new ToolMode
                {
                    Name = "update",
                    Summary = "Change the workout workoutId: the fields given, and every exercise replaced by those sent.",
                    Area = McpScopes.TrainingWrite,
                    Requests = [typeof(SaveNamedWorkoutCommand)],
                    Replaces = ["update_workout"],
                    Build = (args, _) => Workout(args, NamedWorkoutAction.Update),
                },
                new ToolMode
                {
                    Name = "draft",
                    Summary = "Save these exercises as the workout in progress, replacing the draft the web log resumes.",
                    Area = McpScopes.TrainingWrite,
                    Requests = [typeof(SaveNamedWorkoutCommand)],
                    Replaces = ["save_workout_draft"],
                    Build = (args, _) => Workout(args, NamedWorkoutAction.Draft),
                },
            ],
        },
        new TaskTool
        {
            Name = "save_workout_template",
            Title = "Save a workout template or exercise",
            Description = "Create, change or copy a workout template, or add or change one of the person's own exercises. "
                + "A template's next session starts from the last workout logged with it, and before one exists from each exercise's targetWeightKg, so set that from the person's recent weights.",
            Kind = ToolKind.Write,
            ModeArgument = "kind",
            DefaultMode = "template",
            Properties = new JsonObject
            {
                ["templateId"] = ToolSchema.Id("template: the template to change; leave it out to create one."),
                ["copyFrom"] = ToolSchema.Id("template: copy this template (yours or a built-in one) instead; the other fields are ignored."),
                ["name"] = ToolSchema.String("The template's or exercise's name."),
                ["programName"] = ToolSchema.String("template: the program it belongs to."),
                ["sessionOrder"] = ToolSchema.Integer("template: its place in the program."),
                ["notes"] = ToolSchema.String("template: a note."),
                ["exercises"] = ToolSchema.Array("template: the exercises in order (every one is replaced on a change).", ToolSchema.Object(
                    "One template exercise.",
                    new JsonObject
                    {
                        ["exerciseId"] = ToolSchema.Id("From get_training view exercises."),
                        ["sortOrder"] = ToolSchema.Integer("Its place, from 0."),
                        ["sets"] = ToolSchema.Integer("How many sets, 1 to 50."),
                        ["repsPerSet"] = ToolSchema.Integer("Target reps."),
                        ["targetWeightKg"] = ToolSchema.Number("Starting weight, kg."),
                        ["restSecondsMin"] = ToolSchema.Integer("Least rest, seconds."),
                        ["restSecondsMax"] = ToolSchema.Integer("Most rest, seconds."),
                        ["restSecondsFailure"] = ToolSchema.Integer("Rest after a failed set, seconds."),
                        ["supersetWithNext"] = ToolSchema.Boolean("Superset with the next exercise."),
                        ["notes"] = ToolSchema.String("A note."),
                        ["progressionType"] = ToolSchema.Enum("How the weight grows.", ["None", "IncreaseAllEvenly", "IncreaseLowestSet"]),
                        ["progressionStrategy"] = ToolSchema.Enum("Which sets progress.", ["first", "middle", "last", "all"]),
                        ["progressionAmountKg"] = ToolSchema.Number("How much it grows, kg."),
                        ["targetType"] = ToolSchema.String("What a set targets: reps, time or distance."),
                        ["targetSeconds"] = ToolSchema.Integer("Target time, seconds."),
                        ["targetDistanceMeters"] = ToolSchema.Number("Target distance, metres."),
                    },
                    "exerciseId", "sets", "progressionType", "progressionStrategy", "targetType")),
                ["exerciseId"] = ToolSchema.Id("exercise: the exercise to change; leave it out to add one."),
                ["category"] = ToolSchema.String("exercise: its category, such as Strength or Cardio."),
                ["muscleGroup"] = ToolSchema.String("exercise: the muscle group."),
                ["equipment"] = ToolSchema.String("exercise: the equipment."),
                ["description"] = ToolSchema.String("exercise: how it is done."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "template",
                    Summary = "Create a template, change templateId, or copy copyFrom. The default.",
                    Area = McpScopes.TrainingWrite,
                    Requests = [typeof(SaveWorkoutTemplateCommand), typeof(DuplicateWorkoutTemplateCommand)],
                    Replaces = ["create_workout_template", "update_workout_template", "duplicate_workout_template"],
                    Build = (args, _) => args.OptionalGuid("copyFrom") is { } source
                        ? new DuplicateWorkoutTemplateCommand(source)
                        : args.Body<SaveWorkoutTemplateCommand>() with { Id = args.OptionalGuid("templateId"), IsBuiltIn = false },
                    Shape = result => result is Guid id ? new { id } : new { updated = true },
                },
                new ToolMode
                {
                    Name = "exercise",
                    Summary = "Add a custom exercise, or change exerciseId (one of the person's own).",
                    Area = McpScopes.TrainingWrite,
                    Requests = [typeof(CreateExerciseCommand), typeof(UpdateExerciseCommand)],
                    Replaces = ["create_exercise", "update_exercise"],
                    Build = (args, _) => args.OptionalGuid("exerciseId") is { } exerciseId
                        ? new UpdateExerciseCommand(
                            exerciseId,
                            args.RequiredString("name"),
                            args.RequiredString("category"),
                            args.OptionalString("muscleGroup"),
                            args.OptionalString("equipment"),
                            args.OptionalString("description"),
                            null,
                            null)
                        : new CreateExerciseCommand
                        {
                            Name = args.RequiredString("name"),
                            Category = args.RequiredString("category"),
                            MuscleGroup = args.OptionalString("muscleGroup"),
                            Equipment = args.OptionalString("equipment"),
                            Description = args.OptionalString("description"),
                        },
                    Shape = result => result is CreateExerciseResult ? result : new { updated = true },
                },
            ],
        },
    ];

    public static IReadOnlyList<ToolMode> DeleteKinds { get; } =
    [
        new ToolMode
        {
            Name = "workout",
            Summary = "A logged workout, by id.",
            Area = McpScopes.TrainingWrite,
            Requests = [typeof(DeleteWorkoutCommand)],
            Replaces = ["delete_workout"],
            Build = (args, _) => new DeleteWorkoutCommand(args.RequiredGuid("id")),
        },
        new ToolMode
        {
            Name = "workout_draft",
            Summary = "The workout in progress (no id).",
            Area = McpScopes.TrainingWrite,
            Requests = [typeof(DeleteWorkoutDraftCommand)],
            Replaces = ["delete_workout_draft"],
            Build = (_, _) => new DeleteWorkoutDraftCommand(),
        },
        new ToolMode
        {
            Name = "workout_template",
            Summary = "One of the person's workout templates, by id.",
            Area = McpScopes.TrainingWrite,
            Requests = [typeof(DeleteWorkoutTemplateCommand)],
            Replaces = ["delete_workout_template"],
            Build = (args, _) => new DeleteWorkoutTemplateCommand(args.RequiredGuid("id")),
        },
        new ToolMode
        {
            Name = "exercise",
            Summary = "One of the person's own exercises, by id.",
            Area = McpScopes.TrainingWrite,
            Requests = [typeof(DeleteExerciseCommand)],
            Replaces = ["delete_exercise"],
            Build = (args, _) => new DeleteExerciseCommand(args.RequiredGuid("id")),
        },
    ];

    private static SaveNamedWorkoutCommand Workout(ToolArgs args, NamedWorkoutAction action) => new(
        action,
        action == NamedWorkoutAction.Update ? args.RequiredGuid("workoutId") : null,
        args.OptionalString("name"),
        args.OptionalDate("date"),
        // A zero duration, bodyweight or calorie count is one the person did not give.
        args.OptionalInt("durationMinutes") is > 0 and var minutes ? minutes : null,
        args.OptionalDecimal("bodyweightKg") is > 0 and var bodyweight ? bodyweight : null,
        args.OptionalInt("caloriesBurned") is > 0 and var burned ? burned : null,
        args.OptionalString("notes"),
        args.OptionalGuid("templateId"),
        args.As<List<NamedWorkoutExercise>>("exercises") ?? throw new DomainValidationException("'exercises' is required: the exercises with their sets."));

    private static string WorkoutLine(WorkoutSummaryDto w) =>
        $"{w.Id} — {w.WorkoutDate:yyyy-MM-dd} {w.Name ?? "Workout"}: "
        + string.Join(", ", w.Exercises.Select(e => $"{e.ExerciseName} {e.Sets.Count}x"));

    private static string WorkoutText(WorkoutSummaryDto w) =>
        $"{w.Id} — {w.WorkoutDate:yyyy-MM-dd} {w.Name ?? "Workout"}"
        + (w.DurationMinutes is { } minutes ? $", {minutes} min" : string.Empty)
        + "\n" + string.Join("\n", w.Exercises.Select(e => $"{e.ExerciseName}: " + string.Join(", ", e.Sets.Select(SetText))));

    private static string SetText(WorkoutSetDto s) =>
        s.Reps is { } reps
            ? s.WeightKg is { } kg ? $"{reps}x{kg:0.##}kg" : $"{reps} reps"
            : s.DurationSeconds is { } seconds ? $"{seconds}s" : s.DistanceMeters is { } metres ? $"{metres:0.#}m" : "set";
}
