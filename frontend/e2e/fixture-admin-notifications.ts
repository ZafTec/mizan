import type { components } from "../types/api.generated";

/**
 * Notification campaigns in the UI fixture (docs/ADMIN.md#notification-campaigns).
 * Bodies are typed from the generated OpenAPI document; the answers are
 * synthetic. Audiences, previews and the confirmed-count check are the API's
 * (NotificationCampaignStoryTests and CampaignRulesTests cover them); this
 * only answers enough for the pages to be driven. State is per fixture
 * session. `GET /__fixture/admin-notifications` returns what was written,
 * with the Idempotency-Key each write carried.
 */
type Schemas = components["schemas"];
type Campaign = Schemas["NotificationCampaignDto"];
type Save = Schemas["SaveNotificationCampaignRequest"];
type Json = (data: unknown, status?: number, headers?: HeadersInit) => Response;

export const WELCOME_ID = "cccccccc-0000-4000-8000-000000000001";

/** The fixture's audience: four people match, three can be reached. */
const MATCHED = 4;
const REACHABLE = 3;

type Write = {
  method: string;
  path: string;
  body: unknown;
  key: string | null;
};
type State = { campaigns: Campaign[]; writes: Write[] };
const states = new Map<string, State>();

const report = (channels: string[]) =>
  channels.map((channel) => ({
    channel,
    queued: 0,
    sent: 0,
    failed: 0,
    skipped: 0,
    opened: channel === "inApp" ? 0 : null,
  }));

function campaignOf(id: string, request: Save, base?: Campaign): Campaign {
  const channels = [...(request.channels ?? ["inApp"])];
  return {
    id,
    name: request.name,
    purpose: request.purpose,
    kind: request.purpose === "marketing" ? "marketing" : "account",
    status: base?.status ?? "draft",
    phase: base?.phase ?? "draft",
    message: request.message,
    channels,
    audience: request.audience ?? {},
    audienceLabel: "Everyone",
    timing: request.timing,
    summary:
      request.timing.send === "event"
        ? "1 day after sign-up"
        : "As soon as it is started",
    timeZoneId: request.timeZoneId ?? "UTC",
    nextRunAt: null,
    lastRunAt: null,
    confirmedAudience: base?.confirmedAudience ?? null,
    createdAt: base?.createdAt ?? "2026-10-28T08:00:00Z",
    updatedAt: "2026-10-29T08:00:00Z",
    activatedAt: base?.activatedAt ?? null,
    createdByName: "Ada Admin",
    totals: { queued: 0, sent: 0, failed: 0, skipped: 0, opened: 0 },
    report: report(channels),
  };
}

function stateFor(token: string): State {
  let state = states.get(token);
  if (!state) {
    const welcome = campaignOf(WELCOME_ID, {
      name: "Welcome",
      purpose: "lifecycle",
      message: { title: "Welcome, {firstName}", body: "Log your first meal." },
      timing: { send: "event", event: "user.signed_up", eventDays: 1 },
      channels: ["inApp", "email"],
    });
    welcome.status = "active";
    welcome.phase = "live";
    welcome.confirmedAudience = 3;
    welcome.activatedAt = "2026-10-28T09:00:00Z";
    welcome.totals = { queued: 1, sent: 12, failed: 1, skipped: 2, opened: 7 };
    welcome.report = [
      {
        channel: "inApp",
        queued: 0,
        sent: 9,
        failed: 0,
        skipped: 1,
        opened: 7,
      },
      {
        channel: "email",
        queued: 1,
        sent: 3,
        failed: 1,
        skipped: 1,
        opened: null,
      },
    ];
    state = { campaigns: [welcome], writes: [] };
    states.set(token, state);
  }
  return state;
}

const BASE = "/api/admin/notification-campaigns";

