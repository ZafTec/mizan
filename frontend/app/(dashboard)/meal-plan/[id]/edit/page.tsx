import { notFound, redirect } from "next/navigation";
import { getUserServer } from "@/helper/session";
import { dateInTimeZone } from "@/lib/log-date";
import { foodNamesServer, getMealPlanServer } from "@/lib/api/mealPlans.server";
import PlanEditor from "@/components/planning/PlanEditor";

export const dynamic = "force-dynamic";

export default async function EditMealPlanPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const [{ id }, user] = await Promise.all([params, getUserServer()]);
  const plan = await getMealPlanServer(id);
  if (!plan) notFound();
  if (!plan.canEdit) redirect(`/meal-plan/${id}`);
  const names = await foodNamesServer([
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
