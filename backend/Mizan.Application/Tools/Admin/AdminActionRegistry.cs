using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Mizan.Application.Admin;
using Mizan.Application.Ai;
using Mizan.Application.Commands;
using Mizan.Application.Contact;
using Mizan.Application.Features;
using Mizan.Application.Mail;
using Mizan.Application.Notifications;
using Mizan.Application.Planning;
using Mizan.Application.Prices;
using Mizan.Application.Queries;
using Mizan.Application.Settings;
using Mizan.Application.Usage;
using Mizan.Contracts.Notifications;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Tools.Admin;

/// <summary>How an admin action runs (docs/MCP.md#admin-actions).</summary>
public enum AdminActionKind
{
    /// <summary>Only reads. Run through admin_overview, admin_lookup, admin_inbox or find_action.</summary>
    ReadOnly,

    /// <summary>Changes something that is easy to see and undo. run_action does it at once.</summary>
    RunsAtOnce,

    /// <summary>Removes, sends or changes access. run_action previews it and confirm_action carries it out.</summary>
    Destructive,

    /// <summary>Stops harm: run at once, without the confirm step, and audited like every command.</summary>
    SafetyStop,
}

/// <summary>
/// One administrative capability, by the name of the legacy MCP tool it replaces. It builds the
/// same MediatR request the admin console sends, so the command's validation, authorization and
/// audit apply unchanged. <see cref="Arguments"/> are the records whose properties the arguments
/// are read into; find_action describes them from there.
/// </summary>
public sealed record AdminAction(
    string Id,
    AdminActionKind Kind,
    string Summary,
    Type Request,
    IReadOnlyList<Type> Arguments,
    Func<ToolArgs, IBaseRequest> Build)
{
    public JsonObject Schema() => AdminActionSchema.For(Arguments);
}

/// <summary>The id argument of an action whose other arguments are a Contracts record.</summary>
public sealed record AdminIdArgument(Guid Id);

/// <summary>What admin_update_user may change. A password is never set through a connected app.</summary>
public sealed record AdminUserChange(
    Guid UserId,
    string? Role = null,
    bool? Banned = null,
    string? BanReason = null,
    DateTime? BanExpires = null,
    bool? EmailVerified = null);

/// <summary>
/// Every administrative action a connected app can reach: one entry for each of the 95 legacy admin
/// tools. The admin task tools read from here, and a test holds the list to the legacy tools.
/// </summary>
public static class AdminActionRegistry
{
    private static readonly Lazy<IReadOnlyList<AdminAction>> Actions = new(Build);

    public static IReadOnlyList<AdminAction> All => Actions.Value;

