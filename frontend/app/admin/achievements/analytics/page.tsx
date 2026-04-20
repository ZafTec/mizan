import Link from "next/link";
import { getAchievementAnalytics } from "@/data/admin/achievement";

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

export default async function AnalyticsPage() {
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
                <h2 className="section-title">Per-achievement unlocks</h2>
                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="border-b border-charcoal-blue-100 dark:border-white/10">
                                <th className="py-3 pr-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Name</th>
                                <th className="py-3 pr-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Category</th>
                                <th className="py-3 pr-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Unlocked</th>
                                <th className="py-3 pr-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Rate</th>
                                <th className="py-3 pr-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Last unlock</th>
                                <th className="py-3 text-right text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Points</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
                            {rows.length === 0 ? (
                                <tr>
                                    <td colSpan={6} className="py-10 text-center text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                        No data yet.
                                    </td>
                                </tr>
                            ) : (
                                rows.map((r) => {
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
                                            <td className="py-3 pr-4 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400 capitalize">
                                                {r.category ?? "-"}
                                            </td>
                                            <td className="py-3 pr-4">
                                                <div className="flex items-center gap-3">
                                                    <div className="w-24 h-1.5 overflow-hidden rounded-full bg-charcoal-blue-100 dark:bg-charcoal-blue-900">
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
