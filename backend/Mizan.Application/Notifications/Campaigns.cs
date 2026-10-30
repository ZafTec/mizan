using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Application.Mail;
using Mizan.Application.Telegram;
using Mizan.Contracts.Notifications;
using Mizan.Domain.Entities;
using Mizan.Domain.Notifications;
using Mizan.Domain.Streaks;

namespace Mizan.Application.Notifications;

/// <summary>
/// The parts of a campaign request, checked: everything the stored schedule holds. Every surface's
/// request comes through <see cref="Parse"/>, so the rules are the same for the web, MCP and the API.
/// </summary>
internal sealed record CampaignDraft(
    string Name,
    CampaignPurpose Purpose,
    NotificationTemplate Message,
    NotificationChannels Channels,
    NotificationAudience Audience,
    NotificationTrigger Trigger,
    string TimeZoneId)
{
    public static CampaignDraft Parse(SaveNotificationCampaignRequest request, DateTimeOffset now)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0) throw new DomainValidationException("Give the campaign a name.");
        if (name.Length > CampaignRules.MaxNameLength) throw new DomainValidationException($"A name is at most {CampaignRules.MaxNameLength} characters.");

        var purpose = CampaignMessages.Purpose(request.Purpose);
        var zoneId = CampaignMessages.Zone(request.TimeZoneId);
        var zone = StreakClock.Zone(zoneId);
        return new CampaignDraft(
            name,
            purpose,
            CampaignMessages.Template(purpose, request.Message),
            ReminderBook.Channels(request.Channels),
            CampaignMessages.Audience(request.Audience),
            CampaignMessages.Trigger(request.Timing, zone, now),
            zoneId);
    }
}

/// <summary>Turns campaign requests into stored parts and back, and fills each recipient's placeholders.</summary>
public static class CampaignMessages
{
    public static CampaignPurpose Purpose(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "marketing" => CampaignPurpose.Marketing,
        "lifecycle" => CampaignPurpose.Lifecycle,
        _ => throw new DomainValidationException("Purpose is marketing or lifecycle."),
    };

    public static string Name(CampaignPurpose purpose) => purpose == CampaignPurpose.Marketing ? "marketing" : "lifecycle";

    public static string Name(CampaignStatus status) => status.ToString().ToLowerInvariant();

