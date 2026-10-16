import Image from "next/image";
import { getAchievements, getStreak } from "@/data/achievement";
import { getUserServer } from "@/helper/session";

export const dynamic = "force-dynamic";

function criteriaLabel(criteriaType: string | null | undefined, threshold: number): string {
	switch (criteriaType) {
		case "meals_logged":
			return `Log ${threshold} meal${threshold === 1 ? "" : "s"}`;
		case "recipes_created":
			return `Create ${threshold} recipe${threshold === 1 ? "" : "s"}`;
		case "workouts_logged":
			return `Log ${threshold} workout${threshold === 1 ? "" : "s"}`;
		case "body_measurements_logged":
			return `Record ${threshold} body measurement${threshold === 1 ? "" : "s"}`;
		case "goal_progress_logged":
			return `Record ${threshold} goal progress entr${threshold === 1 ? "y" : "ies"}`;
		case "streak_nutrition":
			return `Hit a ${threshold}-day nutrition streak`;
		case "streak_workout":
			return `Hit a ${threshold}-day workout streak`;
		case "points_total":
			return `Earn ${threshold} achievement points`;
		default:
			return "Unlock criteria hidden";
	}
}

const NEXT_LEVEL: Record<string, string> = { Rookie: "Bronze", Bronze: "Silver", Silver: "Gold", Gold: "Platinum" };

type Achievement = Awaited<ReturnType<typeof getAchievements>>["achievements"][number] & {
	threshold?: number;
	progress?: number;
	criteriaType?: string | null;
};

function Row({ achievement, earned }: { achievement: Achievement; earned: boolean }) {
	const threshold = achievement.threshold ?? 0;
	const progress = Math.min(achievement.progress ?? 0, threshold);
	const detail = earned
		? [achievement.category, achievement.earnedAt ? `Earned ${new Date(achievement.earnedAt).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" })}` : null]
		: [achievement.description || criteriaLabel(achievement.criteriaType, threshold), threshold > 0 ? `${progress} / ${threshold}` : null];
	return (
		<li className="ledger-row">
			{achievement.iconUrl && (
				<Image src={achievement.iconUrl} alt="" width={28} height={28} className={`h-7 w-7 shrink-0 ${earned ? "" : "opacity-50 grayscale"}`} />
			)}
			<span className="min-w-0 flex-1">
				<span className="block font-medium">{achievement.name || "Achievement"}</span>
				<span className="num mt-0.5 block text-sm log-muted">{detail.filter(Boolean).join(" · ")}</span>
			</span>
			<span className="num whitespace-nowrap text-sm log-muted">
				<span className="text-base font-semibold text-foreground">{achievement.points}</span> pts
			</span>
		</li>
	);
}

export default async function AchievementsPage() {
	await getUserServer();
	const [{ achievements, totalPoints, earnedCount, levelName, nextLevelAt }, streak] = await Promise.all([getAchievements(), getStreak()]);
	const list = achievements as Achievement[];
	const earned = list.filter((a) => a.isEarned);
	const unearned = list.filter((a) => !a.isEarned);
	const toNext = nextLevelAt != null ? `${nextLevelAt - totalPoints} to ${NEXT_LEVEL[levelName] ?? "the next level"}` : "top level";

	return (
		<div className="log-page space-y-8" data-testid="achievements-page">
			<header className="log-page-header block!">
				<h1>Achievements</h1>
				<p className="log-muted mt-2">Your streak and what it has earned.</p>
			</header>

			<section aria-label="Streak and level" className="facts-strip">
				<div>
					<p className="text-sm log-muted">Streak</p>
					<p className="num nutrition-value">
						{streak?.currentStreak || 0}
						<span className="text-sm font-normal log-muted"> days</span>
					</p>
				</div>
				<div>
					<p className="text-sm log-muted">Longest</p>
					<p className="num nutrition-value">
						{streak?.longestStreak || 0}
						<span className="text-sm font-normal log-muted"> days</span>
					</p>
				</div>
				<div>
					<p className="text-sm log-muted">Level</p>
					<p className="nutrition-value">{levelName}</p>
				</div>
			</section>
			<p className="num -mt-4 text-sm log-muted">
				{totalPoints} points · {toNext}
			</p>

			<section aria-labelledby="earned-title">
				<header className="log-section-header">
					<h2 id="earned-title">Earned</h2>
					<span className="num text-sm log-muted">{earnedCount}</span>
				</header>
				{earned.length === 0 ? (
					<div className="log-empty">
						<p>Nothing earned yet.</p>
						<p className="log-muted mt-2 text-sm">The first one is a single logged meal. The rest are listed below so you know what counts.</p>
					</div>
				) : (
					<ul>
						{earned.map((achievement) => (
							<Row key={achievement.id} achievement={achievement} earned />
						))}
					</ul>
				)}
			</section>

			{unearned.length > 0 && (
				<section aria-labelledby="to-earn-title">
					<header className="log-section-header">
						<h2 id="to-earn-title">Still to earn</h2>
						<span className="num text-sm log-muted">{unearned.length}</span>
					</header>
					<ul>
						{unearned.map((achievement) => (
							<Row key={achievement.id} achievement={achievement} earned={false} />
						))}
					</ul>
				</section>
			)}
		</div>
	);
}
