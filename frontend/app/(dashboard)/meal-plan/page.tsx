import Link from "next/link";
import { ArrowRight, ChevronRight } from "lucide-react";
import { getUserServer } from "@/helper/session";
import { getMealPlans } from "@/data/mealPlan";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";
import { logger } from "@/lib/logger";

const mealLogger = logger.createModuleLogger("meal-plan-page");

export const dynamic = "force-dynamic";

type Plan = Awaited<ReturnType<typeof getMealPlans>>["mealPlans"][number];

const shortDate = (iso: string) =>
  new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
  });

/** A plan is "this week" when today falls inside its dates. */
function isCurrent(plan: Plan, today: string) {
  return plan.startDate <= today && today <= plan.endDate;
}

export default async function MealPlanPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const user = await getUserServer();
  const params = await searchParams;
  const { page, sortBy, sortOrder } = parseListParams(params);
  const baseUrl = buildListUrl("/meal-plan", { sortBy, sortOrder });

  let plans: Plan[] = [];
  let totalCount = 0;
  let totalPages = 0;
  let failed = false;
  try {
    const result = await getMealPlans(page, 20, sortBy ?? undefined, sortOrder);
    plans = result.mealPlans;
    totalCount = result.totalCount;
    totalPages = result.totalPages;
  } catch (error) {
    mealLogger.error("Failed to load meal plans", {
      error: error instanceof Error ? error.message : String(error),
      userID: user.id,
    });
    failed = true;
  }

  const today = new Date().toISOString().slice(0, 10);
  const current = plans.filter((plan) => isCurrent(plan, today));
  const mealsThisWeek = current.reduce(
    (sum, plan) => sum + plan.recipeCount,
    0,
  );

  return (
    <div className="log-page space-y-8" data-testid="meal-plan-page">
      <header className="log-page-header block!">
        <h1>Meal plans</h1>
        <p className="log-muted mt-2">
          A week of recipes your household can shop for.
        </p>
      </header>

      {failed ? (
        <div className="log-empty border-t border-foreground">
          <p>Your plans could not be loaded.</p>
          <p className="log-muted mt-2 text-sm">
            Try again in a moment. Nothing was changed.
          </p>
        </div>
      ) : (
        <>
          {plans.length > 0 && (
            <section aria-label="Plan summary" className="facts-strip">
              <div>
                <p className="text-sm log-muted">Plans</p>
                <p className="num nutrition-value">{totalCount}</p>
              </div>
              <div>
                <p className="text-sm log-muted">This week</p>
                <p className="num nutrition-value">
                  {mealsThisWeek}
                  <span className="text-sm font-normal log-muted"> meals</span>
                </p>
              </div>
              <div>
                <p className="text-sm log-muted">Shopping</p>
                <p className="nutrition-value">
                  <Link
                    href="/meal-plan/shopping-list"
                    className="text-link inline-flex items-center gap-1 text-base font-medium"
                  >
                    Open list{" "}
                    <ArrowRight
                      aria-hidden="true"
                      size={15}
                      strokeWidth={1.7}
                    />
                  </Link>
                </p>
              </div>
            </section>
          )}

          <section aria-labelledby="plans-title">
            <header className="log-section-header">
              <h2 id="plans-title">Your plans</h2>
              {plans.length > 0 && (
                <Link
                  href="/meal-plan/shopping-list"
                  className="text-link text-sm"
                >
                  Shopping list
                </Link>
              )}
            </header>
            {plans.length === 0 ? (
              <div className="log-empty">
                <p>No plans yet.</p>
                <p className="log-muted mt-2 text-sm">
                  Pick a week and add recipes to its days. The shopping list
                  writes itself from what you add.
                </p>
              </div>
            ) : (
              <ul>
                {plans.map((plan) => (
                  <li key={plan.id}>
                    <Link
                      href={`/meal-plan/${plan.id}`}
                      className="ledger-row transition-colors hover:bg-muted"
                    >
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium">
                          {plan.name || "Weekly plan"}
                        </span>
                        <span className="mt-0.5 block text-sm log-muted">
                          {shortDate(plan.startDate)} –{" "}
                          {shortDate(plan.endDate)}
                          {isCurrent(plan, today) ? " · this week" : ""}
                        </span>
                      </span>
                      <span className="num whitespace-nowrap text-sm log-muted">
                        <span className="text-base font-semibold text-foreground">
                          {plan.recipeCount}
                        </span>{" "}
                        meals
                      </span>
                      <ChevronRight
                        aria-hidden="true"
                        size={17}
                        strokeWidth={1.7}
                        className="log-muted"
                      />
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <Link
            href="/meal-plan/create"
            className="btn-primary min-h-11 w-full sm:w-auto"
          >
            New plan
          </Link>

          {totalPages > 1 && (
            <Pagination
              currentPage={page}
              totalPages={totalPages}
              totalCount={totalCount}
              pageSize={20}
              baseUrl={baseUrl}
            />
          )}
        </>
      )}
    </div>
  );
}
