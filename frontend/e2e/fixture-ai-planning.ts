import type { components } from "../types/api.generated";
import {
  addDays,
  detail,
  LIST_ID,
  type PlanningHost,
  type StoredEntry,
  type StoredPlan,
} from "./fixture-planning";

/**
 * The assistant's planning half of the UI fixture: AI consent, usage with
 * its planning line, POST /api/MealPlans/generate and a chat turn that
 * carries typed cards. Typed from the generated OpenAPI schema; a generated
 * draft is saved into the planning fixture's plans, so the plan pages, the
 * list and the decline route treat it like the API's own, and a chat turn is
 * saved into the conversations, so the assistant lists it and opens it again.
 * As in the API, a stored message keeps its text; the cards belong to the
 * turn. Every write lands in `writes[]` with its Idempotency-Key.
 */
type Schemas = components["schemas"];
type AiConsent = Schemas["AiConsentDto"];
type MyAiUsage = Schemas["MyAiUsageDto"];
type GenerateRequest = Schemas["GenerateMealPlanCommand"];
type GenerateResult = Schemas["GenerateMealPlanResult"];
type PlanCost = Schemas["PlanCostDto"];
type ChatTurn = Schemas["AiChatTurnDto"];
/** A stored conversation, as GET /api/Ai/threads/{id} answers it. */
type ChatThread = Schemas["AiChatThreadDetailDto"];

export type AiPlanningOptions = {
  /** Whether the person let the assistant use their log (default false, like the API). */
  consented?: boolean;
  /** Plans already drafted today against the Free allowance of one. */
  plansUsed?: number;
  /** The provider fails every call: generate answers 503 `ai_unavailable`. */
  providerDown?: boolean;
  /** The catalogue lacks the foods: generate answers 422 `plan_needs_foods`. */
  needsFoods?: boolean;
};
export type AiPlanningState = {
  consent: AiConsent;
  plansUsed: number;
  planLimit: number;
  providerDown: boolean;
  needsFoods: boolean;
};
export type AiPlanningHost = {
  ai: AiPlanningState;
  planning: PlanningHost;
  /** The fixture's conversations, newest first; a planning turn is saved into them like any other. */
  threads: ChatThread[];
};

/** The words that make the fixture's chat answer with a drafted week and its cards. */
export const PLAN_PROMPT = "plan my week";

const YOGURT_ID = "11111111-1111-4111-8111-111111111111";
const OATS_ID = "22222222-2222-4222-8222-222222222222";
const RECIPE_ID = "33333333-3333-4333-8333-333333333333";
const UNRESOLVED = "Shiro wat with injera";

export function initialAiPlanning(
  options: AiPlanningOptions = {},
): AiPlanningState {
  const on = options.consented ?? false;
  return {
    consent: {
      enabled: on,
      shareNutrition: on,
      shareTraining: on,
      shareBody: on,
      allowWrites: on,
      writeNutrition: on,
      writeTraining: on,
      writeBody: on,
      updatedAt: on ? "2026-10-01T09:00:00Z" : null,
    },
    plansUsed: options.plansUsed ?? 0,
    planLimit: 1,
    providerDown: options.providerDown ?? false,
    needsFoods: options.needsFoods ?? false,
  };
}

type Json = (data: unknown, status?: number) => Response;

function cost(budget: number | null): PlanCost {
  return {
    regionCode: "ET-AA",
    currency: "ETB",
    total: 1180,
    weeklyTotal: 1180,
    weeklyBudget: budget,
    pricedItems: 2,
    totalItems: 3,
    coverage: 0.67,
    lowestConfidence: "Observed",
    items: [
      {
        name: "Greek yogurt",
        foodId: YOGURT_ID,
        grams: 1400,
        cost: 760,
        confidence: "Observed",
        reason: null,
      },
      {
        name: "Rolled oats",
        foodId: OATS_ID,
        grams: 560,
        cost: 420,
        confidence: "Observed",
        reason: null,
      },
      {
        name: UNRESOLVED,
        foodId: null,
        grams: null,
        cost: null,
        confidence: null,
        reason: "Not a catalogue food",
      },
    ],
  };
}

