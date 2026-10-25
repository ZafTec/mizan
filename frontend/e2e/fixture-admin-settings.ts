import type { components } from "../types/api.generated";

/**
 * The runtime settings half of the UI fixture: GET, PUT and DELETE
 * /api/admin/settings and the two test buttons, typed from the generated
 * OpenAPI schema. A few settings per section stand in for the catalog; the
 * refusal messages are worded as the API words them. Settings live per
 * session (keyed by the session's state object), and every write lands in the
 * session's `writes[]` with its Idempotency-Key.
 */
type Schemas = components["schemas"];
type Setting = Schemas["RuntimeSettingDto"];
type Group = Schemas["RuntimeSettingGroupDto"];
type Json = (data: unknown, status?: number, headers?: HeadersInit) => Response;
type Host = {
  role: string;
  unhandled: string[];
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
};

export const SMTP_TEST_JOB_ID = "e5000000-0000-4000-8000-000000000001";
export const ADMIN_EMAIL = "alex@example.test";

const setting = (
  partial: Partial<Setting> & Pick<Setting, "key" | "group" | "label">,
): Setting => ({
  description: `${partial.label} for the fixture.`,
  type: "string",
  format: "text",
  isSecret: false,
  allowEmpty: false,
  allowedValues: [],
  min: null,
  max: null,
  value: null,
  hasValue: false,
  source: "unset",
  updatedAt: null,
  updatedByUserId: null,
  ...partial,
});

function initialGroups(): Group[] {
  const configured = (value: string) => ({
    value,
    hasValue: true,
    source: "configuration",
  });
  return [
    {
      id: "email",
      title: "Email",
      settings: [
        setting({
          key: "Smtp.Host",
          group: "email",
          label: "SMTP host",
          format: "host",
        }),
        setting({
          key: "Smtp.Port",
          group: "email",
          label: "SMTP port",
          type: "int",
          min: 1,
          max: 65535,
          ...configured("587"),
        }),
        setting({
          key: "Smtp.Security",
          group: "email",
          label: "Security",
          type: "enum",
          allowedValues: ["Auto", "StartTls", "SslOnConnect", "None"],
          ...configured("Auto"),
        }),
        setting({
          key: "Smtp.Password",
          group: "email",
          label: "Password",
          isSecret: true,
        }),
      ],
    },
    {
      id: "ai-provider",
      title: "AI provider",
      settings: [
        setting({
          key: "Ai.BaseUrl",
          group: "ai-provider",
          label: "Endpoint",
          format: "url",
          allowEmpty: true,
          ...configured("https://ai.example.test/v1"),
        }),
        setting({
          key: "Ai.ApiKey",
          group: "ai-provider",
          label: "API key",
          isSecret: true,
          hasValue: true,
          source: "configuration",
        }),
      ],
    },
    {
      id: "ai-limits",
      title: "AI limits",
      settings: [
        setting({
          key: "Ai.Free.DailyRequests",
          group: "ai-limits",
          label: "Free personal requests",
          type: "int",
          min: 0,
          max: 1000000,
          ...configured("5"),
        }),
      ],
    },
    {
      id: "telegram",
      title: "Telegram",
      settings: [
        setting({
          key: "Telegram.BotServiceUrl",
          group: "telegram",
          label: "Bot service URL",
          format: "url",
          allowEmpty: true,
        }),
      ],
    },
    {
      id: "push",
      title: "Push",
      settings: [
        setting({
          key: "Push.FcmProjectId",
          group: "push",
          label: "Firebase project",
          allowEmpty: true,
        }),
      ],
    },
    {
      id: "billing",
      title: "Billing",
      settings: [
        setting({
          key: "Paddle.Environment",
          group: "billing",
          label: "Paddle environment",
          type: "enum",
          allowedValues: ["sandbox", "production"],
          ...configured("sandbox"),
        }),
      ],
    },
  ];
}

