using System.Text.Json.Nodes;
using MediatR;
using Mizan.Application.Commands;
using Mizan.Application.Exceptions;
using Mizan.Application.Tools.Admin;
using Mizan.Contracts.Mcp;

namespace Mizan.Application.Tools.Catalogue;

/// <summary>
/// Administration, for administrators only and never while viewing Mizan as someone else: three
/// reading tools for what is looked at most, and an action runner over every other admin
/// capability in <see cref="AdminActionRegistry"/>. A destructive action previews first and runs
/// only through confirm_action; a safety stop runs at once (docs/MCP.md#admin-actions).
/// </summary>
public static class AdminTools
{
    public const string ConfirmTool = "confirm_action";

    public static IReadOnlyList<TaskTool> All { get; } =
    [
        new TaskTool
        {
            Name = "admin_overview",
            Title = "Admin overview",
            Description = "Admin only. Platform health, usage and analytics, one section at a time.",
            Kind = ToolKind.Read,
            ModeArgument = "section",
            DefaultMode = "overview",
            Properties = new JsonObject
            {
                ["from"] = ToolSchema.Date("platform_usage: inclusive start, this month by default."),
                ["to"] = ToolSchema.Date("platform_usage: inclusive end."),
                ["groupBy"] = ToolSchema.Enum("platform_usage: how rows are grouped.", ["household", "user", "plan", "feature"]),
                ["householdId"] = ToolSchema.Id("platform_usage: one household only."),
                ["userId"] = ToolSchema.Id("platform_usage: one user only."),
                ["searchTerm"] = ToolSchema.String("achievements: part of a name."),
                ["category"] = ToolSchema.String("achievements: one category."),
                ["page"] = ToolSchema.Integer("achievements: page, from 1."),
                ["pageSize"] = ToolSchema.Integer("achievements: rows per page."),
            },
            Modes =
            [
                Section("overview", "admin_get_overview"),
                Section("health", "admin_get_system_health"),
                Section("platform_usage", "admin_get_platform_usage"),
                Section("ai_usage", "admin_get_global_ai_usage"),
                Section("jobs", "admin_get_job_stats"),
                Section("mail", "admin_get_mail_stats"),
                Section("social", "admin_get_social_analytics"),
                Section("achievements", "admin_get_achievement_analytics"),
            ],
        },
        new TaskTool
        {
            Name = "admin_lookup",
            Title = "Admin lookup",
            Description = "Admin only. Find users, sessions, coaching relationships, households or audit entries, or read one user or household "
                + "with its support notes and audit entries (include).",
            Kind = ToolKind.Read,
            ModeArgument = "subject",
            DefaultMode = "users",
            Properties = new JsonObject
            {
                ["id"] = ToolSchema.Id("user, household: the user or household."),
                ["include"] = ToolSchema.Array("user, household: what to read beside it.", ToolSchema.Enum("One of these.", AdminLookupQueryHandler.Includes)),
                ["search"] = ToolSchema.String("users, relationships, households, audit: part of a name or email."),
                ["role"] = ToolSchema.String("users: user, trainer or admin."),
                ["banned"] = ToolSchema.Boolean("users: banned or not."),
                ["status"] = ToolSchema.String("relationships: Pending, Active or Ended."),
                ["activeOnly"] = ToolSchema.Boolean("sessions: only sessions that are still signed in (true by default)."),
                ["subjectType"] = ToolSchema.Enum("notes: what the notes are on.", ["User", "Household"]),
                ["subjectId"] = ToolSchema.Id("notes: the user or household."),
                ["action"] = ToolSchema.String("audit: one action (admin_get_audit_log_facets lists them)."),
                ["entityType"] = ToolSchema.String("audit: one entity type."),
                ["entityId"] = ToolSchema.String("audit: one record."),
                ["userId"] = ToolSchema.Id("audit: what one person did."),
                ["from"] = ToolSchema.Date("audit: inclusive start."),
                ["to"] = ToolSchema.Date("audit: inclusive end."),
                ["page"] = ToolSchema.Integer("Page, from 1."),
                ["pageSize"] = ToolSchema.Integer("Rows per page."),
            },
            Modes =
            [
                Lookup("users", "admin_list_users"),
                new ToolMode
                {
                    Name = "user",
                    Summary = "One user by id with their recent sessions, and the notes or audit entries include asks for.",
                    Area = McpScopes.AdminRead,
                    MinimumRole = ToolRole.Admin,
                    Requests = [typeof(AdminLookupQuery)],
                    Replaces = ["admin_get_user"],
                    Build = (args, _) => new AdminLookupQuery("user", args.RequiredGuid("id"), args.StringList("include")),
                },
                Lookup("sessions", "admin_list_sessions"),
                Lookup("notes", "admin_list_support_notes"),
                Lookup("relationships", "admin_list_relationships"),
                Lookup("households", "admin_list_households"),
                new ToolMode
                {
                    Name = "household",
                    Summary = "One household by id with members, invitations and counts, and the notes or audit entries include asks for.",
                    Area = McpScopes.AdminRead,
                    MinimumRole = ToolRole.Admin,
                    Requests = [typeof(AdminLookupQuery)],
                    Replaces = ["admin_get_household"],
                    Build = (args, _) => new AdminLookupQuery("household", args.RequiredGuid("id"), args.StringList("include")),
                },
                Lookup("audit", "admin_list_audit_logs"),
            ],
        },
        new TaskTool
        {
            Name = "admin_inbox",
            Title = "Admin inbox",
            Description = "Admin only. The queues waiting for an administrator: reported content, price reports, products to match, "
                + "contact messages, background jobs and email. Act on an item with run_action.",
            Kind = ToolKind.Read,
            ModeArgument = "queue",
            DefaultMode = "content_reports",
            Properties = new JsonObject
            {
                ["id"] = ToolSchema.Id("contact_item, mail_item: the message."),
                ["status"] = ToolSchema.String("A status to narrow to; reports default to Open."),
                ["search"] = ToolSchema.String("price_review, contact, mail: part of a name, address or subject."),
                ["sourceId"] = ToolSchema.Id("price_review: one price source."),
                ["type"] = ToolSchema.String("jobs: one job type."),
                ["kind"] = ToolSchema.String("mail: one kind of email."),
                ["from"] = ToolSchema.Date("mail: inclusive start."),
                ["to"] = ToolSchema.Date("mail: inclusive end."),
                ["page"] = ToolSchema.Integer("Page, from 1."),
                ["pageSize"] = ToolSchema.Integer("Rows per page."),
            },
            Modes =
            [
                Queue("content_reports", "admin_list_content_reports"),
                Queue("price_reports", "admin_list_price_reports"),
                Queue("price_review", "admin_list_price_review_queue"),
                Queue("contact", "admin_list_contact"),
                Queue("contact_item", "admin_get_contact"),
                Queue("jobs", "admin_list_jobs"),
                Queue("mail", "admin_list_mail"),
                Queue("mail_item", "admin_get_mail"),
            ],
        },
        new TaskTool
        {
            Name = "find_action",
            Title = "Find an admin action",
            Description = "Admin only. Every other admin capability is an action. search finds actions by words, describe gives one action's "
                + "arguments, and run runs a read-only action. Change something with run_action.",
            Kind = ToolKind.Read,
            ModeArgument = "mode",
            DefaultMode = "search",
            Properties = new JsonObject
            {
                ["query"] = ToolSchema.String("search: words such as \"campaign\", \"price source\" or \"flag\"; empty lists every action."),
                ["id"] = ToolSchema.String("describe, run: the action id, from search."),
                ["arguments"] = ToolSchema.Object("run: the action's arguments, as describe lists them."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "search",
                    Summary = "Actions whose name or summary has every word of query, with how each runs. The default.",
                    Area = McpScopes.AdminRead,
                    MinimumRole = ToolRole.Admin,
                    Requests = [typeof(SearchAdminActionsQuery)],
                    Build = (args, _) => new SearchAdminActionsQuery(args.OptionalString("query"), null),
                },
                new ToolMode
                {
                    Name = "describe",
                    Summary = "One action with its arguments as JSON Schema.",
                    Area = McpScopes.AdminRead,
                    MinimumRole = ToolRole.Admin,
                    Requests = [typeof(SearchAdminActionsQuery)],
                    Build = (args, _) => new SearchAdminActionsQuery(null, args.RequiredString("id")),
                    Shape = result => result is IReadOnlyList<AdminActionInfo> { Count: 1 } one ? one[0] : result,
                },
                new ToolMode
                {
                    Name = "run",
                    Summary = "Run the read-only action id with arguments.",
                    Area = McpScopes.AdminRead,
                    MinimumRole = ToolRole.Admin,
                    Requests = ReadOnly.Select(a => a.Request).Distinct().ToList(),
                    Replaces = ReadOnly.Select(a => a.Id).ToList(),
                    Build = (args, _) => RunReadOnly(args),
                    NotFound = "There is no such record.",
                },
            ],
        },
        RunAction(),
        new TaskTool
        {
            Name = ConfirmTool,
            Title = "Confirm an admin action",
            Description = "Admin only. Carries out a destructive action that run_action previewed, with its token. "
                + "Call it only after showing the person the preview and hearing them agree. A token works once, for ten minutes, "
                + "and is refused when what the preview showed has changed.",
            Kind = ToolKind.Destructive,
            Required = ["token"],
            Properties = new JsonObject
            {
                ["token"] = ToolSchema.String("The token run_action returned."),
            },
            Modes =
            [
                new ToolMode
                {
                    // Not "confirm": that name is the confirm mode a tool gets beside its own previews.
                    Name = "carry_out",
                    Summary = "Carry out the previewed action.",
                    Area = McpScopes.AdminWrite,
                    MinimumRole = ToolRole.Admin,
                    Requests = [typeof(ConfirmToolActionCommand)],
                    Build = (args, context) => new ConfirmToolActionCommand(args.RequiredString("token"), context.Surface),
                },
            ],
        },
    ];

