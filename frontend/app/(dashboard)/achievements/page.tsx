import { getAchievements, getStreak } from "@/data/achievement";
import { getUserServer } from "@/helper/session";
import Image from "next/image";
import { AppFeatureIllustration } from "@/components/illustrations/AppFeatureIllustration";

export const dynamic = 'force-dynamic';

export default async function AchievementsPage() {
    await getUserServer();
    const { achievements } = await getAchievements();
    const streak = await getStreak();

    const earned = achievements.filter(a => a.isEarned);
    const unearned = achievements.filter(a => !a.isEarned);

    return (
			<div className="space-y-6 lg:space-y-8" data-testid="achievements-page">
				<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
					<div className="space-y-2">
						<p className="eyebrow">Milestones</p>
						<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							Achievements
						</h1>
						<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Track your streak, unlock badges and celebrate the small wins.
						</p>
					</div>
				</header>

				<section className="glass-panel p-6 sm:p-8">
					<div className="flex flex-col gap-4 sm:flex-row sm:items-center">
						<div className="streak-gradient flex h-16 w-16 items-center justify-center rounded-2xl text-white shadow-lg">
							<i className="ri-fire-line text-3xl" />
						</div>
						<div className="flex-1">
							<h2 className="text-2xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{streak?.currentStreak || 0} day streak
							</h2>
							<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Longest: {streak?.longestStreak || 0} days
								{streak?.lastActivityDate && ` • Last logged: ${new Date(streak.lastActivityDate).toLocaleDateString()}`}
							</p>
						</div>
						<div className="sm:text-right">
							<p className="text-3xl font-bold text-burnt-peach-600 dark:text-burnt-peach-300">{earned.length}</p>
							<p className="text-xs uppercase tracking-[0.16em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Earned
							</p>
						</div>
					</div>
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
                                        <h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
                                            {achievement.name || "Achievement"}
                                        </h3>
										<p className="mt-1 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
                                            {achievement.description || "No description"}
                                        </p>
                                        <div className="flex items-center gap-3 mt-3">
											<span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-1 text-xs font-medium text-slate-600 dark:bg-slate-900/80 dark:text-slate-300">
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
                        ))}
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
