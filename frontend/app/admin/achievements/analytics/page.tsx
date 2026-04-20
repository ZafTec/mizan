import Link from "next/link";
import { getAchievementAnalytics, type AchievementAnalyticsRow } from "@/data/admin/achievement";
import SortableHeader from "@/components/SortableHeader";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";

export const dynamic = "force-dynamic";

export const metadata = {
    title: "Achievement analytics | Mizan admin",
};

function fmtDate(iso?: string | null): string {
    if (!iso) return "-";
    try {
        return new Date(iso).toLocaleString();
    } catch {
        return iso;
    }
}

type SortKey = "name" | "category" | "unlockedBy" | "unlockRate" | "mostRecentUnlockAt" | "points";

function sortRows(
    rows: AchievementAnalyticsRow[],
    sortBy: SortKey | null,
    sortOrder: "asc" | "desc"
): AchievementAnalyticsRow[] {
    if (!sortBy) return rows;
    const dir = sortOrder === "asc" ? 1 : -1;
    const sorted = [...rows].sort((a, b) => {
        const av = a[sortBy as keyof AchievementAnalyticsRow];
        const bv = b[sortBy as keyof AchievementAnalyticsRow];
        if (av == null && bv == null) return 0;
        if (av == null) return 1;
        if (bv == null) return -1;
        if (typeof av === "number" && typeof bv === "number") return (av - bv) * dir;
        return String(av).localeCompare(String(bv)) * dir;
    });
    return sorted;
}