    private static IReadOnlyList<AdminAction> ReadOnly => AdminActionRegistry.All.Where(a => a.Kind == AdminActionKind.ReadOnly).ToList();

    private static ToolMode Section(string name, string action) => ReadMode(name, action);

    private static ToolMode Lookup(string name, string action) => ReadMode(name, action);

    private static ToolMode Queue(string name, string action) => ReadMode(name, action);

    /// <summary>A reading tool's mode: one read-only action, its arguments at the top level.</summary>
    private static ToolMode ReadMode(string name, string id)
    {
        var action = AdminActionRegistry.Get(id);
        return new ToolMode
        {
            Name = name,
            Summary = action.Summary,
            Area = McpScopes.AdminRead,
            MinimumRole = ToolRole.Admin,
            Requests = [action.Request],
            Replaces = [action.Id],
            Build = (args, _) => action.Build(args),
            NotFound = "There is no such record.",
        };
    }

    private static IBaseRequest RunReadOnly(ToolArgs args)
    {
        var id = args.RequiredString("id");
        var action = AdminActionRegistry.Find(id) ?? throw new EntityNotFoundException($"There is no admin action called '{id}'. Search with find_action.");
        if (action.Kind != AdminActionKind.ReadOnly)
            throw new DomainValidationException($"{action.Id} changes something. Run it with run_action.");
        return action.Build(args.ObjectOrEmpty("arguments"));
    }

