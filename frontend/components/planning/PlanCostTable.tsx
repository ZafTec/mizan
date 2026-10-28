import Link from "next/link";
import type { PlanCost } from "@/lib/api/mealPlans";
import {
  confidenceLabel,
  formatCoverage,
  formatMoney,
  itemCount,
} from "@/lib/prices/format";

/**
 * A plan's cost line by line, as the API priced it in the person's region:
 * the week's figure against the budget, how much carries a price, and each
 * shopping line with its cost or the reason it has none. Every number is the
 * API's; this only prints them.
 */
export default function PlanCostTable({
  cost,
  title = "Cost",
  headingLevel = 2,
  bare = false,
}: {
  cost: PlanCost;
  title?: string;
  headingLevel?: 2 | 3;
  /** Without its own heading, inside a frame that names it. */
  bare?: boolean;
}) {
  const Heading = `h${headingLevel}` as const;
  const Frame = bare ? "div" : "section";
  return (
    <Frame aria-label={bare ? undefined : title} data-testid="plan-cost-table">
      {!bare && (
        <header className="log-section-header">
          <Heading>{title}</Heading>
          <span className="tabular-nums text-sm text-muted-foreground">{cost.regionCode}</span>
        </header>
      )}
      <dl className="facts-strip">
        <div>
          <dt className="text-sm text-muted-foreground">Cost a week</dt>
          <dd className="tabular-nums nutrition-value text-base! break-words whitespace-normal! sm:text-xl!">
            {formatMoney(cost.weeklyTotal, cost.currency)}
          </dd>
          {cost.weeklyBudget != null && (
            <dd className="tabular-nums mt-1 text-sm text-muted-foreground">
              of {formatMoney(cost.weeklyBudget, cost.currency)} budget
            </dd>
          )}
        </div>
        <div>
          <dt className="text-sm text-muted-foreground">Whole plan</dt>
          <dd className="tabular-nums nutrition-value text-base! break-words whitespace-normal! sm:text-xl!">
            {formatMoney(cost.total, cost.currency)}
          </dd>
        </div>
        <div>
          <dt className="text-sm text-muted-foreground">Priced</dt>
          <dd className="tabular-nums nutrition-value">
            {formatCoverage(cost.coverage)}
          </dd>
          <dd className="tabular-nums mt-1 text-sm text-muted-foreground">
            {cost.pricedItems} of {itemCount(cost.totalItems)}
            {cost.lowestConfidence
              ? ` · ${confidenceLabel(cost.lowestConfidence)}`
              : ""}
          </dd>
        </div>
      </dl>
      {cost.items.length > 0 && (
        <ul aria-label="Cost by item">
          {cost.items.map((item, index) => (
            <li
              key={`${item.foodId ?? item.name}-${index}`}
              className="ledger-row text-sm"
            >
              <span className="min-w-0 flex-1">
                <span className="block font-medium break-words">
                  {item.foodId ? (
                    <Link
                      href={`/ingredients/${item.foodId}`}
                      className="text-link"
                    >
                      {item.name}
                    </Link>
                  ) : (
                    item.name
                  )}
                </span>
                {(item.grams != null || item.reason) && (
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    {[
                      item.grams != null
                        ? `${Math.round(item.grams).toLocaleString()} g`
                        : null,
                      item.cost == null ? item.reason : null,
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                )}
              </span>
              <span className="tabular-nums whitespace-nowrap">
                {item.cost != null ? (
                  formatMoney(item.cost, cost.currency)
                ) : (
                  <span className="text-muted-foreground">No price</span>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Frame>
  );
}
