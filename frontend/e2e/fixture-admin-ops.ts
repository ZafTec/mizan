import type { components } from "../types/api.generated";

/**
 * The admin operations half of the UI fixture: the overview and its health
 * grid, the mail log and announcements, the contact inbox, household search
 * and detail, suspensions and support notes, and the public contact form.
 * Typed from the generated OpenAPI schema. State is per session cookie and
 * every write lands in `writes[]`, read back from GET /__fixture/admin-ops.
 *
 * fixture-api.ts asks `isAdminOps` first and hands the request over; this
 * module answers everything it claims, so nothing it owns is ever unhandled.
 */
type Schemas = components["schemas"];
type Email = Schemas["EmailMessageDto"];
type Contact = Schemas["ContactSubmissionDto"];
type Note = Schemas["SupportNoteDto"];
type HouseholdSummary = Schemas["AdminHouseholdSummary"];
type HouseholdDetail = Schemas["AdminHouseholdDetailDto"];
type AdminUser = Schemas["AdminUserDto"];
type Check = Schemas["SystemHealthCheckDto"];
type Json = (data: unknown, status?: number, headers?: HeadersInit) => Response;

export const ADMIN_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
export const LIDYA_ID = "dddddddd-dddd-4ddd-8ddd-000000000001";
export const SAM_ID = "dddddddd-dddd-4ddd-8ddd-000000000002";
export const BLUE_DOOR_ID = "eeeeeeee-eeee-4eee-8eee-000000000001";
export const GREEN_GATE_ID = "eeeeeeee-eeee-4eee-8eee-000000000002";
export const FAILED_MAIL_ID = "ffffffff-ffff-4fff-8fff-000000000002";
export const NEW_CONTACT_ID = "cccccccc-0000-4000-8000-000000000001";

const at = (daysAgo: number, time = "09:00:00") => {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() - daysAgo);
  return `${date.toISOString().slice(0, 10)}T${time}Z`;
};

type OpsState = {
  users: AdminUser[];
  mail: Email[];
  contact: Contact[];
  notes: Note[];
  announcements: string[];
  writes: { method: string; path: string; body: unknown }[];
};

function initial(): OpsState {
  const person = (
    id: string,
    name: string,
    email: string,
    extra: Partial<AdminUser> = {},
  ): AdminUser => ({
    id,
    name,
    email,
    image: null,
    role: "user",
    emailVerified: true,
    banned: false,
    banReason: null,
    banExpires: null,
    createdAt: at(60),
    updatedAt: at(2),
    ...extra,
  });
  const email = (id: string, values: Partial<Email>): Email => ({
    id,
    toUserId: null,
    toAddress: "someone@example.test",
    subject: "",
    kind: "auth.verify",
    status: "Sent",
    attempts: 1,
    lastError: null,
    outboxJobId: id,
    createdAt: at(1),
    sentAt: at(1, "09:00:05"),
    createdByUserId: null,
    createdByName: null,
    canRetry: false,
    ...values,
  });
  return {
    users: [
      person(LIDYA_ID, "Lidya Bekele", "lidya.blue@example.test"),
      person(SAM_ID, "Sam Rivera", "sam@example.test"),
    ],
    mail: [
      email("ffffffff-ffff-4fff-8fff-000000000001", {
        toAddress: "lidya.blue@example.test",
        toUserId: LIDYA_ID,
        subject: "Confirm your email",
        createdAt: at(3),
      }),
      email(FAILED_MAIL_ID, {
        toAddress: "sam@example.test",
        toUserId: SAM_ID,
        subject: "Reset your password",
        kind: "auth.reset",
        status: "Failed",
        attempts: 5,
        lastError: "550 5.1.1 [redacted]: mailbox unavailable",
        sentAt: null,
        canRetry: true,
        createdAt: at(1),
      }),
      email("ffffffff-ffff-4fff-8fff-000000000003", {
        toAddress: "lidya.blue@example.test",
        toUserId: LIDYA_ID,
        subject: "Kitchen closed on Friday",
        kind: "admin.announcement",
        createdByUserId: ADMIN_ID,
        createdByName: "Alex Morgan",
        createdAt: at(0, "08:00:00"),
      }),
    ],
    contact: [
      {
        id: NEW_CONTACT_ID,
        name: "Sam Rivera",
        email: "sam@example.test",
        subject: "Billing question",
        message: "Can I pay for a year at once?",
        userId: SAM_ID,
        status: "New",
        createdAt: at(0, "07:30:00"),
        handledAt: null,
        handledByUserId: null,
        handledByName: null,
        replyEmailMessageId: null,
        replyStatus: null,
      },
      {
        id: "cccccccc-0000-4000-8000-000000000002",
        name: "Pat Lee",
        email: "pat@example.test",
        subject: "Thanks",
        message: "Nice app.",
        userId: null,
        status: "Handled",
        createdAt: at(4),
        handledAt: at(3),
        handledByUserId: ADMIN_ID,
        handledByName: "Alex Morgan",
        replyEmailMessageId: null,
        replyStatus: null,
      },
    ],
    notes: [
      {
        id: "99999999-0000-4000-8000-000000000001",
        subjectType: "Household",
        subjectId: BLUE_DOOR_ID,
        body: "Asked about sharing one shopping list.",
        authorUserId: ADMIN_ID,
        authorName: "Alex Morgan",
        authorEmail: "alex@example.test",
        createdAt: at(5),
        updatedAt: at(5),
      },
    ],
    announcements: [],
    writes: [],
  };
}

