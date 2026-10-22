import Link from "next/link";
import { getUserServer } from "@/helper/session";
import { dateInTimeZone } from "@/lib/log-date";
import {
  getActiveHouseholdServer,
  listTemplatesServer,
} from "@/lib/api/mealPlans.server";
import NewPlanForm from "@/components/planning/NewPlanForm";
import { logger } from "@/lib/logger";

const log = logger.createModuleLogger("meal-plan-new");

export const dynamic = "force-dynamic";
export const metadata = { title: "New plan · Mizan" };

export default async function NewMealPlanPage() {
  const user = await getUserServer();
  const today = dateInTimeZone(user.timeZoneId || "UTC");
  const [templates, household] = await Promise.all([
    listTemplatesServer().catch((error) => {
      log.error("Failed to load templates", {
        error: error instanceof Error ? error.message : String(error),
      });
      return [];
    }),
    getActiveHouseholdServer(),
  ]);
  return (
    <div className="log-page space-y-8" data-testid="meal-plan-new-page">
      <header className="log-page-header block!">
        <p className="text-sm">
          <Link href="/meal-plan" className="text-link">
            Meal plans
          </Link>
        </p>
        <h1 className="mt-1">New plan</h1>
        <p className="log-muted mt-2">
          Start from a template or set your own rules and fill the days later.
        </p>
      </header>
      <NewPlanForm templates={templates} household={household} today={today} />
    </div>
  );
}
