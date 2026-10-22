import Link from "next/link";
import { getPlanDayServer } from "@/lib/api/mealPlans.server";
import { entryQuantity, entryTitle, slotLabel } from "@/lib/planning/rules";
import LogPlanEntryButton from "./LogPlanEntryButton";

/**
 * The plan entries for a day on /today, with Log on each. Absent when no
 * active plan covers the date: the API answers 404 `no_active_plan` and the
 * page shows nothing for it.
 */
export default async function TodayPlan({ date }: { date: string }) {
  const view = await getPlanDayServer(date);
  if (!view || view.day.entryCount === 0) return null;
  const entries = view.day.slots.flatMap((slot) =>
    slot.entries.map((entry) => ({ slot: slot.slot, entry })),
  );
  return (
    <section aria-labelledby="plan-title" className="mb-10">
      <header className="log-section-header">
        <h2 id="plan-title">Today&apos;s plan</h2>
        <Link href={`/meal-plan/${view.planId}`} className="text-link text-sm">
          {view.planName || "Open plan"}
        </Link>
      </header>
      <ul>
        {entries.map(({ slot, entry }) => (
          <li key={entry.id} className="ledger-row">
            <span className="min-w-0 flex-1">
              <span className="block font-medium break-words">
                {entryTitle(entry)}
              </span>
              <span className="mt-0.5 block text-xs log-muted">
                {[slotLabel(slot), entryQuantity(entry)]
                  .filter(Boolean)
                  .join(" · ")}
              </span>
            </span>
            {entry.nutrition && (
              <span className="num whitespace-nowrap text-sm">
                {Math.round(entry.nutrition.calories).toLocaleString()}{" "}
                <span className="text-xs log-muted">kcal</span>
              </span>
            )}
            <LogPlanEntryButton planId={view.planId} entry={entry} />
          </li>
        ))}
      </ul>
      <p className="num mt-2 text-xs log-muted">
        {view.day.loggedCount} of {view.day.entryCount} logged
        {view.rules.eatingWindowEnd
          ? ` · eating window closes at ${view.rules.eatingWindowEnd.slice(0, 5)}`
          : ""}
      </p>
    </section>
  );
}
