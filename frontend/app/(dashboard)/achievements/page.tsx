import { getAchievements, getStreak } from "@/data/achievement";
import { getUserServer } from "@/helper/session";
import Image from "next/image";
import { AppFeatureIllustration } from "@/components/illustrations/AppFeatureIllustration";

export const dynamic = 'force-dynamic';

function criteriaLabel(criteriaType: string | null | undefined, threshold: number): string {
    switch (criteriaType) {
        case "meals_logged": return `Log ${threshold} meal${threshold === 1 ? "" : "s"}`;
        case "recipes_created": return `Create ${threshold} recipe${threshold === 1 ? "" : "s"}`;
        case "workouts_logged": return `Log ${threshold} workout${threshold === 1 ? "" : "s"}`;
        case "body_measurements_logged": return `Record ${threshold} body measurement${threshold === 1 ? "" : "s"}`;
        case "goal_progress_logged": return `Record ${threshold} goal progress entr${threshold === 1 ? "y" : "ies"}`;
        case "streak_nutrition": return `Hit a ${threshold}-day nutrition streak`;
        case "streak_workout": return `Hit a ${threshold}-day workout streak`;
        case "points_total": return `Earn ${threshold} achievement points`;
        default: return "Unlock criteria hidden";
    }
}

export default async function AchievementsPage() {
    await getUserServer();
    const { achievements, totalPoints, earnedCount, level, levelName, levelFloor, nextLevelAt } = await getAchievements();
    const streak = await getStreak();

    const earned = achievements.filter(a => a.isEarned);
    const unearned = achievements.filter(a => !a.isEarned);

    const levelSpan = nextLevelAt != null ? nextLevelAt - levelFloor : 1;
    const levelGained = Math.min(Math.max(totalPoints - levelFloor, 0), levelSpan);
    const levelPercent = nextLevelAt != null ? Math.round((levelGained / Math.max(levelSpan, 1)) * 100) : 100;

    return (
        <div className="space-y-6 lg:space-y-8" data-testid="achievements-page">
            <header className="flex flex-col gap-6 sm:flex-row sm:items-end sm:justify-between">
                <div className="space-y-2">
                    <p className="eyebrow">Milestones</p>
                    <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
                        Achievements
                    </h1>
                    <p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                        Track your streak, unlock badges and celebrate the small wins.
                    </p>
                </div>
                <div className="hidden w-40 shrink-0 drop-shadow-md sm:block">
                    <AppFeatureIllustration variant="achievements" />
                </div>
            </header>

            <section className="glass-panel p-6 sm:p-8">
                <div className="grid gap-6 md:grid-cols-[auto_1fr_auto] md:items-center">
                    <div className="streak-gradient flex h-16 w-16 items-center justify-center rounded-2xl text-white shadow-lg">
                        <i className="ri-fire-line text-3xl" />
                    </div>
                    <div className="space-y-1">
                        <h2 className="text-2xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
                            {streak?.currentStreak || 0} day streak
                        </h2>
                        <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                            Longest: {streak?.longestStreak || 0} days
                            {streak?.lastActivityDate && ` , last logged ${new Date(streak.lastActivityDate).toLocaleDateString()}`}
                        </p>
                    </div>
                    <div className="md:text-right">
                        <p className="text-3xl font-bold text-burnt-peach-600 dark:text-burnt-peach-300">{earnedCount}</p>
                        <p className="text-xs uppercase tracking-[0.16em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
                            Earned
                        </p>
                    </div>
                </div>

                <div className="mt-6 grid gap-4 sm:grid-cols-3">
                    <div className="rounded-xl bg-white/60 p-4 dark:bg-charcoal-blue-950/40">
                        <p className="eyebrow mb-1">Level</p>
                        <p className="text-xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
                            {level}. {levelName}
                        </p>
                    </div>
                    <div className="rounded-xl bg-white/60 p-4 dark:bg-charcoal-blue-950/40">
                        <p className="eyebrow mb-1">Total points</p>
                        <p className="text-xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
                            {totalPoints}
                        </p>
                    </div>
                    <div className="rounded-xl bg-white/60 p-4 dark:bg-charcoal-blue-950/40">
                        <p className="eyebrow mb-1">Next level</p>
                        <p className="text-xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
                            {nextLevelAt != null ? `${nextLevelAt - totalPoints} pts` : "Maxed"}
                        </p>
                    </div>
                </div>

                {nextLevelAt != null && (
                    <div className="mt-4">
                        <div className="h-2 w-full overflow-hidden rounded-full bg-charcoal-blue-100 dark:bg-charcoal-blue-900">
                            <div
                                className="h-full rounded-full bg-brand-500 transition-all"
                                style={{ width: `${levelPercent}%` }}
                            />
                        </div>
                        <p className="mt-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                            {levelGained} / {levelSpan} points to {levelName === "Rookie" ? "Bronze" : levelName === "Bronze" ? "Silver" : levelName === "Silver" ? "Gold" : "Platinum"}
                        </p>
                    </div>
                )}
            </section>

            {earned.length > 0 && (
                <div className="card p-6">
                    <h2 className="section-title mb-6">Earned Achievements</h2>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        {earned.map((achievement) => (
                            <div
                                key={achievement.id}
                                className="card-hover p-5 border border-brand-200 bg-white/70 dark:bg-charcoal-blue-950/60"
                            >
                                <div className="flex items-start gap-4">
                                    <div className="w-12 h-12 rounded-2xl bg-brand-600 flex items-center justify-center shadow-lg shrink-0 dark:bg-brand-500">
                                        {achievement.iconUrl ? (
                                            <Image
                                                src={achievement.iconUrl}
                                                alt={achievement.name || "Achievement"}
                                                width={32}
                                                height={32}
                                                className="w-8 h-8"
                                            />
                                        ) : (
                                            <i className="ri-trophy-line text-xl text-white" />
                                        )}
                                    </div>
                                    <div className="flex-1">
                                        <h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
                                            {achievement.name || "Achievement"}
                                        </h3>
                                        <p className="mt-1 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
                                            {achievement.description || "No description"}
                                        </p>
                                        <div className="flex items-center gap-3 mt-3">
                                            <span className="inline-flex items-center gap-1 rounded-full bg-brand-100 px-2 py-1 text-xs font-medium text-brand-700 dark:bg-brand-950/60 dark:text-brand-200">
                                                <i className="ri-star-line" />
                                                {achievement.points} points
                                            </span>
                                            {achievement.category && (
                                                <span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                                    {achievement.category}
                                                </span>
                                            )}
                                            {achievement.earnedAt && (
                                                <span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                                    Earned {new Date(achievement.earnedAt).toLocaleDateString()}
                                                </span>
                                            )}
                                        </div>
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            {unearned.length > 0 && (
                <div className="card p-6">
                    <h2 className="section-title mb-6">Available Achievements</h2>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        {unearned.map((achievement) => {
                            const threshold = (achievement as { threshold?: number }).threshold ?? 0;
                            const progress = (achievement as { progress?: number }).progress ?? 0;
                            const criteriaType = (achievement as { criteriaType?: string | null }).criteriaType ?? null;
                            const progressPct = threshold > 0 ? Math.min(Math.round((progress / threshold) * 100), 99) : 0;
                            return (
                                <div
                                    key={achievement.id}
                                    className="card-hover p-5 opacity-70 hover:opacity-95 transition-opacity"
                                >
                                    <div className="flex items-start gap-4">
                                        <div className="w-12 h-12 rounded-2xl bg-charcoal-blue-300 flex items-center justify-center shadow-lg shrink-0 dark:bg-charcoal-blue-700">
                                            {achievement.iconUrl ? (
                                                <Image
                                                    src={achievement.iconUrl}
                                                    alt={achievement.name || "Achievement"}
                                                    width={32}
                                                    height={32}
                                                    className="w-8 h-8 grayscale"
                                                />
                                            ) : (
                                                <i className="ri-trophy-line text-xl text-white" />
                                            )}
                                        </div>
                                        <div className="flex-1">
                                            <h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
                                                {achievement.name || "Achievement"}
                                            </h3>
                                            <p className="mt-1 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
                                                {achievement.description || criteriaLabel(criteriaType, threshold)}
                                            </p>
                                            {threshold > 0 && (
                                                <div className="mt-3">
                                                    <div className="h-1.5 w-full overflow-hidden rounded-full bg-charcoal-blue-200 dark:bg-charcoal-blue-800">
                                                        <div
                                                            className="h-full rounded-full bg-burnt-peach-500 transition-all"
                                                            style={{ width: `${progressPct}%` }}
                                                        />
                                                    </div>
                                                    <p className="mt-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                                        {Math.min(progress, threshold)} / {threshold}
                                                    </p>
                                                </div>
                                            )}
                                            <div className="flex items-center gap-3 mt-3">
                                                <span className="inline-flex items-center gap-1 rounded-full bg-charcoal-blue-100 px-2 py-1 text-xs font-medium text-charcoal-blue-600 dark:bg-charcoal-blue-900/60 dark:text-charcoal-blue-300">
                                                    <i className="ri-star-line" />
                                                    {achievement.points} points
                                                </span>
                                                {achievement.category && (
                                                    <span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                                        {achievement.category}
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            )}

            {achievements.length === 0 && (
                <div className="card p-16 text-center flex flex-col items-center">
                    <div className="mb-6 w-full max-w-[18rem] opacity-95 drop-shadow-md">
                        <AppFeatureIllustration variant="achievements" className="h-auto w-full" />
                    </div>
                    <h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-2">No achievements yet</h3>
                    <p className="text-charcoal-blue-500 dark:text-charcoal-blue-400">Start using Mizan to earn achievements!</p>
                </div>
            )}
        </div>
    );
}
