"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ChevronRight } from "lucide-react";
import { ApiError } from "@/lib/api";
import { clientApi } from "@/lib/api.client";
import { getSession } from "@/lib/auth-client";
import {
  getPlanDay,
  logPlanEntry,
  type PlanDayView,
  type PlanEntry,
} from "@/lib/api/mealPlans";
import { listShoppingLists } from "@/lib/api/shoppingLists";
import { dateInTimeZone } from "@/lib/log-date";
import { formatMealType } from "@/lib/utils/mealType";
import { getErrorMessage } from "@/lib/toast";
import { useMainButton } from "@/lib/tg/useMainButton";
import { openExternal } from "@/lib/tg/webApp";
import type { components } from "@/types/api.generated";

type Schemas = components["schemas"];
type Diary = Schemas["FoodDiaryResult"];
type Goal = Schemas["UserGoalDto"];
type ListSummary = Schemas["ShoppingListSummaryDto"];

type Home = {
  name: string | null;
  date: string;
  diary: Diary | null;
  goal: Goal | null;
  plan: PlanDayView | null;
  list: ListSummary | null;
};

/** Each part is fetched on its own; one that fails or has nothing is left off the page. */
async function load(): Promise<Home> {
  const user = await getSession();
  const date = dateInTimeZone(user.timeZoneId || "UTC");
  const [diary, goal, plan, lists] = await Promise.allSettled([
    clientApi<Diary>(`/api/Meals?date=${date}`),
    clientApi<Goal | null>("/api/Goals", { expectedStatuses: [404] }),
    getPlanDay(date),
    listShoppingLists(1),
  ]);
  const value = <T,>(result: PromiseSettledResult<T>) =>
    result.status === "fulfilled" ? result.value : null;
  return {
    name: user.name?.split(" ")[0] || null,
    date,
    diary: value(diary),
    goal: value(goal),
    plan: value(plan),
    list: value(lists)?.items[0] ?? null,
  };
}

const entryName = (entry: PlanEntry) =>
  entry.recipeTitle || entry.foodName || entry.text || "Planned meal";

function nextEntry(plan: PlanDayView | null) {
  for (const slot of plan?.day.slots ?? [])
    for (const entry of slot.entries)
      if (!entry.logged) return { slot: slot.slot, entry };
  return null;
}

const longDate = (date: string) =>
  new Date(`${date}T12:00:00Z`).toLocaleDateString(undefined, {
    weekday: "long",
    day: "numeric",
    month: "long",
    timeZone: "UTC",
  });

/**
 * The Mini App's first page, /tg (design board TgHome.dc.html): today's
 * totals as the API counts them, the next meal on today's plan with a Log
 * button, and the ways to log, see the plan and the shopping list. Every link
 * stays inside /tg; the full app opens in the browser.
 */
export default function MiniAppHome() {
  const router = useRouter();
  const [home, setHome] = useState<Home | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [logging, setLogging] = useState(false);
  const [logged, setLogged] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setHome(await load());
    } catch (error) {
      setFailure(getErrorMessage(error, "Could not load today."));
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  useMainButton({ text: "Log a meal", onClick: () => router.push("/tg/log") });

  if (failure)
    return (
      <section className="log-page space-y-4">
        <p role="alert" className="text-base">
          {failure}
        </p>
        <button
          type="button"
          className="btn-secondary min-h-11"
          onClick={() => window.location.reload()}
        >
          Try again
        </button>
      </section>
    );
  if (!home)
    return (
      <p role="status" className="log-muted">
        Loading today…
      </p>
    );

  const next = nextEntry(home.plan);
  const totals = home.diary?.totals;
  const target = home.goal?.targetCalories;

  const logNext = async () => {
    if (!home.plan || !next) return;
    setLogging(true);
    try {
      await logPlanEntry(home.plan.planId, next.entry.id);
      setLogged(`${entryName(next.entry)} logged.`);
      await refresh();
    } catch (error) {
      setLogged(
        error instanceof ApiError
          ? getErrorMessage(error, "Could not log it.")
          : "Could not log it.",
      );
    } finally {
      setLogging(false);
    }
  };

  return (
    <div className="flex flex-col gap-6" data-testid="tg-home">
      <header className="flex flex-col gap-1">
        <p className="text-[10px] font-semibold tracking-[0.14em] uppercase log-muted">
          {longDate(home.date)}
        </p>
        <h1 className="text-2xl font-semibold">
          {home.name ? `Hi ${home.name}` : "Today"}
        </h1>
      </header>

      {totals && (
        <section
          aria-label="Today"
          className="border-y border-t-foreground border-b-border py-4"
        >
          <p className="flex items-baseline gap-2">
            <span className="num text-[34px] leading-none font-semibold">
              {Math.round(totals.calories).toLocaleString()}
            </span>
            <span className="text-sm log-muted">
              {target ? `kcal of ${target.toLocaleString()}` : "kcal"}
            </span>
          </p>
          <p className="num mt-3 flex flex-wrap gap-x-4 gap-y-1 text-sm">
            <span>Protein {Math.round(totals.protein)} g</span>
            <span>Carbs {Math.round(totals.carbs)} g</span>
            <span>Fat {Math.round(totals.fat)} g</span>
          </p>
        </section>
      )}

      {next && (
        <section
          aria-label="Next on your plan"
          className="flex items-center gap-3 border-b border-border pb-4"
        >
          <span className="min-w-0 flex-1">
            <span className="block text-[10px] font-semibold tracking-[0.14em] uppercase log-muted">
              Next on your plan · {formatMealType(next.slot)}
            </span>
            <span className="mt-1 block text-[17px] font-medium break-words">
              {entryName(next.entry)}
            </span>
          </span>
          <button
            type="button"
            className="btn-secondary min-h-11"
            disabled={logging}
            onClick={logNext}
            aria-label={`Log ${entryName(next.entry)}`}
          >
            {logging ? "Logging…" : "Log"}
          </button>
        </section>
      )}
      {logged && (
        <p role="status" className="-mt-3 text-sm">
          {logged}
        </p>
      )}

      <nav aria-label="Actions">
        <ul>
          <Action
            href="/tg/log"
            title="Log a meal"
            detail="Search, or write what you ate"
          />
          {home.plan && (
            <Action
              href="/tg/plan"
              title="Today's plan"
              detail={`${home.plan.day.loggedCount} of ${home.plan.day.entryCount} logged`}
            />
          )}
          {home.list && (
            <Action
              href="/tg/shopping-list"
              title="Shopping list"
              detail={`${home.list.totalItems} item${home.list.totalItems === 1 ? "" : "s"} · ${home.list.checkedItems} ticked`}
            />
          )}
        </ul>
      </nav>

      <button
        type="button"
        className="text-link min-h-11 self-start text-sm"
        onClick={() => openExternal(`${window.location.origin}/today`)}
      >
        Open in browser
      </button>
    </div>
  );
}

function Action({
  href,
  title,
  detail,
}: {
  href: string;
  title: string;
  detail: string;
}) {
  return (
    <li>
      <Link href={href} className="ledger-row transition-colors hover:bg-muted">
        <span className="min-w-0 flex-1">
          <span className="block text-[15px] font-medium">{title}</span>
          <span className="mt-0.5 block text-[13px] log-muted">{detail}</span>
        </span>
        <ChevronRight
          aria-hidden="true"
          size={18}
          strokeWidth={1.6}
          className="log-muted"
        />
      </Link>
    </li>
  );
}
