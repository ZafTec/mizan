using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Admin;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Features;
using Mizan.Application.Interfaces;
using Mizan.Application.Mail;
using Mizan.Application.Notifications;
using Mizan.Application.Queries;
using Mizan.Application.Settings;
using Mizan.Application.Usage;
using Mizan.Contracts.Admin;
using Mizan.Contracts.Notifications;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Tools.Admin;

/// <summary>One achievement definition, as the admin console's achievement page reads it.</summary>
public record GetAdminAchievementQuery(Guid Id) : IRequest<AchievementDto?>;

public sealed class GetAdminAchievementQueryHandler : IRequestHandler<GetAdminAchievementQuery, AchievementDto?>
{
    private readonly ISender _sender;

    public GetAdminAchievementQueryHandler(ISender sender) => _sender = sender;

    public async Task<AchievementDto?> Handle(GetAdminAchievementQuery request, CancellationToken cancellationToken)
    {
        var all = await _sender.Send(new GetAchievementsQuery { Page = 1, PageSize = int.MaxValue }, cancellationToken);
        return all.Items.FirstOrDefault(a => a.Id == request.Id);
    }
}

/// <summary>An admin action as find_action lists it: what it does, how it runs and, when described, its arguments.</summary>
public sealed record AdminActionInfo(string Id, string Kind, string Summary, string RunWith, JsonObject? Arguments);

/// <summary>
/// The admin actions whose id or summary holds every word of <see cref="Query"/>, or the one named
/// <see cref="Id"/> with its arguments. Static: it reads the registry, never the database.
/// </summary>
public record SearchAdminActionsQuery(string? Query, string? Id) : IRequest<IReadOnlyList<AdminActionInfo>>;