function entry(
  dayIndex: number,
  slot: string,
  fields: Partial<StoredEntry>,
): StoredEntry {
  return {
    id: crypto.randomUUID(),
    dayIndex,
    slot,
    kind: "Food",
    recipeId: null,
    servings: null,
    foodId: null,
    grams: null,
    text: null,
    notes: null,
    sortOrder: 0,
    prepSessionId: null,
    logged: false,
    ...fields,
  };
}

/** What GenerateMealPlanCommand saves: a Draft with `generatedFrom`, one recipe, foods and a line to review. */
function saveDraft(
  host: AiPlanningHost,
  input: GenerateRequest,
  budget: number | null,
): StoredPlan {
  const now = new Date().toISOString();
  const days = input.durationDays;
  const entries = [
    entry(0, "BREAKFAST", {
      kind: "Recipe",
      recipeId: RECIPE_ID,
      servings: 1,
    }),
    entry(0, "LUNCH", { foodId: OATS_ID, grams: 80 }),
    entry(0, "SNACK", { foodId: YOGURT_ID, grams: 200 }),
    entry(Math.min(1, days - 1), "DINNER", {
      kind: "Text",
      text: UNRESOLVED,
      notes: "Review: no catalogue food matched this line.",
    }),
  ];
  const plan: StoredPlan = {
    id: crypto.randomUUID(),
    name: input.name ?? "High-protein week",
    description: null,
    kind: "Personal",
    status: "Draft",
    startDate: input.startDate,
    durationDays: days,
    userId: host.planning.userId,
    householdId: null,
    authoredByUserId: null,
    authoredByName: null,
    global: false,
    rules: {
      ...(input.rules ?? {
        macroMode: "None",
        allowedFoodIds: [],
        forbiddenFoodIds: [],
        prepStyle: "Daily",
        batchDays: [],
      }),
      weeklyBudget: budget,
      dailyBudget: input.dailyBudget ?? null,
      currency: budget === null && input.dailyBudget == null ? null : "ETB",
    },
    entries,
    prepSessions: [],
    generatedFrom: {
      promptKey: "meal.plan",
      promptVersionId: null,
      promptVersion: 3,
      model: "fixture-model",
      budgetCeiling: budget,
      currency: "ETB",
      generatedAt: now,
    },
    createdAt: now,
    updatedAt: now,
  };
  host.planning.planning.plans.push(plan);
  host.ai.plansUsed += 1;
  return plan;
}