const HOUSEHOLDS: (HouseholdDetail & { search: string })[] = [
  {
    id: BLUE_DOOR_ID,
    name: "Blue door",
    createdAt: at(30),
    createdBy: LIDYA_ID,
    ownerUserId: LIDYA_ID,
    ownerName: "Lidya Bekele",
    ownerEmail: "lidya.blue@example.test",
    members: [
      {
        userId: LIDYA_ID,
        name: "Lidya Bekele",
        email: "lidya.blue@example.test",
        role: "owner",
        joinedAt: at(30),
        isOwner: true,
      },
      {
        userId: SAM_ID,
        name: "Sam Rivera",
        email: "sam@example.test",
        role: "member",
        joinedAt: at(12),
        isOwner: false,
      },
    ],
    pendingInvitations: [],
    mealPlanCount: 2,
    shoppingListCount: 1,
    search:
      "blue door lidya bekele lidya.blue@example.test sam rivera sam@example.test",
  },
  {
    id: GREEN_GATE_ID,
    name: "Green gate",
    createdAt: at(20),
    createdBy: SAM_ID,
    ownerUserId: SAM_ID,
    ownerName: "Sam Rivera",
    ownerEmail: "sam@example.test",
    members: [
      {
        userId: SAM_ID,
        name: "Sam Rivera",
        email: "sam@example.test",
        role: "owner",
        joinedAt: at(20),
        isOwner: true,
      },
    ],
    pendingInvitations: [],
    mealPlanCount: 0,
    shoppingListCount: 0,
    search: "green gate sam rivera sam@example.test",
  },
];

const CHECKS: [string, string, string][] = [
  ["database", "ok", "3 ms round trip"],
  ["redis", "ok", "1 ms round trip"],
  ["outbox", "degraded", "0 pending, 1 failed"],
  ["email", "notConfigured", "No SMTP host is set."],
  ["ai", "ok", "1,200 of 4,000 tokens, $0.25 of $1.00 today"],
  ["telegram", "notConfigured", "No bot service address is set."],
  ["push", "notConfigured", "Push is not configured."],
  ["prices", "ok", "Last successful run 3 h ago"],
  ["storage", "ok", "Object storage is configured."],
];

const states = new Map<string, OpsState>();

function tokenOf(request: Request) {
  return request.headers
    .get("cookie")
    ?.match(/(?:^|;\s*)mizan_session=([^;]+)/)?.[1];
}

function stateFor(request: Request) {
  const token = tokenOf(request);
  if (!token) return undefined;
  let state = states.get(token);
  if (!state) {
    state = initial();
    states.set(token, state);
  }
  return state;
}