export async function handleAdminNotifications(
  request: Request,
  url: URL,
  token: string,
  role: string,
  json: Json,
): Promise<Response | null> {
  const path = url.pathname;
  const method = request.method;
  if (path === "/__fixture/admin-notifications") return json(stateFor(token));
  if (path === "/api/Notifications/opt-out" && method === "POST") {
    const { token: code } =
      (await request.json()) as Schemas["MarketingOptOutRequest"];
    stateFor(token).writes.push({
      method,
      path,
      body: { token: code },
      key: request.headers.get("Idempotency-Key"),
    });
    return code === "valid-token"
      ? json({
          kind: "marketing",
          label: "News and offers",
        } satisfies Schemas["MarketingOptOutDto"])
      : json(
          {
            error:
              "This link is not valid. Turn news and offers off in Settings, Notifications.",
          },
          400,
        );
  }
  if (!path.startsWith(BASE)) return null;
  if (role !== "admin") return json({ error: "Forbidden" }, 403);

  const state = stateFor(token);
  const body = method === "GET" ? null : await request.text();
  const parsed = body ? (JSON.parse(body) as unknown) : null;
  if (method !== "GET")
    state.writes.push({
      method,
      path,
      body: parsed,
      key: request.headers.get("Idempotency-Key"),
    });
  const rest = path.slice(BASE.length);

  if (rest === "" && method === "GET") return json(state.campaigns);
  if (rest === "" && method === "POST") {
    const created = campaignOf(
      `cccccccc-0000-4000-8000-00000000000${state.campaigns.length + 1}`,
      parsed as Save,
    );
    state.campaigns.unshift(created);
    return json(created);
  }
  if (rest === "/audience" && method === "POST") {
    const channels = (parsed as Schemas["CampaignAudienceRequest"])
      .channels ?? ["inApp"];
    return json({
      label: "Everyone",
      matched: MATCHED,
      reachable: REACHABLE,
      channels: channels.map((channel) => ({ channel, reachable: REACHABLE })),
    } satisfies Schemas["CampaignAudiencePreviewDto"]);
  }
  if (rest === "/preview" && method === "POST") {
    const request = parsed as Schemas["CampaignPreviewRequest"];
    const fill = (text?: string | null) =>
      text
        ?.replaceAll("{firstName}", "Ada")
        .replaceAll("{appLink}", "https://mizan.test/") ?? null;
    return json({
      channels: (request.channels ?? ["inApp"]).map((channel) => ({
        channel,
        title: fill(request.message.title) ?? "",
        body: fill(request.message.body),
        imageUrl: null,
        buttons: [],
        footer:
          channel === "email" && request.purpose === "marketing"
            ? "Turn them off: https://mizan.test/notifications/opt-out?token=x"
            : null,
      })),
      warnings: request.message.title ? [] : ["Give the message a title."],
    } satisfies Schemas["CampaignPreviewDto"]);
  }

  const match =
    /^\/([0-9a-f-]{36})(?:\/(test|activate|pause|resume|cancel))?$/.exec(rest);
  const campaign = match && state.campaigns.find((c) => c.id === match[1]);
  if (!match || !campaign) return json({ error: "Campaign not found." }, 404);
  const step = match[2];

  if (!step && method === "GET") return json(campaign);
  if (!step && method === "PUT") {
    const updated = campaignOf(campaign.id, parsed as Save, campaign);
    state.campaigns = state.campaigns.map((c) =>
      c.id === campaign.id ? updated : c,
    );
    return json(updated);
  }
  if (step === "test")
    return json({
      queued: ["inApp"],
      skipped: campaign.channels.includes("telegram")
        ? [
            {
              channel: "telegram",
              reason: "Your account has no linked Telegram chat.",
            },
          ]
        : [],
    } satisfies Schemas["CampaignTestResultDto"]);
  if (step === "activate") {
    const count = (parsed as Schemas["ActivateNotificationCampaignRequest"])
      .audienceCount;
    if (count !== REACHABLE)
      return json(
        {
          error: `The audience is ${REACHABLE} people now, not ${count}. Check it and confirm again.`,
        },
        409,
      );
    campaign.status = "active";
    campaign.phase = "sent";
    campaign.confirmedAudience = count;
    campaign.activatedAt = "2026-10-29T09:00:00Z";
    campaign.totals = { ...campaign.totals, queued: count };
    return json(campaign);
  }
  const next: Record<string, [string, string]> = {
    pause: ["paused", "paused"],
    resume: ["active", "sent"],
    cancel: ["cancelled", "cancelled"],
  };
  if (step && next[step]) {
    [campaign.status, campaign.phase] = next[step];
    return json(campaign);
  }
  return null;
}
