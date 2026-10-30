import type { DayPlanCardData } from "@/lib/api/ai";
import PlanDayEntries from "@/components/planning/PlanDayEntries";
import { dayLabel } from "@/components/planning/PlanDetail";
import CardFrame from "./CardFrame";

/** `day_plan`: one day of a plan, in the shape the plan page shows it. */
export default function DayPlanCard({ data }: { data: DayPlanCardData }) {
  const name = data.planName || "Meal plan";
  const label = dayLabel(data.day);
  return (
    <CardFrame
      caption={name}
      label={`${name}, ${label}`}
      href={`/meal-plan/${data.planId}`}
      linkText="Open the plan"
    >
      <PlanDayEntries day={data.day} label={label} headingLevel={4} />
      {data.day.violations.length > 0 && (
        <ul aria-label="Rule breaks" className="space-y-1">
          {data.day.violations.map((violation, index) => (
            <li key={`${violation.rule}-${index}`} className="text-xs">
              {violation.message}
            </li>
          ))}
        </ul>
      )}
    </CardFrame>
  );
}
