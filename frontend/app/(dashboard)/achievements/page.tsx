import { getAchievements, getStreak } from "@/data/achievement";
import { getUserServer } from "@/helper/session";
import Image from "next/image";

export const dynamic = 'force-dynamic';

export default async function AchievementsPage() {
    await getUserServer();
    const { achievements } = await getAchievements();
    const streak = await getStreak();

    const earned = achievements.filter(a => a.isEarned);
    const unearned = achievements.filter(a => !a.isEarned);

    return (
        <div className="space-y-8" data-testid="achievements-page">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Achievements</h1>
                    <p className="text-slate-500 mt-1">Track your progress and earn rewards</p>
                </div>
            </div>

            <div className="card p-6 border-2 border-accent-200 bg-linear-to-br from-accent-50 to-orange-50">
                <div className="flex items-center gap-4">
                    <div className="w-16 h-16 rounded-2xl bg-linear-to-br from-accent-400 to-accent-600 flex items-center justify-center shadow-lg">
                        <i className="ri-fire-line text-3xl text-white" />
                    </div>
                    <div className="flex-1">
                        <h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100">
                            {streak?.currentStreak || 0} Day Streak
                        </h2>
                        <p className="text-slate-600">
                            Longest: {streak?.longestStreak || 0} days
                            {streak?.lastLogDate && ` • Last logged: ${new Date(streak.lastLogDate).toLocaleDateString()}`}
                        </p>
                    </div>
                    <div className="text-center">
                        <div className="text-3xl font-bold text-accent-600">{earned.length}</div>
                        <div className="text-sm text-slate-600">Earned</div>
                    </div>
                </div>
            </div>

            {earned.length > 0 && (
                <div className="card p-6">
                    <h2 className="section-title mb-6">Earned Achievements</h2>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        {earned.map((achievement) => (
                            <div
                                key={achievement.id}
                                className="card-hover p-5 border-2 border-brand-500 bg-linear-to-br from-brand-50 to-green-50"
                            >
                                <div className="flex items-start gap-4">
                                    <div className="w-12 h-12 rounded-2xl bg-linear-to-br from-brand-400 to-brand-600 flex items-center justify-center shadow-lg shrink-0">
                                        {achievement.iconUrl ? (
                                            <Image
                                                src={achievement.iconUrl}
                                                alt={achievement.name || "Achievement"}
                                                width={32}
                                                height={32}
                                                className="w-8 h-8"
                                                unoptimized
                                            />
                                        ) : (
                                            <i className="ri-trophy-line text-xl text-white" />
                                        )}
                                    </div>
                                    <div className="flex-1">
                                        <h3 className="font-semibold text-slate-900 dark:text-slate-100">
                                            {achievement.name || "Achievement"}
                                        </h3>
                                        <p className="text-sm text-slate-600 mt-1">
                                            {achievement.description || "No description"}
                                        </p>
                                        <div className="flex items-center gap-3 mt-3">
                                            <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-brand-100 text-brand-700 text-xs font-medium">
                                                <i className="ri-star-line" />
                                                {achievement.points} points
                                            </span>
                                            {achievement.category && (
                                                <span className="text-xs text-slate-500">
                                                    {achievement.category}
                                                </span>
                                            )}
                                            {achievement.earnedAt && (
                                                <span className="text-xs text-slate-500">
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
                        {unearned.map((achievement) => (
                            <div
                                key={achievement.id}
                                className="card-hover p-5 opacity-60 hover:opacity-80 transition-opacity"
                            >
                                <div className="flex items-start gap-4">
                                    <div className="w-12 h-12 rounded-2xl bg-linear-to-br from-slate-300 to-slate-400 flex items-center justify-center shadow-lg shrink-0">
                                        {achievement.iconUrl ? (
                                            <Image
                                                src={achievement.iconUrl}
                                                alt={achievement.name || "Achievement"}
                                                width={32}
                                                height={32}
                                                className="w-8 h-8 grayscale"
                                                unoptimized
                                            />
                                        ) : (
                                            <i className="ri-trophy-line text-xl text-white" />
                                        )}
                                    </div>
                                    <div className="flex-1">
                                        <h3 className="font-semibold text-slate-900 dark:text-slate-100">
                                            {achievement.name || "Achievement"}
                                        </h3>
                                        <p className="text-sm text-slate-600 mt-1">
                                            {achievement.description || "No description"}
                                        </p>
                                        <div className="flex items-center gap-3 mt-3">
                                            <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-slate-100 text-slate-600 text-xs font-medium">
                                                <i className="ri-star-line" />
                                                {achievement.points} points
                                            </span>
                                            {achievement.category && (
                                                <span className="text-xs text-slate-500">
                                                    {achievement.category}
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

            {achievements.length === 0 && (
                <div className="card p-16 text-center">
                    <div className="w-16 h-16 rounded-2xl bg-slate-100 flex items-center justify-center mx-auto mb-4">
                        <i className="ri-trophy-line text-3xl text-slate-400" />
                    </div>
                    <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">No achievements yet</h3>
                    <p className="text-slate-500">Start using MacroChef to earn achievements!</p>
                </div>
            )}
        </div>
    );
}
