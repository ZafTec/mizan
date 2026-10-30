import Image from "next/image";
import { getAchievements, getStreak } from "@/data/achievement";
import { getUserServer } from "@/helper/session";
import { EarnedAchievementRow, LockedAchievementRow } from "./AchievementRow";

export const dynamic = "force-dynamic";

function criteriaLabel(
  criteriaType: string | null | undefined,
  threshold: number,
): string {
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

const NEXT_LEVEL: Record<string, string> = {
  Rookie: "Bronze",
  Bronze: "Silver",
  Silver: "Gold",
  Gold: "Platinum",
};

/**
 * Badges are small PNGs shown as they are: the defaults ship under /badges
 * (design/achievement-assets.md) and admin uploads come from the media host.
 * The API returns the defaults as absolute URLs on the app origin, so the
 * images stay out of the optimizer, which would otherwise need that host
 * allowlisted and would fetch the app through its own public address.
 * Rookie has no level mark and Platinum has not been rendered yet.
 */
const LEVEL_MARK: Record<string, string> = {
  Bronze: "/badges/128/level-bronze.png",
  Silver: "/badges/128/level-silver.png",
  Gold: "/badges/128/level-gold.png",
};

type Achievement = Awaited<
  ReturnType<typeof getAchievements>
>["achievements"][number] & {
  threshold?: number;
  progress?: number;
  criteriaType?: string | null;
};

function lockedDetail(achievement: Achievement) {
  const threshold = achievement.threshold ?? 0;
  const progress = Math.min(achievement.progress ?? 0, threshold);
  return [
    achievement.description ||
      criteriaLabel(achievement.criteriaType, threshold),
    threshold > 0 ? `${progress} / ${threshold}` : null,
  ];
}

export default async function AchievementsPage() {
  await getUserServer();
  const [
    { achievements, totalPoints, earnedCount, levelName, nextLevelAt },
    streak,
  ] = await Promise.all([getAchievements(), getStreak()]);
  const list = achievements as Achievement[];
  const earned = list.filter((a) => a.isEarned);
  const unearned = list.filter((a) => !a.isEarned);
  const toNext =
    nextLevelAt != null
      ? `${nextLevelAt - totalPoints} to ${NEXT_LEVEL[levelName] ?? "the next level"}`
      : "top level";

  return (
    <div className="log-page space-y-8" data-testid="achievements-page">
      <header className="log-page-header block!">
        <h1>Achievements</h1>
        <p className="text-muted-foreground mt-2">Your streak and what it has earned.</p>
      </header>

      <section aria-label="Streak and level" className="facts-strip">
        <div>
          <p className="text-sm text-muted-foreground">Streak</p>
          <p className="tabular-nums nutrition-value">
            {streak?.currentStreak || 0}
            <span className="text-sm font-normal text-muted-foreground"> days</span>
          </p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Longest</p>
          <p className="tabular-nums nutrition-value">
            {streak?.longestStreak || 0}
            <span className="text-sm font-normal text-muted-foreground"> days</span>
          </p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Level</p>
          <p className="nutrition-value facts-level flex items-center gap-1.5">
            {LEVEL_MARK[levelName] && (
              <Image
                src={LEVEL_MARK[levelName]}
                alt=""
                width={28}
                height={28}
                unoptimized
                className="h-5 w-5 shrink-0"
              />
            )}
            {levelName}
          </p>
        </div>
      </section>
      <p className="tabular-nums -mt-4 text-sm text-muted-foreground">
        {totalPoints} points · {toNext}
      </p>

      <section aria-labelledby="earned-title">
        <header className="log-section-header">
          <h2 id="earned-title">Earned</h2>
          <span className="tabular-nums text-sm text-muted-foreground">{earnedCount}</span>
        </header>
        {earned.length === 0 ? (
          <div className="log-empty">
            <p>Nothing earned yet.</p>
            <p className="text-muted-foreground mt-2 text-sm">
              The first one is a single logged meal. The rest are listed below
              so you know what counts.
            </p>
          </div>
        ) : (
          <ul>
            {earned.map((achievement) => (
              <EarnedAchievementRow
                key={achievement.id}
                achievement={achievement}
              />
            ))}
          </ul>
        )}
      </section>

      {unearned.length > 0 && (
        <section aria-labelledby="to-earn-title">
          <header className="log-section-header">
            <h2 id="to-earn-title">Still to earn</h2>
            <span className="tabular-nums text-sm text-muted-foreground">{unearned.length}</span>
          </header>
          <ul>
            {unearned.map((achievement) => (
              <LockedAchievementRow
                key={achievement.id}
                achievement={achievement}
                detail={lockedDetail(achievement)}
              />
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
