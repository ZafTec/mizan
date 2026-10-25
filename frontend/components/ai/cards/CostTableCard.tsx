import type { CostTableCardData } from "@/lib/api/ai";
import PlanCostTable from "@/components/planning/PlanCostTable";
import CardFrame from "./CardFrame";

/** `cost_table`: what a plan costs, line by line, with the unpriced lines named. */
export default function CostTableCard({ data }: { data: CostTableCardData }) {
  return (
    <CardFrame
      caption={`Cost · ${data.cost.regionCode}`}
      label="Plan cost"
      href={`/meal-plan/${data.planId}`}
      linkText="Open the plan"
    >
      <PlanCostTable cost={data.cost} bare />
    </CardFrame>
  );
}
