import { ApiError } from "@/lib/api";
import { clientApi } from "@/lib/api.client";
import type { components, paths } from "@/types/api.generated";

/**
 * Browser calls for plans, templates, entries and cook sessions. Every type
 * comes from the generated OpenAPI document; nothing here declares a response
 * shape of its own. Every write carries an Idempotency-Key, so a retry after a
 * lost answer replays the first one instead of logging a meal twice or failing
 * on a plan the first try already changed (docs/PLANNING.md#api-step-2-as-built).
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
export type ApplyTemplateForClientRequest =
  Schemas["ApplyTemplateForClientRequest"];
export type CreateTemplateRequest = Schemas["CreateTemplateCommand"];
export type UpdateTemplateRequest = Schemas["UpdateTemplateRequest"];
export type MealPlanCreatedResult = Schemas["MealPlanCreatedResult"];
export type PlanEntryCreatedResult = Schemas["PlanEntryCreatedResult"];
export type PrepSessionCreatedResult = Schemas["PrepSessionCreatedResult"];
export type GenerateShoppingListResult = Schemas["GenerateShoppingListResult"];
export type LogPlanEntryResult = Schemas["LogPlanEntryResult"];
export type MealPlanPage = Schemas["MealPlanSummaryDtoPagedResult"];
export type GenerateMealPlanRequest = Schemas["GenerateMealPlanCommand"];
export type GenerateMealPlanResult = Schemas["GenerateMealPlanResult"];
export type GeneratedPlan = Schemas["GeneratedPlanDto"];
/** The 422 when the draft would leave days with no food or recipe; nothing was saved. */
export type PlanNeedsFoodsError = Schemas["PlanNeedsFoodsError"];
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
    expectedStatuses: [422, 429, 503],
  });

/** The refusal body when the catalogue lacks the foods the draft needed, else null. */
export function readPlanNeedsFoods(error: unknown): PlanNeedsFoodsError | null {
  if (!(error instanceof ApiError) || error.status !== 422) return null;
  const body = error.body as Partial<PlanNeedsFoodsError> | null;
  if (body?.errorCode !== "plan_needs_foods") return null;
  return {
    errorCode: body.errorCode,
    error: body.error ?? "",
    missingFoods: body.missingFoods ?? [],
    emptyDays: body.emptyDays ?? [],
  };
}

export const listTemplates = () =>
  clientApi<Template[]>("/api/MealPlans/templates");

export const applyTemplate = (templateId: string, body: ApplyTemplateRequest) =>
  clientApi<MealPlanCreatedResult>(
    `/api/MealPlans/templates/${templateId}/apply`,
    { method: "POST", body, headers: idempotent() },
  );

/**
 * Keeps a template: a global one (administrators only) or one of the
 * caller's own (coaches and administrators). The API refuses anyone else.
 */
export const createTemplate = (body: CreateTemplateRequest) =>
  clientApi<MealPlanCreatedResult>("/api/MealPlans/templates", {
    method: "POST",
    body,
    headers: idempotent(),
  });

/** Name, description, length and rules. Entries and sessions use the plan routes with the template's id. */
export const updateTemplate = (id: string, body: UpdateTemplateRequest) =>
  clientApi<void>(`/api/MealPlans/templates/${id}`, {
    method: "PUT",
    body,
    headers: idempotent(),
  });

/** Plans already made from the template keep their copy. */
export const deleteTemplate = (id: string) =>
  clientApi<void>(`/api/MealPlans/templates/${id}`, {
    method: "DELETE",
    headers: idempotent(),
  });

/** A coach drafts a client's plan from a template; the client accepts or declines it. */
export const applyTemplateForClient = (
  clientId: string,
  body: ApplyTemplateForClientRequest,
) =>
  clientApi<MealPlanCreatedResult>(
    `/api/Trainers/clients/${clientId}/meal-plans/from-template`,
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
  clientApi<void>(`/api/MealPlans/${id}`, {
    method: "PUT",
    body,
    headers: idempotent(),
  });

export const deleteMealPlan = (id: string) =>
  clientApi<void>(`/api/MealPlans/${id}`, {
    method: "DELETE",
    headers: idempotent(),
  });

/**
 * Throws away an assistant's draft the person did not keep. It declines the
 * draft, as Telegram's Discard does, so the plan is kept under past plans.
 */
export const discardDraft = (id: string) =>
  clientApi<void>(`/api/MealPlans/${id}/decline`, {
    method: "POST",
    headers: idempotent(),
  });

export const setMealPlanStatus = (
  id: string,
  status: Exclude<PlanStatus, "Draft">,
) =>
  clientApi<void>(`/api/MealPlans/${id}/status`, {
    method: "POST",
    body: { status } satisfies Schemas["SetMealPlanStatusRequest"],
    headers: idempotent(),
  });

export const acceptPlan = (id: string) =>
  clientApi<void>(`/api/MealPlans/${id}/accept`, {
    method: "POST",
    headers: idempotent(),
  });

export const declinePlan = (id: string) =>
  clientApi<void>(`/api/MealPlans/${id}/decline`, {
    method: "POST",
    headers: idempotent(),
  });

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
    headers: idempotent(),
  });

export const removePlanEntry = (planId: string, entryId: string) =>
  clientApi<void>(`/api/MealPlans/${planId}/entries/${entryId}`, {
    method: "DELETE",
    headers: idempotent(),
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
    { method: "POST", body, headers: idempotent() },
  );

export const updatePrepSession = (
  planId: string,
  sessionId: string,
  body: PrepSessionRequest,
) =>
  clientApi<void>(`/api/MealPlans/${planId}/prep-sessions/${sessionId}`, {
    method: "PUT",
    body,
    headers: idempotent(),
  });

export const removePrepSession = (planId: string, sessionId: string) =>
  clientApi<void>(`/api/MealPlans/${planId}/prep-sessions/${sessionId}`, {
    method: "DELETE",
    headers: idempotent(),
  });

export const generateShoppingList = (
  planId: string,
  body: Schemas["GenerateShoppingListRequest"] = {},
) =>
  clientApi<GenerateShoppingListResult>(
    `/api/MealPlans/${planId}/shopping-list`,
    { method: "POST", body, headers: idempotent() },
  );