public sealed class SearchAdminActionsQueryHandler : IRequestHandler<SearchAdminActionsQuery, IReadOnlyList<AdminActionInfo>>
{
    public Task<IReadOnlyList<AdminActionInfo>> Handle(SearchAdminActionsQuery request, CancellationToken cancellationToken)
    {
        if (request.Id is { Length: > 0 } id)
        {
            var action = AdminActionRegistry.Find(id.Trim()) ?? throw new EntityNotFoundException($"There is no admin action called '{id}'. Search with find_action.");
            return Task.FromResult<IReadOnlyList<AdminActionInfo>>([Info(action, withArguments: true)]);
        }

        var words = (request.Query ?? "")
            .Split([' ', '_', ',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant())
            .ToList();
        var matches = AdminActionRegistry.All
            .Where(a => words.All(w => a.Id.Contains(w, StringComparison.OrdinalIgnoreCase) || a.Summary.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(a => Info(a, withArguments: false))
            .ToList();
        return Task.FromResult<IReadOnlyList<AdminActionInfo>>(matches);
    }

    public static AdminActionInfo Info(AdminAction action, bool withArguments) => new(
        action.Id,
        action.Kind switch
        {
            AdminActionKind.ReadOnly => "read_only",
            AdminActionKind.RunsAtOnce => "runs_at_once",
            AdminActionKind.SafetyStop => "safety_stop",
            _ => "destructive",
        },
        action.Summary,
        action.Kind switch
        {
            AdminActionKind.ReadOnly => "find_action mode run",
            AdminActionKind.Destructive => "run_action previews it; confirm_action carries it out",
            _ => "run_action",
        },
        withArguments ? action.Schema() : null);
}

/// <summary>
/// A user or household with what an administrator needs beside it: support notes and audit entries
/// when <see cref="Include"/> asks for them. A user always comes with their recent sessions.
/// </summary>
public record AdminLookupQuery(string Subject, Guid Id, IReadOnlyList<string> Include) : IRequest<AdminLookupResult>;

public sealed record AdminLookupResult(object Record, IReadOnlyList<SupportNoteDto>? Notes, PagedResult<AuditLogDto>? Audit);

public sealed class AdminLookupQueryHandler : IRequestHandler<AdminLookupQuery, AdminLookupResult>
{
    public static readonly IReadOnlyList<string> Includes = ["notes", "audit"];

    private readonly ISender _sender;

    public AdminLookupQueryHandler(ISender sender) => _sender = sender;

    public async Task<AdminLookupResult> Handle(AdminLookupQuery request, CancellationToken cancellationToken)
    {
        var user = string.Equals(request.Subject, "user", StringComparison.OrdinalIgnoreCase);
        object record = user
            ? await _sender.Send(new GetAdminUserQuery(request.Id), cancellationToken)
            : await _sender.Send(new GetAdminHouseholdQuery(request.Id), cancellationToken);

        var include = request.Include.Select(i => i.Trim().ToLowerInvariant()).ToHashSet();
        var notes = include.Contains("notes")
            ? await _sender.Send(new ListSupportNotesQuery(user ? "User" : "Household", request.Id), cancellationToken)
            : null;
        var audit = include.Contains("audit")
            ? await _sender.Send(user
                ? new GetAuditLogsQuery { UserId = request.Id, PageSize = 20 }
                : new GetAuditLogsQuery { EntityId = request.Id.ToString(), PageSize = 20 }, cancellationToken)
            : null;
        return new AdminLookupResult(record, notes, audit);
    }
}

/// <summary>
/// What a destructive admin action would do, read before anything changes: who a campaign reaches,
/// what deleting a household removes, whose access changes. The facts hold no clock, so the same
/// state reads the same when the person confirms and a change in between is caught
/// (docs/MCP.md#admin-actions).
/// </summary>
public record GetAdminActionPreviewQuery(string Action, JsonElement Arguments) : IRequest<JsonObject>;

public sealed class GetAdminActionPreviewQueryHandler : IRequestHandler<GetAdminActionPreviewQuery, JsonObject>
{
    /// <summary>The actions this handler previews. A test holds it to the destructive actions of the registry.</summary>
    public static readonly IReadOnlySet<string> Previewed = new HashSet<string>
    {
        "admin_delete_achievement", "admin_resolve_content_report", "admin_promote_exercise", "admin_update_user",
        "admin_end_relationship", "admin_delete_job", "admin_send_announcement", "admin_reply_contact",
        "admin_delete_support_note", "admin_remove_household_member", "admin_delete_household",
        "admin_publish_ai_prompt_version", "admin_set_setting", "admin_clear_setting", "admin_delete_template",
        "admin_update_price_alias", "admin_resolve_price_report", "admin_delete_price_source", "admin_set_feature_flag",
        "admin_set_feature_flag_override", "admin_clear_feature_flag_override", "admin_set_usage_limit",
        "admin_clear_usage_limit", "admin_activate_notification_campaign", "admin_resume_campaign", "admin_cancel_campaign",
    };

    private readonly IMizanDbContext _context;
    private readonly ISender _sender;

    public GetAdminActionPreviewQueryHandler(IMizanDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<JsonObject> Handle(GetAdminActionPreviewQuery request, CancellationToken ct)
    {
        var args = new ToolArgs(request.Arguments);
        var facts = request.Action switch
        {
            "admin_delete_achievement" => await Achievement(args.RequiredGuid("id"), ct),
            "admin_resolve_content_report" => await ContentReport(args, ct),
            "admin_promote_exercise" => await Exercise(args.RequiredGuid("id"), ct),
            "admin_update_user" => await UserChange(args, ct),
            "admin_end_relationship" => await Relationship(args, ct),
            "admin_delete_job" => await Job(args.RequiredGuid("id"), ct),
            "admin_send_announcement" => await Announcement(args, ct),
            "admin_reply_contact" => await ContactReply(args, ct),
            "admin_delete_support_note" => await SupportNote(args.RequiredGuid("id"), ct),
            "admin_remove_household_member" => await HouseholdMember(args.RequiredGuid("householdId"), args.RequiredGuid("targetUserId"), ct),
            "admin_delete_household" => await Household(args.RequiredGuid("householdId"), ct),
            "admin_publish_ai_prompt_version" => await PromptVersion(args.RequiredGuid("id"), ct),
            "admin_set_setting" => await Setting(args.RequiredString("key"), args.OptionalString("value") ?? "", setting: true, ct),
            "admin_clear_setting" => await Setting(args.RequiredString("key"), null, setting: false, ct),
            "admin_delete_template" => await Template(args.RequiredGuid("id"), ct),
            "admin_update_price_alias" => await PriceAlias(args, ct),
            "admin_resolve_price_report" => await PriceReport(args, ct),
            "admin_delete_price_source" => await PriceSource(args.RequiredGuid("id"), ct),
            "admin_set_feature_flag" => await FlagDefault(args.RequiredString("flag"), args.OptionalBool("enabled") ?? false, ct),
            "admin_set_feature_flag_override" => await FlagOverride(args.RequiredString("flag"), args.RequiredGuid("householdId"), args.OptionalBool("enabled"), ct),
            "admin_clear_feature_flag_override" => await FlagOverride(args.RequiredString("flag"), args.RequiredGuid("householdId"), null, ct),
            "admin_set_usage_limit" or "admin_clear_usage_limit" => await UsageLimit(args, clearing: request.Action == "admin_clear_usage_limit", ct),
            "admin_activate_notification_campaign" or "admin_resume_campaign" => await CampaignReach(args.RequiredGuid("id"), args.OptionalInt("audienceCount"), ct),
            "admin_cancel_campaign" => await Campaign(args.RequiredGuid("id"), ct),
            _ => throw new DomainValidationException($"{request.Action} has no preview."),
        };
        facts["action"] = request.Action;
        return facts;
    }

    private async Task<JsonObject> Achievement(Guid id, CancellationToken ct)
    {
        var achievement = await _context.Achievements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new EntityNotFoundException("Achievement", id);
        return new JsonObject
        {
            ["deletes"] = achievement.Name,
            ["category"] = achievement.Category,
            ["earnedBy"] = await _context.UserAchievements.CountAsync(u => u.AchievementId == id, ct),
        };
    }

    private async Task<JsonObject> ContentReport(ToolArgs args, CancellationToken ct)
    {
        var id = args.RequiredGuid("id");
        var report = await _context.ContentReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new EntityNotFoundException("Content report", id);
        return new JsonObject
        {
            ["report"] = report.Reason,
            ["content"] = $"{report.TargetType} {report.TargetId}",
            ["status"] = report.Status,
            ["resolution"] = args.RequiredString("action"),
        };
    }

    private async Task<JsonObject> Exercise(Guid id, CancellationToken ct)
    {
        var exercise = await _context.Exercises.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new { e.Name, e.IsCustom, e.IsApproved, Author = e.CreatedByUser != null ? e.CreatedByUser.Email : null })
            .FirstOrDefaultAsync(ct) ?? throw new EntityNotFoundException("Exercise", id);
        return new JsonObject
        {
            ["promotes"] = exercise.Name,
            ["author"] = exercise.Author,
            ["alreadyShared"] = !exercise.IsCustom || exercise.IsApproved,
            ["usedInWorkouts"] = await _context.WorkoutExercises.CountAsync(w => w.ExerciseId == id, ct),
        };
    }

    private async Task<JsonObject> UserChange(ToolArgs args, CancellationToken ct)
    {
        var id = args.RequiredGuid("userId");
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new EntityNotFoundException("User", id);
        var facts = new JsonObject
        {
            ["user"] = user.Email,
            ["role"] = user.Role,
            ["banned"] = user.Banned,
            ["emailVerified"] = user.EmailVerified,
            ["activeSessions"] = await _context.UserSessions.CountAsync(s => s.UserId == id && s.ExpiresAt > DateTime.UtcNow, ct),
        };
        var changes = new JsonObject();
        if (args.OptionalString("role") is { } role) changes["role"] = role;
        if (args.OptionalBool("banned") is { } banned) changes["banned"] = banned;
        if (args.OptionalString("banReason") is { } reason) changes["banReason"] = reason;
        if (args.OptionalString("banExpires") is { } expires) changes["banExpires"] = expires;
        if (args.OptionalBool("emailVerified") is { } verified) changes["emailVerified"] = verified;
        facts["changes"] = changes;
        return facts;
    }

    private async Task<JsonObject> Relationship(ToolArgs args, CancellationToken ct)
    {
        var id = args.RequiredGuid("id");
        var relationship = await _context.TrainerClientRelationships.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { Trainer = r.Trainer.Email, Client = r.Client.Email, r.Status })
            .FirstOrDefaultAsync(ct) ?? throw new EntityNotFoundException("Relationship", id);
        return new JsonObject
        {
            ["ends"] = $"{relationship.Trainer} coaching {relationship.Client}",
            ["status"] = relationship.Status,
            ["reason"] = args.OptionalString("reason"),
        };
    }

    private async Task<JsonObject> Job(Guid id, CancellationToken ct)
    {
        var job = await _context.OutboxJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct)
            ?? throw new EntityNotFoundException("Job", id);
        return new JsonObject { ["deletes"] = job.Type, ["status"] = job.Status.ToString(), ["attempts"] = job.Attempts };
    }

    private async Task<JsonObject> Announcement(ToolArgs args, CancellationToken ct)
    {
        var audience = await _sender.Send(new GetAnnouncementAudienceQuery(args.RequiredString("audience")), ct);
        return new JsonObject
        {
            ["sends"] = args.RequiredString("subject"),
            ["audience"] = audience.Label,
            ["recipients"] = audience.RecipientCount,
        };
    }

    private async Task<JsonObject> ContactReply(ToolArgs args, CancellationToken ct)
    {
        var id = args.RequiredGuid("id");
        var contact = await _context.ContactSubmissions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new EntityNotFoundException("Contact message", id);
        return new JsonObject
        {
            ["emails"] = contact.Email,
            ["about"] = contact.Subject,
            ["status"] = contact.Status.ToString(),
            ["alreadyReplied"] = contact.ReplyEmailMessageId is not null,
        };
    }

    private async Task<JsonObject> SupportNote(Guid id, CancellationToken ct)
    {
        var note = await _context.SupportNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new EntityNotFoundException("Support note", id);
        return new JsonObject
        {
            ["deletes"] = note.Body.Length > 120 ? note.Body[..120] + "..." : note.Body,
            ["on"] = $"{note.SubjectType} {note.SubjectId}",
        };
    }

    private async Task<JsonObject> HouseholdMember(Guid householdId, Guid userId, CancellationToken ct)
    {
        var household = await _sender.Send(new GetAdminHouseholdQuery(householdId), ct);
        var member = household.Members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new EntityNotFoundException("Household member", userId);
        return new JsonObject
        {
            ["household"] = household.Name,
            ["removes"] = member.Email,
            ["role"] = member.Role,
            ["isOwner"] = member.IsOwner,
            ["membersLeft"] = household.Members.Count - 1,
            ["theirSharedRecipes"] = await _context.Recipes.CountAsync(r => r.HouseholdId == householdId && r.UserId == userId, ct),
        };
    }

    private async Task<JsonObject> Household(Guid id, CancellationToken ct)
    {
        var household = await _sender.Send(new GetAdminHouseholdQuery(id), ct);
        return new JsonObject
        {
            ["deletes"] = household.Name,
            ["members"] = new JsonArray(household.Members.OrderBy(m => m.Email).Select(m => (JsonNode)JsonValue.Create(m.Email)!).ToArray()),
            ["mealPlans"] = household.MealPlanCount,
            ["shoppingLists"] = household.ShoppingListCount,
            ["recipes"] = await _context.Recipes.CountAsync(r => r.HouseholdId == id, ct),
            ["pendingInvitations"] = household.PendingInvitations.Count,
        };
    }

    private async Task<JsonObject> PromptVersion(Guid id, CancellationToken ct)
    {
        var version = await _context.AiPromptVersions.AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new { v.PromptId, Key = v.Prompt!.Key, v.Version, v.Status })
            .FirstOrDefaultAsync(ct) ?? throw new EntityNotFoundException("Prompt version", id);
        var live = await _context.AiPromptVersions.AsNoTracking()
            .Where(v => v.PromptId == version.PromptId && v.Status == AiPromptStatus.Published)
            .Select(v => (int?)v.Version)
            .FirstOrDefaultAsync(ct);
        return new JsonObject
        {
            ["prompt"] = version.Key,
            ["publishes"] = version.Version,
            ["status"] = version.Status.ToString(),
            ["replaces"] = live,
        };
    }

    private async Task<JsonObject> Setting(string key, string? value, bool setting, CancellationToken ct)
    {
        var all = await _sender.Send(new GetRuntimeSettingsQuery(), ct);
        var current = all.Groups.SelectMany(g => g.Settings).FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new EntityNotFoundException($"There is no runtime setting called '{key}'.");

        // A secret would be held in the pending action until confirmed; it is set in the console instead.
        if (setting && current.IsSecret)
            throw new DomainValidationException($"{current.Key} is a secret. Set it in the admin console, so it never passes through a connected app.");

        var facts = new JsonObject
        {
            ["setting"] = current.Key,
            ["label"] = current.Label,
            ["source"] = current.Source,
            ["current"] = current.IsSecret ? (current.HasValue ? "(secret, set)" : "(secret, not set)") : current.Value,
        };
        if (setting) facts["new"] = value;
        return facts;
    }

    private async Task<JsonObject> Template(Guid id, CancellationToken ct)
    {
        var template = await _context.MealPlans.AsNoTracking()
            .Where(p => p.Id == id && p.Kind == PlanKind.Template)
            .Select(p => new { p.Name, p.UserId, Entries = p.Entries.Count })
            .FirstOrDefaultAsync(ct) ?? throw new EntityNotFoundException("Template", id);
        return new JsonObject { ["deletes"] = template.Name, ["global"] = template.UserId is null, ["entries"] = template.Entries };
    }

    private async Task<JsonObject> PriceAlias(ToolArgs args, CancellationToken ct)
    {
        var id = args.RequiredGuid("id");
        var alias = await _context.PriceSourceAliases.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.ProductName, Source = a.Source.Name, Food = a.Food != null ? a.Food.Name : null, a.Ignored })
            .FirstOrDefaultAsync(ct) ?? throw new EntityNotFoundException("Price alias", id);
        string? newFood = null;
        if (args.OptionalGuid("foodId") is { } foodId)
        {
            newFood = await _context.Foods.AsNoTracking().Where(f => f.Id == foodId).Select(f => f.Name).FirstOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException("Food", foodId);
        }