/** The API's checks for the types the fixture carries, in the API's words. */
function check(
  item: Setting,
  raw: string,
): { value: string } | { error: string } {
  const text = item.isSecret ? raw : raw.trim();
  if (!text.trim())
    return item.allowEmpty
      ? { value: "" }
      : {
          error: `${item.label} cannot be empty. Clear the setting to use the configuration value.`,
        };
  if (item.type === "int") {
    const number = Number(text);
    const min = item.min ?? 0;
    const max = item.max ?? 1_000_000_000_000;
    if (!/^-?\d+$/.test(text) || number < min || number > max)
      return {
        error: `${item.label} must be a whole number from ${min} to ${max}.`,
      };
    return { value: String(number) };
  }
  if (item.type === "enum") {
    const match = item.allowedValues.find(
      (v) => v.toLowerCase() === text.toLowerCase(),
    );
    return match
      ? { value: match }
      : {
          error: `${item.label} must be one of ${item.allowedValues.join(", ")}.`,
        };
  }
  if (item.format === "host" && (text.includes("://") || text.includes(":")))
    return {
      error: `${item.label} must be a host name or an IP address, without a scheme or port.`,
    };
  return { value: text };
}

const sessions = new WeakMap<Host, Group[]>();

export async function handleAdminSettings(
  request: Request,
  url: URL,
  host: Host,
  json: Json,
): Promise<Response> {
  const path = url.pathname;
  const method = request.method;
  if (host.role !== "admin")
    return json({ errorCode: "forbidden", error: "Administrators only." }, 403);

  let groups = sessions.get(host);
  if (!groups) {
    groups = initialGroups();
    sessions.set(host, groups);
  }
  const find = (key: string) =>
    groups
      .flatMap((g) => g.settings)
      .find((s) => s.key.toLowerCase() === key.toLowerCase());
  const replace = (next: Setting) => {
    for (const group of groups)
      group.settings = group.settings.map((s) =>
        s.key === next.key ? next : s,
      );
    return next;
  };
  const record = (body: Record<string, unknown>) =>
    host.writes.push({
      path,
      body,
      key: request.headers.get("Idempotency-Key") ?? undefined,
    });

  if (path === "/api/admin/settings" && method === "GET")
    return json({ groups });
  if (path === "/api/admin/settings/smtp/test" && method === "POST") {
    record({});
    return json({ jobId: SMTP_TEST_JOB_ID, to: ADMIN_EMAIL });
  }
  if (path === "/api/admin/settings/ai/test" && method === "POST") {
    record({});
    return json({
      reachable: true,
      errorClass: null,
      model: "fixture-model",
      latencyMs: 412,
    });
  }

  const key = decodeURIComponent(path.slice("/api/admin/settings/".length));
  const item = find(key);
  if (item && method === "PUT") {
    const body = (await request.json()) as Schemas["SetRuntimeSettingRequest"];
    record(body);
    const result = check(item, body.value ?? "");
    if ("error" in result)
      return json(
        {
          errorCode: "validation_failed",
          error: result.error,
          errors: [{ propertyName: "Value", errorMessage: result.error }],
        },
        400,
      );
    return json(
      replace({
        ...item,
        value: item.isSecret ? null : result.value,
        hasValue: result.value !== "",
        source: "database",
        updatedAt: new Date().toISOString(),
      }),
    );
  }
  if (item && method === "DELETE") {
    record({});
    const original = initialGroups()
      .flatMap((g) => g.settings)
      .find((s) => s.key === item.key)!;
    return json(replace(original));
  }
  if (!item && (method === "PUT" || method === "DELETE"))
    return json(
      {
        errorCode: "not_found",
        error: `There is no runtime setting named '${key}'.`,
      },
      404,
    );

  host.unhandled.push(`${method} ${path}`);
  return json({ error: `Fixture does not implement ${method} ${path}` }, 501);
}