    public static string Zone(string? zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId)) return CampaignAudienceRules.DefaultZone;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zoneId.Trim(), out _)) throw new DomainValidationException($"\"{zoneId}\" is not a time zone.");
        return zoneId.Trim();
    }

    public static NotificationTemplate Template(CampaignPurpose purpose, CampaignContent? content)
    {
        if (content is null) throw new DomainValidationException("A campaign needs a message.");
        var title = content.Title?.Trim() ?? string.Empty;
        if (title.Length == 0) throw new DomainValidationException("Give the message a title.");
        if (title.Length > CampaignRules.MaxTitleLength) throw new DomainValidationException($"A title is at most {CampaignRules.MaxTitleLength} characters.");
        var body = string.IsNullOrWhiteSpace(content.Body) ? null : content.Body.Trim();
        if (body is { Length: > CampaignRules.MaxBodyLength }) throw new DomainValidationException($"The body is at most {CampaignRules.MaxBodyLength} characters.");

        var actions = (content.Actions ?? []).Where(a => !string.IsNullOrWhiteSpace(a.Label) || !string.IsNullOrWhiteSpace(a.Path)).ToList();
        if (actions.Count > NotificationMessage.MaxActions) throw new DomainValidationException($"A message has at most {NotificationMessage.MaxActions} buttons.");
        foreach (var action in actions)
        {
            if (string.IsNullOrWhiteSpace(action.Label) || action.Label.Trim().Length > 40) throw new DomainValidationException("Each button needs a label of at most 40 characters.");
            if (!IsAppPath(action.Path)) throw new DomainValidationException("A button opens an app page, such as /today.");
        }

        var link = string.IsNullOrWhiteSpace(content.LinkPath) ? null : content.LinkPath.Trim();
        if (link is not null && !IsAppPath(link)) throw new DomainValidationException("The link is an app page, such as /today.");
        var image = string.IsNullOrWhiteSpace(content.ImageUrl) ? null : content.ImageUrl.Trim();
        if (image is not null && !(IsAppPath(image) || Uri.TryCreate(image, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            throw new DomainValidationException("The image is an HTTPS address or a site path.");

        var unknown = new[] { title, body }.Concat(actions.Select(a => a.Label)).SelectMany(NotificationPlaceholders.Unknown).Distinct().ToList();
        if (unknown.Count > 0)
            throw new DomainValidationException($"{string.Join(", ", unknown)} is not a placeholder. Use {NotificationPlaceholders.FirstName} or {NotificationPlaceholders.AppLink}.");

        return new NotificationTemplate
        {
            Type = CampaignRules.NotificationType,
            Kind = CampaignRules.KindFor(purpose),
            Title = title,
            Body = body,
            ImageUrl = image,
            LinkUrl = link,
            Actions = actions.Select(a => new NotificationActionItem { Label = a.Label.Trim(), Path = a.Path.Trim() }).ToList(),
        };
    }

    private static bool IsAppPath(string? path) =>
        path is { Length: > 1 } p && p[0] == '/' && p[1] != '/' && p.Length <= 500 && !p.Contains('\\');

    public static NotificationAudience Audience(CampaignAudienceFilter? filter)
    {
        filter ??= new CampaignAudienceFilter();
        var audience = new NotificationAudience
        {
            Kind = NotificationAudienceKind.Segment,
            Plans = (filter.Plans ?? []).Select(p => p.Trim().ToLowerInvariant()).Distinct().ToList(),
            SignedUpFrom = Day(filter.SignedUpFrom, "signedUpFrom"),
            SignedUpBefore = Day(filter.SignedUpTo, "signedUpTo")?.AddDays(1),
            InactiveDays = filter.InactiveDays,
            TelegramLinked = filter.TelegramLinked,
            HasHousehold = filter.HasHousehold,
            TimeZones = (filter.TimeZones ?? []).Select(z => z.Trim()).Where(z => z.Length > 0).Distinct().ToList(),
        };
        if (CampaignAudienceRules.Check(audience) is { } error) throw new DomainValidationException(error);
        return audience;
    }

    private static DateTime? Day(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            throw new DomainValidationException($"Give {field} as yyyy-MM-dd.");
        return day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    public static CampaignAudienceFilter Filter(NotificationAudience audience) => new(
        audience.Plans,
        audience.SignedUpFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        audience.SignedUpBefore?.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        audience.InactiveDays,
        audience.TelegramLinked,
        audience.HasHousehold,
        audience.TimeZones);

    /// <summary>The trigger a timing asks for. <c>now</c> is a once with no instant yet: starting the campaign sets it.</summary>
    public static NotificationTrigger Trigger(CampaignTiming? timing, TimeZoneInfo zone, DateTimeOffset now)
    {
        var send = timing?.Send?.Trim().ToLowerInvariant();
        if (timing is null || string.IsNullOrEmpty(send)) throw new DomainValidationException("Say when it sends: now, once, daily, days, every or event.");
        ReminderTriggerResult result = send switch
        {
            "now" => new(new NotificationTrigger { Kind = NotificationTriggerKind.Once }, null),
            "event" => CampaignTriggerRules.ForEvent(timing.Event?.Trim(), timing.EventDays ?? 0),
            "once" or "daily" or "days" or "every" => ReminderRules.Trigger(
                new ReminderTiming(send, timing.Time, timing.Days, timing.Date, null, timing.EveryHours, timing.EveryDays), zone, now),
            _ => ReminderTriggerResult.Fail("Send is now, once, daily, days, every or event."),
        };
        return result.Trigger ?? throw new DomainValidationException(result.Error!);
    }

    public static CampaignTiming Timing(NotificationTrigger trigger, TimeZoneInfo zone)
    {
        string? Date(DateTime? at) => at is { } value
            ? TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)), zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
        string? Time(DateTime? at) => at is { } value
            ? TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)), zone).ToString("HH:mm", CultureInfo.InvariantCulture)
            : null;
        var local = trigger.LocalTime is { } t ? ReminderRules.FormatTime(t) : null;

        return trigger.Kind switch
        {
            NotificationTriggerKind.Once when trigger.At is null => new CampaignTiming("now"),
            NotificationTriggerKind.Once => new CampaignTiming("once", Date(trigger.At), Time(trigger.At)),
            NotificationTriggerKind.Daily => new CampaignTiming("daily", Time: local),
            NotificationTriggerKind.Weekdays => new CampaignTiming("days", Time: local, Days: ReminderRules.DayNamesOf(trigger)),
            NotificationTriggerKind.Interval when trigger.IntervalMinutes % (24 * 60) == 0 =>
                new CampaignTiming("every", Date(trigger.At), local ?? Time(trigger.At), EveryDays: trigger.IntervalMinutes / (24 * 60)),
            NotificationTriggerKind.Interval => new CampaignTiming("every", Date(trigger.At), Time(trigger.At), EveryHours: trigger.IntervalMinutes / 60),
            _ => new CampaignTiming("event", Event: trigger.Event, EventDays: CampaignTriggerRules.EventDays(trigger)),
        };
    }

    public static string Summary(NotificationTrigger trigger, TimeZoneInfo zone) =>
        trigger is { Kind: NotificationTriggerKind.Once, At: null } ? "As soon as it is started" : CampaignTriggerRules.Describe(trigger, zone);

    public static NotificationMessage Message(NotificationTemplate template) => new(
        template.Kind,
        template.Title,
        template.Body,
        template.ImageUrl,
        (template.Actions ?? []).Select(a => new NotificationAction(a.Label, a.Path)).ToList(),
        []);

    /// <summary>The message with one recipient's first name and the app's address in place of the placeholders.</summary>
    public static NotificationMessage Personalise(NotificationMessage message, string? name, string appLink)
    {
        var first = NotificationPlaceholders.FirstNameOf(name);
        return message with
        {
            Title = NotificationPlaceholders.Render(message.Title, first, appLink)!,
            Body = NotificationPlaceholders.Render(message.Body, first, appLink),
            Actions = (message.Actions ?? []).Select(a => a with { Label = NotificationPlaceholders.Render(a.Label, first, appLink)! }).ToList(),
        };
    }

    public static CampaignContent Content(NotificationTemplate template) => new(
        template.Title,
        template.Body,
        template.ImageUrl,
        template.LinkUrl,
        (template.Actions ?? []).Select(a => new NotificationAction(a.Label, a.Path)).ToList());

    public static IReadOnlyList<string> Channels(NotificationChannels channels) =>
        NotificationKinds.Each.Where(c => channels.HasFlag(c)).Select(NotificationKinds.Name).ToList();
}