    /// <summary>
    /// One mode per action that changes something. A destructive one previews through
    /// <see cref="GetAdminActionPreviewQuery"/> and is carried out by confirm_action; the rest run at once.
    /// </summary>
    private static TaskTool RunAction() => new()
    {
        Name = "run_action",
        Title = "Run an admin action",
        Description = "Admin only. Runs an admin action that changes something, by its id from find_action, with its arguments. "
            + "A destructive action does nothing yet: it returns a preview and a token. Show the person the preview, and only when they "
            + "agree call confirm_action with the token. Pausing a campaign and revoking sessions run at once.",
        Kind = ToolKind.Write,
        ModeArgument = "action",
        ConfirmTool = ConfirmTool,
        ModeHelp = "The action id, from find_action search; find_action describe lists its arguments.",
        Properties = new JsonObject
        {
            ["arguments"] = ToolSchema.Object("The action's arguments, as find_action describe lists them."),
        },
        Modes = AdminActionRegistry.All.Where(a => a.Kind != AdminActionKind.ReadOnly).Select(ActionMode).ToList(),
    };

    private static ToolMode ActionMode(AdminAction action) => new()
    {
        Name = action.Id,
        Summary = action.Summary,
        Area = McpScopes.AdminWrite,
        MinimumRole = ToolRole.Admin,
        Requests = [action.Request],
        Replaces = [action.Id],
        Build = (args, _) => action.Build(args.ObjectOrEmpty("arguments")),
        Shape = result => result is null or Unit ? new { action = action.Id, done = true } : result,
        Confirm = action.Kind == AdminActionKind.Destructive
            ? new ToolConfirmStep(
                typeof(GetAdminActionPreviewQuery),
                (args, _) => new GetAdminActionPreviewQuery(action.Id, args.ObjectOrEmpty("arguments").Root),
                result => (result as JsonObject)?.DeepClone() ?? new JsonObject())
            : null,
    };
}