    public static AdminAction? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));

    public static AdminAction Get(string id) => Find(id) ?? throw new InvalidOperationException($"No admin action {id}.");

    private static AdminAction Read<T>(string id, string summary) where T : class, IBaseRequest =>
        new(id, AdminActionKind.ReadOnly, summary, typeof(T), [typeof(T)], args => args.Body<T>());

    private static AdminAction ReadNone<T>(string id, string summary) where T : class, IBaseRequest, new() =>
        new(id, AdminActionKind.ReadOnly, summary, typeof(T), [], _ => new T());

    private static AdminAction Change<T>(string id, AdminActionKind kind, string summary) where T : class, IBaseRequest =>
        new(id, kind, summary, typeof(T), [typeof(T)], args => args.Body<T>());

    private static AdminAction ChangeNone<T>(string id, AdminActionKind kind, string summary) where T : class, IBaseRequest, new() =>
        new(id, kind, summary, typeof(T), [], _ => new T());

    private const AdminActionKind Now = AdminActionKind.RunsAtOnce;
    private const AdminActionKind Confirm = AdminActionKind.Destructive;
    private const AdminActionKind Stop = AdminActionKind.SafetyStop;

    private static IReadOnlyList<AdminAction> Build()
    {
        List<AdminAction> actions =
        [
            // ---- Overview and usage -----------------------------------------------------------
            ReadNone<GetAdminOverviewQuery>("admin_get_overview", "Users, trainers, bans, active sessions and the newest sign-ups."),
            ReadNone<GetSystemHealthQuery>("admin_get_system_health", "The database, cache, storage, mail, AI provider and background jobs, each healthy or not."),
            Read<GetPlatformUsageQuery>("admin_get_platform_usage",
                "AI requests, tokens, cost and refusals from the usage ledger, grouped by household, user, plan or feature; from and to are inclusive dates, this month by default."),
            ReadNone<GetGlobalAiUsageQuery>("admin_get_global_ai_usage", "AI usage across everyone: today, this month, and the heaviest users."),
            ReadNone<GetAdminJobStatsQuery>("admin_get_job_stats", "Background jobs by status and type, with the oldest pending."),
            ReadNone<GetMailStatsQuery>("admin_get_mail_stats", "Email sent, failed and queued, by kind."),
            ReadNone<GetSocialAnalyticsQuery>("admin_get_social_analytics", "Social profiles, follows, posts, reactions and open reports."),
            Read<GetAchievementAnalyticsQuery>("admin_get_achievement_analytics", "How many people earned each achievement, by searchTerm or category."),

            // ---- Users, sessions, relationships, households, audit ---------------------------
            Read<ListAdminUsersQuery>("admin_list_users", "Users by search (name or email), role or banned."),
            Read<GetAdminUserQuery>("admin_get_user", "One user with their active session count and recent sessions."),
            Read<ListAdminSessionsQuery>("admin_list_sessions", "Signed-in sessions, active only by default."),
            Read<ListSupportNotesQuery>("admin_list_support_notes", "Support notes on a user or household: subjectType User or Household, subjectId."),
            Read<ListAdminRelationshipsQuery>("admin_list_relationships", "Coaching relationships by search or status."),
            Read<GetAllHouseholdsQuery>("admin_list_households", "Households by search."),
            Read<GetAdminHouseholdQuery>("admin_get_household", "One household with its members, invitations, meal plans and shopping lists."),
            Read<GetAuditLogsQuery>("admin_list_audit_logs", "The audit log by action, entityType, entityId, userId, search and inclusive from and to dates."),
            ReadNone<GetAuditLogFacetsQuery>("admin_get_audit_log_facets", "The actions and entity types the audit log holds, for filtering it."),

            // ---- Queues ---------------------------------------------------------------------
            Read<GetContentReportsQuery>("admin_list_content_reports", "Reported social content, Open by default."),
            Read<GetPriceReportsQuery>("admin_list_price_reports", "Prices people reported as wrong, Open by default."),
            new("admin_list_price_review_queue", AdminActionKind.ReadOnly, "Scraped products waiting to be matched to a food, by sourceId or search.",
                typeof(GetPriceAliasesQuery), [typeof(GetPriceAliasesQuery)], args => args.Body<GetPriceAliasesQuery>() with { Queue = true }),
            Read<ListContactSubmissionsQuery>("admin_list_contact", "Contact form messages by status or search."),
            Read<GetContactSubmissionQuery>("admin_get_contact", "One contact message with its reply, if any."),
            Read<ListAdminJobsQuery>("admin_list_jobs", "Background jobs by type or status."),
            Read<ListEmailMessagesQuery>("admin_list_mail", "Sent and queued email by status, kind, search and inclusive from and to dates."),
            Read<GetEmailMessageQuery>("admin_get_mail", "One email with its body and delivery status."),

            // ---- Other reads ----------------------------------------------------------------
            Read<GetAdminAchievementQuery>("admin_get_achievement", "One achievement definition by id."),
            ReadNone<ListAiPromptsQuery>("admin_list_ai_prompts", "The AI prompts with their published and draft versions."),
            Read<GetAiPromptQuery>("admin_get_ai_prompt", "One AI prompt by key, with every version."),
            Read<GetAiEvalMatrixQuery>("admin_get_ai_prompt_evals", "The eval results of one prompt version (versionId), case by case."),
            ReadNone<GetRuntimeSettingsQuery>("admin_list_settings", "The runtime settings by group, with where each value comes from. Secrets show only whether they are set."),
            Read<GetPriceSourcesQuery>("admin_list_price_sources", "Price sources, by regionCode, with their last run."),
            new("admin_list_price_regions", AdminActionKind.ReadOnly, "Price regions, disabled ones too.",
                typeof(GetPriceRegionsQuery), [], _ => new GetPriceRegionsQuery(IncludeDisabled: true)),
            ReadNone<ListFeatureFlagsQuery>("admin_list_feature_flags", "Every feature flag with its default and how many households override it."),
            Read<GetFeatureFlagOverridesQuery>("admin_list_feature_flag_overrides", "The households that override one flag, and whether each has it on."),
            Read<GetUsageLimitSubjectQuery>("admin_get_usage_limit", "The AI allowance in force for one subject, with a user's subscription plan tier (free or pro): scope Plan, Household or User, and subjectId."),
            new("admin_list_usage_limits", AdminActionKind.ReadOnly, "AI allowance overrides for scope Plan (the default), Household or User.",
                typeof(ListUsageLimitsQuery), [typeof(ListUsageLimitsQuery)], args => new ListUsageLimitsQuery(args.OptionalString("scope") ?? "Plan")),
            ReadNone<ListNotificationCampaignsQuery>("admin_list_notification_campaigns", "Every campaign and lifecycle message, newest first, with its phase and totals."),
            Read<GetNotificationCampaignQuery>("admin_get_notification_campaign", "One campaign with its message, audience, timing and delivery report per channel."),
            new("admin_preview_audience", AdminActionKind.ReadOnly,
                "How many people an audience reaches now: purpose marketing or lifecycle, audience filters, channels.",
                typeof(PreviewCampaignAudienceQuery), [typeof(CampaignAudienceRequest)],
                args => new PreviewCampaignAudienceQuery(args.Body<CampaignAudienceRequest>())),
            new("admin_preview_campaign_message", AdminActionKind.ReadOnly, "How a campaign message reads on each channel, before it is saved.",
                typeof(PreviewCampaignMessageQuery), [typeof(CampaignPreviewRequest)],
                args => new PreviewCampaignMessageQuery(args.Body<CampaignPreviewRequest>())),

            // ---- Run at once ----------------------------------------------------------------
            Change<CreateAchievementCommand>("admin_create_achievement", Now, "Create an achievement: name, points, category, criteriaType, threshold."),
            Change<UpdateAchievementCommand>("admin_update_achievement", Now, "Change an achievement, sending every field."),
            new("admin_save_builtin_workout_template", Now, "Create a built-in workout template, or update one with id.",
                typeof(SaveWorkoutTemplateCommand), [typeof(SaveWorkoutTemplateCommand)],
                args => args.Body<SaveWorkoutTemplateCommand>() with { IsBuiltIn = true }),
            Change<RetryAdminJobCommand>("admin_retry_job", Now, "Run a failed background job again."),
            Change<RetryEmailMessageCommand>("admin_retry_mail", Now, "Send a failed email again."),
            Change<MarkContactHandledCommand>("admin_mark_contact_handled", Now, "Mark a contact message handled."),
            Change<AddSupportNoteCommand>("admin_add_support_note", Now, "Add a support note to a user or household."),
            Change<UpdateSupportNoteCommand>("admin_update_support_note", Now, "Change a support note."),
            Change<CreateAiPromptDraftCommand>("admin_create_ai_prompt_draft", Now, "Start a draft version of a prompt, from the published one when body is left out."),
            Change<UpdateAiPromptDraftCommand>("admin_update_ai_prompt_draft", Now, "Change a draft prompt version."),
            Change<RunAiPromptEvalsCommand>("admin_run_ai_prompt_evals", Now, "Run the eval cases against a prompt version. Spends the eval allowance."),
            ChangeNone<SendTestEmailCommand>("admin_test_smtp", Now, "Send a test email to yourself with the current mail settings."),
            new("admin_create_template", Now, "Publish a global meal plan template everyone can apply.",
                typeof(CreateTemplateCommand), [typeof(CreateTemplateRequest)],
                args => args.Body<CreateTemplateCommand>() with { Global = true }),
            new("admin_update_template", Now, "Change a global template's name, description, duration or rules.",
                typeof(UpdateTemplateCommand), [typeof(AdminIdArgument), typeof(UpdateTemplateRequest)],
                args => new UpdateTemplateCommand(args.RequiredGuid("id"), args.Body<UpdateTemplateRequest>())),
            Change<RunPriceSourceCommand>("admin_run_price_source", Now, "Run a price source now."),
            Change<AcceptPriceAliasSuggestionsCommand>("admin_accept_price_alias_suggestions", Now, "Accept the suggested food for each alias in aliasIds."),
            ChangeNone<ImportStarterFoodsCommand>("admin_import_starter_foods", Now, "Import the starter food catalogue; foods already there are kept."),
            Change<CreateManualPriceCommand>("admin_add_manual_price", Now, "Record a price seen in a shop for a food in a region."),
            new("admin_create_price_region", Now, "Add a price region: regionCode, name, currency, enabled.",
                typeof(CreatePriceRegionCommand), [typeof(CreatePriceRegionCommand)],
                args =>
                {
                    var region = args.Body<CreatePriceRegionCommand>();
                    return region with { RegionCode = (region.RegionCode ?? "").Trim().ToUpperInvariant(), Currency = (region.Currency ?? "").Trim().ToUpperInvariant() };
                }),
            Change<UpdatePriceRegionCommand>("admin_update_price_region", Now, "Change a price region's name, currency or whether it is enabled."),
            new("admin_create_price_source", Now, "Add a price source: name, kind, regionCode, url, scraperKey, schedule, enabled.",
                typeof(SavePriceSourceCommand), [typeof(SavePriceSourceCommand)],
                args => args.Body<SavePriceSourceCommand>() with { Id = null }),
            new("admin_update_price_source", Now, "Change a price source, by id, sending every field.",
                typeof(SavePriceSourceCommand), [typeof(SavePriceSourceCommand)],
                args => args.Body<SavePriceSourceCommand>() with { Id = args.RequiredGuid("id") }),
            ChangeNone<SeedStaplePricesCommand>("admin_seed_staple_prices", Now, "Seed reference prices for staple foods where none exist."),
            new("admin_create_notification_campaign", Now,
                "Save a campaign as a draft: name, purpose, message, timing, audience, channels, timeZoneId. Nothing is sent until it is activated.",
                typeof(CreateNotificationCampaignCommand), [typeof(SaveNotificationCampaignRequest)],
                args => new CreateNotificationCampaignCommand(args.Body<SaveNotificationCampaignRequest>())),
            new("admin_update_notification_campaign", Now, "Change a draft or paused campaign, sending every field; an active one is paused first.",
                typeof(UpdateNotificationCampaignCommand), [typeof(AdminIdArgument), typeof(SaveNotificationCampaignRequest)],
                args => new UpdateNotificationCampaignCommand(args.RequiredGuid("id"), args.Body<SaveNotificationCampaignRequest>())),
            Change<SendCampaignTestCommand>("admin_send_test_notification", Now, "Send a campaign to yourself alone, now, on each of its channels."),

            // ---- Destructive: preview, then confirm_action ----------------------------------
            Change<DeleteAchievementCommand>("admin_delete_achievement", Confirm, "Delete an achievement and everyone's progress on it."),
            Change<ResolveContentReportCommand>("admin_resolve_content_report", Confirm, "Close a content report: action dismiss or remove (removes the content), with a note."),
            Change<PromoteExerciseCommand>("admin_promote_exercise", Confirm, "Make someone's custom exercise part of the shared catalogue."),
            new("admin_update_user", Confirm, "Change a user's role, ban them or lift a ban, or set their email as verified.",
                typeof(UpdateAdminUserCommand), [typeof(AdminUserChange)],
                args =>
                {
                    var change = args.Body<AdminUserChange>();
                    return new UpdateAdminUserCommand(change.UserId, change.Role, change.Banned, change.BanReason, change.BanExpires, change.EmailVerified, NewPassword: null);
                }),
            Change<EndAdminRelationshipCommand>("admin_end_relationship", Confirm, "End a coaching relationship, with a reason."),
            Change<DeleteAdminJobCommand>("admin_delete_job", Confirm, "Delete a background job; it does not run."),
            Change<SendAnnouncementCommand>("admin_send_announcement", Confirm, "Email an announcement to an audience: all, verified, trainers or pro."),
            Change<ReplyContactCommand>("admin_reply_contact", Confirm, "Email a reply to a contact message."),
            Change<DeleteSupportNoteCommand>("admin_delete_support_note", Confirm, "Delete a support note."),
            Change<AdminRemoveHouseholdMemberCommand>("admin_remove_household_member", Confirm, "Remove a member (targetUserId) from a household."),
            Change<AdminDeleteHouseholdCommand>("admin_delete_household", Confirm, "Delete a household with its shared plans, lists and recipes."),
            Change<PublishAiPromptVersionCommand>("admin_publish_ai_prompt_version", Confirm, "Publish a prompt version: every new AI call uses it."),
            Change<SetRuntimeSettingCommand>("admin_set_setting", Confirm, "Set a runtime setting by key. A secret is set in the admin console, not here."),
            Change<ClearRuntimeSettingCommand>("admin_clear_setting", Confirm, "Clear a runtime setting so its default applies."),
            Change<DeleteTemplateCommand>("admin_delete_template", Confirm, "Delete a global meal plan template. Plans made from it are kept."),
            Change<UpdatePriceAliasCommand>("admin_update_price_alias", Confirm, "Match a scraped product to a food with its quantity and unit, or ignore it."),
            Change<ResolvePriceReportCommand>("admin_resolve_price_report", Confirm, "Accept a price report (its price is recorded) or reject it, with a note."),
            Change<DeletePriceSourceCommand>("admin_delete_price_source", Confirm, "Delete a price source with its aliases."),
            Change<SetFeatureFlagDefaultCommand>("admin_set_feature_flag", Confirm, "Set a flag's default for every household without an override."),
            Change<SetFeatureFlagOverrideCommand>("admin_set_feature_flag_override", Confirm, "Turn a flag on or off for one household."),
            Change<ClearFeatureFlagOverrideCommand>("admin_clear_feature_flag_override", Confirm, "Put one household back on a flag's default."),
            Change<SetUsageLimitCommand>("admin_set_usage_limit", Confirm,
                "Create or replace an AI allowance override: scope, subjectId, line (or every line), the caps (costs in millionths) and a note."),
            Change<ClearUsageLimitCommand>("admin_clear_usage_limit", Confirm, "Remove an AI allowance override so the subject inherits again."),
            Change<ActivateNotificationCampaignCommand>("admin_activate_notification_campaign", Confirm,
                "Start a draft campaign. audienceCount is the reach the preview showed; a campaign sent now goes out at once."),
            Change<ResumeNotificationCampaignCommand>("admin_resume_campaign", Confirm, "Start a paused campaign again from its next occurrence."),
            Change<CancelNotificationCampaignCommand>("admin_cancel_campaign", Confirm, "End a campaign for good; nothing queued goes out."),

            // ---- Safety stops: at once ------------------------------------------------------
            Change<PauseNotificationCampaignCommand>("admin_pause_campaign", Stop, "Pause an active campaign now. Deliveries due while paused are not sent."),
            Change<RevokeAdminSessionCommand>("admin_revoke_session", Stop, "Sign out one session now."),
            Change<RevokeUserSessionsCommand>("admin_revoke_user_sessions", Stop, "Sign a user out everywhere now."),
        ];

        var duplicate = actions.GroupBy(a => a.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Two admin actions are called {duplicate.Key}.");
        return actions;
    }
}