/// <summary>
/// A campaign's delivery report (docs/ADMIN.md#notification-campaigns), read from the outbox jobs its
/// deliveries staged, keyed by the campaign, and from the in-app rows it wrote.
/// </summary>
public sealed class CampaignReports
{
    private readonly IMizanDbContext _context;

    public CampaignReports(IMizanDbContext context) => _context = context;

    public async Task<IReadOnlyList<CampaignChannelTally>> ForAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var key = $"schedule:{scheduleId:N}:";
        var deliveries = await _context.OutboxJobs.AsNoTracking()
            .Where(j => j.Type == OutboxJobTypes.NotificationDelivery && j.DedupeKey!.StartsWith(key))
            .GroupBy(j => new
            {
                Channel = j.DedupeKey!.EndsWith(":inApp") ? 0 : j.DedupeKey.EndsWith(":telegram") ? 1 : j.DedupeKey.EndsWith(":push") ? 2 : 3,
                j.Status,
            })
            .Select(g => new { g.Key.Channel, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        string telegram = $"telegram:notification:{key}", push = $"push:{key}", email = $"email:notification:{key}";
        var channelJobs = await _context.OutboxJobs.AsNoTracking()
            .Where(j => j.DedupeKey!.StartsWith(telegram) || j.DedupeKey.StartsWith(push) || j.DedupeKey.StartsWith(email))
            .GroupBy(j => new { j.Type, j.Status })
            .Select(g => new { g.Key.Type, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var rows = await _context.Notifications.AsNoTracking()
            .Where(n => n.ScheduleId == scheduleId)
            .GroupBy(n => n.ReadAt != null)
            .Select(g => new { Read = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        static JobCounts Count(IEnumerable<(OutboxJobStatus Status, int Count)> jobs)
        {
            var list = jobs.ToList();
            return new JobCounts(
                list.Where(j => j.Status is OutboxJobStatus.Pending or OutboxJobStatus.Running or OutboxJobStatus.Failed).Sum(j => j.Count),
                list.Where(j => j.Status == OutboxJobStatus.Succeeded).Sum(j => j.Count),
                list.Where(j => j.Status == OutboxJobStatus.DeadLettered).Sum(j => j.Count));
        }

        JobCounts DeliveriesOf(int channel) => Count(deliveries.Where(d => d.Channel == channel).Select(d => (d.Status, d.Count)));
        JobCounts JobsOf(string type) => Count(channelJobs.Where(j => j.Type == type).Select(j => (j.Status, j.Count)));

        return
        [
            CampaignReport.Tally(NotificationChannels.InApp, DeliveriesOf(0), JobCounts.None, rows.Sum(r => r.Count), rows.Where(r => r.Read).Sum(r => r.Count)),
            CampaignReport.Tally(NotificationChannels.Telegram, DeliveriesOf(1), JobsOf(OutboxJobTypes.TelegramMessage)),
            CampaignReport.Tally(NotificationChannels.Push, DeliveriesOf(2), JobsOf(OutboxJobTypes.Push)),
            CampaignReport.Tally(NotificationChannels.Email, DeliveriesOf(3), JobsOf(OutboxJobTypes.Email)),
        ];
    }

    public async Task<NotificationCampaignDto> DescribeAsync(NotificationSchedule schedule, CancellationToken cancellationToken)
    {
        var campaign = schedule.Campaign!;
        var zone = StreakClock.Zone(campaign.TimeZoneId);
        var report = (await ForAsync(schedule.Id, cancellationToken)).Where(t => schedule.Channels.HasFlag(t.Channel)).ToList();
        var creator = campaign.CreatedByUserId is { } by
            ? await _context.Users.AsNoTracking().Where(u => u.Id == by).Select(u => u.Name ?? u.Email).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new NotificationCampaignDto(
            schedule.Id,
            campaign.Name,
            CampaignMessages.Name(campaign.Purpose),
            schedule.Message.Kind,
            CampaignMessages.Name(campaign.Status),
            CampaignRules.Phase(campaign.Status, schedule.Trigger.Kind, schedule.NextRunAt),
            CampaignMessages.Content(schedule.Message),
            CampaignMessages.Channels(schedule.Channels),
            CampaignMessages.Filter(schedule.Audience),
            CampaignAudienceRules.Describe(schedule.Audience),
            CampaignMessages.Timing(schedule.Trigger, zone),
            CampaignMessages.Summary(schedule.Trigger, zone),
            campaign.TimeZoneId ?? CampaignAudienceRules.DefaultZone,
            schedule.NextRunAt,
            schedule.LastRunAt,
            campaign.ConfirmedAudience,
            schedule.CreatedAt,
            schedule.UpdatedAt,
            campaign.ActivatedAt,
            creator,
            new CampaignReportTotals(report.Sum(r => r.Queued), report.Sum(r => r.Sent), report.Sum(r => r.Failed), report.Sum(r => r.Skipped), report.Sum(r => r.Opened ?? 0)),
            report.Select(r => new CampaignChannelReport(NotificationKinds.Name(r.Channel), r.Queued, r.Sent, r.Failed, r.Skipped, r.Opened)).ToList());
    }
}

internal static class CampaignLookup
{
    public static async Task<NotificationSchedule> FindAsync(IMizanDbContext context, Guid id, CancellationToken cancellationToken) =>
        await context.NotificationSchedules.FirstOrDefaultAsync(s => s.Id == id && s.Campaign != null, cancellationToken)
        ?? throw new EntityNotFoundException("Campaign", id);

    public static CampaignStatus Apply(NotificationCampaign campaign, CampaignAction action)
    {
        var (next, error) = CampaignRules.Apply(campaign.Status, action);
        return next ?? throw new ConflictException(error!);
    }
}

// ---- Queries ---------------------------------------------------------------

/// <summary>Every campaign and lifecycle message, newest first, with its report totals.</summary>
public record ListNotificationCampaignsQuery : IRequest<IReadOnlyList<NotificationCampaignDto>>;

public sealed class ListNotificationCampaignsQueryHandler : IRequestHandler<ListNotificationCampaignsQuery, IReadOnlyList<NotificationCampaignDto>>
{
    private readonly IMizanDbContext _context;
    private readonly CampaignReports _reports;

    public ListNotificationCampaignsQueryHandler(IMizanDbContext context, CampaignReports reports)
    {
        _context = context;
        _reports = reports;
    }

    public async Task<IReadOnlyList<NotificationCampaignDto>> Handle(ListNotificationCampaignsQuery request, CancellationToken cancellationToken)
    {
        var schedules = await _context.NotificationSchedules.AsNoTracking()
            .Where(s => s.Campaign != null)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
        var list = new List<NotificationCampaignDto>(schedules.Count);
        foreach (var schedule in schedules) list.Add(await _reports.DescribeAsync(schedule, cancellationToken));
        return list;
    }
}

public record GetNotificationCampaignQuery(Guid Id) : IRequest<NotificationCampaignDto>;

public sealed class GetNotificationCampaignQueryHandler : IRequestHandler<GetNotificationCampaignQuery, NotificationCampaignDto>
{
    private readonly IMizanDbContext _context;
    private readonly CampaignReports _reports;

    public GetNotificationCampaignQueryHandler(IMizanDbContext context, CampaignReports reports)
    {
        _context = context;
        _reports = reports;
    }

    public async Task<NotificationCampaignDto> Handle(GetNotificationCampaignQuery request, CancellationToken cancellationToken) =>
        await _reports.DescribeAsync(await CampaignLookup.FindAsync(_context, request.Id, cancellationToken), cancellationToken);
}

/// <summary>How many people an audience reaches, before anything is sent.</summary>
public record PreviewCampaignAudienceQuery(CampaignAudienceRequest Request) : IRequest<CampaignAudiencePreviewDto>;

public sealed class PreviewCampaignAudienceQueryHandler : IRequestHandler<PreviewCampaignAudienceQuery, CampaignAudiencePreviewDto>
{
    private readonly CampaignAudiences _audiences;

    public PreviewCampaignAudienceQueryHandler(CampaignAudiences audiences) => _audiences = audiences;

    public async Task<CampaignAudiencePreviewDto> Handle(PreviewCampaignAudienceQuery request, CancellationToken cancellationToken)
    {
        var purpose = CampaignMessages.Purpose(request.Request.Purpose);
        var filters = CampaignMessages.Audience(request.Request.Audience);
        var channels = ReminderBook.Channels(request.Request.Channels);
        var audience = await _audiences.ResolveAsync(filters, CampaignRules.KindFor(purpose), channels, null, cancellationToken);
        return new CampaignAudiencePreviewDto(
            CampaignAudienceRules.Describe(filters),
            audience.Matched,
            audience.Recipients.Count,
            NotificationKinds.Each.Where(c => channels.HasFlag(c))
                .Select(c => new CampaignChannelCount(NotificationKinds.Name(c), audience.Reachable(c)))
                .ToList());
    }
}

/// <summary>The message as each chosen channel would show it, with the administrator's own name in the placeholders.</summary>
public record PreviewCampaignMessageQuery(CampaignPreviewRequest Request) : IRequest<CampaignPreviewDto>;

public sealed class PreviewCampaignMessageQueryHandler : IRequestHandler<PreviewCampaignMessageQuery, CampaignPreviewDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAppUrls _urls;
    private readonly INotificationOptOutTokens _optOut;

    public PreviewCampaignMessageQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, IAppUrls urls, INotificationOptOutTokens optOut)
    {
        _context = context;
        _currentUser = currentUser;
        _urls = urls;
        _optOut = optOut;
    }

    public async Task<CampaignPreviewDto> Handle(PreviewCampaignMessageQuery request, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        var purpose = CampaignMessages.Purpose(request.Request.Purpose);
        var channels = ReminderBook.Channels(request.Request.Channels);
        var content = request.Request.Message ?? new CampaignContent(string.Empty);

        var warnings = new List<string>();
        var unknown = new[] { content.Title, content.Body }.Concat((content.Actions ?? []).Select(a => a.Label)).SelectMany(NotificationPlaceholders.Unknown).Distinct().ToList();
        if (unknown.Count > 0) warnings.Add($"{string.Join(", ", unknown)} is not a placeholder. Use {NotificationPlaceholders.FirstName} or {NotificationPlaceholders.AppLink}.");
        if (string.IsNullOrWhiteSpace(content.Title)) warnings.Add("Give the message a title.");
        if (content.Body is { Length: > CampaignRules.MaxBodyLength }) warnings.Add($"The body is at most {CampaignRules.MaxBodyLength} characters.");
        if (channels.HasFlag(NotificationChannels.Telegram) && content.ImageUrl is { Length: > 0 } image && !image.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            warnings.Add("Telegram shows the picture only from an HTTPS address; it sends the text alone otherwise.");

        var admin = await _context.Users.AsNoTracking().Where(u => u.Id == adminId).Select(u => new { u.Name, u.Email }).FirstOrDefaultAsync(cancellationToken);
        var kind = CampaignRules.KindFor(purpose);
        var link = string.IsNullOrWhiteSpace(content.LinkPath) ? null : content.LinkPath.Trim();
        var actions = (content.Actions ?? []).Where(a => !string.IsNullOrWhiteSpace(a.Label)).Take(NotificationMessage.MaxActions).ToList();
        var message = CampaignMessages.Personalise(
            new NotificationMessage(kind, content.Title?.Trim() ?? string.Empty, string.IsNullOrWhiteSpace(content.Body) ? null : content.Body.Trim(), _urls.Asset(content.ImageUrl), actions),
            admin?.Name,
            _urls.Home());
        var marketing = purpose == CampaignPurpose.Marketing;
        var buttons = actions.Count > 0 ? actions.Select(a => a.Label.Trim()).ToList() : link is not null ? ["Open"] : new List<string>();

        var previews = new List<CampaignChannelPreview>();
        foreach (var channel in NotificationKinds.Each.Where(c => channels.HasFlag(c)))
        {
            switch (channel)
            {
                case NotificationChannels.InApp:
                    previews.Add(new("inApp", message.Title, message.Body, message.ImageUrl,
                        message.Actions!.Select(a => a.Label).ToList(), marketing ? TelegramMarketingCallbacksLabel : null));
                    break;
                case NotificationChannels.Telegram:
                    var card = TelegramCards.For(message, link).Card!;
                    previews.Add(new("telegram", card.Title, card.Body, card.ImageUrl,
                        card.Buttons.Select(b => b.Label).Concat((card.Callbacks ?? []).Select(c => c.Label)).ToList(), null));
                    break;
                case NotificationChannels.Push:
                    previews.Add(new("push", message.Title, message.Body, null, [], null));
                    break;
                case NotificationChannels.Email:
                    var footer = MailTemplates.NotificationFooter(marketing ? _urls.MarketingOptOut(_optOut.Create(adminId)) : null);
                    previews.Add(new("email", message.Title, message.Body, null,
                        buttons.Select(b => $"{b}: {_urls.Asset(actions.FirstOrDefault(a => a.Label.Trim() == b)?.Path ?? link) ?? _urls.Home()}").ToList(), footer));
                    break;
            }
        }

        return new CampaignPreviewDto(previews, warnings);
    }

    private const string TelegramMarketingCallbacksLabel = Contracts.Telegram.TelegramMarketingCallbacks.Label;
}

// ---- Commands --------------------------------------------------------------

/// <summary>Saves a new campaign as a draft. Nothing is sent until it is started.</summary>
public record CreateNotificationCampaignCommand(SaveNotificationCampaignRequest Request) : IRequest<NotificationCampaignDto>;

public sealed class CreateNotificationCampaignCommandHandler : IRequestHandler<CreateNotificationCampaignCommand, NotificationCampaignDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly CampaignReports _reports;
    private readonly TimeProvider _time;

    public CreateNotificationCampaignCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, CampaignReports reports, TimeProvider time)
    {
        _context = context;
        _currentUser = currentUser;
        _reports = reports;
        _time = time;
    }

    public async Task<NotificationCampaignDto> Handle(CreateNotificationCampaignCommand request, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var draft = CampaignDraft.Parse(request.Request, now);
        var schedule = new NotificationSchedule
        {
            Id = Guid.CreateVersion7(),
            Owner = NotificationScheduleOwner.Platform,
            Audience = draft.Audience,
            Message = draft.Message,
            Channels = draft.Channels,
            Trigger = draft.Trigger,
            Enabled = false,
            Campaign = new NotificationCampaign
            {
                Name = draft.Name,
                Purpose = draft.Purpose,
                Status = CampaignStatus.Draft,
                TimeZoneId = draft.TimeZoneId,
                CreatedByUserId = _currentUser.UserId,
            },
            CreatedAt = now.UtcDateTime,
            UpdatedAt = now.UtcDateTime,
        };
        _context.NotificationSchedules.Add(schedule);
        await _context.SaveChangesAsync(cancellationToken);
        return await _reports.DescribeAsync(schedule, cancellationToken);
    }
}

/// <summary>Changes a draft or a paused campaign. An active one is paused first.</summary>
public record UpdateNotificationCampaignCommand(Guid Id, SaveNotificationCampaignRequest Request) : IRequest<NotificationCampaignDto>;

public sealed class UpdateNotificationCampaignCommandHandler : IRequestHandler<UpdateNotificationCampaignCommand, NotificationCampaignDto>
{
    private readonly IMizanDbContext _context;
    private readonly CampaignReports _reports;
    private readonly TimeProvider _time;

    public UpdateNotificationCampaignCommandHandler(IMizanDbContext context, CampaignReports reports, TimeProvider time)
    {
        _context = context;
        _reports = reports;
        _time = time;
    }

    public async Task<NotificationCampaignDto> Handle(UpdateNotificationCampaignCommand request, CancellationToken cancellationToken)
    {
        var schedule = await CampaignLookup.FindAsync(_context, request.Id, cancellationToken);
        CampaignLookup.Apply(schedule.Campaign!, CampaignAction.Edit);
        var now = _time.GetUtcNow();
        var draft = CampaignDraft.Parse(request.Request, now);

        schedule.Campaign!.Name = draft.Name;
        schedule.Campaign.Purpose = draft.Purpose;
        schedule.Campaign.TimeZoneId = draft.TimeZoneId;
        schedule.Message = draft.Message;
        schedule.Channels = draft.Channels;
        schedule.Audience = draft.Audience;
        schedule.Trigger = draft.Trigger;
        schedule.UpdatedAt = now.UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);
        return await _reports.DescribeAsync(schedule, cancellationToken);
    }
}

/// <summary>
/// Starts a draft. <see cref="AudienceCount"/> is the count the administrator was shown; when the
/// audience is a different size now the start is refused with 409 and the new count, so nobody sends
/// to more people than they confirmed. A campaign sent now is staged at once.
/// </summary>
public record ActivateNotificationCampaignCommand(Guid Id, int AudienceCount) : IRequest<NotificationCampaignDto>;

public sealed class ActivateNotificationCampaignCommandHandler : IRequestHandler<ActivateNotificationCampaignCommand, NotificationCampaignDto>
{
    private readonly IMizanDbContext _context;
    private readonly CampaignAudiences _audiences;
    private readonly NotificationScheduler _scheduler;
    private readonly CampaignReports _reports;
    private readonly TimeProvider _time;

