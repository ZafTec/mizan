"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ApiError } from "@/lib/api";
import { getSession } from "@/lib/auth-client";
import {
  getMealPlan,
  getPlanDay,
  type MealPlanDetail,
} from "@/lib/api/mealPlans";
import { dateInTimeZone, isLogDate } from "@/lib/log-date";
import { getErrorMessage } from "@/lib/toast";
import PlanDetail from "@/components/planning/PlanDetail";

type Loaded =
  | {
      kind: "plan";
      plan: MealPlanDetail;
      today: string;
      viewerId: string;
      initialDay: string;
    }
  | { kind: "message"; message: string; plans: boolean };

async function load(planId?: string, day?: string): Promise<Loaded> {
  try {
    const user = await getSession();
    const today = dateInTimeZone(user.timeZoneId || "UTC");
    let id = planId;
    let initialDay = isLogDate(day) ? day : undefined;
    if (!id) {
      // The API dates "today" in the person's zone and picks the plan.
      const view = await getPlanDay();
      id = view.planId;
      initialDay ??= view.date;
    }
    const plan = await getMealPlan(id);
    return {
      kind: "plan",
      plan,
      today,
      viewerId: user.id,
      initialDay: initialDay ?? today,
    };
  } catch (error) {
    if (error instanceof ApiError && error.status === 404)
      return {
        kind: "message",
        message: planId
          ? "This plan is not available any more."
          : "No active plan covers today.",
        plans: true,
      };
    return {
      kind: "message",
      message: getErrorMessage(error, "Could not load the plan."),
      plans: false,
    };
  }
}

/** The plan's day in the Mini App: /tg/plan?planId=&day=, or today's plan without them. */
export default function MiniAppPlan({
  planId,
  day,
}: {
  planId?: string;
  day?: string;
}) {
  const [loaded, setLoaded] = useState<Loaded | null>(null);

  useEffect(() => {
    let cancelled = false;
    void load(planId, day).then((next) => {
      if (!cancelled) setLoaded(next);
    });
    return () => {
      cancelled = true;
    };
  }, [planId, day]);

  if (!loaded)
    return (
      <p role="status" className="log-muted">
        Loading the plan…
      </p>
    );
  if (loaded.kind === "message")
    return (
      <section className="log-page space-y-4">
        <p className="text-base">{loaded.message}</p>
        {loaded.plans && (
          <Link href="/meal-plan" className="btn-secondary min-h-11">
            Meal plans
          </Link>
        )}
      </section>
    );
  return (
    <PlanDetail
      plan={loaded.plan}
      today={loaded.today}
      viewerId={loaded.viewerId}
      initialDay={loaded.initialDay}
      embedded
    />
  );
}