        return new JsonObject
        {
            ["product"] = alias.ProductName,
            ["source"] = alias.Source,
            ["food"] = alias.Food,
            ["newFood"] = newFood,
            ["ignore"] = args.OptionalBool("ignored") ?? false,
        };
    }

    private async Task<JsonObject> PriceReport(ToolArgs args, CancellationToken ct)
    {
        var id = args.RequiredGuid("id");
        var report = await _context.PriceReports.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { Food = r.Food.Name, r.RegionCode, r.SuggestedPrice, r.Quantity, r.Unit, r.Status })
            .FirstOrDefaultAsync(ct) ?? throw new EntityNotFoundException("Price report", id);
        return new JsonObject
        {
            ["food"] = report.Food,
            ["region"] = report.RegionCode,
            ["suggested"] = $"{report.SuggestedPrice} for {report.Quantity} {report.Unit}",
            ["status"] = report.Status.ToString(),
            ["accept"] = args.OptionalBool("accept") ?? false,
        };
    }

    private async Task<JsonObject> PriceSource(Guid id, CancellationToken ct)
    {
        var source = await _context.PriceSources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new EntityNotFoundException("Price source", id);
        return new JsonObject
        {
            ["deletes"] = source.Name,
            ["region"] = source.RegionCode,
            ["aliases"] = await _context.PriceSourceAliases.CountAsync(a => a.SourceId == id, ct),
            ["prices"] = await _context.FoodPrices.CountAsync(p => p.SourceId == id, ct),
        };
    }

    private async Task<JsonObject> FlagDefault(string key, bool enabled, CancellationToken ct)
    {
        var flags = await _sender.Send(new ListFeatureFlagsQuery(), ct);
        var flag = flags.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new EntityNotFoundException($"There is no feature flag called '{key}'.");
        return new JsonObject
        {
            ["flag"] = flag.Key,
            ["default"] = flag.DefaultEnabled,
            ["newDefault"] = enabled,
            ["householdsWithOverride"] = flag.OverrideCount,
        };
    }

    private async Task<JsonObject> FlagOverride(string key, Guid householdId, bool? enabled, CancellationToken ct)
    {
        var household = await _context.Households.AsNoTracking().Where(h => h.Id == householdId).Select(h => h.Name).FirstOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException("Household", householdId);
        var overrides = await _sender.Send(new GetFeatureFlagOverridesQuery(key), ct);
        var current = overrides.FirstOrDefault(o => o.HouseholdId == householdId);
        var facts = new JsonObject
        {
            ["flag"] = key,
            ["household"] = household,
            ["override"] = current?.Enabled,
        };
        if (enabled is not null) facts["newOverride"] = enabled;
        return facts;
    }

    private async Task<JsonObject> UsageLimit(ToolArgs args, bool clearing, CancellationToken ct)
    {
        var subject = await _sender.Send(new GetUsageLimitSubjectQuery(args.RequiredString("scope"), args.RequiredGuid("subjectId")), ct);
        var line = args.OptionalString("line");
        var current = subject.Overrides.FirstOrDefault(o => string.Equals(o.Line, line, StringComparison.OrdinalIgnoreCase));
        var facts = new JsonObject
        {
            ["subject"] = $"{subject.Scope} {subject.Name}",
            ["line"] = line ?? "every line",
            ["override"] = current is null ? null : JsonSerializer.SerializeToNode(new
            {
                current.DailyRequests,
                current.DailyTokens,
                current.DailyCostMicros,
                current.MonthlyCostMicros,
                current.Note,
            }, ToolArgs.Json),
        };
        if (!clearing)
        {
            facts["new"] = JsonSerializer.SerializeToNode(new
            {
                DailyRequests = args.OptionalInt("dailyRequests"),
                DailyTokens = args.OptionalDecimal("dailyTokens"),
                DailyCostMicros = args.OptionalDecimal("dailyCostMicros"),
                MonthlyCostMicros = args.OptionalDecimal("monthlyCostMicros"),
                Note = args.OptionalString("note"),
            }, ToolArgs.Json);
        }
        else if (current is null)
        {
            throw new EntityNotFoundException($"{subject.Name} has no override for {line ?? "every line"}.");
        }

        return facts;
    }

    private async Task<JsonObject> CampaignReach(Guid id, int? audienceCount, CancellationToken ct)
    {
        var campaign = await _sender.Send(new GetNotificationCampaignQuery(id), ct);
        var reach = await _sender.Send(new PreviewCampaignAudienceQuery(new CampaignAudienceRequest(campaign.Purpose, campaign.Audience, campaign.Channels)), ct);
        var facts = await Campaign(campaign);
        facts["audience"] = reach.Label;
        facts["reaches"] = reach.Reachable;
        facts["byChannel"] = new JsonObject(reach.Channels.OrderBy(c => c.Channel).Select(c => KeyValuePair.Create(c.Channel, (JsonNode?)c.Reachable)));
        if (audienceCount is not null) facts["confirmedCount"] = audienceCount;
        return facts;
    }

    private async Task<JsonObject> Campaign(Guid id, CancellationToken ct) =>
        await Campaign(await _sender.Send(new GetNotificationCampaignQuery(id), ct));

    private static Task<JsonObject> Campaign(NotificationCampaignDto campaign) => Task.FromResult(new JsonObject
    {
        ["campaign"] = campaign.Name,
        ["purpose"] = campaign.Purpose,
        ["status"] = campaign.Status,
        ["when"] = campaign.Summary,
        ["channels"] = string.Join(", ", campaign.Channels),
    });
}