    public ActivateNotificationCampaignCommandHandler(
        IMizanDbContext context, CampaignAudiences audiences, NotificationScheduler scheduler, CampaignReports reports, TimeProvider time)
    {
        _context = context;
        _audiences = audiences;
        _scheduler = scheduler;
        _reports = reports;
        _time = time;
    }

    public async Task<NotificationCampaignDto> Handle(ActivateNotificationCampaignCommand request, CancellationToken cancellationToken)
    {
        var schedule = await CampaignLookup.FindAsync(_context, request.Id, cancellationToken);
        var campaign = schedule.Campaign!;
        var status = CampaignLookup.Apply(campaign, CampaignAction.Activate);
        var now = _time.GetUtcNow();
        var zone = StreakClock.Zone(campaign.TimeZoneId);

        var audience = await _audiences.ResolveAsync(schedule.Audience, schedule.Message.Kind, schedule.Channels, null, cancellationToken);
        if (audience.Recipients.Count != request.AudienceCount)
            throw new ConflictException($"The audience is {audience.Recipients.Count} people now, not {request.AudienceCount}. Check it and confirm again.");
        var isEvent = schedule.Trigger.Kind == NotificationTriggerKind.EventOffset;
        if (!isEvent && audience.Recipients.Count == 0)
            throw new DomainValidationException("Nobody in that audience can receive it.");

        if (schedule.Trigger is { Kind: NotificationTriggerKind.Once, At: null })
            schedule.Trigger = NotificationTrigger.Once(new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc));
        if (!isEvent)
        {
            var next = NotificationOccurrences.Next(schedule.Trigger, zone, now.AddMinutes(-1));
            schedule.NextRunAt = next?.UtcDateTime ?? throw new DomainValidationException("That time has passed. Choose another.");
        }