export async function handleAiPlanning(
  request: Request,
  url: URL,
  host: AiPlanningHost,
  today: string,
  json: Json,
): Promise<Response | null> {
  const { ai, planning } = host;
  const path = url.pathname;
  const method = request.method;
  const record = (data: Record<string, unknown>) =>
    planning.writes.push({
      path,
      body: data,
      key: request.headers.get("Idempotency-Key") ?? undefined,
    });
  const resetsAt = `${addDays(today, 1)}T00:00:00Z`;

  if (path === "/api/Ai/consent" && method === "GET") return json(ai.consent);
  if (path === "/api/Ai/consent" && method === "PUT") {
    const input = (await request.json()) as Omit<AiConsent, "updatedAt">;
    ai.consent = { ...input, updatedAt: new Date().toISOString() };
    record(input);
    return json(ai.consent);
  }
  if (path === "/api/Ai/usage" && method === "GET")
    return json({
      today: {
        requestsUsed: 0,
        requestLimit: 20,
        tokensUsed: 0,
        tokenLimit: 50000,
        resetsAt: `${today}T23:59:59Z`,
        plan: "Free",
      },
      history: [],
      byFeature: [],
      planning: {
        requestsUsed: ai.plansUsed,
        requestLimit: ai.planLimit,
        tokensUsed: ai.plansUsed * 2400,
        tokenLimit: 30000,
        resetsAt,
        plan: "Free",
      },
    } satisfies MyAiUsage);

  if (path === "/api/MealPlans/generate" && method === "POST") {
    const input = (await request.json()) as GenerateRequest;
    record(input as unknown as Record<string, unknown>);
    if (!input.startDate)
      return json(
        { errorCode: "validation_failed", error: "A plan needs a start date." },
        400,
      );
    if (!(input.durationDays >= 1 && input.durationDays <= 14))
      return json(
        {
          errorCode: "validation_failed",
          error: "A generated plan runs for 1 to 14 days.",
        },
        400,
      );
    if (ai.plansUsed >= ai.planLimit)
      return json(
        {
          errorCode: "ai_quota_exceeded",
          error:
            "You have used your AI allowance for now. It resets at 00:00 UTC.",
          scope: "user",
          resetsAt,
        },
        429,
      );
    if (ai.providerDown)
      return json(
        {
          errorCode: "ai_unavailable",
          error: "The assistant could not draft a plan right now.",
        },
        503,
      );
    if (ai.needsFoods)
      return json(
        {
          errorCode: "plan_needs_foods",
          error:
            "Days 2 and 3 of the draft have no food from your catalogue. Add foods such as tilapia and gouda, or ask for something else.",
          missingFoods: ["Tilapia", "Gouda"],
          emptyDays: [1, 2],
        } satisfies Schemas["PlanNeedsFoodsError"],
        422,
      );
    const budget = input.budgetCeilings[0] ?? null;
    const plan = saveDraft(host, input, budget);
    const read = detail(plan, planning);
    return json({
      drafts: [
        {
          plan: read,
          budgetCeiling: budget,
          violations: read.violations,
          cost: cost(budget),
          unresolved: [
            {
              dayIndex: Math.min(1, plan.durationDays - 1),
              slot: "DINNER",
              text: UNRESOLVED,
              reason: "No catalogue food matched this line.",
            },
          ],
          note: "Breakfast repeats so the yogurt is bought once.",
        },
      ],
      skipped: [],
    } satisfies GenerateResult);
  }

  if (path === "/api/Ai/chat" && method === "POST") {
    const input = (await request.clone().json()) as {
      message: string;
      threadId: string | null;
    };
    if (!input.message.toLowerCase().includes(PLAN_PROMPT)) return null;
    record({ message: input.message });
    const plan = saveDraft(
      host,
      { startDate: addDays(today, 1), durationDays: 7, budgetCeilings: [] },
      1500,
    );
    const read = detail(plan, planning);
    const now = new Date().toISOString();
    const thread = host.threads.find((item) => item.id === input.threadId) ?? {
      id: crypto.randomUUID(),
      title: "Plan my week",
      updatedAt: now,
      messages: [],
    };
    const message = {
      id: crypto.randomUUID(),
      fromUser: true,
      content: input.message,
      createdAt: now,
      imageUrl: null,
    };
    const reply = {
      id: crypto.randomUUID(),
      fromUser: false,
      content:
        "Here is a draft for next week. It stays a draft until you accept it.",
      createdAt: now,
      imageUrl: null,
    };
    thread.messages.push(message, reply);
    thread.updatedAt = now;
    const known = host.threads.indexOf(thread);
    if (known >= 0) host.threads.splice(known, 1);
    host.threads.unshift(thread);
    return json({
      threadId: thread.id,
      title: thread.title,
      message,
      reply,
      performed: [
        {
          tool: "generate_meal_plan",
          summary: `Drafted ${read.name}`,
          succeeded: true,
          error: null,
        },
      ],
      cards: [
        {
          type: "plan_preview",
          data: {
            planId: plan.id,
            name: read.name,
            status: read.status,
            startDate: read.startDate,
            durationDays: read.durationDays,
            entryCount: read.entryCount,
            unresolvedCount: 1,
            budgetCeiling: 1500,
            weeklyCost: 1180,
            currency: "ETB",
            violations: [
              {
                dayIndex: null,
                slot: null,
                rule: "Budget",
                message: "Two lines have no price, so the week may cost more.",
              },
            ],
          } satisfies Schemas["PlanPreviewCardData"],
        },
        {
          type: "day_plan",
          data: {
            planId: plan.id,
            planName: read.name,
            day: read.days[0],
          } satisfies Schemas["DayPlanCardData"],
        },
        {
          type: "cost_table",
          data: {
            planId: plan.id,
            cost: cost(1500),
          } satisfies Schemas["CostTableCardData"],
        },
        {
          type: "shopping_list",
          data: {
            listId: LIST_ID,
            itemCount: 6,
            needsReviewCount: 1,
          } satisfies Schemas["ShoppingListCardData"],
        },
        // A type this web build does not know: the reply's text stands alone.
        { type: "recipe_card", data: { title: "Not rendered" } },
      ],
      consentRequired: [],
    } satisfies ChatTurn);
  }
  return null;
}