/// <summary>The JSON Schema of an action's arguments, read from the records it binds them to.</summary>
public static class AdminActionSchema
{
    private static readonly NullabilityInfoContext Nullability = new();

    public static JsonObject For(IReadOnlyList<Type> types)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var type in types)
        {
            foreach (var (name, schema, isRequired) in Members(type, depth: 0))
            {
                if (properties.ContainsKey(name)) continue;
                properties[name] = schema;
                if (isRequired) required.Add(name);
            }
        }

        var root = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Count > 0) root["required"] = required;
        return root;
    }

    private static IEnumerable<(string Name, JsonNode Schema, bool Required)> Members(Type type, int depth)
    {
        // A positional record binds through its constructor; any other record through its settable properties.
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        var parameters = constructor?.GetParameters() ?? [];
        if (parameters.Length > 0 && !(parameters.Length == 1 && parameters[0].ParameterType == type))
        {
            foreach (var parameter in parameters)
            {
                var nullable = Nullable.GetUnderlyingType(parameter.ParameterType) is not null
                    || (!parameter.ParameterType.IsValueType && Nullability.Create(parameter).WriteState == NullabilityState.Nullable);
                yield return (JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!), SchemaOf(parameter.ParameterType, depth), !nullable && !parameter.HasDefaultValue);
            }

            yield break;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.SetMethod!.IsPublic))
        {
            yield return (JsonNamingPolicy.CamelCase.ConvertName(property.Name), SchemaOf(property.PropertyType, depth), false);
        }
    }

    private static JsonNode SchemaOf(Type type, int depth)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) return new JsonObject { ["type"] = "string" };
        if (type == typeof(Guid)) return new JsonObject { ["type"] = "string", ["format"] = "uuid" };
        if (type == typeof(bool)) return new JsonObject { ["type"] = "boolean" };
        if (type == typeof(int) || type == typeof(long)) return new JsonObject { ["type"] = "integer" };
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return new JsonObject { ["type"] = "number" };
        if (type == typeof(DateOnly)) return new JsonObject { ["type"] = "string", ["format"] = "date" };
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return new JsonObject { ["type"] = "string", ["format"] = "date-time" };
        if (type == typeof(TimeOnly)) return new JsonObject { ["type"] = "string", ["format"] = "time" };
        if (type.IsEnum)
            return new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(Enum.GetNames(type).Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()) };
        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            var item = type.IsArray ? type.GetElementType()! : type.GetGenericArguments().FirstOrDefault() ?? typeof(object);
            return new JsonObject { ["type"] = "array", ["items"] = SchemaOf(item, depth + 1) };
        }

        var schema = new JsonObject { ["type"] = "object" };
        if (depth < 2 && type != typeof(object) && type.Namespace?.StartsWith("Mizan", StringComparison.Ordinal) == true)
        {
            var properties = new JsonObject();
            foreach (var (name, child, _) in Members(type, depth + 1)) properties[name] = child;
            schema["properties"] = properties;
        }

        return schema;
    }
}
