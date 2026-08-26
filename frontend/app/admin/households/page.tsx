import { listHouseholdsAdmin, type AdminHouseholdSummary } from "@/data/admin/household";
import {
	DataTable,
	TableToolbar,
	readPage,
	readSort,
	type Column,
} from "@/components/ui/data-table";
import HouseholdRowActions from "./HouseholdRowActions";

export const dynamic = "force-dynamic";

export const metadata = {
	title: "Households | Mizan admin",
	description: "Every household on the platform",
};

const PAGE_SIZE = 20;

const columns: Column<AdminHouseholdSummary>[] = [
	{
		id: "name",
		header: "Household",
		sortKey: "name",
		cell: (h) => (
			<p className="truncate font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
				{h.name}
			</p>
		),
	},
	{
		id: "creator",
		header: "Creator",
		cell: (h) => (
			<div className="min-w-0">
				<p className="truncate text-sm">{h.createdByName || "Unnamed"}</p>
				<p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{h.createdByEmail}
				</p>
			</div>
		),
	},
	{
		id: "members",
		header: "Members",
		align: "center",
		cell: (h) => <span className="tabular-nums font-medium">{h.memberCount}</span>,
	},
	{
		id: "pending",
		header: "Pending",
		align: "center",
		secondary: true,
		cell: (h) => <span className="tabular-nums">{h.pendingInviteCount || "—"}</span>,
	},
	{
		id: "created",
		header: "Created",
		sortKey: "createdAt",
		secondary: true,
		cell: (h) => (
			<span className="whitespace-nowrap text-xs tabular-nums text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{new Date(h.createdAt).toLocaleDateString()}
			</span>
		),
	},
	{
		id: "actions",
		header: "",
		align: "right",
		width: "1%",
		cell: (h) => <HouseholdRowActions id={h.id} name={h.name} />,
	},
];

export default async function AdminHouseholdsPage({
	searchParams,
}: {
	searchParams: Promise<Record<string, string | undefined>>;
}) {
	const params = await searchParams;
	const page = readPage(params);
	const sort = readSort(params);

	const { items, totalCount, totalPages } = await listHouseholdsAdmin({
		page,
		pageSize: PAGE_SIZE,
		searchTerm: params.search || undefined,
		sortBy: sort.sortBy ?? "createdAt",
		sortOrder: sort.sortBy ? sort.sortOrder : "desc",
	});

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="space-y-2">
				<p className="eyebrow">Administration</p>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Households
				</h1>
			</header>

			<TableToolbar searchPlaceholder="Search households…" />

			<DataTable
				columns={columns}
				rows={items}
				rowKey={(h) => h.id}
				pathname="/admin/households"
				searchParams={params}
				sort={sort}
				page={{ page, pageSize: PAGE_SIZE, totalCount, totalPages }}
				empty={{ title: "No households match", description: "Try a different search." }}
			/>
		</div>
	);
}