function param(url: URL, name: string) {
  return (
    [...url.searchParams].find(
      ([key]) => key.toLowerCase() === name.toLowerCase(),
    )?.[1] ?? ""
  );
}

function paged<T>(items: T[], url: URL) {
  const page = Number(param(url, "page") || 1);
  const pageSize = Number(param(url, "pageSize") || 20);
  return {
    items: items.slice((page - 1) * pageSize, page * pageSize),
    totalCount: items.length,
    page,
    pageSize,
    totalPages: Math.max(1, Math.ceil(items.length / pageSize)),
  };
}

const userDetail = /^\/api\/admin\/users\/([^/]+)$/;

/** True for every path this module answers. A user it does not know falls through to fixture-api. */
export function isAdminOps(request: Request, url: URL) {
  const path = url.pathname;
  if (path === "/__fixture/admin-ops") return true;
  if (path === "/api/Contact") return true;
  if (path === "/api/admin/overview" || path === "/api/admin/overview/health")
    return true;
  if (path === "/api/AuditLogs") return true;
  if (path === "/api/admin/users" && request.method === "GET") return true;
  const user = path.match(userDetail);
  if (user)
    return (
      request.method === "PATCH" ||
      initial().users.some((u) => u.id === user[1])
    );
  return [
    "/api/admin/mail",
    "/api/admin/contact",
    "/api/admin/support-notes",
    "/api/Households/admin",
  ].some((prefix) => path === prefix || path.startsWith(`${prefix}/`));
}

