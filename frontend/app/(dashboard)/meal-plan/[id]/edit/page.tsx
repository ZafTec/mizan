import { notFound, redirect } from "next/navigation";
import { getUserServer } from "@/helper/session";
import { dateInTimeZone } from "@/lib/log-date";
import { serverApi } from "@/lib/api.server";
import { getMealPlanServer } from "@/lib/api/mealPlans.server";
import PlanEditor from "@/components/planning/PlanEditor";
import type { components } from "@/types/api.generated";

export const dynamic = "force-dynamic";

/** The rules hold food ids; the form shows names, read once here. */
async function foodNames(ids: string[]): Promise<Record<string, string>> {
  const names: Record<string, string> = {};
  await Promise.all(
    [...new Set(ids)].map(async (id) => {
      try {
        const food = await serverApi<components["schemas"]["FoodDto"]>(
          `/api/Foods/${id}`,
        );
        if (food?.name) names[id] = food.name;
      } catch {
        // The id still shows as "Food"; the rule is kept as it is.
      }
    }),
  );
  return names;
}

export default async function EditMealPlanPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const [{ id }, user] = await Promise.all([params, getUserServer()]);
  const plan = await getMealPlanServer(id);
  if (!plan) notFound();
  if (!plan.canEdit) redirect(`/meal-plan/${id}`);
  const names = await foodNames([
    ...plan.rules.allowedFoodIds,
    ...plan.rules.forbiddenFoodIds,
  ]);
  return (
    <PlanEditor
      plan={plan}
      today={dateInTimeZone(user.timeZoneId || "UTC")}
      foodNames={names}
    />
  );
}
