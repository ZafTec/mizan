import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { getUserServer } from "@/helper/session";
import { dateInTimeZone, shiftDate } from "@/lib/log-date";
import {
  getMealPlanServer,
  listMealPlansServer,
} from "@/lib/api/mealPlans.server";
import { listShoppingListsServer } from "@/lib/api/shoppingLists.server";
import type { MealPlanSummary } from "@/lib/api/mealPlans";
import { planDates } from "@/lib/planning/rules";
import { DRAFTED_BY_ASSISTANT, isGenerated } from "@/lib/planning/draft";
import DraftActions from "@/components/planning/DraftActions";
import { logger } from "@/lib/logger";

const log = logger.createModuleLogger("meal-plan-page");

export const dynamic = "force-dynamic";
export const metadata = { title: "Meal plans · Mizan" };

function covers(plan: MealPlanSummary, date: string) {
  return (
    !!plan.startDate &&
    !!plan.endDate &&
    plan.startDate <= date &&
    date <= plan.endDate
  );
}

function PlanRow({
  plan,
  today,
  viewerId,
  children,
}: {
  plan: MealPlanSummary;
  today: string;
  viewerId: string;
  children?: React.ReactNode;
}) {
  const name = plan.name || "Meal plan";
  const notes = [
    planDates(plan),
    covers(plan, today) ? "this week" : null,
    plan.authoredByUserId && plan.authoredByUserId !== viewerId
      ? `from ${plan.authoredByName ?? "your coach"}`
      : null,
    plan.authoredByUserId === viewerId && plan.userId !== viewerId
      ? "draft for a client"
      : null,
    plan.householdId ? "household" : null,
    isGenerated(plan) ? DRAFTED_BY_ASSISTANT : null,
  ].filter(Boolean);
  return (
    <li className="ledger-row">
      <Link
        href={`/meal-plan/${plan.id}`}
        className="flex min-w-0 flex-1 items-center gap-3"
      >
        <span className="min-w-0 flex-1">
          <span className="block font-medium">{name}</span>
          <span className="mt-0.5 block text-sm text-muted-foreground">
            {notes.join(" · ")}
          </span>
        </span>
        <span className="tabular-nums whitespace-nowrap text-sm text-muted-foreground">
          <span className="text-base font-semibold text-foreground">
            {plan.entryCount}
          </span>{" "}
          {plan.entryCount === 1 ? "entry" : "entries"}
        </span>
        {!children && (
          <ChevronRight
            aria-hidden="true"
            size={17}
            strokeWidth={1.7}
            className="text-muted-foreground"
          />
        )}
      </Link>
      {children}
    </li>
  );
}

export default async function MealPlanPage() {
  const user = await getUserServer();
  const today = dateInTimeZone(user.timeZoneId || "UTC");
  let plans: MealPlanSummary[] = [];
  let listCount = 0;
  let failed = false;
  try {
    const [planPage, listPage] = await Promise.all([
      listMealPlansServer({ PageSize: 100 }),
      listShoppingListsServer(1),
    ]);
    plans = planPage.items;
    listCount = listPage.totalCount;
  } catch (error) {
    log.error("Failed to load meal plans", {
      error: error instanceof Error ? error.message : String(error),
    });
    failed = true;
  }

  const active = plans
    .filter((plan) => plan.status === "Active")
    .sort((a, b) => Number(covers(b, today)) - Number(covers(a, today)));
  const drafts = plans.filter((plan) => plan.status === "Draft");
  const past = plans.filter((plan) => plan.status === "Archived");
  const current = active.find((plan) => covers(plan, today)) ?? null;
  const weekEnd = shiftDate(today, 6);
  let entriesThisWeek = 0;
  if (current) {
    const detail = await getMealPlanServer(current.id).catch(() => null);
    entriesThisWeek =
      detail?.days
        .filter((day) => day.date && day.date >= today && day.date <= weekEnd)
        .reduce((sum, day) => sum + day.entryCount, 0) ?? 0;
  }

  return (
    <div className="log-page space-y-8" data-testid="meal-plan-page">
      <header className="log-page-header block!">
        <h1>Meal plans</h1>
        <p className="text-muted-foreground mt-2">
          What you plan to eat, and the list it needs.
        </p>
      </header>

      {failed ? (
        <p className="log-empty">Your plans could not be loaded.</p>
      ) : (
        <>
          {plans.length > 0 && (
            <section aria-label="Plan summary" className="facts-strip">
              <div>
                <p className="text-sm text-muted-foreground">Active plan</p>
                <p className="nutrition-value truncate text-xl!">
                  {current ? current.name || "Meal plan" : "None"}
                </p>
              </div>
              <div>
                <p className="text-sm text-muted-foreground">This week</p>
                <p className="tabular-nums nutrition-value">
                  {entriesThisWeek}
                  <span className="text-sm font-normal text-muted-foreground">
                    {" "}
                    {entriesThisWeek === 1 ? "entry" : "entries"}
                  </span>
                </p>
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Lists</p>
                <p className="tabular-nums nutrition-value">
                  <Link href="/meal-plan/shopping-list" className="text-link">
                    {listCount}
                  </Link>
                </p>
              </div>
            </section>
          )}

          {plans.length === 0 ? (
            <p className="log-empty">No plans yet.</p>
          ) : (
            <>
              {active.length > 0 && (
                <section aria-labelledby="active-title">
                  <header className="log-section-header">
                    <h2 id="active-title">Active</h2>
                  </header>
                  <ul>
                    {active.map((plan) => (
                      <PlanRow
                        key={plan.id}
                        plan={plan}
                        today={today}
                        viewerId={user.id}
                      />
                    ))}
                  </ul>
                </section>
              )}
              {drafts.length > 0 && (
                <section aria-labelledby="drafts-title">
                  <header className="log-section-header">
                    <h2 id="drafts-title">
                      {drafts.some(
                        (plan) =>
                          plan.authoredByUserId &&
                          plan.authoredByUserId !== user.id,
                      )
                        ? "From your coach"
                        : "Drafts"}
                    </h2>
                  </header>
                  <ul>
                    {drafts.map((plan) => (
                      <PlanRow
                        key={plan.id}
                        plan={plan}
                        today={today}
                        viewerId={user.id}
                      >
                        {(plan.authoredByUserId &&
                          plan.authoredByUserId !== user.id) ||
                        isGenerated(plan) ? (
                          <DraftActions
                            planId={plan.id}
                            planName={plan.name || "Meal plan"}
                          />
                        ) : undefined}
                      </PlanRow>
                    ))}
                  </ul>
                </section>
              )}
              {past.length > 0 && (
                <section aria-labelledby="past-title">
                  <header className="log-section-header">
                    <h2 id="past-title">Past plans</h2>
                  </header>
                  <ul>
                    {past.map((plan) => (
                      <PlanRow
                        key={plan.id}
                        plan={plan}
                        today={today}
                        viewerId={user.id}
                      />
                    ))}
                  </ul>
                </section>
              )}
            </>
          )}

          <Link
            href="/meal-plan/new"
            className="btn-primary min-h-11 w-full sm:w-auto"
          >
            New plan
          </Link>
        </>
      )}
    </div>
  );
}
