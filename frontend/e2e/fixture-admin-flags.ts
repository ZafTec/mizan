import type { components } from "../types/api.generated";
import { BLUE_DOOR_ID, GREEN_GATE_ID, SAM_ID } from "./fixture-admin-ops";

/**
 * The platform administration half of the UI fixture: feature flags with
 * household overrides, allowance overrides, and platform usage with its CSV
 * export (docs/ADMIN.md). Bodies are typed from the generated OpenAPI
 * document. State is kept per fixture session, keyed by its cookie.
 *
 * A spec turns flags off with `POST /__fixture/admin-flags { off: [...] }`
 * and reads what was written with `GET /__fixture/admin-flags`. With
 * `planning.generate` off, `POST /api/MealPlans/generate` answers the API's
 * 403 before the assistant planning fixture sees it. Households and users are
 * the ones fixture-admin-ops seeds; its handler answers the searches.
 */
type Schemas = components["schemas"];
type FeatureFlag = Schemas["FeatureFlagDto"];
type FlagOverride = Schemas["FeatureFlagOverrideDto"];
type Subject = Schemas["UsageLimitSubjectDto"];
type LimitOverride = Schemas["UsageLimitOverrideDto"];
type UsageRow = Schemas["PlatformUsageRowDto"];
type Json = (data: unknown, status?: number, headers?: HeadersInit) => Response;

export const PLAN_ID = "bbbbbbbb-0000-4000-8000-000000000001";

const CATALOG: [string, string][] = [
  ["planning.generate", "Drafting meal plans with the assistant."],
  ["prices", "Food price lookups and price reports."],
  ["telegram.bot", "The Telegram bot's writes and the Mini App sign-in."],
  [
    "ai.assistant",
    "Assistant chat and food analysis from a photo or a sentence.",
  ],
  [
    "social.feed",
    "The social feed: reading it, posting, reactions and comments.",
  ],
];
const LINES = ["Personal", "Planning", "Onboarding", "TrainerClient", "Eval"];
const TIERS: Record<string, Record<string, [number, number]>> = {
  free: {
    Personal: [5, 20000],
    Planning: [1, 30000],
    Onboarding: [60, 60000],
    TrainerClient: [300, 600000],
    Eval: [400, 400000],
  },
  pro: {
    Personal: [200, 500000],
    Planning: [20, 400000],
    Onboarding: [60, 60000],
    TrainerClient: [300, 600000],
    Eval: [400, 400000],
  },
};
const HOUSEHOLDS = [
  { id: BLUE_DOOR_ID, name: "Blue door", memberCount: 2 },
  { id: GREEN_GATE_ID, name: "Green gate", memberCount: 1 },
];
const USERS = [{ id: SAM_ID, name: "Sam Rivera", email: "sam@example.test" }];

type AdminFlagsState = {
  defaults: Record<string, boolean>;
  overrides: Record<string, FlagOverride[]>;
  limits: LimitOverride[];
  writes: { method: string; path: string; body: unknown }[];
};

const states = new Map<string, AdminFlagsState>();

function stateFor(token: string): AdminFlagsState {
  let state = states.get(token);
  if (!state) {
    state = {
      defaults: Object.fromEntries(CATALOG.map(([key]) => [key, true])),
      overrides: {},
      limits: [],
      writes: [],
    };
    states.set(token, state);
  }
  return state;
}

function flagDto(state: AdminFlagsState, key: string): FeatureFlag {
  const overrides = state.overrides[key] ?? [];
  const enabled = overrides.filter((o) => o.enabled).length;
  return {
    key,
    description: CATALOG.find(([k]) => k === key)![1],
    defaultEnabled: state.defaults[key],
    catalogDefault: true,
    overrideCount: overrides.length,
    enabledOverrides: enabled,
    disabledOverrides: overrides.length - enabled,
    updatedAt: state.defaults[key] ? null : "2026-10-20T08:00:00Z",
  };
}

function subjectName(scope: string, id: string) {
  if (scope === "Plan" && id === PLAN_ID)
    return { name: "Pro monthly", detail: "5.00 USD a month" };
  if (scope === "Household") {
    const found = HOUSEHOLDS.find((h) => h.id === id);
    return (
      found && { name: found.name, detail: `${found.memberCount} members` }
    );
  }
  if (scope === "User") {
    const found = USERS.find((u) => u.id === id);
    return found && { name: found.name, detail: found.email };
  }
  return null;
}

const FIELDS = [
  "dailyRequests",
  "dailyTokens",
  "dailyCostMicros",
  "monthlyCostMicros",
] as const;

