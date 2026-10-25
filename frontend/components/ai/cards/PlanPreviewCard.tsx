import type { PlanPreviewCardData } from "@/lib/api/ai";
import { formatPlanDate, STATUS_LABELS } from "@/lib/planning/rules";
import { formatMoney } from "@/lib/prices/format";
import CardFrame from "./CardFrame";

/** `plan_preview`: a plan at a glance, with what breaks its rules. */
export default function PlanPreviewCard({
  data,
}: {
  data: PlanPreviewCardData;
}) {
  const name = data.name || "Meal plan";
  const facts = [
    STATUS_LABELS[data.status] ?? data.status,
    data.startDate
      ? `from ${formatPlanDate(data.startDate)}, ${data.durationDays} days`
      : `${data.durationDays} days`,
    `${data.entryCount} ${data.entryCount === 1 ? "entry" : "entries"}`,
    data.unresolvedCount > 0 ? `${data.unresolvedCount} to review` : null,
  ].filter(Boolean);
  return (
    <CardFrame
      caption="Plan"
      label={`Plan ${name}`}
      href={`/meal-plan/${data.planId}`}
      linkText="Open the plan"
    >
      <div>
        <p className="font-semibold break-words">{name}</p>
        <p className="mt-0.5 text-xs log-muted">{facts.join(" · ")}</p>
      </div>
      {data.weeklyCost != null && data.currency && (
        <p className="num">
          <span className="log-muted">Cost a week </span>
          {formatMoney(data.weeklyCost, data.currency)}
          {data.budgetCeiling != null && (
            <span className="log-muted">
              {" "}
              of {formatMoney(data.budgetCeiling, data.currency)} budget
            </span>
          )}
        </p>
      )}
      {data.violations.length > 0 && (
        <ul aria-label="Rule breaks" className="space-y-1">
          {data.violations.map((violation, index) => (
            <li key={`${violation.rule}-${index}`} className="text-xs">
              {violation.message}
            </li>
          ))}
        </ul>
      )}
    </CardFrame>
  );
}
