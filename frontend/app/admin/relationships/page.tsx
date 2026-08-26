import Link from "next/link";
import {
	listAdminRelationships,
	type AdminRelationship,
} from "@/data/admin/relationships";
import {
	DataTable,
	Pill,
	TableToolbar,
	readPage,
	readSort,
	type Column,
} from "@/components/ui/data-table";
import EndRelationshipButton from "./EndRelationshipButton";

export const dynamic = "force-dynamic";

export const metadata = {
	title: "Trainer relationships | Mizan admin",
	description: "Who coaches whom, and what they can see",
};

const PAGE_SIZE = 20;

const STATUS_TONE: Record<string, "good" | "warn" | "neutral"> = {
	active: "good",
	pending: "warn",
	paused: "warn",
};

/**
 * The grants, as three letters. The full words do not fit and the question an
 * admin is answering is usually "can this coach see their weight", which a
 * lit or unlit N/W/M answers at a glance.
 */
function Grants({ r }: { r: AdminRelationship }) {
	const axes: [string, boolean, string][] = [
		["N", r.canViewNutrition, "Nutrition"],
		["W", r.canViewWorkouts, "Workouts"],
		["M", r.canViewMeasurements, "Measurements"],
	];

	return (
		<span className="inline-flex gap-1">
			{axes.map(([letter, granted, label]) => (
				<span
					key={letter}
					title={`${label}: ${granted ? "shared" : "not shared"}`}
					className={
						granted
							? "flex h-5 w-5 items-center justify-center rounded bg-verdigris-100 text-[11px] font-semibold text-verdigris-800 dark:bg-verdigris-500/20 dark:text-verdigris-300"
							: "flex h-5 w-5 items-center justify-center rounded bg-charcoal-blue-100 text-[11px] text-charcoal-blue-400 dark:bg-white/5 dark:text-charcoal-blue-600"
					}
				>
					{letter}
				</span>
			))}
		</span>
	);
}

const columns: Column<AdminRelationship>[] = [
	{
		id: "trainer",
		header: "Trainer",
		sortKey: "trainer",
		cell: (r) => (
			<div className="min-w-0">
				<Link
					href={`/admin/users/${r.trainerId}`}
					className="truncate font-medium text-charcoal-blue-900 hover:underline dark:text-charcoal-blue-50"
				>
					{r.trainerName || "Unnamed"}
				</Link>
				<p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{r.trainerEmail}
				</p>
			</div>
		),
	},
	{
		id: "client",
		header: "Client",
		sortKey: "client",
		cell: (r) => (
			<div className="min-w-0">
				<Link
					href={`/admin/users/${r.clientId}`}
					className="truncate font-medium text-charcoal-blue-900 hover:underline dark:text-charcoal-blue-50"
				>
					{r.clientName || "Unnamed"}
				</Link>
				<p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{r.clientEmail}
				</p>
			</div>
		),
	},
	{
		id: "status",
		header: "Status",
		sortKey: "status",
		cell: (r) => <Pill tone={STATUS_TONE[r.status] ?? "neutral"}>{r.status}</Pill>,
	},
	{
		id: "grants",
		header: "Shares",
		align: "center",
		cell: (r) => <Grants r={r} />,
	},
	{
		id: "since",
		header: "Since",
		sortKey: "createdAt",
		secondary: true,
		cell: (r) => (
			<span className="whitespace-nowrap text-xs tabular-nums text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{new Date(r.startedAt ?? r.createdAt).toLocaleDateString()}
			</span>
		),
	},
	{
		id: "actions",
		header: "",
		align: "right",
		width: "1%",
		cell: (r) =>
			r.status === "ended" ? null : (
				<EndRelationshipButton
					id={r.id}
					trainer={r.trainerName || r.trainerEmail}
					client={r.clientName || r.clientEmail}
				/>
			),
	},
];

export default async function AdminRelationshipsPage({
	searchParams,
}: {
	searchParams: Promise<Record<string, string | undefined>>;
}) {
	const params = await searchParams;
	const page = readPage(params);
	const sort = readSort(params);

	const result = await listAdminRelationships({
		page,
		pageSize: PAGE_SIZE,
		search: params.search,
		status: params.status,
		sortBy: sort.sortBy ?? undefined,
		sortOrder: sort.sortBy ? sort.sortOrder : undefined,
	});

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="space-y-2">
				<p className="eyebrow">Administration</p>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Trainer relationships
				</h1>
				<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Which axes a client shares is the client&apos;s decision and is read-only
					here. Ending a relationship revokes the coach&apos;s access immediately.
				</p>
			</header>

			<TableToolbar
				searchPlaceholder="Trainer or client…"
				filters={[
					{
						name: "status",
						label: "Any status",
						options: ["pending", "active", "paused", "ended"].map((v) => ({
							value: v,
							label: v,
						})),
					},
				]}
			/>

			<DataTable
				columns={columns}
				rows={result.items}
				rowKey={(r) => r.id}
				pathname="/admin/relationships"
				searchParams={params}
				sort={sort}
				page={{
					page: result.page,
					pageSize: result.pageSize,
					totalCount: result.totalCount,
					totalPages: result.totalPages,
				}}
				empty={{
					title: "No relationships match",
					description: "Try a different search or clear the filters.",
				}}
			/>
		</div>
	);
}