function subjectDto(
  state: AdminFlagsState,
  scope: string,
  id: string,
): Subject {
  const named = subjectName(scope, id)!;
  const tier = scope === "Plan" ? "pro" : "free";
  const mine = state.limits.filter(
    (l) => l.scope === scope && l.subjectId === id,
  );
  return {
    scope,
    subjectId: id,
    name: named.name,
    detail: named.detail,
    tier,
    overrides: mine,
    effective: LINES.map((line) => {
      const rows = [
        mine.find((l) => l.line === line),
        mine.find((l) => !l.line),
      ].filter(Boolean) as LimitOverride[];
      const pick = (
        field: (typeof FIELDS)[number],
        fallback: number | null,
      ) => {
        const row = rows.find(
          (r) => r[field] !== null && r[field] !== undefined,
        );
        return row
          ? { value: row[field] as number, source: scope }
          : { value: fallback, source: "Default" };
      };
      const [requests, tokens] = TIERS[tier][line];
      return {
        line,
        dailyRequests: pick("dailyRequests", requests),
        dailyTokens: pick("dailyTokens", tokens),
        dailyCostMicros: pick("dailyCostMicros", null),
        monthlyCostMicros: pick("monthlyCostMicros", null),
      };
    }),
  };
}

const USAGE: Record<string, UsageRow[]> = {
  household: [
    {
      key: BLUE_DOOR_ID,
      label: "Blue door",
      requests: 42,
      tokens: 81000,
      costMicros: 24300,
      cost: 0.0243,
      refusals: 3,
    },
    {
      key: GREEN_GATE_ID,
      label: "Green gate",
      requests: 5,
      tokens: 9000,
      costMicros: 2700,
      cost: 0.0027,
      refusals: 0,
    },
    {
      key: null,
      label: "No household",
      requests: 2,
      tokens: 1200,
      costMicros: 360,
      cost: 0.00036,
      refusals: 1,
    },
  ],
  user: [
    {
      key: SAM_ID,
      label: "Sam Rivera",
      requests: 30,
      tokens: 60000,
      costMicros: 18000,
      cost: 0.018,
      refusals: 2,
    },
  ],
  plan: [
    {
      key: PLAN_ID,
      label: "Pro monthly",
      requests: 40,
      tokens: 80000,
      costMicros: 24000,
      cost: 0.024,
      refusals: 0,
    },
    {
      key: "free",
      label: "Free",
      requests: 9,
      tokens: 11200,
      costMicros: 3360,
      cost: 0.00336,
      refusals: 4,
    },
  ],
  feature: [
    {
      key: "chat",
      label: "chat",
      requests: 35,
      tokens: 50000,
      costMicros: 15000,
      cost: 0.015,
      refusals: 4,
    },
    {
      key: "meal-planning",
      label: "meal-planning",
      requests: 14,
      tokens: 41200,
      costMicros: 12360,
      cost: 0.01236,
      refusals: 0,
    },
  ],
};

function usage(url: URL): Schemas["PlatformUsageDto"] {
  const groupBy = url.searchParams.get("groupBy") ?? "household";
  const to =
    url.searchParams.get("to") ?? new Date().toISOString().slice(0, 10);
  const from = url.searchParams.get("from") ?? `${to.slice(0, 8)}01`;
  const narrowed =
    url.searchParams.get("userId") || url.searchParams.get("householdId");
  const rows = (USAGE[groupBy] ?? USAGE.household).slice(
    0,
    narrowed ? 1 : undefined,
  );
  const sum = (
    field: "requests" | "tokens" | "costMicros" | "cost" | "refusals",
  ) => rows.reduce((total, row) => total + row[field], 0);
  return {
    from,
    to,
    groupBy,
    rows,
    total: {
      key: null,
      label: "Total",
      requests: sum("requests"),
      tokens: sum("tokens"),
      costMicros: sum("costMicros"),
      cost: sum("cost"),
      refusals: sum("refusals"),
    },
  };
}