export default async function AnalyticsPage({
    searchParams,
}: {
    searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
    const params = await searchParams;
    const { page, pageSize, sortBy, sortOrder } = parseListParams(params, {
        pageSize: 20,
        sortBy: "unlockedBy",
        sortOrder: "desc",
    });
    const searchTerm = typeof params.search === "string" ? params.search.trim() : "";
    const categoryFilter = typeof params.category === "string" ? params.category.trim() : "";

    const analytics = await getAchievementAnalytics();

    if (!analytics) {
        return (
            <div className="space-y-6">
                <header className="space-y-2">
                    <p className="eyebrow">Gamification</p>
                    <h1 className="text-3xl font-semibold tracking-tight">Achievement analytics</h1>
                </header>
                <div className="card p-8 text-center text-charcoal-blue-500 dark:text-charcoal-blue-400">
                    Analytics unavailable. Check backend connectivity and try again.
                </div>
            </div>
        );
    }

    const {
        totalAchievements,
        totalUsers,
        totalUnlocks,
        usersWithAtLeastOne,
        averageUnlocksPerUser,
        rows,
        categories,
    } = analytics;

    const engagementPct = totalUsers > 0 ? Math.round((usersWithAtLeastOne / totalUsers) * 100) : 0;
    const maxUnlocked = rows.reduce((m, r) => Math.max(m, r.unlockedBy), 0);
    const zeroUnlock = rows.filter((r) => r.unlockedBy === 0);

    // Client-side filter + sort + paginate (row count is bounded by total achievements).
    const needle = searchTerm.toLowerCase();
    const filteredRows = rows.filter((r) => {
        if (needle && !r.name.toLowerCase().includes(needle) && !(r.category ?? "").toLowerCase().includes(needle)) {
            return false;
        }
        if (categoryFilter && (r.category ?? "") !== categoryFilter) return false;
        return true;
    });
    const sortedRows = sortRows(filteredRows, sortBy as SortKey | null, sortOrder);
    const totalCount = sortedRows.length;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
    const safePage = Math.min(Math.max(1, page), totalPages);
    const pagedRows = sortedRows.slice((safePage - 1) * pageSize, safePage * pageSize);

    const baseUrl = buildListUrl("/admin/achievements/analytics", {
        search: searchTerm || undefined,
        category: categoryFilter || undefined,
    });

    return (
        <div className="space-y-6 lg:space-y-8">
            <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div className="space-y-2">
                    <p className="eyebrow">Gamification</p>
                    <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
                        Achievement analytics
                    </h1>
                    <p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                        How many users earn each achievement and where gaps exist in the unlock funnel.
                    </p>
                </div>
                <Link href="/admin/achievements" className="btn-ghost">
                    <i className="ri-arrow-left-line" />
                    Back to list
                </Link>
            </header>

            <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
                <StatTile label="Achievements" value={totalAchievements.toLocaleString()} />
                <StatTile label="Users" value={totalUsers.toLocaleString()} />
                <StatTile label="Total unlocks" value={totalUnlocks.toLocaleString()} />
                <StatTile
                    label="Users with 1+"
                    value={`${usersWithAtLeastOne.toLocaleString()} (${engagementPct}%)`}
                />
                <StatTile label="Avg per user" value={averageUnlocksPerUser.toFixed(2)} />
            </div>

            {zeroUnlock.length > 0 && (
                <div className="rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-200">
                    <strong>{zeroUnlock.length}</strong> achievement{zeroUnlock.length === 1 ? "" : "s"} with zero unlocks:{" "}
                    {zeroUnlock.map((r) => r.name).join(", ")}. Consider lowering the threshold or archiving.
                </div>
            )}

            <section className="card p-6">
                <h2 className="section-title mb-4">By category</h2>
                <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
                    {categories.length === 0 ? (
                        <div className="col-span-full text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                            No categories yet.
                        </div>
                    ) : (
                        categories.map((c) => (
                            <div key={c.category} className="rounded-xl border border-charcoal-blue-100 bg-white/60 p-4 dark:border-white/10 dark:bg-charcoal-blue-950/40">
                                <p className="eyebrow mb-1 capitalize">{c.category}</p>
                                <p className="text-2xl font-bold text-charcoal-blue-900 dark:text-charcoal-blue-50">
                                    {c.totalUnlocks.toLocaleString()}
                                </p>
                                <p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                    {c.achievementCount} achievement{c.achievementCount === 1 ? "" : "s"}, {c.totalPointsEarned.toLocaleString()} pts earned
                                </p>
                            </div>
                        ))
                    )}
                </div>
            </section>

            <section className="card p-6 space-y-4">
                <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                    <h2 className="section-title">Per-achievement unlocks</h2>
                    <form method="GET" className="flex flex-col gap-2 sm:flex-row">
                        <input
                            type="search"
                            name="search"
                            placeholder="Search name or category..."
                            defaultValue={searchTerm}
                            className="input h-10 w-full sm:w-64"
                        />
                        <select
                            name="category"
                            defaultValue={categoryFilter}
                            className="input h-10 w-full sm:w-40"
                        >
                            <option value="">All categories</option>
                            {categories.map((c) => (
                                <option key={c.category} value={c.category}>
                                    {c.category}
                                </option>
                            ))}
                        </select>
                        <button type="submit" className="btn-primary h-10">
                            Apply
                        </button>
                        {(searchTerm || categoryFilter) && (
                            <Link href="/admin/achievements/analytics" className="btn-ghost h-10">
                                Clear
                            </Link>
                        )}
                    </form>
                </div>

                <div className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                    Showing <span className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{pagedRows.length}</span> of{" "}
                    <span className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{totalCount}</span>
                    {totalCount !== rows.length ? ` (filtered from ${rows.length})` : ""}
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="border-b border-charcoal-blue-100 dark:border-white/10">
                                <SortableHeader sortKey="name" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="py-3 pr-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Name</SortableHeader>
                                <SortableHeader sortKey="category" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="py-3 pr-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Category</SortableHeader>
                                <SortableHeader sortKey="unlockedBy" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="py-3 pr-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Unlocked</SortableHeader>
                                <SortableHeader sortKey="unlockRate" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="py-3 pr-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Rate</SortableHeader>
                                <SortableHeader sortKey="mostRecentUnlockAt" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="py-3 pr-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Last unlock</SortableHeader>
                                <SortableHeader sortKey="points" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="py-3 text-right text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Points</SortableHeader>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
                            {pagedRows.length === 0 ? (
                                <tr>
                                    <td colSpan={6} className="py-10 text-center text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                        No results. {searchTerm || categoryFilter ? "Try clearing filters." : "No data yet."}
                                    </td>
                                </tr>
                            ) : (
                                pagedRows.map((r) => {
                                    const widthPct = maxUnlocked > 0 ? Math.round((r.unlockedBy / maxUnlocked) * 100) : 0;
                                    return (
                                        <tr key={r.id}>
                                            <td className="py-3 pr-4">
                                                <Link
                                                    href={`/admin/achievements/${r.id}/edit`}
                                                    className="font-medium text-charcoal-blue-900 hover:underline dark:text-charcoal-blue-100"
                                                >
                                                    {r.name}
                                                </Link>
                                            </td>
                                            <td className="py-3 pr-4 text-sm capitalize text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                                {r.category ?? "-"}
                                            </td>
                                            <td className="py-3 pr-4">
                                                <div className="flex items-center gap-3">
                                                    <div className="h-1.5 w-24 overflow-hidden rounded-full bg-charcoal-blue-100 dark:bg-charcoal-blue-900">
                                                        <div
                                                            className="h-full rounded-full bg-brand-500"
                                                            style={{ width: `${widthPct}%` }}
                                                        />
                                                    </div>
                                                    <span className="text-sm font-semibold text-charcoal-blue-800 dark:text-charcoal-blue-200">
                                                        {r.unlockedBy}
                                                    </span>
                                                </div>
                                            </td>
                                            <td className="py-3 pr-4 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
                                                {r.unlockRate}%
                                            </td>
                                            <td className="py-3 pr-4 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                                {fmtDate(r.mostRecentUnlockAt)}
                                            </td>
                                            <td className="py-3 text-right text-sm font-semibold text-charcoal-blue-800 dark:text-charcoal-blue-200">
                                                {r.points}
                                            </td>
                                        </tr>
                                    );
                                })
                            )}
                        </tbody>
                    </table>
                </div>

                {totalPages > 1 && (
                    <Pagination
                        currentPage={safePage}
                        totalPages={totalPages}
                        totalCount={totalCount}
                        pageSize={pageSize}
                        baseUrl={baseUrl}
                    />
                )}
            </section>
        </div>
    );
}

function StatTile({ label, value }: { label: string; value: string }) {
    return (
        <div className="rounded-xl border border-charcoal-blue-100 bg-white/60 p-4 dark:border-white/10 dark:bg-charcoal-blue-950/40">
            <p className="eyebrow mb-1">{label}</p>
            <p className="text-2xl font-bold text-charcoal-blue-900 dark:text-charcoal-blue-50">{value}</p>
        </div>
    );
}
