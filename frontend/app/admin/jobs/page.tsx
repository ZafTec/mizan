import {
	getAdminJobStats,
	listAdminJobs,
	type AdminJob,
	type JobStatus,
} from "@/data/admin/jobs";
import {
	DataTable,
	Pill,
	TableToolbar,
	readPage,
	readSort,
	type Column,
	type PillTone,
} from "@/components/ui/data-table";
import JobActions from "./JobActions";

export const dynamic = "force-dynamic";

export const metadata = {
	title: "Background jobs | Mizan admin",
	description: "Queued work, and the work that failed",
};

const PAGE_SIZE = 20;

const STATUS_TONE: Record<JobStatus, PillTone> = {
	Pending: "info",
	Running: "info",
	Succeeded: "good",
	Failed: "warn",
	DeadLettered: "bad",
};

const STATUS_LABEL: Record<JobStatus, string> = {
	Pending: "Queued",
	Running: "Running",
	Succeeded: "Done",
	Failed: "Retrying",
	DeadLettered: "Dead",
};

const TYPE_LABEL: Record<string, string> = {
	email: "Email",
	"eval-run": "Eval suite",
};

function when(value?: string | null) {
	if (!value) return "—";
	return new Date(value).toLocaleString(undefined, {
		month: "short",
		day: "numeric",
		hour: "2-digit",
		minute: "2-digit",
	});
}

const columns: Column<AdminJob>[] = [
	{
		id: "type",
		header: "Job",
		sortKey: "type",
		cell: (j) => (
			<div className="min-w-0">
				<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{TYPE_LABEL[j.type] ?? j.type}
				</p>
				<p className="truncate font-mono text-[11px] text-charcoal-blue-400 dark:text-charcoal-blue-500">
					{j.id.slice(0, 8)}
				</p>
			</div>
		),
	},
	{
		id: "status",
		header: "Status",
		sortKey: "status",
		cell: (j) => <Pill tone={STATUS_TONE[j.status] ?? "neutral"}>{STATUS_LABEL[j.status] ?? j.status}</Pill>,
	},
	{
		id: "attempts",
		header: "Tries",
		sortKey: "attempts",
		align: "center",
		cell: (j) => <span className="tabular-nums text-sm">{j.attempts}</span>,
	},
	{
		id: "error",
		header: "Last error",
		cell: (j) =>
			j.lastError ? (
				<p
					title={j.lastError}
					className="line-clamp-2 max-w-md text-xs text-red-700 dark:text-red-400"
				>
					{j.lastError}
				</p>
			) : (
				<span className="text-xs text-charcoal-blue-400">—</span>
			),
	},
	{
		id: "createdAt",
		header: "Queued",
		sortKey: "createdAt",
		secondary: true,
		cell: (j) => (
			<span className="whitespace-nowrap text-xs tabular-nums text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{when(j.createdAt)}
			</span>
		),
	},
	{
		id: "next",
		header: "Next run",
		sortKey: "runAfter",
		secondary: true,
		cell: (j) => (
			<span className="whitespace-nowrap text-xs tabular-nums text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{j.status === "Pending" || j.status === "Failed" ? when(j.runAfter) : "—"}
			</span>
		),
	},
	{
		id: "actions",
		header: "",
		align: "right",
		width: "1%",
		cell: (j) => <JobActions job={j} />,
	},
];

function Stat({
	label,
	value,
	tone,
}: {
	label: string;
	value: number;
	tone?: "bad" | "warn";
}) {
	const colour =
		tone === "bad" && value > 0
			? "text-red-600 dark:text-red-400"
			: tone === "warn" && value > 0
				? "text-amber-600 dark:text-amber-400"
				: "text-charcoal-blue-900 dark:text-charcoal-blue-50";

	return (
		<div className="surface-panel px-4 py-3">
			<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{label}</p>
			<p className={`text-2xl font-semibold tabular-nums ${colour}`}>{value}</p>
		</div>
	);
}

export default async function AdminJobsPage({
	searchParams,
}: {
	searchParams: Promise<Record<string, string | undefined>>;
}) {
	const params = await searchParams;
	const page = readPage(params);
	const sort = readSort(params);

	const [result, stats] = await Promise.all([
		listAdminJobs({
			page,
			pageSize: PAGE_SIZE,
			type: params.type,
			status: params.status,
			sortBy: sort.sortBy ?? undefined,
			sortOrder: sort.sortBy ? sort.sortOrder : undefined,
		}),
		getAdminJobStats(),
	]);

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="space-y-2">
				<p className="eyebrow">Administration</p>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Background jobs
				</h1>
				<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Outbound email and eval suites run here rather than inside the request
					that asked for them. A dead job is something a user asked for that never
					happened - fix the cause, then retry it.
				</p>
			</header>

			<div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
				<Stat label="Dead" value={stats.deadLettered} tone="bad" />
				<Stat label="Retrying" value={stats.failed} tone="warn" />
				<Stat label="Queued" value={stats.pending + stats.running} />
				<Stat label="Done" value={stats.succeeded} />
			</div>

			<TableToolbar
				filters={[
					{
						name: "status",
						label: "Any status",
						options: [
							{ value: "DeadLettered", label: "Dead" },
							{ value: "Failed", label: "Retrying" },
							{ value: "Pending", label: "Queued" },
							{ value: "Running", label: "Running" },
							{ value: "Succeeded", label: "Done" },
						],
					},
					{
						name: "type",
						label: "Any job",
						options: stats.types.map((t) => ({
							value: t,
							label: TYPE_LABEL[t] ?? t,
						})),
					},
				]}
			/>

			<DataTable
				columns={columns}
				rows={result.items}
				rowKey={(j) => j.id}
				rowClassName={(j) => (j.status === "DeadLettered" ? "wash-danger" : undefined)}
				pathname="/admin/jobs"
				searchParams={params}
				sort={sort}
				page={{
					page: result.page,
					pageSize: result.pageSize,
					totalCount: result.totalCount,
					totalPages: result.totalPages,
				}}
				empty={{
					title: "Nothing in the queue",
					description: "Jobs appear here as they are enqueued.",
				}}
			/>
		</div>
	);
}