        campaign.Status = status;
        campaign.ActivatedAt = now.UtcDateTime;
        campaign.ConfirmedAudience = audience.Recipients.Count;
        schedule.Enabled = true;
        schedule.UpdatedAt = now.UtcDateTime;
        // A campaign sent now goes out without waiting for the next scan.
        await _scheduler.StageDueAsync(schedule, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return await _reports.DescribeAsync(schedule, cancellationToken);
    }
}

/// <summary>Stops sending. Deliveries already queued are dropped when their time comes; resume starts from the next occurrence.</summary>
public record PauseNotificationCampaignCommand(Guid Id) : IRequest<NotificationCampaignDto>;

/// <summary>Starts a paused campaign again from its next occurrence.</summary>
public record ResumeNotificationCampaignCommand(Guid Id) : IRequest<NotificationCampaignDto>;

/// <summary>Ends a campaign for good. Nothing queued goes out, and it cannot be started again.</summary>
public record CancelNotificationCampaignCommand(Guid Id) : IRequest<NotificationCampaignDto>;

public sealed class CampaignStatusCommandHandler :
    IRequestHandler<PauseNotificationCampaignCommand, NotificationCampaignDto>,
    IRequestHandler<ResumeNotificationCampaignCommand, NotificationCampaignDto>,
    IRequestHandler<CancelNotificationCampaignCommand, NotificationCampaignDto>
{
    private readonly IMizanDbContext _context;
    private readonly CampaignReports _reports;
    private readonly TimeProvider _time;

    public CampaignStatusCommandHandler(IMizanDbContext context, CampaignReports reports, TimeProvider time)
    {
        _context = context;
        _reports = reports;
        _time = time;
    }

    public Task<NotificationCampaignDto> Handle(PauseNotificationCampaignCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.Id, CampaignAction.Pause, cancellationToken);

    public Task<NotificationCampaignDto> Handle(ResumeNotificationCampaignCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.Id, CampaignAction.Resume, cancellationToken);

    public Task<NotificationCampaignDto> Handle(CancelNotificationCampaignCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.Id, CampaignAction.Cancel, cancellationToken);

    private async Task<NotificationCampaignDto> ChangeAsync(Guid id, CampaignAction action, CancellationToken cancellationToken)
    {
        var schedule = await CampaignLookup.FindAsync(_context, id, cancellationToken);
        var campaign = schedule.Campaign!;
        var status = CampaignLookup.Apply(campaign, action);
        var now = _time.GetUtcNow();

        campaign.Status = status;
        schedule.Enabled = status == CampaignStatus.Active;
        switch (action)
        {
            case CampaignAction.Pause:
                campaign.PausedAt = now.UtcDateTime;
                break;
            case CampaignAction.Resume:
                campaign.PausedAt = null;
                if (schedule.Trigger.Kind != NotificationTriggerKind.EventOffset)
                    schedule.NextRunAt = NotificationOccurrences.Next(schedule.Trigger, StreakClock.Zone(campaign.TimeZoneId), now)?.UtcDateTime;
                break;
            case CampaignAction.Cancel:
                campaign.CancelledAt = now.UtcDateTime;
                schedule.NextRunAt = null;
                break;
        }

        schedule.UpdatedAt = now.UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);
        return await _reports.DescribeAsync(schedule, cancellationToken);
    }
}

