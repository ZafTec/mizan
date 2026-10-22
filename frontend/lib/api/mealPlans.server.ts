import { ApiError } from "@/lib/api";
import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import type { components } from "@/types/api.generated";
import type {
  MealPlanDetail,
  MealPlanListQuery,
  MealPlanPage,
  PlanDayView,
  Template,
} from "./mealPlans";

/**
 * Server-component reads for the planning pages. They forward the session
 * cookie and answer with the generated types; nothing here is cached beyond
 * the request, so a plan accepted a moment ago shows on the next render.
 */
const log = logger.createModuleLogger("meal-plans");

export async function listMealPlansServer(
  query: MealPlanListQuery = {},
): Promise<MealPlanPage> {
  const params = new URLSearchParams();
  if (query.Status) params.set("Status", query.Status);
  if (query.Page) params.set("Page", String(query.Page));
  if (query.PageSize) params.set("PageSize", String(query.PageSize));
  return serverApi<MealPlanPage>(`/api/MealPlans?${params.toString()}`);
}

/** Null for a plan the viewer cannot open; the page turns that into 404. */
export async function getMealPlanServer(
  id: string,
): Promise<MealPlanDetail | null> {
  try {
    return await serverApi<MealPlanDetail>(`/api/MealPlans/${id}`, {
      expectedStatuses: [404],
    });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}

/** Null when no active plan covers the date (`no_active_plan`) or the call fails: /today never breaks over it. */
export async function getPlanDayServer(
  date: string,
): Promise<PlanDayView | null> {
  try {
    return await serverApi<PlanDayView>(
      `/api/MealPlans/day?date=${encodeURIComponent(date)}`,
      { expectedStatuses: [404] },
    );
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 404))
      log.error("Failed to load the plan for the day", {
        date,
        error: error instanceof Error ? error.message : String(error),
      });
    return null;
  }
}

export const listTemplatesServer = () =>
  serverApi<Template[]>("/api/MealPlans/templates");

export type MyHouseholds = components["schemas"]["GetMyHouseholdsResult"];

/** The active household, when the person is in one, so a plan can be shared with it. */
export async function getActiveHouseholdServer(): Promise<{
  id: string;
  name: string;
} | null> {
  try {
    const result = await serverApi<MyHouseholds>("/api/Households/mine");
    const active = result.households?.find(
      (household) => household.id === result.activeHouseholdId,
    );
    return active ? { id: active.id, name: active.name } : null;
  } catch (error) {
    log.error("Failed to load households", {
      error: error instanceof Error ? error.message : String(error),
    });
    return null;
  }
}

export type TrainerClient = components["schemas"]["TrainerClientDto"];

/** The coach's client, or null when the relationship does not exist. */
export async function getTrainerClientServer(
  clientId: string,
): Promise<TrainerClient | null> {
  const result = await serverApi<
    components["schemas"]["TrainerClientDtoPagedResult"]
  >("/api/Trainers/clients?PageSize=100");
  return result.items.find((client) => client.clientId === clientId) ?? null;
}