function csv(data: Schemas["PlatformUsageDto"]) {
  const field = (value: string) =>
    /[",\r\n]/.test(value) ? `"${value.replaceAll('"', '""')}"` : value;
  return (
    [
      "group,key,label,requests,tokens,cost_micros,refusals",
      ...data.rows.map((row) =>
        [
          data.groupBy,
          row.key ?? "",
          field(row.label),
          row.requests,
          row.tokens,
          row.costMicros,
          row.refusals,
        ].join(","),
      ),
    ].join("\r\n") + "\r\n"
  );
}

export async function handleAdminFlags(
  request: Request,
  url: URL,
  token: string,
  role: string,
  json: Json,
): Promise<Response | null> {
  const path = url.pathname;
  const method = request.method;
  const state = stateFor(token);
  const read = async () => (await request.json()) as Record<string, unknown>;
  const wrote = (body: unknown) => state.writes.push({ method, path, body });

  if (path === "/__fixture/admin-flags") {
    if (method === "POST") {
      const options = (await read()) as { off?: string[] };
      for (const key of options.off ?? []) state.defaults[key] = false;
      return json(state);
    }
    return json(state);
  }

  if (
    path === "/api/MealPlans/generate" &&
    method === "POST" &&
    !state.defaults["planning.generate"]
  )
    return json(
      {
        errorCode: "feature_disabled",
        error: "This feature is turned off for your household.",
        feature: "planning.generate",
      },
      403,
    );

  const admin =
    path.startsWith("/api/admin/feature-flags") ||
    path.startsWith("/api/admin/usage");
  if (!admin) return null;
  if (role !== "admin")
    return json(
      { errorCode: "forbidden", error: "You do not have access to this." },
      403,
    );

  // ---- Feature flags ----
  if (path === "/api/admin/feature-flags" && method === "GET")
    return json(CATALOG.map(([key]) => flagDto(state, key)));
  const flag = path.match(
    /^\/api\/admin\/feature-flags\/([^/]+)(?:\/overrides(?:\/([^/]+))?)?$/,
  );
  if (flag) {
    const key = decodeURIComponent(flag[1]);
    if (!(key in state.defaults))
      return json(
        {
          errorCode: "not_found",
          error: `There is no feature flag named '${key}'.`,
        },
        404,
      );
    const overrides = (state.overrides[key] ??= []);
    const isOverrides = path.includes("/overrides");
    if (!isOverrides && method === "PUT") {
      const body = await read();
      wrote(body);
      state.defaults[key] = Boolean(body.enabled);
      return json(flagDto(state, key));
    }
    if (isOverrides && !flag[2] && method === "GET") return json(overrides);
    if (flag[2] && method === "PUT") {
      const body = await read();
      wrote(body);
      const household = HOUSEHOLDS.find((h) => h.id === flag[2])!;
      const row: FlagOverride = {
        householdId: household.id,
        householdName: household.name,
        memberCount: household.memberCount,
        enabled: Boolean(body.enabled),
        updatedAt: new Date().toISOString(),
        updatedByName: "Ada Admin",
      };
      state.overrides[key] = [
        ...overrides.filter((o) => o.householdId !== household.id),
        row,
      ];
      return json(row);
    }
    if (flag[2] && method === "DELETE") {
      wrote({});
      state.overrides[key] = overrides.filter((o) => o.householdId !== flag[2]);
      return json(null, 204);
    }
  }

  // ---- Usage limits ----
  if (path === "/api/admin/usage-limits" && method === "GET") {
    const scope = url.searchParams.get("scope") ?? "Plan";
    const ids =
      scope === "Plan"
        ? [PLAN_ID]
        : [
            ...new Set(
              state.limits
                .filter((l) => l.scope === scope)
                .map((l) => l.subjectId),
            ),
          ];
    return json(ids.map((id) => subjectDto(state, scope, id)));
  }
  const limit = path.match(
    /^\/api\/admin\/usage-limits\/(Plan|Household|User)\/([^/]+)$/,
  );
  if (limit) {
    const [, scope, id] = limit;
    if (!subjectName(scope, id))
      return json({ errorCode: "not_found", error: "No such subject." }, 404);
    if (method === "GET") return json(subjectDto(state, scope, id));
    if (method === "PUT") {
      const body = (await read()) as Schemas["SetUsageLimitRequest"];
      wrote(body);
      const line = body.line ?? null;
      state.limits = [
        ...state.limits.filter(
          (l) =>
            !(
              l.scope === scope &&
              l.subjectId === id &&
              (l.line ?? null) === line
            ),
        ),
        {
          id: crypto.randomUUID(),
          scope,
          subjectId: id,
          line,
          dailyRequests: body.dailyRequests ?? null,
          dailyTokens: body.dailyTokens ?? null,
          dailyCostMicros: body.dailyCostMicros ?? null,
          monthlyCostMicros: body.monthlyCostMicros ?? null,
          note: body.note ?? null,
          updatedAt: new Date().toISOString(),
        },
      ];
      return json(subjectDto(state, scope, id));
    }
    if (method === "DELETE") {
      wrote({});
      const line = url.searchParams.get("line");
      state.limits = state.limits.filter(
        (l) =>
          !(
            l.scope === scope &&
            l.subjectId === id &&
            (l.line ?? null) === line
          ),
      );
      return json(null, 204);
    }
  }

  // ---- Usage ----
  if (path === "/api/admin/usage" && method === "GET") return json(usage(url));
  if (path === "/api/admin/usage/export" && method === "GET") {
    const data = usage(url);
    return new Response(csv(data), {
      status: 200,
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": `attachment; filename="mizan-usage-${data.groupBy}-${data.from}-${data.to}.csv"`,
        "Access-Control-Allow-Origin": request.headers.get("origin") ?? "*",
        "Access-Control-Allow-Credentials": "true",
        "Access-Control-Expose-Headers": "Content-Disposition",
      },
    });
  }
  return null;
}
