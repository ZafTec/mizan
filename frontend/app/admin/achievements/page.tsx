import Link from "next/link";
import { getAchievementsAdmin } from "@/data/admin/achievement";
import AchievementActions from "./AchievementActions";
import SortableHeader from "@/components/SortableHeader";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";

export const dynamic = "force-dynamic";

export const metadata = {
    title: "Manage achievements | Mizan",
    description: "Admin: create, edit and retire achievements.",
};

function criteriaSummary(criteriaType?: string | null, threshold?: number): string {
    if (!criteriaType) return "Manual";
    const t = threshold ?? 0;
    switch (criteriaType) {
        case "meals_logged": return `${t} meals logged`;
        case "recipes_created": return `${t} recipes created`;
        case "workouts_logged": return `${t} workouts logged`;
        case "body_measurements_logged": return `${t} body measurements`;
        case "goal_progress_logged": return `${t} goal progress entries`;
        case "streak_nutrition": return `${t}-day nutrition streak`;
        case "streak_workout": return `${t}-day workout streak`;
        case "points_total": return `${t} total points`;
        default: return `${criteriaType} : ${t}`;
    }
}

export default async function AdminAchievementsPage({
    searchParams,
}: {
    searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
    const params = await searchParams;
    const { page, pageSize, sortBy, sortOrder } = parseListParams(params, {
        pageSize: 20,
        sortBy: "category",
        sortOrder: "asc",
    });
    const searchTerm = typeof params.search === "string" ? params.search.trim() : "";
    const categoryFilter = typeof params.category === "string" ? params.category.trim() : "";

    const { items, totalCount, totalPages } = await getAchievementsAdmin({
        page,
        pageSize,
        searchTerm: searchTerm || undefined,
        category: categoryFilter || undefined,
        sortBy: sortBy ?? undefined,
        sortOrder,
    });

    const baseUrl = buildListUrl("/admin/achievements", {
        search: searchTerm || undefined,
        category: categoryFilter || undefined,
    });

    return (
        <div className="space-y-6 lg:space-y-8">
            <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div className="space-y-2">
                    <p className="eyebrow">Gamification</p>
                    <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
                        Manage achievements
                    </h1>
                </div>
                <div className="flex items-center gap-2">
                    <Link href="/admin/achievements/analytics" className="btn-ghost">
                        <i className="ri-bar-chart-2-line" />
                        Analytics
                    </Link>
                    <Link href="/admin/achievements/new" className="btn-primary">
                        <i className="ri-add-line" />
                        New achievement
                    </Link>
                </div>
            </header>

            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <form method="GET" className="flex flex-col gap-2 sm:flex-row">
                    <input
                        type="search"
                        name="search"
                        placeholder="Search name, description, category..."
                        defaultValue={searchTerm}
                        className="input h-10 w-full sm:w-80"
                    />
                    <input
                        type="text"
                        name="category"
                        placeholder="Category"
                        defaultValue={categoryFilter}
                        className="input h-10 w-full sm:w-40"
                    />
                    <button type="submit" className="btn-primary h-10">Apply</button>
                    {(searchTerm || categoryFilter) && (
                        <Link href="/admin/achievements" className="btn-ghost h-10">
                            Clear
                        </Link>
                    )}
                </form>

                <div className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                    Showing <span className="font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">{items.length}</span>{" "}
                    of <span className="font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">{totalCount}</span> achievements
                </div>
            </div>

            <div className="card overflow-hidden">
                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="border-b border-charcoal-blue-100 bg-charcoal-blue-50/50 dark:border-white/10 dark:bg-charcoal-blue-900/60">
                                <SortableHeader sortKey="name" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Name</SortableHeader>
                                <SortableHeader sortKey="category" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Category</SortableHeader>
                                <SortableHeader sortKey="criteriaType" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Unlock criteria</SortableHeader>
                                <SortableHeader sortKey="points" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Points</SortableHeader>
                                <th className="px-6 py-4 text-right text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Actions</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
                            {items.length === 0 ? (
                                <tr>
                                    <td colSpan={5} className="px-6 py-12 text-center text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                        {searchTerm || categoryFilter
                                            ? "No achievements match your filters."
                                            : "No achievements yet. Create one to get started."}
                                    </td>
                                </tr>
                            ) : (
                                items.map((a) => (
                                    <tr key={a.id} className="hover:bg-charcoal-blue-50/50 dark:hover:bg-charcoal-blue-900/40">
                                        <td className="px-6 py-4">
                                            <div className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
                                                {a.name}
                                            </div>
                                            {a.description && (
                                                <div className="mt-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                                    {a.description}
                                                </div>
                                            )}
                                        </td>
                                        <td className="px-6 py-4 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
                                            {a.category ?? "(none)"}
                                        </td>
                                        <td className="px-6 py-4 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
                                            {criteriaSummary(a.criteriaType, a.threshold)}
                                        </td>
                                        <td className="px-6 py-4 text-center text-sm font-semibold text-charcoal-blue-800 dark:text-charcoal-blue-200">
                                            {a.points}
                                        </td>
                                        <td className="px-6 py-4">
                                            <AchievementActions id={a.id} name={a.name} />
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
