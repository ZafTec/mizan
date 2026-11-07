using System.Text.Json.Nodes;
using Mizan.Application.Commands;
using Mizan.Application.Queries;
using Mizan.Contracts.Mcp;

namespace Mizan.Application.Tools.Catalogue;

/// <summary>The person's own coaching: finding a trainer, their trainer and what the trainer may see, and the chat with them.</summary>
public static class CoachTools
{
    public static IReadOnlyList<TaskTool> All { get; } =
    [
        new TaskTool
        {
            Name = "get_my_coach",
            Title = "My coach",
            Description = "Read the person's coaching: their trainer and what the trainer may see and do, their requests to trainers, trainers taking new clients, or the chat with their trainer.",
            Kind = ToolKind.Read,
            ModeArgument = "view",
            DefaultMode = "mine",
            Properties = new JsonObject
            {
                ["query"] = ToolSchema.String("trainers: part of a name or speciality."),
                ["relationshipId"] = ToolSchema.Id("chat: the coaching relationship, from view mine."),
                ["page"] = ToolSchema.Integer("trainers, requests: page, from 1."),
                ["pageSize"] = ToolSchema.Integer("trainers, requests: results per page, 20 by default."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "mine",
                    Summary = "The active trainer, the relationship id and the permissions granted, or none. The default.",
                    Area = McpScopes.CoachingRead,
                    Requests = [typeof(GetMyTrainerQuery)],
                    Replaces = ["get_my_trainer"],
                    Build = (_, _) => new GetMyTrainerQuery(),
                    Shape = result => result ?? new { trainer = (object?)null, message = "The person has no active trainer." },
                },
                new ToolMode
                {
                    Name = "requests",
                    Summary = "The person's requests to trainers and their status.",
                    Area = McpScopes.CoachingRead,
                    Requests = [typeof(GetMyTrainerRequestsQuery)],
                    Replaces = ["get_my_trainer_requests"],
                    Build = (args, _) => new GetMyTrainerRequestsQuery
                    {
                        Page = Math.Max(1, args.OptionalInt("page") ?? 1),
                        PageSize = Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 100),
                    },
                },
                new ToolMode
                {
                    Name = "trainers",
                    Summary = "Trainers that accept new clients, by query.",
                    Area = McpScopes.CoachingRead,
                    Requests = [typeof(GetAvailableTrainersQuery)],
                    Replaces = ["list_available_trainers"],
                    Build = (args, _) => new GetAvailableTrainersQuery
                    {
                        SearchTerm = args.OptionalString("query"),
                        Page = Math.Max(1, args.OptionalInt("page") ?? 1),
                        PageSize = Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 100),
                    },
                },
                new ToolMode
                {
                    Name = "chat",
                    Summary = "The messages with the trainer in relationshipId, with the conversation id manage_my_coach message needs.",
                    Area = McpScopes.CoachingRead,
                    Requests = [typeof(GetChatConversationQuery)],
                    Replaces = ["get_coach_chat"],
                    Build = (args, _) => new GetChatConversationQuery(args.RequiredGuid("relationshipId")),
                    NotFound = "There is no such conversation.",
                },
            ],
        },
        new TaskTool
        {
            Name = "manage_my_coach",
            Title = "Manage my coach",
            Description = "Ask a trainer to coach the person, change what their trainer may see and do (or end the coaching), or message their trainer. "
                + "Ending the coaching is for the person to decide: confirm with them first.",
            Kind = ToolKind.Write,
            ModeArgument = "action",
            Properties = new JsonObject
            {
                ["trainerId"] = ToolSchema.Id("request: the trainer, from get_my_coach view trainers."),
                ["relationshipId"] = ToolSchema.Id("permissions: the coaching relationship, from get_my_coach view mine."),
                ["canViewNutrition"] = ToolSchema.Boolean("request, permissions: the trainer sees the food diary and nutrition."),
                ["canViewWorkouts"] = ToolSchema.Boolean("request, permissions: the trainer sees workouts."),
                ["canViewMeasurements"] = ToolSchema.Boolean("request, permissions: the trainer sees body measurements."),
                ["canMessage"] = ToolSchema.Boolean("request, permissions: the trainer may message (true by default on a request)."),
                ["canPlanMeals"] = ToolSchema.Boolean("permissions: the trainer may draft meal plans."),
                ["end"] = ToolSchema.Boolean("permissions: true ends the coaching."),
                ["conversationId"] = ToolSchema.Id("message: the conversation, from get_my_coach view chat."),
                ["content"] = ToolSchema.String("message: the message."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "request",
                    Summary = "Ask the trainer trainerId to coach the person, with what they may see.",
                    Area = McpScopes.CoachingWrite,
                    Requests = [typeof(SendTrainerRequestCommand)],
                    Replaces = ["send_trainer_request"],
                    Build = (args, context) => new SendTrainerRequestCommand(
                        context.UserId,
                        args.RequiredGuid("trainerId"),
                        args.OptionalBool("canViewNutrition") ?? false,
                        args.OptionalBool("canViewWorkouts") ?? false,
                        args.OptionalBool("canViewMeasurements") ?? false,
                        args.OptionalBool("canMessage") ?? true),
                    Shape = result => new { relationshipId = result },
                },
                new ToolMode
                {
                    Name = "permissions",
                    Summary = "Change what the trainer in relationshipId may see and do; a value left out keeps its setting. end: true ends the coaching.",
                    Area = McpScopes.CoachingWrite,
                    Requests = [typeof(UpdateTrainerGrantsCommand)],
                    Replaces = ["set_trainer_permissions"],
                    Build = (args, _) => new UpdateTrainerGrantsCommand(
                        args.RequiredGuid("relationshipId"),
                        args.OptionalBool("canViewNutrition"),
                        args.OptionalBool("canViewWorkouts"),
                        args.OptionalBool("canViewMeasurements"),
                        args.OptionalBool("canMessage"),
                        args.OptionalBool("end") ?? false,
                        args.OptionalBool("canPlanMeals")),
                    Shape = result => new { updated = result is true },
                },
                new ToolMode
                {
                    Name = "message",
                    Summary = "Send content in the conversation conversationId.",
                    Area = McpScopes.CoachingWrite,
                    Requests = [typeof(SendChatMessageCommand)],
                    Replaces = ["send_coach_message"],
                    Build = (args, context) => new SendChatMessageCommand(args.RequiredGuid("conversationId"), context.UserId, args.RequiredString("content")),
                    Shape = result => result is SendChatMessageResult sent ? new { messageId = sent.Id } : result,
                },
            ],
        },
    ];
}