export async function handleAdminOps(
  request: Request,
  url: URL,
  json: Json,
): Promise<Response> {
  const path = url.pathname;
  const method = request.method;
  const body =
    method === "GET" || method === "DELETE"
      ? undefined
      : ((await request.json().catch(() => ({}))) as Record<string, unknown>);

  // Public, like the real endpoint: a signed-out visitor may write.
  if (path === "/api/Contact" && method === "POST") {
    const state = stateFor(request);
    state?.writes.push({ method, path, body });
    return json({ accepted: true }, 202);
  }

  const state = stateFor(request);
  if (!state) return json({ error: "Fixture session required" }, 401);
  if (method !== "GET") state.writes.push({ method, path, body });

  if (path === "/__fixture/admin-ops") return json(state);

  if (path === "/api/admin/overview")
    return json({
      totalUsers: 128,
      activeTrainers: 4,
      bannedUsers: state.users.filter((u) => u.banned).length,
      activeSessions: 37,
      recentUsers: state.users,
    });
  if (path === "/api/AuditLogs") return json(paged([], url));
  if (path === "/api/admin/overview/health") {
    const measuredAt = new Date().toISOString();
    const checks: Check[] = CHECKS.map(([name, status, detail]) => ({
      name,
      status,
      detail,
      measuredAt,
    }));
    return json({ checks, measuredAt });
  }

  // ---- Mail -------------------------------------------------------------
  if (path === "/api/admin/mail" && method === "GET") {
    const status = param(url, "status");
    const kind = param(url, "kind");
    const search = param(url, "search").toLowerCase();
    const from = param(url, "from");
    const to = param(url, "to");
    const items = state.mail
      .filter((m) => !status || m.status === status)
      .filter((m) => !kind || m.kind === kind)
      .filter((m) => !search || m.toAddress.toLowerCase().includes(search))
      .filter((m) => !from || m.createdAt.slice(0, 10) >= from)
      .filter((m) => !to || m.createdAt.slice(0, 10) <= to)
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    return json(paged(items, url));
  }
  if (path === "/api/admin/mail/stats")
    return json({
      queued: state.mail.filter((m) => m.status === "Queued").length,
      sent: state.mail.filter((m) => m.status === "Sent").length,
      failed: state.mail.filter((m) => m.status === "Failed").length,
      kinds: [...new Set(state.mail.map((m) => m.kind))].sort(),
    });
  if (path === "/api/admin/mail/announcements/audience") {
    const { recipients: _recipients, ...preview } = resolveAudience(
      state,
      param(url, "audience"),
    );
    void _recipients;
    const typed: Schemas["AnnouncementAudienceDto"] = preview;
    return json(typed);
  }
  if (path === "/api/admin/mail/announcements" && method === "POST") {
    const request = body as {
      subject: string;
      body: string;
      audience: string;
      announcementId: string;
    };
    const resolved = resolveAudience(state, request.audience);
    if (!state.announcements.includes(request.announcementId)) {
      state.announcements.push(request.announcementId);
      for (const recipient of resolved.recipients)
        state.mail.push({
          id: crypto.randomUUID(),
          toUserId: recipient.id,
          toAddress: recipient.email,
          subject: request.subject,
          kind: "admin.announcement",
          status: "Queued",
          attempts: 0,
          lastError: null,
          outboxJobId: crypto.randomUUID(),
          createdAt: new Date().toISOString(),
          sentAt: null,
          createdByUserId: ADMIN_ID,
          createdByName: "Alex Morgan",
          canRetry: false,
        });
    }
    return json({
      announcementId: request.announcementId,
      audience: resolved.audience,
      queued: resolved.recipientCount,
    });
  }
  const mail = path.match(/^\/api\/admin\/mail\/([^/]+)(\/retry)?$/);
  if (mail) {
    const found = state.mail.find((m) => m.id === mail[1]);
    if (!found)
      return json({ errorCode: "not_found", error: "Not found" }, 404);
    if (!mail[2] && method === "GET") return json(found);
    if (mail[2] && method === "POST") {
      if (!found.canRetry)
        return json(
          { errorCode: "conflict", error: "This email was sent." },
          409,
        );
      Object.assign(found, { status: "Queued", canRetry: false });
      return json(found);
    }
  }

  // ---- Contact ----------------------------------------------------------
  if (path === "/api/admin/contact" && method === "GET") {
    const status = param(url, "status");
    const search = param(url, "search").toLowerCase();
    const items = state.contact
      .filter((c) => !status || c.status === status)
      .filter(
        (c) =>
          !search ||
          [c.name, c.email, c.subject, c.message].some((v) =>
            v.toLowerCase().includes(search),
          ),
      );
    return json(paged(items, url));
  }
  const contact = path.match(
    /^\/api\/admin\/contact\/([^/]+)(\/handled|\/reply)?$/,
  );
  if (contact) {
    const found = state.contact.find((c) => c.id === contact[1]);
    if (!found)
      return json({ errorCode: "not_found", error: "Not found" }, 404);
    if (!contact[2] && method === "GET") return json(found);
    const handled = {
      status: "Handled",
      handledAt: found.handledAt ?? new Date().toISOString(),
      handledByUserId: found.handledByUserId ?? ADMIN_ID,
      handledByName: found.handledByName ?? "Alex Morgan",
    };
    if (contact[2] === "/handled") {
      Object.assign(found, handled);
      return json(found);
    }
    if (contact[2] === "/reply") {
      if (found.replyEmailMessageId)
        return json(
          { errorCode: "conflict", error: "This message already has a reply." },
          409,
        );
      const id = crypto.randomUUID();
      state.mail.push({
        id,
        toUserId: found.userId ?? null,
        toAddress: found.email,
        subject: `Re: ${found.subject}`,
        kind: "contact.reply",
        status: "Queued",
        attempts: 0,
        lastError: null,
        outboxJobId: crypto.randomUUID(),
        createdAt: new Date().toISOString(),
        sentAt: null,
        createdByUserId: ADMIN_ID,
        createdByName: "Alex Morgan",
        canRetry: false,
      });
      Object.assign(found, handled, {
        replyEmailMessageId: id,
        replyStatus: "Queued",
      });
      return json(found);
    }
  }

  // ---- Households -------------------------------------------------------
  if (path === "/api/Households/admin/all") {
    const search = param(url, "search").toLowerCase();
    const items: HouseholdSummary[] = HOUSEHOLDS.filter(
      (h) => !search || h.search.includes(search),
    ).map((h) => ({
      id: h.id,
      name: h.name,
      createdBy: h.createdBy,
      createdByName: h.ownerName,
      createdByEmail: h.ownerEmail,
      createdAt: h.createdAt,
      memberCount: h.members.length,
      pendingInviteCount: 0,
    }));
    return json(paged(items, url));
  }
  const household = path.match(/^\/api\/Households\/admin\/([^/]+)$/);
  if (household && method === "GET") {
    const found = HOUSEHOLDS.find((h) => h.id === household[1]);
    if (!found)
      return json({ errorCode: "not_found", error: "Not found" }, 404);
    const { search: _search, ...detail } = found;
    void _search;
    return json(detail);
  }

  // ---- Users ------------------------------------------------------------
  if (path === "/api/admin/users" && method === "GET") {
    const search = param(url, "search").toLowerCase();
    return json(
      paged(
        state.users.filter(
          (u) =>
            !search ||
            u.email.toLowerCase().includes(search) ||
            (u.name ?? "").toLowerCase().includes(search),
        ),
        url,
      ),
    );
  }
  const user = path.match(userDetail);
  if (user) {
    const found = state.users.find((u) => u.id === user[1]);
    if (method === "PATCH") {
      if (found) {
        const patch = body as Partial<AdminUser>;
        Object.assign(found, {
          banned: patch.banned ?? found.banned,
          banReason:
            patch.banned === false
              ? null
              : (patch.banReason ?? found.banReason),
          banExpires:
            patch.banned === false ? null : (patch.banExpires ?? null),
        });
      }
      return json(null, 204);
    }
    if (found)
      return json({ user: found, activeSessionCount: 1, recentSessions: [] });
  }

  // ---- Support notes ----------------------------------------------------
  if (path === "/api/admin/support-notes" && method === "GET")
    return json(
      state.notes
        .filter(
          (n) =>
            n.subjectType === param(url, "subjectType") &&
            n.subjectId === param(url, "subjectId"),
        )
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
    );
  if (path === "/api/admin/support-notes" && method === "POST") {
    const input = body as {
      subjectType: string;
      subjectId: string;
      body: string;
    };
    const now = new Date().toISOString();
    const note: Note = {
      id: crypto.randomUUID(),
      subjectType: input.subjectType,
      subjectId: input.subjectId,
      body: input.body,
      authorUserId: ADMIN_ID,
      authorName: "Alex Morgan",
      authorEmail: "alex@example.test",
      createdAt: now,
      updatedAt: now,
    };
    state.notes.push(note);
    return json(note, 201);
  }
  const note = path.match(/^\/api\/admin\/support-notes\/([^/]+)$/);
  if (note) {
    const found = state.notes.find((n) => n.id === note[1]);
    if (!found)
      return json({ errorCode: "not_found", error: "Not found" }, 404);
    if (method === "PUT") {
      Object.assign(found, {
        body: (body as { body: string }).body,
        updatedAt: new Date().toISOString(),
      });
      return json(found);
    }
    if (method === "DELETE") {
      state.notes = state.notes.filter((n) => n !== found);
      return json(null, 204);
    }
  }

  return json(
    { error: `Admin fixture does not implement ${method} ${path}` },
    501,
  );
}

function resolveAudience(state: OpsState, audience: string) {
  const [kind, id] = audience.split(":");
  const everyone = state.users.filter((u) => !u.banned && u.emailVerified);
  if (kind === "household") {
    const found = HOUSEHOLDS.find((h) => h.id === id);
    const members = found?.members.map((m) => m.userId) ?? [];
    const recipients = everyone.filter((u) => members.includes(u.id));
    return {
      audience,
      label: found?.name ?? "Unknown household",
      recipientCount: recipients.length,
      recipients,
    };
  }
  if (kind === "user") {
    const recipients = everyone.filter((u) => u.id === id);
    return {
      audience,
      label: recipients[0]?.email ?? "Unknown person",
      recipientCount: recipients.length,
      recipients,
    };
  }
  return {
    audience,
    label:
      kind === "all"
        ? "Everyone"
        : kind === "pro"
          ? "Pro subscribers"
          : "Free accounts",
    recipientCount: everyone.length,
    recipients: everyone,
  };
}
