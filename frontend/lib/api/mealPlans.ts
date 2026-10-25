import { clientApi } from "@/lib/api.client";
import type { components, paths } from "@/types/api.generated";

/**
 * Browser calls for plans, templates, entries and cook sessions. Every type
 * comes from the generated OpenAPI document; nothing here declares a response
 * shape of its own. Writes a person may retry carry an Idempotency-Key so a
 * double tap cannot log a meal twice (docs/PLANNING.md#api-step-2-as-built).
 */
type Schemas = components["schemas"];

export type MealPlanSummary = Schemas["MealPlanSummaryDto"];
export type MealPlanDetail = Schemas["MealPlanDetailDto"];
export type PlanDay = Schemas["PlanDayDto"];
export type PlanSlot = Schemas["PlanSlotDto"];
export type PlanEntry = Schemas["PlanEntryDto"];
export type PlanRules = Schemas["PlanRulesDto"];
export type PlanViolation = Schemas["PlanViolationDto"];
export type PrepSession = Schemas["PrepSessionDto"];
export type EntryNutrition = Schemas["EntryNutritionDto"];
export type PlanDayView = Schemas["PlanDayViewDto"];
export type Template = Schemas["TemplateDto"];
export type PlanEntryRequest = Schemas["PlanEntryRequest"];
export type PrepSessionRequest = Schemas["PrepSessionRequest"];
export type CreateMealPlanRequest = Schemas["CreateMealPlanRequest"];
export type UpdateMealPlanRequest = Schemas["UpdateMealPlanRequest"];
export type ApplyTemplateRequest = Schemas["ApplyTemplateRequest"];
export type MealPlanCreatedResult = Schemas["MealPlanCreatedResult"];
export type PlanEntryCreatedResult = Schemas["PlanEntryCreatedResult"];
export type PrepSessionCreatedResult = Schemas["PrepSessionCreatedResult"];
export type GenerateShoppingListResult = Schemas["GenerateShoppingListResult"];
export type LogPlanEntryResult = Schemas["LogPlanEntryResult"];
export type MealPlanPage = Schemas["MealPlanSummaryDtoPagedResult"];
export type GenerateMealPlanRequest = Schemas["GenerateMealPlanCommand"];
export type GenerateMealPlanResult = Schemas["GenerateMealPlanResult"];
export type GeneratedPlan = Schemas["GeneratedPlanDto"];
export type PlanCost = Schemas["PlanCostDto"];
export type PlanCostItem = Schemas["PlanCostItemDto"];
export type PlanGeneration = Schemas["PlanGenerationDto"];
export type MealPlanListQuery = NonNullable<
  paths["/api/MealPlans"]["get"]["parameters"]["query"]
>;
export type PlanStatus = "Draft" | "Active" | "Archived";

export const SLOTS = ["BREAKFAST", "LUNCH", "DINNER", "SNACK"] as const;

function idempotent(): Record<string, string> {
  return { "Idempotency-Key": crypto.randomUUID() };
}

function listQuery(query: MealPlanListQuery): string {
  const params = new URLSearchParams();
  if (query.Status) params.set("Status", query.Status);
  if (query.Page) params.set("Page", String(query.Page));
  if (query.PageSize) params.set("PageSize", String(query.PageSize));
  return params.toString();
}

export const listMealPlans = (query: MealPlanListQuery = {}) =>
  clientApi<MealPlanPage>(`/api/MealPlans?${listQuery(query)}`);

export const getMealPlan = (id: string) =>
  clientApi<MealPlanDetail>(`/api/MealPlans/${id}`);

/** Throws an ApiError with status 404 (`no_active_plan`) when no plan covers the date. */
export const getPlanDay = (date?: string) =>
  clientApi<PlanDayView>(
    `/api/MealPlans/day${date ? `?date=${encodeURIComponent(date)}` : ""}`,
    { expectedStatuses: [404] },
  );

/**
 * The assistant drafts a plan: one saved Draft per budget ceiling, each with
 * its violations and cost table. 429 `ai_quota_exceeded` when the planning
 * allowance is spent, 503 `ai_unavailable` when the provider fails. The key is
 * the caller's, so trying again after a lost answer replays the draft that was
 * made instead of spending the allowance twice.
 */
