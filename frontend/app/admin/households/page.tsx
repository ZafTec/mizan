import Link from "next/link";
import Pagination from "@/components/Pagination";
import SortableHeader from "@/components/SortableHeader";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";
import { listHouseholdsAdmin } from "@/data/admin/household";
import HouseholdRowActions from "./HouseholdRowActions";

export const dynamic = "force-dynamic";

export const metadata = {
	title: "Manage households | Mizan admin",
	description: "Admin view of every household on the platform.",
};

export default async function AdminHouseholdsPage({
	searchParams,
}: {
	searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
	const params = await searchParams;
	const { page, pageSize, sortBy, sortOrder } = parseListParams(params, {
		pageSize: 20,
		sortBy: "createdAt",
		sortOrder: "desc",
	});
	const searchTerm = typeof params.search === "string" ? params.search.trim() : "";

	const { items, totalCount, totalPages } = await listHouseholdsAdmin({
		page,
		pageSize,
		searchTerm: searchTerm || undefined,
		sortBy: sortBy ?? undefined,
		sortOrder,
	});

	const baseUrl = buildListUrl("/admin/households", {
		search: searchTerm || undefined,
	});

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Admin</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Households
					</h1>
				</div>
			</header>

			<div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
				<form method="GET" className="flex flex-col gap-2 sm:flex-row">
					<input
						type="search"
						name="search"
						placeholder="Search household name..."
						defaultValue={searchTerm}
						className="input h-10 w-full sm:w-80"
					/>
					<button type="submit" className="btn-primary h-10">Apply</button>
					{searchTerm && (
						<Link href="/admin/households" className="btn-ghost h-10">
							Clear
						</Link>
					)}
				</form>
				<div className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Showing <span className="font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">{items.length}</span>{" "}
					of <span className="font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">{totalCount}</span> households
				</div>
			</div>

			<div className="card overflow-hidden">
				<div className="overflow-x-auto">
					<table className="w-full text-left border-collapse">
						<thead>
							<tr className="border-b border-charcoal-blue-100 bg-charcoal-blue-50/50 dark:border-white/10 dark:bg-charcoal-blue-900/60">
								<SortableHeader sortKey="name" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Name</SortableHeader>
								<th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Creator</th>
								<th className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Members</th>
								<th className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Pending</th>
								<SortableHeader sortKey="createdAt" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Created</SortableHeader>
								<th className="px-6 py-4 text-right text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Actions</th>
							</tr>
						</thead>
						<tbody className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
							{items.length === 0 ? (
								<tr>
									<td colSpan={6} className="px-6 py-12 text-center text-charcoal-blue-500 dark:text-charcoal-blue-400">
										No households found.
									</td>
								</tr>
							) : (
								items.map((h) => (
									<tr key={h.id} className="hover:bg-charcoal-blue-50/50 dark:hover:bg-charcoal-blue-900/40">
										<td className="px-6 py-4">
											<Link href={`/admin/households/${h.id}`} className="font-semibold text-charcoal-blue-900 hover:text-brand-700 dark:text-charcoal-blue-100 dark:hover:text-brand-300">
												{h.name}
											</Link>
										</td>
										<td className="px-6 py-4 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
											<div>{h.createdByName ?? "-"}</div>
											<div className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{h.createdByEmail}</div>
										</td>
										<td className="px-6 py-4 text-center text-sm font-semibold text-charcoal-blue-800 dark:text-charcoal-blue-200">
											{h.memberCount}
										</td>
										<td className="px-6 py-4 text-center text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
											{h.pendingInviteCount}
										</td>
										<td className="px-6 py-4 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
											{new Date(h.createdAt).toLocaleDateString()}
										</td>
										<td className="px-6 py-4">
											<HouseholdRowActions id={h.id} name={h.name} />
										</td>
									</tr>
								))
							)}
						</tbody>
					</table>
				</div>
			</div>

			{totalPages > 1 && (
				<Pagination
					currentPage={page}
					totalPages={totalPages}
					totalCount={totalCount}
					pageSize={pageSize}
					baseUrl={baseUrl}
				/>
			)}
		</div>
	);
}
