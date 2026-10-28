import {
  DataTable,
  Pill,
  TableToolbar,
  readPage,
  withParams,
  type Column,
  type PillTone,
} from "@/components/ui/data-table";
import AdminDrawer, { DrawerField } from "@/components/admin/AdminDrawer";
import { MAIL_STATUSES, type EmailMessage } from "@/lib/api/admin-ops";
import {
  getMailServer,
  getMailStatsServer,
  listMailServer,
} from "@/lib/api/admin-ops.server";
import { mailKindLabel } from "@/lib/admin-ops/labels";
import ComposeAnnouncement from "./ComposeAnnouncement";
import DateRangeFilter from "./DateRangeFilter";
import RetryEmailButton from "./RetryEmailButton";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Mail | Mizan admin",
  description: "Every email the system sent, and announcements",
};

const PAGE_SIZE = 20;

const STATUS_TONE: Record<string, PillTone> = {
  Queued: "info",
  Sent: "good",
  Failed: "bad",
};

function when(value?: string | null) {
  if (!value) return "-";
  return new Date(value).toLocaleString("en-GB", {
    day: "numeric",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
  });
}

/**
 * /admin/mail: the email log (docs/ADMIN.md#mail). Filters are query
 * parameters, a row opens its detail in a side sheet with `?open=`, and
 * Compose sends an announcement after showing how many people it reaches.
 */
export default async function AdminMailPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const params = await searchParams;
  const page = readPage(params);
  const filters = {
    Status: params.status,
    Kind: params.kind,
    Search: params.search,
    From: params.from,
    To: params.to,
  };
  const [list, stats, open] = await Promise.all([
    listMailServer({ ...filters, Page: page, PageSize: PAGE_SIZE }),
    getMailStatsServer(),
    params.open ? getMailServer(params.open) : Promise.resolve(null),
  ]);

  const columns: Column<EmailMessage>[] = [
    {
      id: "subject",
      header: "Email",
      cell: (m) => (
        <span className="block min-w-0">
          <span className="block truncate font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
            {m.subject}
          </span>
          <span className="block truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
            {m.toAddress}
          </span>
        </span>
      ),
    },
    {
      id: "status",
      header: "Status",
      cell: (m) => (
        <Pill tone={STATUS_TONE[m.status] ?? "neutral"}>{m.status}</Pill>
      ),
    },
    {
      id: "kind",
      header: "Kind",
      secondary: true,
      cell: (m) => (
        <span className="whitespace-nowrap text-xs text-charcoal-blue-600 dark:text-charcoal-blue-300">
          {mailKindLabel(m.kind)}
        </span>
      ),
    },
    {
      id: "created",
      header: "Queued (UTC)",
      secondary: true,
      cell: (m) => (
        <span className="whitespace-nowrap text-xs tabular-nums text-charcoal-blue-500 dark:text-charcoal-blue-400">
          {when(m.createdAt)}
        </span>
      ),
    },
  ];

  const closeHref = withParams("/admin/mail", params, { open: null });

  return (
    <div className="space-y-6 lg:space-y-8">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="space-y-2">
          <p className="eyebrow">Administration</p>
          <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
            Mail
          </h1>
          <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
            <span className="tabular-nums">{stats.queued}</span> queued ·{" "}
            <span className="tabular-nums">{stats.sent}</span> sent ·{" "}
            <span
              className={
                stats.failed > 0
                  ? "tabular-nums font-medium text-burnt-peach-700 dark:text-burnt-peach-400"
                  : "tabular-nums"
              }
            >
              {stats.failed}
            </span>{" "}
            failed
          </p>
        </div>
        <ComposeAnnouncement />
      </header>

      <TableToolbar
        searchPlaceholder="Search by recipient…"
        filters={[
          {
            name: "status",
            label: "Any status",
            options: MAIL_STATUSES.map((s) => ({ value: s, label: s })),
          },
          {
            name: "kind",
            label: "Any kind",
            options: stats.kinds.map((k) => ({
              value: k,
              label: mailKindLabel(k),
            })),
          },
        ]}
      >
        <DateRangeFilter />
      </TableToolbar>

      <DataTable
        columns={columns}
        rows={list.items}
        rowKey={(m) => m.id}
        pathname="/admin/mail"
        searchParams={params}
        sort={{ sortBy: null, sortOrder: "desc" }}
        page={{
          page,
          pageSize: PAGE_SIZE,
          totalCount: list.totalCount,
          totalPages: list.totalPages,
        }}
        href={(m) => withParams("/admin/mail", params, { open: m.id })}
        rowClassName={(m) =>
          m.status === "Failed" ? "bg-danger-wash" : undefined
        }
        empty={{
          title: "No email matches",
          description: "Try another filter, or clear them.",
        }}
      />

      {open && (
        <AdminDrawer
          title={open.subject}
          description={`To ${open.toAddress}`}
          closeHref={closeHref}
        >
          <dl>
            <DrawerField label="Status">
              <Pill tone={STATUS_TONE[open.status] ?? "neutral"}>
                {open.status}
              </Pill>
            </DrawerField>
            <DrawerField label="Kind">{mailKindLabel(open.kind)}</DrawerField>
            <DrawerField label="Attempts">{open.attempts}</DrawerField>
            <DrawerField label="Queued">{when(open.createdAt)} UTC</DrawerField>
            <DrawerField label="Sent">
              {open.sentAt ? `${when(open.sentAt)} UTC` : "Not yet"}
            </DrawerField>
            {open.createdByName && (
              <DrawerField label="Written by">{open.createdByName}</DrawerField>
            )}
          </dl>
          {open.lastError && (
            <section className="mt-4" aria-labelledby="mail-error">
              <h3
                id="mail-error"
                className="mb-1 text-sm font-medium text-charcoal-blue-700 dark:text-charcoal-blue-300"
              >
                {open.status === "Failed" ? "Why it failed" : "Last error"}
              </h3>
              <p className="whitespace-pre-wrap break-words border border-burnt-peach-200 bg-burnt-peach-50 p-3 font-mono text-xs text-burnt-peach-800 dark:border-burnt-peach-800 dark:bg-burnt-peach-950/40 dark:text-burnt-peach-200">
                {open.lastError}
              </p>
            </section>
          )}
          <p className="mt-4 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
            The body is not kept: it can hold a sign-in link.
          </p>
          {open.canRetry && (
            <div className="mt-4">
              <RetryEmailButton id={open.id} />
            </div>
          )}
        </AdminDrawer>
      )}
    </div>
  );
}
