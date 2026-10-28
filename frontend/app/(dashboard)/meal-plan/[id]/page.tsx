import { notFound } from "next/navigation";
import { getUserServer } from "@/helper/session";
import { dateInTimeZone } from "@/lib/log-date";
import { getMealPlanServer } from "@/lib/api/mealPlans.server";
import PlanDetail from "@/components/planning/PlanDetail";
import DirectionalTransition from "@/components/motion/DirectionalTransition";

export const dynamic = "force-dynamic";

export default async function MealPlanDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const [{ id }, user] = await Promise.all([params, getUserServer()]);
  const plan = await getMealPlanServer(id);
  if (!plan) notFound();
  const today = dateInTimeZone(user.timeZoneId || "UTC");
  return (
    <DirectionalTransition>
      <PlanDetail plan={plan} today={today} viewerId={user.id} />
    </DirectionalTransition>
  );
}
