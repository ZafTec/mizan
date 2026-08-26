import Link from "next/link";
import {
	getAchievementAnalytics,
	type AchievementAnalyticsRow,
} from "@/data/admin/achievements";
import {
	DataTable,
	Pill,
	TableToolbar,
	readPage,
	readSort,
	type Column,
} from "@/components/ui/data-table";
import DeleteAchievementButton from "./DeleteAchievementButton";

export const dynamic = "force-dynamic";

export const metadata = {
	title: "Achievements | Mizan admin",
	description: "The catalogue, and how it is performing",
};

const PAGE_SIZE = 25;

/**
 * A rate nobody has ever hit is the useful signal on this screen - it means a
 * threshold set too high, or a criteria type that never fires.
 */
function rateTone(rate: number, unlocked: number) {
	if (unlocked === 0) return "bad" as const;
	if (rate < 0.02) return "warn" as const;
	return "good" as const;
}

const columns: Column<AchievementAnalyticsRow>[] = [
	{
		id: "name",
		header: "Achievement",
		sortKey: "name",
		cell: (a) => (
			<div className="min-w-0">
				<Link
					href={`/admin/achievements/${a.id}/edit`}
					className="truncate font-medium text-charcoal-blue-900 hover:underline dark:text-charcoal-blue-50"
				>
					{a.name}
				</Link>
				<p className="truncate font-mono text-[11px] text-charcoal-blue-400 dark:text-charcoal-blue-500">
					{a.criteriaType ?? "no criteria"} ≥ {a.threshold}
				</p>
			</div>
		),
	},
	{
		id: "category",
		header: "Category",
		sortKey: "category",
		secondary: true,
		cell: (a) => (a.category ? <Pill tone="info">{a.category}</Pill> : <Pill>—</Pill>),
	},
	{
		id: "points",
		header: "Points",
		sortKey: "points",
		align: "center",
		cell: (a) => <span className="tabular-nums">{a.points}</span>,
	},
	{
		id: "unlocked",
		header: "Unlocked by",
		align: "center",
		cell: (a) => <span className="tabular-nums">{a.unlockedBy}</span>,
	},
	{
		id: "rate",
		header: "Rate",
		align: "center",
		cell: (a) => (
			<Pill tone={rateTone(a.unlockRate, a.unlockedBy)}>
				{(a.unlockRate * 100).toFixed(1)}%
			</Pill>
		),
	},
	{
		id: "recent",
		header: "Last unlock",
		secondary: true,
		cell: (a) => (
			<span className="whitespace-nowrap text-xs tabular-nums text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{a.mostRecentUnlockAt ? new Date(a.mostRecentUnlockAt).toLocaleDateString() : "never"}
			</span>
		),
	},
	{
		id: "actions",
		header: "",
		align: "right",
		width: "1%",
		cell: (a) => (
			<div className="flex items-center justify-end gap-3">
				<Link
					href={`/admin/achievements/${a.id}/edit`}
					className="text-xs text-verdigris-700 hover:underline dark:text-verdigris-300"
				>
					Edit
				</Link>
				<DeleteAchievementButton id={a.id} name={a.name} unlockedBy={a.unlockedBy} />
			</div>
		),
	},
];

function Stat({ label, value }: { label: string; value: string | number }) {
	return (
		<div className="rounded-3xl border border-charcoal-blue-200 p-4 dark:border-white/10">
			<p className="text-xs uppercase tracking-wide text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{label}
			</p>
			<p className="mt-1 text-2xl font-semibold tabular-nums text-charcoal-blue-900 dark:text-charcoal-blue-50">
				{value}
			</p>
		</div>
	);
}

export default async function AdminAchievementsPage({
	searchParams,
}: {
	searchParams: Promise<Record<string, string | undefined>>;
}) {
	const params = await searchParams;
	const page = readPage(params);
	const sort = readSort(params);

	const analytics = await getAchievementAnalytics({
		page,
		pageSize: PAGE_SIZE,
		searchTerm: params.search,
		category: params.category,
		sortBy: sort.sortBy ?? undefined,
		sortOrder: sort.sortBy ? sort.sortOrder : undefined,
	});

	const categories = analytics.categories
		.map((c) => c.category)
		.filter((c): c is string => Boolean(c));

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Administration</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Achievements
					</h1>
				</div>
				<Link href="/admin/achievements/new" className="btn-primary !rounded-2xl">
					New achievement
				</Link>
			</header>

			<div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
				<Stat label="In catalogue" value={analytics.totalAchievements} />
				<Stat label="Total unlocks" value={analytics.totalUnlocks} />
				<Stat
					label="Users with one"
					value={`${analytics.usersWithAtLeastOne} / ${analytics.totalUsers}`}
				/>
				<Stat label="Average each" value={analytics.averageUnlocksPerUser.toFixed(1)} />
			</div>

			<TableToolbar
				searchPlaceholder="Search achievements…"
				filters={
					categories.length > 0
						? [
								{
									name: "category",
									label: "Any category",
									options: categories.map((c) => ({ value: c, label: c })),
								},
							]
						: []
				}
			/>

			<DataTable
				columns={columns}
				rows={analytics.rows}
				rowKey={(a) => a.id}
				pathname="/admin/achievements"
				searchParams={params}
				sort={sort}
				page={{
					page: analytics.page,
					pageSize: analytics.pageSize,
					totalCount: analytics.rowsTotalCount,
					totalPages: analytics.totalPages,
				}}
				empty={{
					title: "No achievements match",
					description: "Try a different search, or add one.",
					action: (
						<Link href="/admin/achievements/new" className="btn-primary !rounded-2xl">
							New achievement
						</Link>
					),
				}}
			/>
		</div>
	);
}
