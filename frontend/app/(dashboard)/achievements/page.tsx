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
						<p className="mt-1 text-slate-500 dark:text-slate-400">Track your progress and earn rewards</p>
					</div>
				</div>

			<div className="card p-6 border border-accent-200 bg-slate-50/90 dark:bg-slate-900/60">
					<div className="flex items-center gap-4">
						<div className="w-16 h-16 rounded-2xl bg-accent-600 flex items-center justify-center shadow-lg">
                        <i className="ri-fire-line text-3xl text-white" />
                    </div>
                    <div className="flex-1">
                        <h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100">
                            {streak?.currentStreak || 0} Day Streak
                        </h2>
						<p className="text-slate-600 dark:text-slate-300">
                            Longest: {streak?.longestStreak || 0} days
                            {streak?.lastActivityDate && ` • Last logged: ${new Date(streak.lastActivityDate).toLocaleDateString()}`}
                        </p>
                    </div>
                    <div className="text-center">
                        <div className="text-3xl font-bold text-accent-600">{earned.length}</div>
						<div className="text-sm text-slate-600 dark:text-slate-300">Earned</div>
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
							className="card-hover p-5 border border-brand-200 bg-slate-50/90 dark:bg-slate-900/60"
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
                                        <h3 className="font-semibold text-slate-900 dark:text-slate-100">
                                            {achievement.name || "Achievement"}
                                        </h3>
										<p className="mt-1 text-sm text-slate-600 dark:text-slate-300">
                                            {achievement.description || "No description"}
                                        </p>
                                        <div className="flex items-center gap-3 mt-3">
											<span className="inline-flex items-center gap-1 rounded-full bg-brand-100 px-2 py-1 text-xs font-medium text-brand-700 dark:bg-brand-950/60 dark:text-brand-200">
                                                <i className="ri-star-line" />
                                                {achievement.points} points
                                            </span>
                                            {achievement.category && (
												<span className="text-xs text-slate-500 dark:text-slate-400">
                                                    {achievement.category}
                                                </span>
                                            )}
                                            {achievement.earnedAt && (
												<span className="text-xs text-slate-500 dark:text-slate-400">
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
								<div className="w-12 h-12 rounded-2xl bg-slate-400 flex items-center justify-center shadow-lg shrink-0 dark:bg-slate-700">
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
                                        <h3 className="font-semibold text-slate-900 dark:text-slate-100">
                                            {achievement.name || "Achievement"}
                                        </h3>
										<p className="mt-1 text-sm text-slate-600 dark:text-slate-300">
                                            {achievement.description || "No description"}
                                        </p>
                                        <div className="flex items-center gap-3 mt-3">
											<span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-1 text-xs font-medium text-slate-600 dark:bg-slate-900/80 dark:text-slate-300">
                                                <i className="ri-star-line" />
                                                {achievement.points} points
                                            </span>
                                            {achievement.category && (
												<span className="text-xs text-slate-500 dark:text-slate-400">
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
				<div className="card p-16 text-center flex flex-col items-center">
                    <div className="relative w-48 h-48 mb-6 opacity-90 drop-shadow-md">
                        <Image src="/assets/gamification-badge.svg" alt="No achievements yet" fill className="object-contain" />
                    </div>
                    <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">No achievements yet</h3>
					<p className="text-slate-500 dark:text-slate-400">Start using MacroChef to earn achievements!</p>
				</div>
			)}
        </div>
    );
}
