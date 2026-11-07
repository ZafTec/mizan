import { notFound } from "next/navigation";
import { getUserServer } from "@/helper/session";
import { dateInTimeZone } from "@/lib/log-date";
import { foodNamesServer, getMealPlanServer } from "@/lib/api/mealPlans.server";
import PlanEditor from "@/components/planning/PlanEditor";

export const dynamic = "force-dynamic";
export const metadata = { title: "Template · Mizan" };

/** One of the coach's own templates in the plan editor. */
export default async function TrainerTemplatePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const [{ id }, user] = await Promise.all([params, getUserServer()]);
  const plan = await getMealPlanServer(id);
  if (!plan || plan.kind !== "Template" || plan.userId !== user.id) notFound();
  const names = await foodNamesServer([
    ...plan.rules.allowedFoodIds,
    ...plan.rules.forbiddenFoodIds,
  ]);
  return (
    <PlanEditor
      plan={plan}
      today={dateInTimeZone(user.timeZoneId || "UTC")}
      foodNames={names}
      back={{ href: "/trainer/templates", label: "Templates" }}
    />
  );
}
