import Link from "next/link";
import ExportUsageButton from "@/components/admin/platform/ExportUsageButton";
import { DataTable, type Column } from "@/components/ui/data-table";
import {
  USAGE_GROUPS,
  type PlatformUsageRow,
  type UsageFilters,
} from "@/lib/api/admin-platform";
import { getPlatformUsageServer } from "@/lib/api/admin-platform.server";
import { Select } from "@/components/ui/select";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Usage | Mizan admin",
  description: "AI requests, tokens, cost and refusals across the platform",
};

const GROUP_LABEL: Record<string, string> = {
  household: "Household",
  user: "User",
  plan: "Plan",
  feature: "Feature",
};

const DATE = /^\d{4}-\d{2}-\d{2}$/;

const columns = (groupBy: string): Column<PlatformUsageRow>[] => [
  {
    id: "label",
    header: GROUP_LABEL[groupBy] ?? "Group",
    cell: (row) => <span className="font-medium break-words">{row.label}</span>,
  },
  {
    id: "requests",
    header: "Requests",
    align: "right",
    cell: (row) => row.requests.toLocaleString(),
  },
  {
    id: "tokens",
    header: "Tokens",
    align: "right",
    cell: (row) => row.tokens.toLocaleString(),
  },
  {
    id: "cost",
    header: "Cost",
    align: "right",
    cell: (row) =>
      row.cost.toLocaleString(undefined, { maximumFractionDigits: 4 }),
  },
  {
    id: "refusals",
    header: "Refused",
    align: "right",
    cell: (row) => row.refusals.toLocaleString(),
  },
];

/**
 * /admin/usage: the AI usage ledger for a date range, grouped by household,
 * user, plan or feature, with the same table as CSV (docs/ADMIN.md#usage).
 * The filters are a plain GET form, so the URL is the view.
 */
export default async function AdminUsagePage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const params = await searchParams;
  const filters: UsageFilters = {
    groupBy:
      USAGE_GROUPS.find((value) => value === params.groupBy) ?? "household",
    from: params.from && DATE.test(params.from) ? params.from : undefined,
    to: params.to && DATE.test(params.to) ? params.to : undefined,
    householdId: params.householdId || undefined,
    userId: params.userId || undefined,
  };
  const usage = await getPlatformUsageServer(filters);
  const narrowed = filters.householdId || filters.userId;

  return (
    <div className="log-page space-y-8" data-testid="admin-usage-page">
      <header className="log-page-header block!">
        <p className="eyebrow">Administration</p>
        <h1 className="mt-1">Usage</h1>
        <p className="text-muted-foreground mt-2">
          AI requests, tokens and cost from {usage.from} to {usage.to} (UTC).
          Refused calls cost nothing and are counted apart.
        </p>
      </header>

      <form
        method="get"
        action="/admin/usage"
        aria-label="Usage filters"
        className="flex flex-wrap items-end gap-3"
      >
        <div>
          <label htmlFor="usage-from" className="label">
            From
          </label>
          <input
            id="usage-from"
            name="from"
            type="date"
            className="input"
            defaultValue={usage.from}
          />
        </div>
        <div>
          <label htmlFor="usage-to" className="label">
            To
          </label>
          <input
            id="usage-to"
            name="to"
            type="date"
            className="input"
            defaultValue={usage.to}
          />
        </div>
        <div>
          <label htmlFor="usage-group" className="label">
            Group by
          </label>
          <Select
            id="usage-group"
            name="groupBy"
            defaultValue={usage.groupBy}
            options={USAGE_GROUPS.map((group) => ({
              value: group,
              label: GROUP_LABEL[group],
            }))}
          />
        </div>
        {filters.householdId && (
          <input type="hidden" name="householdId" value={filters.householdId} />
        )}
        {filters.userId && (
          <input type="hidden" name="userId" value={filters.userId} />
        )}
        <button type="submit" className="btn-primary btn-sm min-h-11">
          Show
        </button>
        <ExportUsageButton
          filters={{ ...filters, from: usage.from, to: usage.to }}
        />
      </form>

      {narrowed && (
        <p className="text-sm">
          Showing one {filters.householdId ? "household" : "user"} only.{" "}
          <Link
            href={`/admin/usage?groupBy=${usage.groupBy}&from=${usage.from}&to=${usage.to}`}
            className="text-link"
          >
            Show everyone
          </Link>
        </p>
      )}

      <section aria-label="Usage totals" className="facts-strip">
        <div>
          <p className="text-sm text-muted-foreground">Requests</p>
          <p className="tabular-nums nutrition-value">
            {usage.total.requests.toLocaleString()}
          </p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Tokens</p>
          <p className="tabular-nums nutrition-value">
            {usage.total.tokens.toLocaleString()}
          </p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Refused</p>
          <p className="tabular-nums nutrition-value">
            {usage.total.refusals.toLocaleString()}
          </p>
        </div>
      </section>

      <DataTable
        columns={columns(usage.groupBy)}
        rows={usage.rows}
        rowKey={(row) => row.key ?? "none"}
        pathname="/admin/usage"
        searchParams={params}
        caption={`Total cost ${usage.total.cost.toLocaleString(undefined, { maximumFractionDigits: 4 })}, in the provider's currency.`}
        empty={{ title: "No AI usage in this range." }}
      />
    </div>
  );
}