export const generateMealPlan = (
  body: GenerateMealPlanRequest,
  idempotencyKey: string,
) =>
  clientApi<GenerateMealPlanResult>("/api/MealPlans/generate", {
    method: "POST",
    body,
    headers: { "Idempotency-Key": idempotencyKey },
    expectedStatuses: [429, 503],
  });

export const listTemplates = () =>
  clientApi<Template[]>("/api/MealPlans/templates");

export const applyTemplate = (templateId: string, body: ApplyTemplateRequest) =>
  clientApi<MealPlanCreatedResult>(
    `/api/MealPlans/templates/${templateId}/apply`,
    { method: "POST", body, headers: idempotent() },
  );

export const createMealPlan = (body: CreateMealPlanRequest) =>
  clientApi<MealPlanCreatedResult>("/api/MealPlans", {
    method: "POST",
    body,
    headers: idempotent(),
  });

/** A coach drafts a plan for a client who granted `canPlanMeals`. */
export const createPlanForClient = (
  clientId: string,
  body: CreateMealPlanRequest,
) =>
  clientApi<MealPlanCreatedResult>(
    `/api/Trainers/clients/${clientId}/meal-plans`,
    { method: "POST", body, headers: idempotent() },
  );

export const updateMealPlan = (id: string, body: UpdateMealPlanRequest) =>
  clientApi<void>(`/api/MealPlans/${id}`, { method: "PUT", body });

export const deleteMealPlan = (id: string) =>
  clientApi<void>(`/api/MealPlans/${id}`, { method: "DELETE" });

/** Throws away an assistant's draft the person did not keep: the plan is deleted. */
export const discardDraft = (id: string) =>
  clientApi<void>(`/api/MealPlans/${id}`, {
    method: "DELETE",
    headers: idempotent(),
  });

export const setMealPlanStatus = (
  id: string,
  status: Exclude<PlanStatus, "Draft">,
) =>
  clientApi<void>(`/api/MealPlans/${id}/status`, {
    method: "POST",
    body: { status } satisfies Schemas["SetMealPlanStatusRequest"],
  });

export const acceptPlan = (id: string) =>
  clientApi<void>(`/api/MealPlans/${id}/accept`, { method: "POST" });

export const declinePlan = (id: string) =>
  clientApi<void>(`/api/MealPlans/${id}/decline`, { method: "POST" });

export const addPlanEntry = (planId: string, body: PlanEntryRequest) =>
  clientApi<PlanEntryCreatedResult>(`/api/MealPlans/${planId}/entries`, {
    method: "POST",
    body,
    headers: idempotent(),
  });

export const updatePlanEntry = (
  planId: string,
  entryId: string,
  body: PlanEntryRequest,
) =>
  clientApi<void>(`/api/MealPlans/${planId}/entries/${entryId}`, {
    method: "PUT",
    body,
  });

export const removePlanEntry = (planId: string, entryId: string) =>
  clientApi<void>(`/api/MealPlans/${planId}/entries/${entryId}`, {
    method: "DELETE",
  });

/** Writes the entry to the diary on its own date and slot. */
export const logPlanEntry = (
  planId: string,
  entryId: string,
  body: Schemas["LogPlanEntryRequest"] = {},
) =>
  clientApi<LogPlanEntryResult>(
    `/api/MealPlans/${planId}/entries/${entryId}/log`,
    { method: "POST", body, headers: idempotent() },
  );

export const addPrepSession = (planId: string, body: PrepSessionRequest) =>
  clientApi<PrepSessionCreatedResult>(
    `/api/MealPlans/${planId}/prep-sessions`,
    { method: "POST", body },
  );

export const updatePrepSession = (
  planId: string,
  sessionId: string,
  body: PrepSessionRequest,
) =>
  clientApi<void>(`/api/MealPlans/${planId}/prep-sessions/${sessionId}`, {
    method: "PUT",
    body,
  });

export const removePrepSession = (planId: string, sessionId: string) =>
  clientApi<void>(`/api/MealPlans/${planId}/prep-sessions/${sessionId}`, {
    method: "DELETE",
  });

export const generateShoppingList = (
  planId: string,
  body: Schemas["GenerateShoppingListRequest"] = {},
) =>
  clientApi<GenerateShoppingListResult>(
    `/api/MealPlans/${planId}/shopping-list`,
    { method: "POST", body, headers: idempotent() },
  );
