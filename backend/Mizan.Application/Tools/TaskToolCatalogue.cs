using System.Text.Json.Nodes;
using Mizan.Application.Tools.Catalogue;

namespace Mizan.Application.Tools;

/// <summary>
/// The task tools: each defined once, here, and offered to the MCP connector, outside agents and
/// the in-app assistant through <see cref="ToolFilter"/> (docs/MCP.md#task-tools). A personal or
/// household tool is open to everyone, a coaching tool to trainers and an admin tool to
/// administrators; within that, each mode is held to one area.
/// </summary>
public static class TaskToolCatalogue
{
    /// <summary>The mode that carries out a previewed high-impact action by its token.</summary>
    public const string ConfirmMode = "confirm";

    private static readonly Lazy<IReadOnlyList<TaskTool>> Tools = new(Build);

    public static IReadOnlyList<TaskTool> All => Tools.Value;

    public static TaskTool? Find(string name) => All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));

    private static IReadOnlyList<TaskTool> Build()
    {
        var tools = new List<TaskTool>();
        tools.AddRange(NutritionTools.All);
        tools.AddRange(TrainingTools.All);
        tools.AddRange(BodyTools.All);
        tools.AddRange(PlanningTools.All);
        tools.AddRange(AccountTools.All);
        tools.AddRange(SocialTools.All);
        tools.AddRange(CoachTools.All);
        tools.AddRange(CoachingTools.All);
        tools.Add(DeleteTool());
        tools.AddRange(HouseholdTools.All);
        tools.AddRange(AdminTools.All);

        var duplicate = tools.GroupBy(t => t.Name).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Two task tools are called {duplicate.Key}.");
        return tools.Select(WithConfirm).ToList();
    }

    /// <summary>
    /// One delete tool for every kind of thing a person can remove. Each area contributes its
    /// kinds, held to its own write scope. High-impact kinds preview first and return a token.
    /// </summary>
    private static TaskTool DeleteTool()
    {
        var kinds = new List<ToolMode>();
        kinds.AddRange(NutritionTools.DeleteKinds);
        kinds.AddRange(TrainingTools.DeleteKinds);
        kinds.AddRange(BodyTools.DeleteKinds);
        kinds.AddRange(PlanningTools.DeleteKinds);
        kinds.AddRange(SocialTools.DeleteKinds);
        kinds.AddRange(HouseholdTools.DeleteKinds);
        kinds.AddRange(CoachingTools.DeleteKinds);

        return new TaskTool
        {
            Name = "delete",
            Title = "Delete or remove one thing",
            Description = "Delete or remove one thing by its kind and id. A meal plan, a recipe, leaving or removing a household member, "
                + "the social profile and a list's ticked items are high-impact: the first call only previews what goes and returns a token; "
                + "show the person the preview, and only when they agree call delete again with kind \"confirm\" and the token.",
            Kind = ToolKind.Destructive,
            ModeArgument = "kind",
            Properties = new JsonObject
            {
                ["id"] = ToolSchema.Id("The id of the thing to remove, as a tool returned it."),
                ["listId"] = ToolSchema.Id("shopping_item, checked_items: the shopping list."),
                ["planId"] = ToolSchema.Id("plan_entry, prep_session: the meal plan."),
                ["householdId"] = ToolSchema.Id("household_membership: the household."),
                ["memberId"] = ToolSchema.Id("household_membership: the member to remove; leave it out to leave the household yourself."),
                ["imageUrl"] = ToolSchema.String("food_photo: the photo's address, as an estimate returned it."),
                ["emoji"] = ToolSchema.String("reaction: the emoji to take back from the post id."),
            },
            Modes = kinds,
        };
    }

    /// <summary>A tool with a high-impact mode also gets the mode that confirms it, and the token argument.</summary>
    private static TaskTool WithConfirm(TaskTool tool)
    {
        if (tool.Modes.All(m => m.Confirm is null) || tool.ConfirmTool is not null) return tool;
        var properties = (JsonObject)tool.Properties.DeepClone();
        properties["token"] = ToolSchema.String("confirm: the token a preview returned. It works once, for ten minutes.");
        return new TaskTool
        {
            Name = tool.Name,
            Title = tool.Title,
            Description = tool.Description,
            Kind = tool.Kind,
            ModeArgument = tool.ModeArgument,
            DefaultMode = tool.DefaultMode,
            Properties = properties,
            Required = tool.Required,
            App = tool.App,
            ModeHelp = tool.ModeHelp,
            Additive = tool.Additive,
            Idempotent = tool.Idempotent,
            OpenWorld = tool.OpenWorld,
            Modes =
            [
                .. tool.Modes,
                new ToolMode
                {
                    Name = ConfirmMode,
                    Summary = "Carry out what a preview showed, with its token, once the person agreed.",
                    Area = ToolMode.AnyConnection,
                    Surfaces = ToolSurface.Mcp,
                    Requests = [typeof(ConfirmToolActionCommand)],
                    Build = (args, context) => new ConfirmToolActionCommand(args.RequiredString("token"), context.Surface),
                },
            ],
        };
    }
}

/// <summary>Short builders for the JSON Schema properties of the tools.</summary>
public static class ToolSchema
{
    public static JsonObject String(string description) => new() { ["type"] = "string", ["description"] = description };

    public static JsonObject Id(string description) => new() { ["type"] = "string", ["format"] = "uuid", ["description"] = description };

    public static JsonObject Date(string description) => new() { ["type"] = "string", ["format"] = "date", ["description"] = description };

    public static JsonObject Number(string description) => new() { ["type"] = "number", ["description"] = description };

    public static JsonObject Integer(string description) => new() { ["type"] = "integer", ["description"] = description };

    public static JsonObject Boolean(string description) => new() { ["type"] = "boolean", ["description"] = description };

    public static JsonObject Enum(string description, IEnumerable<string> values) => new()
    {
        ["type"] = "string",
        ["enum"] = new JsonArray(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()),
        ["description"] = description,
    };

    public static JsonObject Array(string description, JsonObject items) => new()
    {
        ["type"] = "array",
        ["items"] = items,
        ["description"] = description,
    };

    /// <summary>An object with free-form properties: the shape a Contracts record takes, described in words.</summary>
    public static JsonObject Object(string description, JsonObject? properties = null, params string[] required)
    {
        var schema = new JsonObject { ["type"] = "object", ["description"] = description };
        if (properties is not null) schema["properties"] = properties;
        if (required.Length > 0) schema["required"] = new JsonArray(required.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray());
        return schema;
    }
}