/// <summary>
/// Sends the campaign to the administrator alone, now, on each of its channels, with their own name
/// in the placeholders. Their choices for the kind and their quiet hours are set aside; a channel
/// they cannot receive on is named with the reason. A test is not counted in the report.
/// </summary>
public record SendCampaignTestCommand(Guid Id) : IRequest<CampaignTestResultDto>;

public sealed class SendCampaignTestCommandHandler : IRequestHandler<SendCampaignTestCommand, CampaignTestResultDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationWriter _writer;
    private readonly IEntitlementService _entitlements;
    private readonly IPushSender _push;
    private readonly ITelegramMessenger _bot;
    private readonly IAppUrls _urls;

    public SendCampaignTestCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        INotificationWriter writer,
        IEntitlementService entitlements,
        IPushSender push,
        ITelegramMessenger bot,
        IAppUrls urls)
    {
        _context = context;
        _currentUser = currentUser;
        _writer = writer;
        _entitlements = entitlements;
        _push = push;
        _bot = bot;
        _urls = urls;
    }

    public async Task<CampaignTestResultDto> Handle(SendCampaignTestCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        var schedule = await CampaignLookup.FindAsync(_context, request.Id, cancellationToken);
        var admin = await _context.Users.AsNoTracking()
            .Where(u => u.Id == adminId)
            .Select(u => new
            {
                u.Name,
                u.NotifyByTelegram,
                Emailable = u.EmailVerified && u.Email != "",
                Linked = _context.TelegramLinks.Any(l => l.UserId == u.Id),
                HasDevice = _context.DeviceTokens.Any(d => d.UserId == u.Id),
            })
            .FirstOrDefaultAsync(cancellationToken) ?? throw new ForbiddenAccessException();

        var skipped = new List<CampaignSkippedChannel>();
        var channels = schedule.Channels;
        if (channels.HasFlag(NotificationChannels.Telegram))
        {
            var reason = !await _bot.IsConfiguredAsync(cancellationToken) ? "The Telegram bot is not configured."
                : !admin.Linked ? "Your account has no linked Telegram chat."
                : !admin.NotifyByTelegram ? "Notifications to Telegram are off for your account."
                : !(await _entitlements.GetAsync(adminId, cancellationToken)).IsPro ? "Telegram notifications need Pro."
                : null;
            if (reason is not null) skipped.Add(new("telegram", reason));
        }
        if (channels.HasFlag(NotificationChannels.Push) && !(await _push.IsConfiguredAsync(cancellationToken) && admin.HasDevice))
            skipped.Add(new("push", "No phone is registered for push on your account."));
        if (channels.HasFlag(NotificationChannels.Email) && !admin.Emailable)
            skipped.Add(new("email", "Your email address is not verified."));
        foreach (var skip in skipped)
            channels &= ~NotificationKinds.Each.First(c => NotificationKinds.Name(c) == skip.Channel);

        if (channels != NotificationChannels.None)
        {
            var message = CampaignMessages.Personalise(CampaignMessages.Message(schedule.Message), admin.Name, _urls.Home());
            await _writer.SendAsync(
                new NotificationSend(adminId, schedule.Message.Type, message, schedule.Message.LinkUrl)
                {
                    Channels = channels,
                    DeliveryKey = $"campaign-test:{schedule.Id:N}:{Guid.CreateVersion7():N}",
                    Test = true,
                },
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new CampaignTestResultDto(CampaignMessages.Channels(channels), skipped);
    }
}

/// <summary>
/// Turns news and offers off on every channel for the person a marketing message's link names, signed
/// in or not (docs/ADMIN.md#notification-campaigns). The token is never stored in the audit log.
/// </summary>
public record OptOutOfMarketingCommand(string Token) : IRequest<MarketingOptOutDto>, IRedactedAudit
{
    public object AuditDetails => new { Kind = NotificationKinds.Marketing };
}

public sealed class OptOutOfMarketingCommandHandler : IRequestHandler<OptOutOfMarketingCommand, MarketingOptOutDto>
{
    private readonly IMizanDbContext _context;
    private readonly INotificationOptOutTokens _tokens;
    private readonly TimeProvider _time;

    public OptOutOfMarketingCommandHandler(IMizanDbContext context, INotificationOptOutTokens tokens, TimeProvider time)
    {
        _context = context;
        _tokens = tokens;
        _time = time;
    }

    public async Task<MarketingOptOutDto> Handle(OptOutOfMarketingCommand request, CancellationToken cancellationToken)
    {
        var userId = _tokens.Read(request.Token) is { } id && await _context.Users.AnyAsync(u => u.Id == id, cancellationToken)
            ? id
            : throw new DomainValidationException("This link is not valid. Turn news and offers off in Settings, Notifications.");

        var row = await _context.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId && p.Kind == NotificationKinds.Marketing, cancellationToken);
        if (row is null)
        {
            row = new NotificationPreference { UserId = userId, Kind = NotificationKinds.Marketing };
            _context.NotificationPreferences.Add(row);
        }
        row.InApp = row.Telegram = row.Push = row.Email = false;
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);
        return new MarketingOptOutDto(NotificationKinds.Marketing, NotificationKinds.Info(NotificationKinds.Marketing).Label);
    }
}
