using System.Text.Json.Nodes;
using Mizan.Application.Commands;
using Mizan.Application.Queries;
using Mizan.Contracts.Mcp;

namespace Mizan.Application.Tools.Catalogue;

/// <summary>Body measurements: the trend, and one measurement a day.</summary>
public static class BodyTools
{
    public static IReadOnlyList<TaskTool> All { get; } =
    [
        new TaskTool
        {
            Name = "get_body_trend",
            Title = "Body measurement trend",
            Description = "Body measurements (weight, body fat, muscle mass, circumferences) over a range of days, newest first. Renders the body-trend view in hosts that show apps.",
            Kind = ToolKind.Read,
            App = "ui://mizan/body-trend.html",
            Properties = new JsonObject
            {
                ["from"] = ToolSchema.Date("First day, YYYY-MM-DD. Optional."),
                ["to"] = ToolSchema.Date("Last day, YYYY-MM-DD. Optional."),
                ["page"] = ToolSchema.Integer("Page, from 1."),
                ["pageSize"] = ToolSchema.Integer("Measurements per page, 20 by default (at most 25)."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "trend",
                    Summary = "Measurements between from and to.",
                    Area = McpScopes.BodyRead,
                    Requests = [typeof(GetBodyMeasurementsQuery)],
                    Replaces = ["list_body_measurements"],
                    Build = (args, context) => new GetBodyMeasurementsQuery
                    {
                        UserId = context.UserId,
                        Page = args.OptionalInt("page") ?? 1,
                        // A page stays well under a host's reply limit (docs/MCP.md#reply-size).
                        PageSize = Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 25),
                        SortBy = "date",
                        SortOrder = "desc",
                        From = args.OptionalDate("from"),
                        To = args.OptionalDate("to"),
                    },
                },
            ],
        },
        new TaskTool
        {
            Name = "log_body",
            Title = "Log body measurements",
            Description = "Record weight and any other measurements for a day. One measurement a day: a later write for the same day updates it. Log what was measured; leave the rest out.",
            Kind = ToolKind.Write,
            Idempotent = true,
            Properties = new JsonObject
            {
                ["date"] = ToolSchema.Date("YYYY-MM-DD, today by default."),
                ["weightKg"] = ToolSchema.Number("Weight in kg."),
                ["bodyFatPercentage"] = ToolSchema.Number("Body fat, percent."),
                ["muscleMassKg"] = ToolSchema.Number("Muscle mass in kg."),
                ["waistCm"] = ToolSchema.Number("Waist in cm."),
                ["hipsCm"] = ToolSchema.Number("Hips in cm."),
                ["chestCm"] = ToolSchema.Number("Chest in cm."),
                ["leftArmCm"] = ToolSchema.Number("Left arm in cm."),
                ["rightArmCm"] = ToolSchema.Number("Right arm in cm."),
                ["leftThighCm"] = ToolSchema.Number("Left thigh in cm."),
                ["rightThighCm"] = ToolSchema.Number("Right thigh in cm."),
                ["notes"] = ToolSchema.String("A note. Optional."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "measurement",
                    Summary = "One day's measurements.",
                    Area = McpScopes.BodyWrite,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(LogBodyMeasurementCommand)],
                    Replaces = ["log_body_measurement"],
                    Build = (args, context) => new LogBodyMeasurementCommand(
                        context.UserId,
                        (args.OptionalDate("date") ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        args.OptionalDecimal("weightKg"),
                        args.OptionalDecimal("bodyFatPercentage"),
                        args.OptionalDecimal("muscleMassKg"),
                        args.OptionalDecimal("waistCm"),
                        args.OptionalDecimal("hipsCm"),
                        args.OptionalDecimal("chestCm"),
                        args.OptionalDecimal("leftArmCm"),
                        args.OptionalDecimal("rightArmCm"),
                        args.OptionalDecimal("leftThighCm"),
                        args.OptionalDecimal("rightThighCm"),
                        args.OptionalString("notes")),
                    Describe = _ => "Recorded your measurement.",
                },
            ],
        },
    ];

    public static IReadOnlyList<ToolMode> DeleteKinds { get; } =
    [
        new ToolMode
        {
            Name = "measurement",
            Summary = "A body measurement, by id.",
            Area = McpScopes.BodyWrite,
            Requests = [typeof(DeleteBodyMeasurementCommand)],
            Replaces = ["delete_body_measurement"],
            Build = (args, _) => new DeleteBodyMeasurementCommand(args.RequiredGuid("id")),
        },
    ];
}
