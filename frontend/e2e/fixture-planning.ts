import type { components } from "../types/api.generated";
import type { MealEntry } from "../data/meal";

/**
 * The planning half of the UI fixture: plans, templates, entries, cook
 * sessions, shopping lists and the coach's clients, typed from the generated
 * OpenAPI schema so a drifted DTO fails the type check. State is per session;
 * every write lands in `writes[]` and a route the fixture does not know is
 * reported by the caller as unhandled.
 */
type Schemas = components["schemas"];
type PlanRules = Schemas["PlanRulesDto"];
type PlanEntryDto = Schemas["PlanEntryDto"];
type PlanDayDto = Schemas["PlanDayDto"];
type PlanViolation = Schemas["PlanViolationDto"];
type MealPlanSummary = Schemas["MealPlanSummaryDto"];
type MealPlanDetail = Schemas["MealPlanDetailDto"];
type PlanDayView = Schemas["PlanDayViewDto"];
type TemplateDto = Schemas["TemplateDto"];
type PlanEntryRequest = Schemas["PlanEntryRequest"];
type PrepSessionRequest = Schemas["PrepSessionRequest"];
type CreateMealPlanRequest = Schemas["CreateMealPlanRequest"];
type UpdateMealPlanRequest = Schemas["UpdateMealPlanRequest"];
type ApplyTemplateRequest = Schemas["ApplyTemplateRequest"];
type MealPlanCreatedResult = Schemas["MealPlanCreatedResult"];
type LogPlanEntryResult = Schemas["LogPlanEntryResult"];
type GenerateShoppingListResult = Schemas["GenerateShoppingListResult"];
type ShoppingListSummary = Schemas["ShoppingListSummaryDto"];
type ShoppingListDetail = Schemas["ShoppingListDetailDto"];
type ShoppingListItem = Schemas["ShoppingListItemDto"];
type TrainerClient = Schemas["TrainerClientDto"];
type Nutrition = Schemas["EntryNutritionDto"];

export type StoredEntry = {
  id: string;
  dayIndex: number;
  slot: string;
  kind: string;
  recipeId: string | null;
  servings: number | null;
  foodId: string | null;
  grams: number | null;
  text: string | null;
  notes: string | null;
  sortOrder: number;
  prepSessionId: string | null;
  logged: boolean;
};
export type StoredSession = {
  id: string;
  dayIndex: number;
  title: string;
  notes: string | null;
};
export type StoredPlan = {
  id: string;
  name: string | null;
  description: string | null;
  kind: "Personal" | "Template";
  status: "Draft" | "Active" | "Archived";
  startDate: string | null;
  durationDays: number;
  userId: string | null;
  householdId: string | null;
  authoredByUserId: string | null;
  authoredByName: string | null;
  global: boolean;
  rules: PlanRules;
  entries: StoredEntry[];
  prepSessions: StoredSession[];
  createdAt: string;
  updatedAt: string;
};
export type StoredList = {
  id: string;
  name: string | null;
  userId: string;
  householdId: string | null;
  mealPlanId: string | null;
  generatedAt: string | null;
  items: ShoppingListItem[];
  createdAt: string;
  updatedAt: string;
};
export type PlanningState = {
  plans: StoredPlan[];
  lists: StoredList[];
  trainerClients: TrainerClient[];
};
export type PlanningOptions = {
  /** No plans, lists or templates at all. */
  none?: boolean;
  /** Whether the coach's client has granted meal planning (default true). */
  clientGrantsPlanning?: boolean;
};

type Food = {
  id: string;
  name: string;
  caloriesPer100g: number;
  proteinPer100g: number;
  carbsPer100g: number;
  fatPer100g: number;
  fiberPer100g?: number | null;
};
type Recipe = {
  id: string;
  title: string;
  servings?: number;
  nutrition?: {
    caloriesPerServing?: number;
    proteinGrams?: number;
    carbsGrams?: number;
    fatGrams?: number;
    fiberGrams?: number;
  } | null;
  ingredients?: {
    foodId: string | null;
    foodName?: string;
    ingredientText: string;
    amount: number | null;
    unit: string;
  }[];
};
export type PlanningHost = {
  userId: string;
  planning: PlanningState;
  writes: { path: string; body: Record<string, unknown> }[];
  recipes: Record<string, unknown>[];
  meals: MealEntry[];
  foods: Food[];
};

export const COACH_ID = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";
export const CLIENT_ID = "dddddddd-dddd-4ddd-8ddd-dddddddddddd";
export const ACTIVE_PLAN_ID = "a1000000-0000-4000-8000-000000000001";
export const DRAFT_PLAN_ID = "a1000000-0000-4000-8000-000000000002";
export const PAST_PLAN_ID = "a1000000-0000-4000-8000-000000000003";
export const TEMPLATE_ID = "a1000000-0000-4000-8000-000000000010";
export const LIST_ID = "b1000000-0000-4000-8000-000000000001";
const RECIPE_ID = "33333333-3333-4333-8333-333333333333";
const YOGURT_ID = "11111111-1111-4111-8111-111111111111";
const OATS_ID = "22222222-2222-4222-8222-222222222222";
const SLOT_ORDER = ["BREAKFAST", "LUNCH", "DINNER", "SNACK"];

