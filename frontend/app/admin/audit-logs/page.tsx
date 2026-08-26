import {
	getAuditFacets,
	listAuditLogs,
	type AuditLogRow,
} from "@/data/admin/audit";
import {
	DataTable,
	Pill,
	readPage,
	readSort,
	type Column,
} from "@/components/ui/data-table";
import AuditToolbar from "./AuditToolbar";
import AuditDetails from "./AuditDetails";

export const dynamic = "force-dynamic";

export const metadata = {
	title: "Audit log | Mizan admin",
	description: "Who did what, and when",
};

const PAGE_SIZE = 50;

/** Writes are the interesting ones; reads are noise you scroll past. */
function tone(action: string) {
	const a = action.toLowerCase();
	if (a.includes("delete") || a.includes("ban") || a.includes("revoke")) return "bad" as const;
	if (a.includes("create") || a.includes("publish")) return "good" as const;
	if (a.includes("update") || a.includes("patch")) return "warn" as const;
	return "neutral" as const;
}

const columns: Column<AuditLogRow>[] = [
	{
		id: "timestamp",
		header: "When",
		sortKey: "timestamp",
		cell: (r) => (
			<span className="whitespace-nowrap text-xs tabular-nums text-charcoal-blue-600 dark:text-charcoal-blue-300">
				{new Date(r.timestamp).toLocaleString()}
			</span>
		),
	},
	{
		id: "actor",
		header: "Actor",
		cell: (r) => (
			<span className="truncate text-sm">{r.userEmail ?? "system"}</span>
		),
	},
	{
		id: "action",
		header: "Action",
		sortKey: "action",
		cell: (r) => <Pill tone={tone(r.action)}>{r.action}</Pill>,
	},
	{
		id: "entity",
		header: "Entity",
		sortKey: "entityType",
		secondary: true,
		cell: (r) => (
			<div className="min-w-0">
				<p className="truncate text-sm">{r.entityType}</p>
				{r.entityId && (
					<p className="truncate font-mono text-[11px] text-charcoal-blue-400 dark:text-charcoal-blue-500">
						{r.entityId}
					</p>
				)}
			</div>
		),
	},
	{
		id: "ip",
		header: "IP",
		secondary: true,
		cell: (r) => (
			<span className="font-mono text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{r.ipAddress || "—"}
			</span>
		),
	},
	{
		id: "details",
		header: "",
		align: "right",
		width: "1%",
		cell: (r) => (r.details ? <AuditDetails details={r.details} /> : null),
	},
];

export default async function AdminAuditLogsPage({
	searchParams,
}: {
	searchParams: Promise<Record<string, string | undefined>>;
}) {
	const params = await searchParams;
	const page = readPage(params);
	const sort = readSort(params);

	const filters = {
		page,
		pageSize: PAGE_SIZE,
		action: params.action,
		entityType: params.entityType,
		search: params.search,
		from: params.from,
		to: params.to,
		sortBy: sort.sortBy ?? undefined,
		sortOrder: sort.sortBy ? sort.sortOrder : undefined,
	};

	const [result, facets] = await Promise.all([listAuditLogs(filters), getAuditFacets()]);

	const exportQuery = new URLSearchParams();
	for (const [key, value] of Object.entries(filters)) {
		if (value !== undefined && value !== "" && key !== "page" && key !== "pageSize") {
			exportQuery.set(key, String(value));
		}
	}

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="space-y-2">
				<p className="eyebrow">Administration</p>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Audit log
				</h1>
				<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Every command that changed something, with who ran it. Append-only —
					nothing here can be edited or removed.
				</p>
			</header>

			<AuditToolbar
				actions={facets.actions}
				entityTypes={facets.entityTypes}
				exportQuery={exportQuery.toString()}
			/>

			<DataTable
				columns={columns}
				rows={result.items}
				rowKey={(r) => r.id}
				pathname="/admin/audit-logs"
				searchParams={params}
				sort={sort}
				page={{
					page: result.page,
					pageSize: result.pageSize,
					totalCount: result.totalCount,
					totalPages: result.totalPages,
				}}
				empty={{
					title: "Nothing matches",
					description: "Widen the date range or clear the filters.",
				}}
			/>
		</div>
	);
}
