using System.Text.Json.Nodes;
using Mizan.Application.Ai;
using Mizan.Application.Commands;
using Mizan.Application.Planning;
using Mizan.Application.Queries;
using Mizan.Contracts.Mcp;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Tools.Catalogue;

/// <summary>
/// The coach's side of coaching, for trainers only: their clients and requests, what a client
/// shared, drafting a client's plan, the coach's own plan templates, and asking the assistant about
/// a client. A connected app reads a client's area only when the client shared it with the coach
/// and allowed AI to read it (docs/AI.md#coaching).
/// </summary>
public static class CoachingTools
{
    private const string ClientHelp = "the client, from get_clients view clients.";

    public static IReadOnlyList<TaskTool> All { get; } =
    [
        new TaskTool
        {
            Name = "get_clients",
            Title = "My clients",
            Description = "For coaches. Read the coach's clients and what each may be seen, the requests waiting for an answer, "
                + "a client's nutrition for a day or their workouts, or the chat with a client. A client's nutrition and workouts are "
                + "readable only when the client shared them and allowed AI to read them; otherwise the call says which is missing.",
            Kind = ToolKind.Read,
            ModeArgument = "view",
            DefaultMode = "clients",
            Properties = new JsonObject
            {
                ["clientId"] = ToolSchema.Id("nutrition, workouts: " + ClientHelp),
                ["relationshipId"] = ToolSchema.Id("chat: the coaching relationship, from view clients."),
                ["date"] = ToolSchema.Date("nutrition: the day, today by default."),
                ["page"] = ToolSchema.Integer("clients, requests, workouts: page, from 1."),
                ["pageSize"] = ToolSchema.Integer("clients, requests, workouts: results per page, 20 by default."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "clients",
                    Summary = "The coach's clients with the relationship id and what each client shares. The default.",
                    Area = McpScopes.CoachingRead,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(GetTrainerClientsQuery)],
                    Replaces = ["list_trainer_clients"],
                    Build = (args, _) => new GetTrainerClientsQuery { Page = Page(args), PageSize = PageSize(args) },
                },
                new ToolMode
                {
                    Name = "requests",
                    Summary = "Requests from people who want the coach, waiting for an answer.",
                    Area = McpScopes.CoachingRead,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(GetTrainerPendingRequestsQuery)],
                    Replaces = ["list_trainer_pending_requests"],
                    Build = (args, _) => new GetTrainerPendingRequestsQuery { Page = Page(args), PageSize = PageSize(args) },
                },
                new ToolMode
                {
                    Name = "nutrition",
                    Summary = "What clientId ate on date, against their targets.",
                    Area = McpScopes.CoachingRead,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(GetClientNutritionQuery)],
                    Replaces = ["get_client_nutrition"],
                    Build = (args, _) => new GetClientNutritionQuery(
                        args.RequiredGuid("clientId"),
                        args.OptionalDate("date")?.ToDateTime(TimeOnly.MinValue)),
                    NotFound = "There is no such client.",
                },
                new ToolMode
                {
                    Name = "workouts",
                    Summary = "clientId's logged workouts, newest first.",
                    Area = McpScopes.CoachingRead,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(GetClientWorkoutsQuery)],
                    Replaces = ["get_client_workouts"],
                    Build = (args, _) => new GetClientWorkoutsQuery(args.RequiredGuid("clientId"), Page(args), PageSize(args)),
                },
                new ToolMode
                {
                    Name = "chat",
                    Summary = "The messages with the client in relationshipId, with the conversation id manage_clients message needs.",
                    Area = McpScopes.CoachingRead,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(GetChatConversationQuery)],
                    Replaces = ["get_coach_chat"],
                    Build = (args, _) => new GetChatConversationQuery(args.RequiredGuid("relationshipId")),
                    NotFound = "There is no such conversation.",
                },
            ],
        },
        new TaskTool
        {
            Name = "manage_clients",
            Title = "Manage clients",
            Description = "For coaches. Accept or decline a request to coach someone, or message a client. "
                + "What the coach may see is the client's choice: a coach cannot change it.",
            Kind = ToolKind.Write,
            ModeArgument = "action",
            Properties = new JsonObject
            {
                ["relationshipId"] = ToolSchema.Id("answer_request: the request, from get_clients view requests."),
                ["accept"] = ToolSchema.Boolean("answer_request: true accepts, false declines."),
                ["conversationId"] = ToolSchema.Id("message: the conversation, from get_clients view chat."),
                ["content"] = ToolSchema.String("message: the message."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "answer_request",
                    Summary = "Accept or decline the request relationshipId. The person is told.",
                    Area = McpScopes.CoachingWrite,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(RespondToTrainerRequestCommand)],
                    Replaces = ["respond_to_trainer_request"],
                    Build = (args, _) => new RespondToTrainerRequestCommand(
                        args.RequiredGuid("relationshipId"),
                        args.OptionalBool("accept") ?? throw new Exceptions.DomainValidationException("'accept' is required.")),
                    Shape = result => new { answered = result is true },
                },
                new ToolMode
                {
                    Name = "message",
                    Summary = "Send content in the conversation conversationId, when the client allows messages.",
                    Area = McpScopes.CoachingWrite,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(SendChatMessageCommand)],
                    Replaces = ["send_coach_message"],
                    Build = (args, context) => new SendChatMessageCommand(args.RequiredGuid("conversationId"), context.UserId, args.RequiredString("content")),
                    Shape = result => result is SendChatMessageResult sent ? new { messageId = sent.Id } : result,
                },
            ],
        },
        new TaskTool
        {
            Name = "draft_client_plan",
            Title = "Draft a client's meal plan",
            Description = "For coaches whose client allowed meal planning. Draft a meal plan for the client, empty or with entries, "
                + "or from a template. The plan is the client's: it waits as a draft until they accept or decline it, and they are told.",
            Kind = ToolKind.Write,
            ModeArgument = "action",
            Required = ["clientId", "startDate"],
            Properties = new JsonObject
            {
                ["clientId"] = ToolSchema.Id(ClientHelp),
                ["startDate"] = ToolSchema.Date("The plan's first day, YYYY-MM-DD. Ask the coach when they have not said; never assume one."),
                ["name"] = ToolSchema.String("The plan's name; from_template uses the template's when left out."),
                ["description"] = ToolSchema.String("create: a description."),
                ["durationDays"] = ToolSchema.Integer("create: how many days the plan runs, 7 by default."),
                ["rules"] = ToolSchema.Object("create: the plan's rules, as get_meal_plan returns them."),
                ["entries"] = ToolSchema.Array("create: entries (dayIndex from 0, slot, kind Recipe/Food/Text, recipeId and servings, foodId and grams, or text).",
                    new JsonObject { ["type"] = "object" }),
                ["prepSessions"] = ToolSchema.Array("create: cooking sessions (dayIndex, title, notes).", new JsonObject { ["type"] = "object" }),
                ["templateId"] = ToolSchema.Id("from_template: a template of the coach's own or a global one (get_meal_plan view templates)."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "create",
                    Summary = "A draft plan from startDate, empty or with entries and prep sessions.",
                    Area = McpScopes.CoachingWrite,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(CreatePlanForClientCommand)],
                    Replaces = ["create_plan_for_client"],
                    Build = (args, _) => new CreatePlanForClientCommand(args.RequiredGuid("clientId"), new CreateMealPlanRequest
                    {
                        Name = args.OptionalString("name"),
                        Description = args.OptionalString("description"),
                        StartDate = args.RequiredDate("startDate"),
                        DurationDays = args.OptionalInt("durationDays") ?? 7,
                        Rules = args.As<PlanRulesDto>("rules"),
                        Entries = args.As<List<PlanEntryRequest>>("entries") ?? [],
                        PrepSessions = args.As<List<PrepSessionRequest>>("prepSessions") ?? [],
                    }),
                },
                new ToolMode
                {
                    Name = "from_template",
                    Summary = "A draft plan copied from templateId, starting on startDate.",
                    Area = McpScopes.CoachingWrite,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(ApplyTemplateForClientCommand)],
                    Replaces = ["apply_template_for_client"],
                    Build = (args, _) => new ApplyTemplateForClientCommand(
                        args.RequiredGuid("clientId"),
                        args.RequiredGuid("templateId"),
                        args.RequiredDate("startDate"),
                        args.OptionalString("name")),
                },
            ],
        },
        new TaskTool
        {
            Name = "save_plan_template",
            Title = "Save a plan template",
            Description = "For coaches. Keep a meal plan template of the coach's own, to draft client plans from with draft_client_plan "
                + "from_template. Only the coach sees it. A template's entries are changed with edit_meal_plan, passing its id as planId; "
                + "delete it with delete kind meal_plan_template.",
            Kind = ToolKind.Write,
            ModeArgument = "action",
            Required = ["name"],
            Properties = new JsonObject
            {
                ["templateId"] = ToolSchema.Id("update: the template."),
                ["name"] = ToolSchema.String("The template's name."),
                ["description"] = ToolSchema.String("A description."),
                ["durationDays"] = ToolSchema.Integer("How many days the template covers, 7 by default."),
                ["rules"] = ToolSchema.Object("The template's rules, as get_meal_plan returns them."),
                ["entries"] = ToolSchema.Array("create: entries (dayIndex from 0, slot, kind Recipe/Food/Text, recipeId and servings, foodId and grams, or text).",
                    new JsonObject { ["type"] = "object" }),
                ["prepSessions"] = ToolSchema.Array("create: cooking sessions (dayIndex, title, notes).", new JsonObject { ["type"] = "object" }),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "create",
                    Summary = "A new template.",
                    Area = McpScopes.CoachingWrite,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(CreateTemplateCommand)],
                    Replaces = ["create_template"],
                    Build = (args, _) => new CreateTemplateCommand
                    {
                        Name = args.RequiredString("name"),
                        Description = args.OptionalString("description"),
                        DurationDays = args.OptionalInt("durationDays") ?? 7,
                        Global = false,
                        Rules = args.As<PlanRulesDto>("rules"),
                        Entries = args.As<List<PlanEntryRequest>>("entries") ?? [],
                        PrepSessions = args.As<List<PrepSessionRequest>>("prepSessions") ?? [],
                    },
                },
                new ToolMode
                {
                    Name = "update",
                    Summary = "Change templateId's name, description, duration or rules.",
                    Area = McpScopes.CoachingWrite,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(UpdateTemplateCommand)],
                    Replaces = ["update_template"],
                    Build = (args, _) => new UpdateTemplateCommand(args.RequiredGuid("templateId"), new UpdateTemplateRequest
                    {
                        Name = args.RequiredString("name"),
                        Description = args.OptionalString("description"),
                        DurationDays = args.OptionalInt("durationDays") ?? 7,
                        Rules = args.As<PlanRulesDto>("rules"),
                    }),
                    Shape = _ => new { updated = true },
                },
            ],
        },
        new TaskTool
        {
            Name = "ask_about_client",
            Title = "Ask about a client",
            Description = "For coaches. Ask the Mizan assistant about one client. It reads only what the client shared with the coach and "
                + "allowed AI to read, never writes to the client's log, and spends the coach's AI allowance. Pass threadId to continue a thread.",
            Kind = ToolKind.Write,
            Required = ["clientId", "message"],
            Properties = new JsonObject
            {
                ["clientId"] = ToolSchema.Id(ClientHelp),
                ["message"] = ToolSchema.String("The question."),
                ["threadId"] = ToolSchema.Id("A thread from an earlier answer, to continue it."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "ask",
                    Summary = "Ask about clientId.",
                    Area = McpScopes.CoachingWrite,
                    MinimumRole = ToolRole.Trainer,
                    Requests = [typeof(AskAboutClientCommand)],
                    Replaces = ["ask_ai_about_client"],
                    Build = (args, _) => new AskAboutClientCommand(args.RequiredGuid("clientId"), args.OptionalGuid("threadId"), args.RequiredString("message")),
                },
            ],
        },
    ];

    /// <summary>The coach's own templates are deleted through the delete tool.</summary>
    public static IReadOnlyList<ToolMode> DeleteKinds { get; } =
    [
        new ToolMode
        {
            Name = "meal_plan_template",
            Summary = "For coaches: one of their own meal plan templates, by id. Plans drafted from it are kept.",
            Area = McpScopes.CoachingWrite,
            MinimumRole = ToolRole.Trainer,
            Requests = [typeof(DeleteTemplateCommand)],
            Replaces = ["delete_template"],
            Build = (args, _) => new DeleteTemplateCommand(args.RequiredGuid("id")),
            Shape = _ => new { deleted = true },
        },
    ];

    private static int Page(ToolArgs args) => Math.Max(1, args.OptionalInt("page") ?? 1);

    private static int PageSize(ToolArgs args) => Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 100);
}