export function addDays(iso: string, days: number): string {
  const date = new Date(`${iso}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

const emptyRules = (): PlanRules => ({
  macroMode: "None",
  allowedFoodIds: [],
  forbiddenFoodIds: [],
  prepStyle: "Daily",
  batchDays: [],
});

export function initialPlanning(
  userId: string,
  today: string,
  options: PlanningOptions = {},
): PlanningState {
  const trainerClients: TrainerClient[] = [
    {
      relationshipId: "re000000-0000-4000-8000-000000000001",
      clientId: CLIENT_ID,
      clientName: "Sam Rivera",
      clientEmail: "sam@example.test",
      status: "active",
      canViewNutrition: true,
      canViewWorkouts: false,
      canMessage: true,
      canPlanMeals: options.clientGrantsPlanning ?? true,
      startedAt: `${addDays(today, -30)}T08:00:00Z`,
    },
  ];
  if (options.none) return { plans: [], lists: [], trainerClients };
  const start = addDays(today, -2);
  const stamp = (daysAgo: number) => `${addDays(today, -daysAgo)}T09:00:00Z`;
  const entry = (
    id: string,
    dayIndex: number,
    partial: Partial<StoredEntry>,
  ): StoredEntry => ({
    id,
    dayIndex,
    slot: "DINNER",
    kind: "Recipe",
    recipeId: null,
    servings: null,
    foodId: null,
    grams: null,
    text: null,
    notes: null,
    sortOrder: 0,
    prepSessionId: null,
    logged: false,
    ...partial,
  });
  const active: StoredPlan = {
    id: ACTIVE_PLAN_ID,
    name: "Steady week",
    description: "Breakfast you like, lunch you can carry.",
    kind: "Personal",
    status: "Active",
    startDate: start,
    durationDays: 7,
    userId,
    householdId: null,
    authoredByUserId: null,
    authoredByName: null,
    global: false,
    rules: {
      ...emptyRules(),
      macroMode: "Custom",
      targetCalories: 1800,
      targetProtein: 120,
      maxCarbs: 150,
      eatingWindowStart: "08:00:00",
      eatingWindowEnd: "20:00:00",
      prepStyle: "Batch",
      batchDays: [0],
    },
    prepSessions: [
      {
        id: "s1000000-0000-4000-8000-000000000001",
        dayIndex: 0,
        title: "Sunday batch",
        notes: "Cook the oats for the week.",
      },
    ],
    entries: [
      ...Array.from({ length: 7 }, (_, day) =>
        entry(`e100000${day}-0000-4000-8000-000000000001`, day, {
          slot: "BREAKFAST",
          kind: "Recipe",
          recipeId: RECIPE_ID,
          servings: 1,
          prepSessionId: "s1000000-0000-4000-8000-000000000001",
        }),
      ),
      entry("e2000002-0000-4000-8000-000000000002", 2, {
        slot: "LUNCH",
        kind: "Food",
        foodId: YOGURT_ID,
        grams: 150,
        notes: "With honey.",
      }),
      entry("e2000003-0000-4000-8000-000000000003", 2, {
        slot: "DINNER",
        kind: "Text",
        text: "Eat out",
      }),
      entry("e2000004-0000-4000-8000-000000000004", 3, {
        slot: "LUNCH",
        kind: "Food",
        foodId: OATS_ID,
        grams: 80,
      }),
    ],
    createdAt: stamp(3),
    updatedAt: stamp(1),
  };
  const draft: StoredPlan = {
    id: DRAFT_PLAN_ID,
    name: "Coach's cut",
    description: "Lower carbs for two weeks.",
    kind: "Personal",
    status: "Draft",
    startDate: addDays(today, 1),
    durationDays: 14,
    userId,
    householdId: null,
    authoredByUserId: COACH_ID,
    authoredByName: "Dana Coach",
    global: false,
    rules: { ...emptyRules(), macroMode: "Keto", maxCarbs: 30 },
    prepSessions: [],
    entries: [
      entry("e3000000-0000-4000-8000-000000000001", 0, {
        slot: "BREAKFAST",
        kind: "Food",
        foodId: YOGURT_ID,
        grams: 200,
      }),
    ],
    createdAt: stamp(0),
    updatedAt: stamp(0),
  };
  const past: StoredPlan = {
    id: PAST_PLAN_ID,
    name: "September reset",
    description: null,
    kind: "Personal",
    status: "Archived",
    startDate: addDays(today, -30),
    durationDays: 7,
    userId,
    householdId: null,
    authoredByUserId: null,
    authoredByName: null,
    global: false,
    rules: emptyRules(),
    prepSessions: [],
    entries: [
      entry("e4000000-0000-4000-8000-000000000001", 0, {
        slot: "DINNER",
        kind: "Recipe",
        recipeId: RECIPE_ID,
        servings: 2,
      }),
    ],
    createdAt: stamp(31),
    updatedAt: stamp(23),
  };
  const template: StoredPlan = {
    id: TEMPLATE_ID,
    name: "Simple week",
    description: "Yogurt and oats every morning.",
    kind: "Template",
    status: "Active",
    startDate: null,
    durationDays: 7,
    userId: null,
    householdId: null,
    authoredByUserId: null,
    authoredByName: null,
    global: true,
    rules: { ...emptyRules(), macroMode: "HighProtein", targetProtein: 130 },
    prepSessions: [],
    entries: Array.from({ length: 7 }, (_, day) =>
      entry(`e500000${day}-0000-4000-8000-000000000001`, day, {
        slot: "BREAKFAST",
        kind: "Recipe",
        recipeId: RECIPE_ID,
        servings: 1,
      }),
    ),
    createdAt: stamp(60),
    updatedAt: stamp(60),
  };
  const list: StoredList = {
    id: LIST_ID,
    name: "Steady week shopping",
    userId,
    householdId: null,
    mealPlanId: ACTIVE_PLAN_ID,
    generatedAt: stamp(1),
    items: [
      {
        id: "i1000000-0000-4000-8000-000000000001",
        foodId: YOGURT_ID,
        foodName: "Greek yogurt",
        itemName: "Greek yogurt",
        grams: 1550,
        needsReview: false,
        prepSessionId: "s1000000-0000-4000-8000-000000000001",
        prepSessionTitle: "Sunday batch",
        sortOrder: 0,
        isChecked: false,
      },
      {
        id: "i1000000-0000-4000-8000-000000000002",
        foodId: OATS_ID,
        foodName: "Rolled oats",
        itemName: "Rolled oats",
        grams: 430,
        needsReview: false,
        prepSessionId: "s1000000-0000-4000-8000-000000000001",
        prepSessionTitle: "Sunday batch",
        sortOrder: 1,
        isChecked: true,
      },
      {
        id: "i1000000-0000-4000-8000-000000000003",
        itemName: "Eat out",
        amount: 1,
        unit: "meal",
        needsReview: true,
        sortOrder: 2,
        isChecked: false,
      },
    ],
    createdAt: stamp(1),
    updatedAt: stamp(1),
  };
  return {
    plans: [active, draft, past, template],
    lists: [list],
    trainerClients,
  };
}

function nutritionFor(
  entry: StoredEntry,
  host: PlanningHost,
): Nutrition | undefined {
  if (entry.kind === "Food" && entry.foodId && entry.grams != null) {
    const food = host.foods.find((item) => item.id === entry.foodId);
    if (!food) return undefined;
    const factor = entry.grams / 100;
    return {
      calories: food.caloriesPer100g * factor,
      proteinGrams: food.proteinPer100g * factor,
      carbsGrams: food.carbsPer100g * factor,
      fatGrams: food.fatPer100g * factor,
      fiberGrams: (food.fiberPer100g ?? 0) * factor,
    };
  }
  if (entry.kind === "Recipe" && entry.recipeId && entry.servings != null) {
    const recipe = host.recipes.find((item) => item.id === entry.recipeId) as
      Recipe | undefined;
    if (!recipe?.nutrition) return undefined;
    const n = recipe.nutrition;
    const s = entry.servings;
    return {
      calories: (n.caloriesPerServing ?? 0) * s,
      proteinGrams: (n.proteinGrams ?? 0) * s,
      carbsGrams: (n.carbsGrams ?? 0) * s,
      fatGrams: (n.fatGrams ?? 0) * s,
      fiberGrams: (n.fiberGrams ?? 0) * s,
    };
  }
  return undefined;
}

function entryDto(
  plan: StoredPlan,
  entry: StoredEntry,
  host: PlanningHost,
): PlanEntryDto {
  const recipe = host.recipes.find((item) => item.id === entry.recipeId) as
    Recipe | undefined;
  const food = host.foods.find((item) => item.id === entry.foodId);
  return {
    id: entry.id,
    dayIndex: entry.dayIndex,
    date: plan.startDate ? addDays(plan.startDate, entry.dayIndex) : null,
    slot: entry.slot,
    kind: entry.kind,
    recipeId: entry.recipeId,
    recipeTitle: recipe?.title ?? null,
    recipeImageUrl: null,
    servings: entry.servings,
    foodId: entry.foodId,
    foodName: food?.name ?? null,
    grams: entry.grams,
    text: entry.text,
    notes: entry.notes,
    sortOrder: entry.sortOrder,
    prepSessionId: entry.prepSessionId,
    nutrition: nutritionFor(entry, host),
    logged: entry.logged,
    cost: null,
  };
}

function violationsFor(
  plan: StoredPlan,
  entries: PlanEntryDto[],
  totals: Nutrition,
  dayIndex: number,
): PlanViolation[] {
  const violations: PlanViolation[] = [];
  for (const entry of entries) {
    if (entry.foodId && plan.rules.forbiddenFoodIds.includes(entry.foodId))
      violations.push({
        dayIndex,
        slot: entry.slot,
        rule: "ForbiddenFood",
        message: `${entry.foodName} is on the forbidden list.`,
      });
    if (
      entry.foodId &&
      plan.rules.allowedFoodIds.length > 0 &&
      !plan.rules.allowedFoodIds.includes(entry.foodId)
    )
      violations.push({
        dayIndex,
        slot: entry.slot,
        rule: "AllowedFoods",
        message: `${entry.foodName} is not on the allowed list.`,
      });
  }
  if (plan.rules.maxCarbs != null && totals.carbsGrams > plan.rules.maxCarbs)
    violations.push({
      dayIndex,
      slot: null,
      rule: "MaxCarbs",
      message: `${Math.round(totals.carbsGrams)} g carbs is over the ${plan.rules.maxCarbs} g cap.`,
    });
  return violations;
}

function dayDto(
  plan: StoredPlan,
  dayIndex: number,
  host: PlanningHost,
): PlanDayDto {
  const entries = plan.entries
    .filter((entry) => entry.dayIndex === dayIndex)
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((entry) => entryDto(plan, entry, host));
  const slots = SLOT_ORDER.filter((slot) =>
    entries.some((entry) => entry.slot === slot),
  ).map((slot) => ({
    slot,
    entries: entries.filter((entry) => entry.slot === slot),
  }));
  const totals = entries.reduce<Nutrition>(
    (sum, entry) => ({
      calories: sum.calories + (entry.nutrition?.calories ?? 0),
      proteinGrams: sum.proteinGrams + (entry.nutrition?.proteinGrams ?? 0),
      carbsGrams: sum.carbsGrams + (entry.nutrition?.carbsGrams ?? 0),
      fatGrams: sum.fatGrams + (entry.nutrition?.fatGrams ?? 0),
      fiberGrams: sum.fiberGrams + (entry.nutrition?.fiberGrams ?? 0),
    }),
    { calories: 0, proteinGrams: 0, carbsGrams: 0, fatGrams: 0, fiberGrams: 0 },
  );
  return {
    dayIndex,
    date: plan.startDate ? addDays(plan.startDate, dayIndex) : null,
    slots,
    totals,
    unresolvedCount: entries.filter((entry) => !entry.nutrition).length,
    entryCount: entries.length,
    loggedCount: entries.filter((entry) => entry.logged).length,
    violations: violationsFor(plan, entries, totals, dayIndex),
    cost: null,
  };
}

function summary(plan: StoredPlan): MealPlanSummary {
  return {
    id: plan.id,
    name: plan.name,
    description: plan.description,
    kind: plan.kind,
    status: plan.status,
    startDate: plan.startDate,
    endDate: plan.startDate
      ? addDays(plan.startDate, plan.durationDays - 1)
      : null,
    durationDays: plan.durationDays,
    userId: plan.userId,
    householdId: plan.householdId,
    authoredByUserId: plan.authoredByUserId,
    authoredByName: plan.authoredByName,
    entryCount: plan.entries.length,
    hasRules: plan.rules.macroMode !== "None",
    createdAt: plan.createdAt,
    updatedAt: plan.updatedAt,
  };
}

function detail(plan: StoredPlan, host: PlanningHost): MealPlanDetail {
  const days = Array.from({ length: plan.durationDays }, (_, index) =>
    dayDto(plan, index, host),
  );
  const canEdit =
    plan.kind === "Template" ||
    (plan.status === "Draft" && plan.authoredByUserId
      ? plan.authoredByUserId === host.userId
      : plan.userId === host.userId || plan.householdId !== null);
  return {
    ...summary(plan),
    rules: plan.rules,
    days,
    prepSessions: plan.prepSessions.map((session) => ({
      id: session.id,
      dayIndex: session.dayIndex,
      date: plan.startDate ? addDays(plan.startDate, session.dayIndex) : null,
      title: session.title,
      notes: session.notes,
      entryCount: plan.entries.filter(
        (entry) => entry.prepSessionId === session.id,
      ).length,
    })),
    violations: days.flatMap((day) => day.violations),
    loggedCount: plan.entries.filter((entry) => entry.logged).length,
    canEdit,
    weeklyCost: null,
  };
}

function templateDto(plan: StoredPlan): TemplateDto {
  return {
    id: plan.id,
    name: plan.name,
    description: plan.description,
    durationDays: plan.durationDays,
    global: plan.global,
    ownerId: plan.userId,
    entryCount: plan.entries.length,
    rules: plan.rules,
    createdAt: plan.createdAt,
    updatedAt: plan.updatedAt,
  };
}

function validEntry(
  request: PlanEntryRequest,
  plan: StoredPlan,
): string | null {
  if (request.dayIndex < 0 || request.dayIndex >= plan.durationDays)
    return "The day is outside the plan.";
  if (!SLOT_ORDER.includes(request.slot)) return "Unknown meal slot.";
  if (
    request.kind === "Recipe" &&
    (!request.recipeId || !(request.servings! > 0))
  )
    return "A recipe entry needs a recipe and servings.";
  if (request.kind === "Food" && (!request.foodId || !(request.grams! > 0)))
    return "A food entry needs a food and grams.";
  if (request.kind === "Text" && !request.text?.trim())
    return "A text entry needs text.";
  return null;
}

function storedEntry(
  id: string,
  request: PlanEntryRequest,
  plan: StoredPlan,
  logged = false,
): StoredEntry {
  return {
    id,
    dayIndex: request.dayIndex,
    slot: request.slot,
    kind: request.kind,
    recipeId: request.kind === "Recipe" ? (request.recipeId ?? null) : null,
    servings: request.kind === "Recipe" ? (request.servings ?? null) : null,
    foodId: request.kind === "Food" ? (request.foodId ?? null) : null,
    grams: request.kind === "Food" ? (request.grams ?? null) : null,
    text: request.kind === "Text" ? (request.text ?? null) : null,
    notes: request.notes ?? null,
    sortOrder: request.sortOrder ?? 0,
    prepSessionId:
      request.prepSessionId ??
      (request.prepSessionIndex != null
        ? (plan.prepSessions[request.prepSessionIndex]?.id ?? null)
        : null),
    logged,
  };
}

function listSummary(list: StoredList): ShoppingListSummary {
  return {
    id: list.id,
    name: list.name,
    userId: list.userId,
    householdId: list.householdId,
    mealPlanId: list.mealPlanId,
    generatedAt: list.generatedAt,
    totalItems: list.items.length,
    checkedItems: list.items.filter((item) => item.isChecked).length,
    needsReviewCount: list.items.filter((item) => item.needsReview).length,
    createdAt: list.createdAt,
    updatedAt: list.updatedAt,
  };
}

function listDetail(list: StoredList, host: PlanningHost): ShoppingListDetail {
  const plan = host.planning.plans.find((item) => item.id === list.mealPlanId);
  return {
    ...listSummary(list),
    mealPlanName: plan?.name ?? null,
    items: list.items,
    canEdit: list.userId === host.userId || list.householdId !== null,
    totalCost: null,
  };
}

/** The ShoppingAggregator of the fixture: recipes scale by servings, foods keep grams, text needs review. */
function aggregate(plan: StoredPlan, host: PlanningHost): ShoppingListItem[] {
  const items = new Map<string, ShoppingListItem>();
  const sessionTitle = (id: string | null) =>
    plan.prepSessions.find((session) => session.id === id)?.title ?? null;
  const put = (
    key: string,
    item: Omit<ShoppingListItem, "id" | "sortOrder" | "isChecked">,
  ) => {
    const existing = items.get(key);
    if (existing && existing.grams != null && item.grams != null)
      existing.grams += item.grams;
    else if (!existing)
      items.set(key, {
        ...item,
        id: crypto.randomUUID(),
        sortOrder: items.size,
        isChecked: false,
      });
  };
  for (const entry of plan.entries) {
    if (entry.kind === "Food" && entry.foodId) {
      const food = host.foods.find((item) => item.id === entry.foodId);
      put(`food:${entry.foodId}:${entry.prepSessionId ?? ""}`, {
        foodId: entry.foodId,
        foodName: food?.name,
        itemName: food?.name ?? "Food",
        grams: entry.grams,
        needsReview: false,
        prepSessionId: entry.prepSessionId,
        prepSessionTitle: sessionTitle(entry.prepSessionId),
      });
    } else if (entry.kind === "Recipe" && entry.recipeId) {
      const recipe = host.recipes.find((item) => item.id === entry.recipeId) as
        Recipe | undefined;
      const scale = (entry.servings ?? 1) / (recipe?.servings ?? 1);
      for (const line of recipe?.ingredients ?? []) {
        const grams =
          line.amount != null && (line.unit === "g" || line.unit === "")
            ? line.amount * scale
            : null;
        put(
          `${line.foodId ? `food:${line.foodId}` : `text:${line.ingredientText}`}:${entry.prepSessionId ?? ""}`,
          {
            foodId: line.foodId,
            foodName: line.foodName,
            itemName: line.foodName || line.ingredientText,
            grams,
            amount: grams === null ? line.amount : null,
            unit: grams === null ? line.unit || null : null,
            needsReview: grams === null,
            prepSessionId: entry.prepSessionId,
            prepSessionTitle: sessionTitle(entry.prepSessionId),
          },
        );
      }
    } else if (entry.kind === "Text") {
      put(`text:${entry.text}`, {
        itemName: entry.text ?? "",
        amount: 1,
        unit: "meal",
        needsReview: true,
        prepSessionId: entry.prepSessionId,
        prepSessionTitle: sessionTitle(entry.prepSessionId),
      });
    }
  }
  return [...items.values()];
}

type Json = (data: unknown, status?: number) => Response;

function parameter(url: URL, name: string) {
  return [...url.searchParams].find(
    ([key]) => key.toLowerCase() === name.toLowerCase(),
  )?.[1];
}

function paged<T>(items: T[], url: URL) {
  const currentPage = Number(parameter(url, "page") ?? 1);
  const pageSize = Number(parameter(url, "pageSize") ?? 20);
  return {
    items: items.slice((currentPage - 1) * pageSize, currentPage * pageSize),
    totalCount: items.length,
    page: currentPage,
    pageSize,
    totalPages: Math.ceil(items.length / pageSize),
  };
}

const notFound = (json: Json, what: string) =>
  json({ errorCode: "not_found", error: `${what} not found` }, 404);

/** Answers a planning route, or null when the path is not one. */
export async function handlePlanning(
  request: Request,
  url: URL,
  host: PlanningHost,
  today: string,
  json: Json,
): Promise<Response | null> {
  const { planning, writes } = host;
  const path = url.pathname;
  const method = request.method;
  const now = new Date().toISOString();
  const body = async <T>() =>
    ((await request.text().then((text) => (text ? JSON.parse(text) : {}))) ??
      {}) as T;
  const record = (data: Record<string, unknown>) =>
    writes.push({ path, body: data });
  const personal = planning.plans.filter((plan) => plan.kind === "Personal");
  const templates = planning.plans.filter((plan) => plan.kind === "Template");
  const created = (plan: StoredPlan): MealPlanCreatedResult => ({
    id: plan.id,
    name: plan.name,
    status: plan.status,
    entryCount: plan.entries.length,
  });
  const newPlan = (
    input: CreateMealPlanRequest,
    overrides: Partial<StoredPlan>,
  ): StoredPlan => {
    const plan: StoredPlan = {
      id: crypto.randomUUID(),
      name: input.name ?? null,
      description: input.description ?? null,
      kind: "Personal",
      status: "Active",
      startDate: input.startDate ?? today,
      durationDays: input.durationDays,
      userId: host.userId,
      householdId: input.householdId ?? null,
      authoredByUserId: null,
      authoredByName: null,
      global: false,
      rules: input.rules ?? emptyRules(),
      entries: [],
      prepSessions: (input.prepSessions ?? []).map((session) => ({
        id: crypto.randomUUID(),
        dayIndex: session.dayIndex,
        title: session.title,
        notes: session.notes ?? null,
      })),
      createdAt: now,
      updatedAt: now,
      ...overrides,
    };
    plan.entries = (input.entries ?? []).map((entry) =>
      storedEntry(crypto.randomUUID(), entry, plan),
    );
    return plan;
  };

  if (path === "/api/MealPlans" && method === "GET") {
    const status = parameter(url, "status");
    const order = { Active: 0, Draft: 1, Archived: 2 };
    const rows = personal
      .filter((plan) => !status || plan.status === status)
      .sort(
        (a, b) =>
          order[a.status] - order[b.status] ||
          b.updatedAt.localeCompare(a.updatedAt),
      )
      .map(summary);
    return json(paged(rows, url));
  }
  if (path === "/api/MealPlans" && method === "POST") {
    const input = await body<CreateMealPlanRequest>();
    if (!(input.durationDays >= 1 && input.durationDays <= 31))
      return json(
        { errorCode: "validation", error: "Length is 1 to 31 days." },
        400,
      );
    const plan = newPlan(input, {});
    planning.plans.push(plan);
    record(input as unknown as Record<string, unknown>);
    return json(created(plan), 201);
  }
  if (path === "/api/MealPlans/day" && method === "GET") {
    const date = parameter(url, "date") ?? today;
    const plan = personal
      .filter(
        (candidate) =>
          candidate.status === "Active" &&
          candidate.startDate &&
          candidate.startDate <= date &&
          date <= addDays(candidate.startDate, candidate.durationDays - 1),
      )
      .sort(
        (a, b) =>
          Number(b.userId === host.userId) - Number(a.userId === host.userId) ||
          b.updatedAt.localeCompare(a.updatedAt),
      )[0];
    if (!plan)
      return json(
        {
          errorCode: "no_active_plan",
          error: "No active plan covers that date.",
        },
        404,
      );
    const view: PlanDayView = {
      planId: plan.id,
      planName: plan.name,
      date,
      day: dayDto(
        plan,
        Math.round(
          (Date.parse(`${date}T00:00:00Z`) -
            Date.parse(`${plan.startDate}T00:00:00Z`)) /
            86_400_000,
        ),
        host,
      ),
      rules: plan.rules,
    };
    return json(view);
  }
  if (path === "/api/MealPlans/templates" && method === "GET")
    return json(templates.map(templateDto));
  if (path === "/api/MealPlans/templates" && method === "POST") {
    const input = await body<Schemas["CreateTemplateCommand"]>();
    const plan = newPlan(
      {
        ...input,
        startDate: null,
        entries: input.entries,
        prepSessions: input.prepSessions,
      },
      {
        kind: "Template",
        startDate: null,
        global: input.global,
        userId: input.global ? null : host.userId,
      },
    );
    planning.plans.push(plan);
    record(input as unknown as Record<string, unknown>);
    return json(created(plan), 201);
  }
  const template = path.match(
    /^\/api\/MealPlans\/templates\/([^/]+)(\/apply)?$/,
  );
  if (template) {
    const plan = templates.find((item) => item.id === template[1]);
    if (!plan) return notFound(json, "Template");
    if (template[2] && method === "POST") {
      const input = await body<ApplyTemplateRequest>();
      if (!input.startDate)
        return json(
          { errorCode: "validation", error: "A start date is required." },
          400,
        );
      const copy = newPlan(
        {
          name: input.name ?? plan.name,
          description: plan.description,
          startDate: input.startDate,
          durationDays: plan.durationDays,
          householdId: input.householdId ?? null,
          rules: plan.rules,
          entries: [],
          prepSessions: plan.prepSessions.map((session) => ({
            dayIndex: session.dayIndex,
            title: session.title,
            notes: session.notes,
          })),
        },
        {},
      );
      copy.entries = plan.entries.map((entry) => ({
        ...entry,
        id: crypto.randomUUID(),
        logged: false,
        prepSessionId: entry.prepSessionId
          ? (copy.prepSessions[
              plan.prepSessions.findIndex((s) => s.id === entry.prepSessionId)
            ]?.id ?? null)
          : null,
      }));
      planning.plans.push(copy);
      record(input as unknown as Record<string, unknown>);
      return json(created(copy), 201);
    }
    if (!template[2] && method === "PUT") {
      const input = await body<Schemas["UpdateTemplateRequest"]>();
      Object.assign(plan, {
        name: input.name,
        description: input.description ?? null,
        durationDays: input.durationDays,
        rules: input.rules ?? plan.rules,
        updatedAt: now,
      });
      record(input as unknown as Record<string, unknown>);
      return json(null, 204);
    }
    if (!template[2] && method === "DELETE") {
      planning.plans = planning.plans.filter((item) => item !== plan);
      record({});
      return json(null, 204);
    }
  }
  const planRoute = path.match(
    /^\/api\/MealPlans\/([^/]+)(?:\/(status|accept|decline|entries|prep-sessions|shopping-list))?(?:\/([^/]+))?(?:\/(log))?$/,
  );
  if (planRoute) {
    const plan = planning.plans.find((item) => item.id === planRoute[1]);
    if (!plan) return notFound(json, "Plan");
    const [, , action, childId, log] = planRoute;
    if (!action && method === "GET") return json(detail(plan, host));
    if (!action && method === "PUT") {
      const input = await body<UpdateMealPlanRequest>();
      if (!(input.durationDays >= 1 && input.durationDays <= 31))
        return json(
          { errorCode: "validation", error: "Length is 1 to 31 days." },
          400,
        );
      if (plan.entries.some((entry) => entry.dayIndex >= input.durationDays))
        return json(
          {
            errorCode: "validation",
            error: "The plan has entries past that length.",
          },
          400,
        );
      Object.assign(plan, {
        name: input.name ?? null,
        description: input.description ?? null,
        startDate:
          plan.kind === "Template" ? null : (input.startDate ?? plan.startDate),
        durationDays: input.durationDays,
        rules: input.rules ?? plan.rules,
        updatedAt: now,
      });
      record(input as unknown as Record<string, unknown>);
      return json(null, 204);
    }
    if (!action && method === "DELETE") {
      planning.plans = planning.plans.filter((item) => item !== plan);
      record({});
      return json(null, 204);
    }
    const archiveOthers = () => {
      for (const other of personal)
        if (
          other !== plan &&
          other.status === "Active" &&
          other.userId === plan.userId &&
          !other.householdId
        )
          other.status = "Archived";
    };
    if (action === "status" && method === "POST") {
      const input = await body<Schemas["SetMealPlanStatusRequest"]>();
      if (input.status !== "Active" && input.status !== "Archived")
        return json(
          { errorCode: "validation", error: "Status is Active or Archived." },
          400,
        );
      plan.status = input.status;
      plan.updatedAt = now;
      if (input.status === "Active") archiveOthers();
      record({ status: input.status });
      return json(null, 204);
    }
    if (action === "accept" && method === "POST") {
      if (plan.status !== "Draft")
        return json(
          { errorCode: "conflict", error: "Only a draft can be accepted." },
          409,
        );
      plan.status = "Active";
      plan.updatedAt = now;
      archiveOthers();
      record({});
      return json(null, 204);
    }
    if (action === "decline" && method === "POST") {
      if (plan.status !== "Draft")
        return json(
          { errorCode: "conflict", error: "Only a draft can be declined." },
          409,
        );
      plan.status = "Archived";
      plan.updatedAt = now;
      record({});
      return json(null, 204);
    }
    if (action === "entries") {
      if (!childId && method === "POST") {
        const input = await body<PlanEntryRequest>();
        const problem = validEntry(input, plan);
        if (problem)
          return json({ errorCode: "validation", error: problem }, 400);
        const entry = storedEntry(crypto.randomUUID(), input, plan);
        plan.entries.push(entry);
        plan.updatedAt = now;
        record(input as unknown as Record<string, unknown>);
        return json(
          { id: entry.id } satisfies Schemas["PlanEntryCreatedResult"],
          201,
        );
      }
      const entry = plan.entries.find((item) => item.id === childId);
      if (!entry) return notFound(json, "Entry");
      if (log && method === "POST") {
        if (entry.kind === "Text")
          return json(
            {
              errorCode: "validation",
              error: "A text entry has no nutrition to log.",
            },
            400,
          );
        const input = await body<Schemas["LogPlanEntryRequest"]>();
        const dto = entryDto(plan, entry, host);
        const date = input.date ?? dto.date ?? today;
        const mealType = input.mealType ?? entry.slot;
        entry.logged = true;
        const diaryId = crypto.randomUUID();
        host.meals.push({
          id: diaryId,
          foodId: entry.foodId ?? undefined,
          recipeId: entry.recipeId ?? undefined,
          name: dto.recipeTitle ?? dto.foodName ?? "Plan entry",
          mealType,
          servings: entry.servings ?? (entry.grams ?? 0) / 100,
          calories: dto.nutrition?.calories,
          proteinGrams: dto.nutrition?.proteinGrams,
          carbsGrams: dto.nutrition?.carbsGrams,
          fatGrams: dto.nutrition?.fatGrams,
          fiberGrams: dto.nutrition?.fiberGrams,
          loggedAt: `${date}T12:00:00Z`,
        });
        record({ entryId: entry.id, ...input });
        const result: LogPlanEntryResult = {
          diaryEntryIds: [diaryId],
          calories: dto.nutrition?.calories ?? 0,
          proteinGrams: dto.nutrition?.proteinGrams ?? 0,
          carbsGrams: dto.nutrition?.carbsGrams ?? 0,
          fatGrams: dto.nutrition?.fatGrams ?? 0,
          date,
          mealType,
          message: `${dto.recipeTitle ?? dto.foodName} logged`,
          unlockedAchievements: [],
        };
        return json(result);
      }
      if (!log && method === "PUT") {
        const input = await body<PlanEntryRequest>();
        const problem = validEntry(input, plan);
        if (problem)
          return json({ errorCode: "validation", error: problem }, 400);
        Object.assign(entry, storedEntry(entry.id, input, plan, entry.logged));
        plan.updatedAt = now;
        record(input as unknown as Record<string, unknown>);
        return json(null, 204);
      }
      if (!log && method === "DELETE") {
        plan.entries = plan.entries.filter((item) => item !== entry);
        plan.updatedAt = now;
        record({});
        return json(null, 204);
      }
    }
    if (action === "prep-sessions") {
      if (!childId && method === "POST") {
        const input = await body<PrepSessionRequest>();
        if (!input.title?.trim())
          return json(
            { errorCode: "validation", error: "A session needs a title." },
            400,
          );
        const session: StoredSession = {
          id: crypto.randomUUID(),
          dayIndex: input.dayIndex,
          title: input.title,
          notes: input.notes ?? null,
        };
        plan.prepSessions.push(session);
        record(input as unknown as Record<string, unknown>);
        return json(
          { id: session.id } satisfies Schemas["PrepSessionCreatedResult"],
          201,
        );
      }
      const session = plan.prepSessions.find((item) => item.id === childId);
      if (!session) return notFound(json, "Session");
      if (method === "PUT") {
        const input = await body<PrepSessionRequest>();
        Object.assign(session, {
          dayIndex: input.dayIndex,
          title: input.title,
          notes: input.notes ?? null,
        });
        record(input as unknown as Record<string, unknown>);
        return json(null, 204);
      }
      if (method === "DELETE") {
        plan.prepSessions = plan.prepSessions.filter(
          (item) => item !== session,
        );
        for (const entry of plan.entries)
          if (entry.prepSessionId === session.id) entry.prepSessionId = null;
        record({});
        return json(null, 204);
      }
    }
    if (action === "shopping-list" && method === "POST") {
      const input = await body<Schemas["GenerateShoppingListRequest"]>();
      const items = aggregate(plan, host);
      let list = planning.lists.find((item) => item.id === input.replaceListId);
      if (list) {
        list.items = items;
        list.generatedAt = now;
        list.updatedAt = now;
      } else {
        list = {
          id: crypto.randomUUID(),
          name: input.name ?? `${plan.name ?? "Plan"} shopping`,
          userId: host.userId,
          householdId: plan.householdId,
          mealPlanId: plan.id,
          generatedAt: now,
          items,
          createdAt: now,
          updatedAt: now,
        };
        planning.lists.push(list);
      }
      record(input as unknown as Record<string, unknown>);
      const result: GenerateShoppingListResult = {
        listId: list.id,
        itemCount: items.length,
        needsReviewCount: items.filter((item) => item.needsReview).length,
      };
      return json(result, 201);
    }
  }

  if (path === "/api/Trainers/clients" && method === "GET")
    return json(paged(planning.trainerClients, url));
  const clientPlan = path.match(
    /^\/api\/Trainers\/clients\/([^/]+)\/meal-plans$/,
  );
  if (clientPlan && method === "POST") {
    const client = planning.trainerClients.find(
      (item) => item.clientId === clientPlan[1],
    );
    if (!client) return notFound(json, "Client");
    if (!client.canPlanMeals)
      return json(
        {
          errorCode: "forbidden",
          error: "This client has not granted meal planning.",
        },
        403,
      );
    const input = await body<CreateMealPlanRequest>();
    const plan = newPlan(input, {
      status: "Draft",
      userId: client.clientId,
      authoredByUserId: host.userId,
      authoredByName: "You",
    });
    planning.plans.push(plan);
    record(input as unknown as Record<string, unknown>);
    return json(created(plan), 201);
  }

  if (path === "/api/ShoppingLists" && method === "GET")
    return json(
      paged(
        [...planning.lists]
          .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
          .map(listSummary),
        url,
      ),
    );
  if (path === "/api/ShoppingLists" && method === "POST") {
    const input = await body<Schemas["CreateShoppingListRequest"]>();
    if (!input.name?.trim())
      return json(
        { errorCode: "validation", error: "A list needs a name." },
        400,
      );
    const list: StoredList = {
      id: crypto.randomUUID(),
      name: input.name,
      userId: host.userId,
      householdId: input.householdId ?? null,
      mealPlanId: null,
      generatedAt: null,
      items: [],
      createdAt: now,
      updatedAt: now,
    };
    planning.lists.push(list);
    record(input as unknown as Record<string, unknown>);
    return json(list.id, 201);
  }
  const itemRoute = path.match(
    /^\/api\/ShoppingLists\/items\/([^/]+)(\/toggle)?$/,
  );
  if (itemRoute) {
    const list = planning.lists.find((candidate) =>
      candidate.items.some((item) => item.id === itemRoute[1]),
    );
    const item = list?.items.find((candidate) => candidate.id === itemRoute[1]);
    if (!list || !item) return notFound(json, "Item");
    if (itemRoute[2] && method === "PATCH") {
      const input = await body<Schemas["ToggleShoppingListItemRequest"]>();
      item.isChecked = Boolean(input.isChecked);
      list.updatedAt = now;
      record({ isChecked: input.isChecked });
      return json(null, 204);
    }
    if (!itemRoute[2] && method === "PUT") {
      const input = await body<Schemas["UpdateShoppingListItemRequest"]>();
      if (input.itemName != null) item.itemName = input.itemName;
      if (input.amount !== undefined) item.amount = input.amount;
      if (input.unit !== undefined) item.unit = input.unit;
      if (input.grams !== undefined) item.grams = input.grams;
      if (input.category !== undefined) item.category = input.category;
      if (input.isChecked != null) item.isChecked = input.isChecked;
      if (input.needsReview != null) item.needsReview = input.needsReview;
      if (input.sortOrder != null) item.sortOrder = input.sortOrder;
      list.updatedAt = now;
      record(input as unknown as Record<string, unknown>);
      return json(null, 204);
    }
    if (!itemRoute[2] && method === "DELETE") {
      list.items = list.items.filter((candidate) => candidate !== item);
      list.updatedAt = now;
      record({});
      return json(null, 204);
    }
  }
  const listRoute = path.match(
    /^\/api\/ShoppingLists\/([^/]+)(?:\/(items|clear-checked))?$/,
  );
  if (listRoute) {
    const list = planning.lists.find((item) => item.id === listRoute[1]);
    if (!list) return notFound(json, "List");
    if (!listRoute[2] && method === "GET") return json(listDetail(list, host));
    if (!listRoute[2] && method === "PUT") {
      const input = await body<Schemas["UpdateShoppingListRequest"]>();
      if (!input.name?.trim())
        return json(
          { errorCode: "validation", error: "A list needs a name." },
          400,
        );
      list.name = input.name;
      list.updatedAt = now;
      record({ name: input.name });
      return json(null, 204);
    }
    if (!listRoute[2] && method === "DELETE") {
      planning.lists = planning.lists.filter((item) => item !== list);
      record({});
      return json(null, 204);
    }
    if (listRoute[2] === "items" && method === "POST") {
      const input = await body<Schemas["AddShoppingListItemRequest"]>();
      if (!input.itemName?.trim())
        return json(
          { errorCode: "validation", error: "An item needs a name." },
          400,
        );
      const item: ShoppingListItem = {
        id: crypto.randomUUID(),
        foodId: input.foodId ?? null,
        itemName: input.itemName,
        amount: input.amount ?? null,
        unit: input.unit ?? null,
        grams: input.grams ?? null,
        category: input.category ?? null,
        needsReview: false,
        prepSessionId: input.prepSessionId ?? null,
        sortOrder: list.items.length,
        isChecked: false,
      };
      list.items.push(item);
      list.updatedAt = now;
      record(input as unknown as Record<string, unknown>);
      return json(item.id);
    }
    if (listRoute[2] === "clear-checked" && method === "POST") {
      const before = list.items.length;
      list.items = list.items.filter((item) => !item.isChecked);
      list.updatedAt = now;
      record({});
      return json({
        removed: before - list.items.length,
      } satisfies Schemas["ClearCheckedResult"]);
    }
  }
  return null;
}
