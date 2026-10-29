import type { BodyMeasurement } from "../data/bodyMeasurement";
import type { Ingredient } from "../data/ingredient";
import type { MealEntry } from "../data/meal";
import type { WorkoutSummary } from "../data/workout";
import type { components } from "../types/api.generated";
import {
  handlePlanning,
  initialPlanning,
  type PlanningOptions,
  type PlanningState,
} from "./fixture-planning";
import {
  handlePrices,
  initialPrices,
  type PricesOptions,
  type PricesState,
} from "./fixture-prices";
import { handleTelegram } from "./fixture-tg";
import { handleAdminSettings } from "./fixture-admin-settings";
import {
  handleUsers,
  initialTelegram,
  type TelegramOptions,
  type TelegramState,
} from "./fixture-users";
import { handleAdminOps, isAdminOps } from "./fixture-admin-ops";
import { handleAdminFlags } from "./fixture-admin-flags";
import { handleAdminNotifications } from "./fixture-admin-notifications";
import {
  handleAiPlanning,
  initialAiPlanning,
  type AiPlanningOptions,
  type AiPlanningState,
} from "./fixture-ai-planning";

const port = Number(process.env.PLAYWRIGHT_API_PORT ?? 5100);
const webOrigin = process.env.PLAYWRIGHT_BASE_URL ?? "http://localhost:3100";
const today = new Date().toISOString().slice(0, 10);
const dateAgo = (days: number) => {
  const date = new Date(`${today}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() - days);
  return date.toISOString().slice(0, 10);
};
const food: Ingredient = {
  id: "11111111-1111-4111-8111-111111111111",
  name: "Greek yogurt",
  servingSize: 100,
  servingUnit: "g",
  caloriesPer100g: 97,
  proteinPer100g: 9,
  carbsPer100g: 4,
  fatPer100g: 5,
  fiberPer100g: 0,
  proteinCalorieRatio: 37.1,
  isVerified: true,
};
const oats: Ingredient = {
  ...food,
  id: "22222222-2222-4222-8222-222222222222",
  name: "Rolled oats",
  caloriesPer100g: 389,
  proteinPer100g: 17,
  carbsPer100g: 66,
  fatPer100g: 7,
  fiberPer100g: 11,
};
const foods = [food, oats];
const user = {
  id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  email: "alex@example.test",
  name: "Alex Morgan",
  role: "user",
  emailVerified: true,
  themePreference: "light",
  compactMode: false,
  reduceAnimations: false,
  hasPassword: true,
  timeZoneId: "UTC",
  image: null,
};
const householdId = "55555555-5555-4555-8555-555555555555";
const scopeGroups = [
  {
    group: "nutrition",
    title: "Food and nutrition",
    description: "Food diary, foods, daily totals and calorie goals.",
    hasWrite: true,
    readScope: "nutrition:read",
    writeScope: "nutrition:write",
  },
  {
    group: "training",
    title: "Training",
    description: "Workouts, workout templates and exercises.",
    hasWrite: true,
    readScope: "training:read",
    writeScope: "training:write",
  },
  {
    group: "planning",
    title: "Meal plans and shopping",
    description: "Meal plans and shopping lists.",
    hasWrite: true,
    readScope: "planning:read",
    writeScope: "planning:write",
  },
  {
    group: "ai",
    title: "Mizan assistant",
    description: "Ask the assistant and analyze photos.",
    hasWrite: false,
    readScope: "ai:use",
    writeScope: null,
  },
];
type FixtureConnection = {
  id: string;
  clientName: string;
  source: string;
  verifiedHost: string | null;
  logoUri: null;
  isFirstParty: boolean;
  scopes: string[];
  householdMode: string;
  households: { id: string; name: string }[];
  createdAt: string;
  lastUsedAt: string | null;
  calls30Days: number;
  failed30Days: number;
};
const consentRequest = {
  clientName: "Claude",
  source: "dynamic",
  verifiedHost: null,
  logoUri: null,
  clientUri: null,
  redirectHost: "localhost",
  audience: "mcp",
  requestedScopes: [],
  scopeGroups,
  isFirstParty: false,
  expiresAt: `${today}T23:59:00Z`,
  households: [{ id: householdId, name: "Home" }],
  existingGrant: null,
};
type FixtureHousehold = {
  otherMembers: number;
  lists: number;
  plans: number;
  version: number;
  staleOnce: boolean;
};
// What an admin has on sale: $2.99 monthly, $24 yearly with a first-year deal.
const plans = [
  {
    id: "66666666-6666-4666-8666-666666666661",
    name: "Pro Monthly",
    description: null,
    interval: "month",
    amountCents: 299,
    currency: "USD",
    trialDays: null,
    paddlePriceId: "pri_fixture_monthly",
    deal: null,
  },
  {
    id: "66666666-6666-4666-8666-666666666662",
    name: "Pro Yearly",
    description: null,
    interval: "year",
    amountCents: 2400,
    currency: "USD",
    trialDays: 7,
    paddlePriceId: "pri_fixture_yearly",
    deal: {
      paddleDiscountId: "dsc_fixture",
      label: "Launch offer",
      type: "percentage",
      amount: 25,
      recurring: false,
      maximumRecurringIntervals: null,
      expiresAt: null,
      discountedAmountCents: 1800,
    },
  },
];
function householdPreview(household: FixtureHousehold) {
  return {
    householdId,
    householdName: "Home",
    otherMemberCount: household.otherMembers,
    shoppingListCount: household.lists,
    shoppingListItemCount: household.lists * 3,
    mealPlanCount: household.plans,
    mealPlanRecipeCount: household.plans * 5,
    pendingInvitationCount: 0,
    version: `v${household.version}`,
  };
}
/** A diary row as GET /api/Meals sends it (FoodDiaryEntryDto); a logged recipe's rows share a group. */
type DiaryRow = MealEntry;
type DiaryTotals = components["schemas"]["DailyTotalsDto"];
type MealPreviewLine = components["schemas"]["MealPreviewLine"];
type MealPreviewResult = components["schemas"]["MealPreviewResult"];
type FixtureState = {
  userId: string;
  meals: DiaryRow[];
  measurements: BodyMeasurement[];
  workouts: WorkoutSummary[];
  recipes: Record<string, unknown>[];
  draft: { payload: string; updatedAt: string } | null;
  empty: boolean;
  notifications: FixtureNotification[];
  notificationPreferences: FixtureNotificationPreferences;
  reminders: FixtureReminder[];
  failures: string[];
  writeFailures: Record<string, number>;
  unhandled: string[];
  household: FixtureHousehold | null;
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
  connections: FixtureConnection[];
  role: string;
  pro: boolean;
  impersonating: boolean;
  threads: FixtureThread[];
  exercises: FixtureExercise[];
  planning: PlanningState;
  prices: PricesState;
  aiPlanning: AiPlanningState;
  telegram: TelegramState;
};
type FixtureNotification = components["schemas"]["NotificationDto"];
type FixtureReminder = components["schemas"]["ReminderDto"];

/** A reminder as the API would answer for a request, with the API's wording fixed for the fixture. */
function fixtureReminder(
  body: components["schemas"]["CreateReminderRequest"],
): FixtureReminder {
  const schedule = body.schedule;
  const summary =
    body.kind === "pre_cook"
      ? `Once, before the prep at ${body.prepTime ?? "18:00"}`
      : schedule?.repeat === "days" && schedule.days?.length === 5
        ? `Weekdays at ${schedule.time}`
        : schedule?.repeat === "every"
          ? "Every 2 hours"
          : `Every day at ${schedule?.time ?? "07:00"}`;
  const titles: Record<string, string> = {
    workout: "Time for your workout",
    measurement: "Time to take your measurements",
    meal_log: "Log your meal",
    pre_cook: "Time to start the prep",
  };
  return {
    id: crypto.randomUUID(),
    kind: body.kind,
    kindLabel: body.kind,
    title: body.title || titles[body.kind] || "Reminder",
    repeat: schedule?.repeat ?? "once",
    time: schedule?.time ?? null,
    days: schedule?.days ?? [],
    summary,
    channels: ["inApp", "telegram", "push", "email"],
    enabled: true,
    active: true,
    nextRunAt: "2026-11-02T04:00:00Z",
    nextRunText: "Mon 2 Nov at 07:00",
    workoutTemplateId: body.workoutTemplateId ?? null,
    mealPlanId: body.mealPlanId ?? null,
    prepSessionId: body.prepSessionId ?? null,
    actionLabel: "Open Mizan",
    actionPath: "/today",
    createdAt: new Date().toISOString(),
  };
}
type FixtureNotificationPreferences =
  components["schemas"]["NotificationPreferencesDto"];

/** The kinds GET /api/Notifications/preferences lists, with the API's defaults: news and offers off. */
function initialNotificationPreferences(
  telegram?: Pick<TelegramState, "linked" | "notifyByTelegram">,
): FixtureNotificationPreferences {
  const kinds: [string, string, string][] = [
    [
      "achievements",
      "Achievements and streaks",
      "Badges you earn, streak milestones and goal targets you reach.",
    ],
    [
      "reminders",
      "Reminders",
      "Reminders you set, and the evening reminder before your eating window closes.",
    ],
    [
      "planning",
      "Meal plans",
      "Plans drafted for you, and answers to plans you sent.",
    ],
    [
      "marketing",
      "News and offers",
      "Product news and offers from Mizan. Off until you turn it on.",
    ],
  ];
  return {
    kinds: kinds.map(([kind, label, description]) => {
      const on = kind !== "marketing";
      return {
        kind,
        label,
        description,
        inApp: on,
        telegram: on,
        push: on,
        email: false,
      };
    }),
    quietHoursStart: null,
    quietHoursEnd: null,
    timeZoneId: "Africa/Addis_Ababa",
    telegramLinked: Boolean(telegram?.linked),
    telegramEnabled: Boolean(telegram?.notifyByTelegram),
  };
}
type FixtureThread = {
  id: string;
  title: string;
  updatedAt: string;
  messages: {
    id: string;
    fromUser: boolean;
    content: string;
    createdAt: string;
    imageUrl?: string | null;
  }[];
};
type FixtureExercise = {
  id: string;
  name: string;
  category: string;
  muscleGroup?: string | null;
  equipment?: string | null;
  description?: string | null;
  videoUrl?: string | null;
  imageUrl?: string | null;
  modelUrl?: string | null;
  isCustom: boolean;
  isApproved: boolean;
};
// A one-pixel PNG, so a "stored" picture really renders.
const pixel = `data:image/svg+xml;utf8,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="240" height="160"><rect width="240" height="160" fill="#c9d6cf"/><circle cx="120" cy="80" r="50" fill="#e8dcc4"/></svg>')}`;
const analysis = {
  foods: [
    {
      name: "Scrambled eggs",
      portionGrams: 120,
      calories: 180,
      protein: 12,
      carbs: 2,
      fat: 14,
      per100g: {
        calories: 150,
        proteinGrams: 10,
        carbsGrams: 1.67,
        fatGrams: 11.67,
      },
    },
    {
      name: "Toast with butter",
      portionGrams: 60,
      calories: 190,
      protein: 4,
      carbs: 22,
      fat: 9,
      per100g: {
        calories: 316.67,
        proteinGrams: 6.67,
        carbsGrams: 36.67,
        fatGrams: 15,
      },
    },
  ],
  totalCalories: 370,
  confidence: 0.7,
  note: "Assumed two eggs and one slice.",
};
const states = new Map<string, FixtureState>();
function initialState(empty = false): FixtureState {
  return {
    planning: initialPlanning(user.id, today, { none: empty }),
    prices: initialPrices(today),
    aiPlanning: initialAiPlanning(),
    telegram: initialTelegram(),
    role: "user",
    pro: false,
    impersonating: false,
    threads: empty
      ? []
      : [
          {
            id: "thread-1",
            title: "Protein ideas",
            updatedAt: `${dateAgo(1)}T09:00:00Z`,
            messages: [
              {
                id: "m1",
                fromUser: true,
                content: "How is my protein this week?",
                createdAt: `${dateAgo(1)}T09:00:00Z`,
              },
              {
                id: "m2",
                fromUser: false,
                content:
                  "You averaged **128 g** a day, close to your 140 g goal.",
                createdAt: `${dateAgo(1)}T09:00:05Z`,
              },
              {
                id: "m3",
                fromUser: true,
                content: "What about this plate?",
                createdAt: `${dateAgo(1)}T09:01:00Z`,
                imageUrl: pixel,
              },
              {
                id: "m4",
                fromUser: false,
                content: "It looks like rice and chicken, about 520 kcal.",
                createdAt: `${dateAgo(1)}T09:01:06Z`,
              },
            ],
          },
        ],
    exercises: [
      {
        id: "exercise-bench",
        name: "Bench press",
        category: "Strength",
        muscleGroup: "Chest",
        equipment: "Barbell",
        description: null,
        videoUrl: null,
        imageUrl: null,
        modelUrl: null,
        isCustom: false,
        isApproved: true,
      },
      {
        id: "exercise-row",
        name: "Barbell row",
        category: "Strength",
        muscleGroup: "Back",
        equipment: "Barbell",
        description: "Pull to the belly.",
        videoUrl: null,
        imageUrl: null,
        modelUrl: null,
        isCustom: true,
        isApproved: true,
      },
    ],
    userId: user.id,
    empty,
    notifications: [],
    notificationPreferences: initialNotificationPreferences(),
    reminders: [],
    draft: null,
    household: null,
    failures: [],
    writeFailures: {},
    unhandled: [],
    writes: [],
    connections: empty
      ? []
      : [
          {
            id: "c0000000-0000-4000-8000-000000000001",
            clientName: "Claude",
            source: "metadata",
            verifiedHost: "claude.ai",
            logoUri: null,
            isFirstParty: false,
            scopes: [
              "nutrition:read",
              "nutrition:write",
              "training:read",
              "ai:use",
            ],
            householdMode: "selected",
            households: [{ id: householdId, name: "Home" }],
            createdAt: `${dateAgo(9)}T08:00:00Z`,
            lastUsedAt: `${today}T07:30:00Z`,
            calls30Days: 42,
            failed30Days: 2,
          },
          {
            id: "c0000000-0000-4000-8000-000000000002",
            clientName: "Gemini CLI",
            source: "dynamic",
            verifiedHost: null,
            logoUri: null,
            isFirstParty: false,
            scopes: ["nutrition:read"],
            householdMode: "none",
            households: [],
            createdAt: `${dateAgo(2)}T08:00:00Z`,
            lastUsedAt: null,
            calls30Days: 0,
            failed30Days: 0,
          },
        ],
    recipes: empty
      ? []
      : [
          {
            id: "33333333-3333-4333-8333-333333333333",
            title: "Yogurt and oats",
            servings: 1,
            isFavorited: true,
            isOwner: true,
            timesLogged: 6,
            isPublic: true,
            description: "A breakfast to come back to.",
            instructions: "Stir the yogurt and oats together. Serve chilled.",
            ingredients: [
              {
                foodId: food.id,
                foodName: food.name,
                ingredientText: food.name,
                amount: 200,
                unit: "g",
              },
              {
                foodId: oats.id,
                foodName: oats.name,
                ingredientText: oats.name,
                amount: 50,
                unit: "g",
              },
            ],
            lastUsedAt: `${dateAgo(1)}T08:00:00Z`,
            nutrition: {
              caloriesPerServing: 389,
              proteinGrams: 26.5,
              carbsGrams: 41,
              fatGrams: 13.5,
              fiberGrams: 5.5,
            },
            unmeasuredIngredients: [],
            unresolvedIngredients: [],
          },
          {
            id: "44444444-4444-4444-8444-444444444444",
            title: "Imported lentil stew",
            servings: 4,
            isFavorited: false,
            isOwner: true,
            timesLogged: 0,
            isPublic: true,
            description: null,
            instructions: null,
            lastUsedAt: null,
            nutrition: null,
            ingredients: [
              {
                foodId: oats.id,
                foodName: oats.name,
                ingredientText: "200g oats",
                amount: 200,
                unit: "g",
              },
              {
                foodId: null,
                foodName: "",
                ingredientText: "1 lemon (50g)",
                amount: 50,
                unit: "g",
              },
              {
                foodId: null,
                foodName: "",
                ingredientText: "salt to taste",
                amount: null,
                unit: "",
              },
            ],
            unmeasuredIngredients: ["salt to taste"],
            unresolvedIngredients: ["1 lemon (50g)"],
          },
        ],
    meals: empty
      ? []
      : [
          {
            id: "meal-yogurt",
            foodId: food.id,
            name: "Greek yogurt",
            mealType: "BREAKFAST",
            servings: 2,
            calories: 194,
            proteinGrams: 18,
            carbsGrams: 8,
            fatGrams: 10,
            fiberGrams: 0,
            loggedAt: `${today}T08:15:00Z`,
          },
          {
            id: "meal-oats",
            foodId: oats.id,
            name: "Rolled oats",
            mealType: "BREAKFAST",
            servings: 0.5,
            calories: 195,
            proteinGrams: 8.5,
            carbsGrams: 33,
            fatGrams: 3.5,
            fiberGrams: 5.5,
            loggedAt: `${today}T08:15:00Z`,
          },
          {
            id: "meal-yesterday",
            name: "Chicken and rice",
            mealType: "LUNCH",
            servings: 1,
            calories: 620,
            proteinGrams: 42,
            carbsGrams: 65,
            fatGrams: 19,
            fiberGrams: 4,
            loggedAt: `${dateAgo(1)}T12:30:00Z`,
          },
        ],
    measurements: empty
      ? []
      : [0, 4, 8, 12, 16, 20, 24, 28].map((days, index) => ({
          id: `weight-${index}`,
          date: dateAgo(days),
          weightKg: 74.2 + index * 0.2,
          waistCm: 80.5,
        })),
    workouts: empty
      ? []
      : [0, 7, 14, 21].map((days, index) => ({
          id: `workout-${index}`,
          name: "Upper body",
          workoutDate: dateAgo(days),
          durationMinutes: 45,
          caloriesBurned: null,
          notes: null,
          createdAt: `${dateAgo(days)}T17:00:00Z`,
          exercises: [
            {
              id: "exercise-bench",
              exerciseId: "exercise-bench",
              exerciseName: "Bench press",
              category: "STRENGTH",
              muscleGroup: "Chest",
              sortOrder: 0,
              sets: [1, 2, 3].map((setNumber) => ({
                setNumber,
                reps: 8,
                weightKg: 60 - index * 2.5,
                durationSeconds: null,
                distanceMeters: null,
                completed: true,
              })),
            },
          ],
        })),
  };
}
/** EntryNutrition.Sum on the server: added up, then rounded to a tenth. */
function subtotal(entries: MealEntry[]): DiaryTotals {
  const sum = totals(entries);
  const tenth = (value: number) => Math.round(value * 10) / 10;
  return {
    calories: tenth(sum.calories),
    protein: tenth(sum.protein),
    carbs: tenth(sum.carbs),
    fat: tenth(sum.fat),
    fiber: tenth(sum.fiber),
  };
}
function totals(entries: MealEntry[]) {
  return entries.reduce(
    (sum, entry) => ({
      calories: sum.calories + (entry.calories ?? 0),
      protein: sum.protein + (entry.proteinGrams ?? 0),
      carbs: sum.carbs + (entry.carbsGrams ?? 0),
      fat: sum.fat + (entry.fatGrams ?? 0),
      fiber: sum.fiber + (entry.fiberGrams ?? 0),
    }),
    { calories: 0, protein: 0, carbs: 0, fat: 0, fiber: 0 },
  );
}
const achievements = [
  {
    id: "ac000000-0000-0000-0000-000000000001",
    name: "First Meal",
    description: "Log your first meal",
    iconUrl: "/badges/128/achievement-first-meal.png",
    points: 10,
    category: "nutrition",
    criteriaType: "meals_logged",
    threshold: 1,
    progress: 1,
    isEarned: true,
    earnedAt: `${dateAgo(3)}T12:00:00Z`,
  },
  {
    id: "ac000000-0000-0000-0000-000000000002",
    name: "Getting Started",
    description: "Log 10 meals",
    iconUrl: "/badges/128/achievement-getting-started.png",
    points: 25,
    category: "nutrition",
    criteriaType: "meals_logged",
    threshold: 10,
    progress: 3,
    isEarned: false,
    earnedAt: null,
  },
];
const goal = {
  id: "goal-1",
  targetCalories: 2200,
  targetProteinGrams: 140,
  targetCarbsGrams: 250,
  targetFatGrams: 70,
  targetWeight: 73,
  goalType: "maintenance",
  isActive: true,
  createdAt: `${dateAgo(30)}T12:00:00Z`,
};
function parameter(url: URL, name: string) {
  return [...url.searchParams].find(
    ([key]) => key.toLowerCase() === name.toLowerCase(),
  )?.[1];
}
function page<T>(items: T[], url: URL) {
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
function inRange(date: string, url: URL) {
  const from = parameter(url, "from");
  const to = parameter(url, "to");
  return (!from || date >= from) && (!to || date <= to);
}
function json(data: unknown, status = 200, headers: HeadersInit = {}) {
  return new Response(status === 204 ? null : JSON.stringify(data), {
    status,
    headers: {
      "Content-Type": "application/json",
      "Access-Control-Allow-Origin": webOrigin,
      "Access-Control-Allow-Credentials": "true",
      "Access-Control-Allow-Headers":
        "Content-Type,Idempotency-Key,ngrok-skip-browser-warning",
      "Access-Control-Allow-Methods": "GET,POST,PUT,PATCH,DELETE,OPTIONS",
      ...headers,
    },
  });
}
const server = Bun.serve({
  hostname: "127.0.0.1",
  port,
  async fetch(request) {
    const url = new URL(request.url);
    const path = url.pathname;
    const method = request.method;
    if (method === "OPTIONS") return json(null, 204);
    if (path === "/health") return json({ status: "fixture", today });
    if (path === "/__fixture/session" && method === "POST") {
      const options = (await request.json()) as {
        role?: string;
        pro?: boolean;
        userId?: string;
        empty?: boolean;
        unread?: number;
        failures?: string[];
        writeFailures?: Record<string, number>;
        household?: Partial<FixtureHousehold>;
        planning?: PlanningOptions;
        prices?: PricesOptions;
        aiPlanning?: AiPlanningOptions;
        telegram?: TelegramOptions;
        /** A recipe logged at dinner today, as two ingredient rows in one group. */
        recipeGroup?: boolean;
        /** An achievement notification as the writer stores it: badge, facts and two actions. */
        card?: boolean;
      };
      const token = crypto.randomUUID();
      const state = initialState(options.empty);
      // `unread` seeds that many unread notifications above one already read, newest first.
      if (options.unread)
        state.notifications = [
          ...Array.from({ length: options.unread }, (_, index) => ({
            id: `f4100000-0000-4000-8000-${String(index + 1).padStart(12, "0")}`,
            type: "achievement",
            title: `Achievement ${index + 1} unlocked`,
            body: null,
            linkUrl: null,
            createdAt: `${today}T0${Math.min(index, 7) + 1}:00:00Z`,
            readAt: null,
            kind: "achievements",
            actions: [],
            facts: [],
          })).reverse(),
          {
            id: "f4100000-0000-4000-8000-000000000000",
            type: "household_invite",
            title: "You joined the Abebe household",
            body: null,
            linkUrl: null,
            createdAt: `${today}T00:30:00Z`,
            readAt: `${today}T00:45:00Z`,
            kind: "household",
            actions: [],
            facts: [],
          },
        ];
      // What CreatePriceReportCommand writes for every administrator.
      if ((options.role ?? "user") === "admin")
        state.notifications = [
          {
            id: "f4000000-0000-4000-8000-000000000001",
            type: "price_report",
            title: "Price report for Greek yogurt",
            body: "200 ETB for 500 g.",
            linkUrl: "/admin/prices#reports",
            createdAt: `${today}T08:30:00Z`,
            readAt: null,
            kind: "account",
            actions: [],
            facts: [],
          },
        ];
      if (options.card)
        state.notifications.unshift({
          id: "f4200000-0000-4000-8000-000000000001",
          type: "achievement",
          kind: "achievements",
          title: "Achievement unlocked: First Meal",
          body: "Log your first meal",
          linkUrl: "/achievements",
          imageUrl: "/badges/128/achievement-gold-badge.png",
          createdAt: `${today}T09:00:00Z`,
          readAt: null,
          actions: [
            { label: "See achievements", path: "/achievements" },
            { label: "Log a meal", path: "/log" },
          ],
          facts: [
            { label: "Points", value: "+10" },
            { label: "Category", value: "Nutrition" },
          ],
        });
      state.failures = options.failures ?? [];
      state.writeFailures = options.writeFailures ?? {};
      state.userId = options.userId ?? user.id;
      state.role = options.role ?? "user";
      state.pro = options.pro ?? false;
      state.planning = initialPlanning(state.userId, today, {
        none: options.empty,
        ...options.planning,
      });
      state.prices = initialPrices(today, options.prices);
      state.aiPlanning = initialAiPlanning(options.aiPlanning);
      state.telegram = initialTelegram(options.telegram);
      state.notificationPreferences = initialNotificationPreferences(
        state.telegram,
      );
      if (options.recipeGroup)
        state.meals.push(
          ...[
            ["Red lentils", 180.25, 13],
            ["Onion and berbere", 95.5, 2],
          ].map(([name, calories, proteinGrams]) => ({
            id: crypto.randomUUID(),
            name: String(name),
            mealType: "DINNER",
            servings: 1,
            calories: Number(calories),
            proteinGrams: Number(proteinGrams),
            carbsGrams: 0,
            fatGrams: 0,
            fiberGrams: 0,
            loggedAt: `${today}T19:00:00Z`,
            groupId: "9a000000-0000-4000-8000-000000000001",
            groupName: "Misir wat",
          })),
        );
      if (options.household)
        state.household = {
          otherMembers: 0,
          lists: 0,
          plans: 0,
          version: 1,
          staleOnce: false,
          ...options.household,
        };
      states.set(token, state);
      return json({ user: { ...user, id: state.userId }, today }, 200, {
        "Set-Cookie": `mizan_session=${token}; Path=/; HttpOnly; SameSite=Lax`,
      });
    }
    const telegram = await handleTelegram(
      request,
      url,
      { self: `http://127.0.0.1:${port}`, webOrigin },
      json,
    );
    if (telegram) return telegram;
    if (isAdminOps(request, url)) return handleAdminOps(request, url, json);
    // Public, like the real endpoint: the landing page reads it signed out.
    if (method === "GET" && path === "/api/Subscriptions/plans")
      return json(plans);
    if (method === "GET" && path === "/api/Subscriptions/checkout-settings")
      return json({ clientToken: null, environment: "sandbox" });
    const token = request.headers
      .get("cookie")
      ?.match(/(?:^|;\s*)mizan_session=([^;]+)/)?.[1];
    const state = token ? states.get(token) : undefined;
    if (!state) {
      if (method === "GET" && path === "/api/Recipes")
        return json(
          page(
            initialState().recipes.map((recipe) => ({
              ...recipe,
              isOwner: false,
              isFavorited: false,
            })),
            url,
          ),
        );
      if (
        method === "GET" &&
        path === "/api/Recipes/33333333-3333-4333-8333-333333333333"
      )
        return json({
          ...initialState().recipes[0],
          isOwner: false,
          isFavorited: false,
        });
      return json({ error: "Fixture session required" }, 401);
    }
    if (path === "/__fixture/state") return json(state);
    if (state.failures.includes(path))
      return json({ error: "This service is temporarily unavailable." }, 503);
    if (method !== "GET" && state.writeFailures[path] > 0) {
      state.writeFailures[path] -= 1;
      return json(
        { error: "Could not save this entry. Please try again." },
        503,
      );
    }
    const flagged = await handleAdminFlags(
      request,
      url,
      token!,
      state.role,
      json,
    );
    if (flagged) return flagged;
    const campaigns = await handleAdminNotifications(
      request,
      url,
      token!,
      state.role,
      json,
    );
    if (campaigns) return campaigns;
    const assisted = await handleAiPlanning(
      request,
      url,
      {
        ai: state.aiPlanning,
        planning: {
          userId: state.userId,
          role: state.role,
          planning: state.planning,
          writes: state.writes,
          unhandled: state.unhandled,
          recipes: state.recipes,
          meals: state.meals,
          foods,
          prices: state.prices,
        },
        threads: state.threads,
      },
      today,
      json,
    );
    if (assisted) return assisted;
    if (path === "/api/Auth/me")
      return json({
        ...user,
        id: state.userId,
        role: state.impersonating ? "user" : state.role,
        impersonation: state.impersonating
          ? {
              impersonatorId: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
              impersonatorName: "Ada Admin",
              expiresAt: new Date(Date.now() + 55 * 60000).toISOString(),
            }
          : null,
      });
    if (path === "/api/Auth/logout")
      return json(null, 204, {
        "Set-Cookie": "mizan_session=; Path=/; Max-Age=0",
      });
    if (path === "/api/Subscriptions/me")
      return json({
        isPro: state.pro,
        plan: state.pro ? "Pro" : "Free",
        status: state.pro ? "active" : "none",
        isLifetime: false,
      });
    if (path === "/api/Households/mine") {
      const household = state.household;
      return json(
        household
          ? {
              households: [
                {
                  id: householdId,
                  name: "Home",
                  myRole: "admin",
                  memberCount: household.otherMembers + 1,
                  joinedAt: `${today}T08:00:00Z`,
                  isActive: true,
                },
              ],
              activeHouseholdId: householdId,
              pendingInvitations: [],
            }
          : { households: [], activeHouseholdId: null, pendingInvitations: [] },
      );
    }
    if (
      state.household &&
      path === `/api/Households/${householdId}` &&
      method === "GET"
    )
      return json({
        id: householdId,
        name: "Home",
        createdBy: state.userId,
        createdAt: `${today}T08:00:00Z`,
        members: [
          {
            userId: state.userId,
            name: user.name,
            email: user.email,
            role: "admin",
            joinedAt: `${today}T08:00:00Z`,
          },
          ...Array.from(
            { length: state.household.otherMembers },
            (_, index) => ({
              userId: `member-${index}`,
              name: `Member ${index + 1}`,
              email: `member${index + 1}@example.test`,
              role: "member",
              joinedAt: `${today}T09:00:00Z`,
            }),
          ),
        ],
      });
    if (
      state.household &&
      path === `/api/Households/${householdId}/invitations`
    )
      return json([]);
    if (state.household && path === `/api/Households/${householdId}/deletion`)
      return json({
        status: state.household.otherMembers > 0 ? "HasOtherMembers" : "Ready",
        preview: householdPreview(state.household),
      });
    if (
      state.household &&
      path === `/api/Households/${householdId}` &&
      method === "DELETE"
    ) {
      const household = state.household;
      const body = (await request.json()) as {
        version: string;
        deletePlans: boolean;
      };
      if (household.staleOnce) {
        household.staleOnce = false;
        household.lists += 1;
        household.version += 1;
        return json(
          {
            status: "Stale",
            message:
              "This household changed after you opened the confirmation. Review it again.",
            preview: householdPreview(household),
          },
          409,
        );
      }
      if (body.version !== `v${household.version}`)
        return json(
          {
            status: "Stale",
            message: "Stale",
            preview: householdPreview(household),
          },
          409,
        );
      if (household.lists + household.plans > 0 && !body.deletePlans)
        return json(
          { status: "PlansNotConfirmed", preview: householdPreview(household) },
          409,
        );
      state.writes.push({ path, body });
      state.household = null;
      return json({ status: "Deleted" });
    }
    if (path === "/api/McpConnections" && method === "GET")
      return json(state.connections);
    if (path === "/api/McpConnections/scopes") return json(scopeGroups);
    if (path === "/api/McpConnections/analytics")
      return json({
        overview: {
          totalCalls: 42,
          successfulCalls: 40,
          failedCalls: 2,
          successRate: 95.2,
          averageExecutionTimeMs: 180,
          uniqueClientsUsed: 1,
        },
        toolUsage: [
          {
            toolName: "log_food",
            callCount: 30,
            successCount: 28,
            failureCount: 2,
            averageExecutionTimeMs: 210,
          },
        ],
        clientUsage: [
          {
            grantId: state.connections[0]?.id,
            clientName: "Claude",
            callCount: 42,
            failureCount: 2,
            lastUsed: `${today}T07:30:00Z`,
          },
        ],
        dailyUsage: [
          {
            date: dateAgo(1),
            callCount: 12,
            successCount: 12,
            failureCount: 0,
          },
          { date: today, callCount: 30, successCount: 28, failureCount: 2 },
        ],
      });
    const connection = path.match(/^\/api\/McpConnections\/([^/]+)$/);
    if (connection && method === "PATCH") {
      const target = state.connections.find(
        (item) => item.id === connection[1],
      );
      if (!target) return json({ error: "Connection not found" }, 404);
      const body = (await request.json()) as {
        scopes?: string[];
        householdMode?: string;
        householdIds?: string[];
      };
      if (body.scopes?.some((scope) => !target.scopes.includes(scope)))
        return json({ error: "A connection can only lose access here." }, 400);
      if (body.scopes) target.scopes = body.scopes;
      if (body.householdMode) target.householdMode = body.householdMode;
      state.writes.push({ path, body });
      return json(null, 204);
    }
    if (connection && method === "DELETE") {
      state.connections = state.connections.filter(
        (item) => item.id !== connection[1],
      );
      state.writes.push({ path, body: {} });
      return json(null, 204);
    }
    if (
      path === "/api/oauth/authorization-requests/view" &&
      method === "POST"
    ) {
      const body = (await request.json()) as { request: string };
      if (body.request === "first-party")
        return json({
          ...consentRequest,
          clientName: "Mizan for Android",
          source: "first_party",
          isFirstParty: true,
          requestedScopes: ["full"],
        });
      if (body.request === "asks-for-little")
        return json({
          ...consentRequest,
          clientName: "Notes helper",
          requestedScopes: ["nutrition:read"],
        });
      return body.request === "fixture-request"
        ? json(consentRequest)
        : json({ error: "This connection request has expired." }, 404);
    }
    if (
      path === "/api/oauth/authorization-requests/decision" &&
      method === "POST"
    ) {
      const body = (await request.json()) as Record<string, unknown>;
      state.writes.push({ path, body });
      return json({
        redirectUrl: body.approve
          ? "http://localhost:8123/callback?code=fixture-code&state=s1&iss=fixture"
          : "http://localhost:8123/callback?error=access_denied&state=s1",
      });
    }
    if (path === "/api/Nutrition/ai/analyze-text" && method === "POST") {
      if (!state.pro)
        return json(
          { errorCode: "upgrade_required", error: "This is part of Pro." },
          402,
        );
      const body = (await request.json()) as { description?: string };
      state.writes.push({ path, body });
      return body.description?.trim()
        ? json(analysis)
        : json(
            { errorCode: "no_description", error: "Describe what you ate." },
            400,
          );
    }
    if (path === "/api/Ai/threads" && method === "GET")
      return json(
        state.threads.map(({ id, title, updatedAt }) => ({
          id,
          title,
          updatedAt,
        })),
      );
    const thread = path.match(/^\/api\/Ai\/threads\/([^/]+)$/);
    if (thread && method === "GET") {
      const found = state.threads.find((item) => item.id === thread[1]);
      return found
        ? json(found)
        : json({ errorCode: "not_found", error: "Not found" }, 404);
    }
    if (thread && method === "DELETE") {
      state.threads = state.threads.filter((item) => item.id !== thread[1]);
      state.writes.push({ path, body: {} });
      return json(null, 204);
    }
    if (
      (path === "/api/Ai/chat" || path === "/api/Ai/chat/image") &&
      method === "POST"
    ) {
      let message = "";
      let threadId: string | null = null;
      let image = false;
      if (path.endsWith("/image")) {
        const form = await request.formData();
        message = String(form.get("message") ?? "");
        threadId = (form.get("threadId") as string | null) ?? null;
        image = true;
      } else {
        const body = (await request.json()) as {
          message: string;
          threadId: string | null;
        };
        message = body.message;
        threadId = body.threadId;
      }
      if (message.includes("fail please"))
        return json(
          {
            errorCode: "ai_quota_exceeded",
            error:
              "You have used today's assistant allowance. It resets at midnight.",
          },
          429,
        );
      const now = new Date().toISOString();
      const target = state.threads.find((item) => item.id === threadId) ?? {
        id: `thread-${state.threads.length + 1}`,
        title: message.slice(0, 40) || "Photo",
        updatedAt: now,
        messages: [],
      };
      if (!state.threads.includes(target)) state.threads.unshift(target);
      // "log ..." asks for a write; without the switch on, the turn comes back with the typed
      // refusal, as AiToolRunner answers it.
      const consent = state.aiPlanning.consent;
      const refused =
        /^log /i.test(message) &&
        !(consent.allowWrites && consent.writeNutrition);
      const reply = {
        id: crypto.randomUUID(),
        fromUser: false,
        content: refused
          ? "Logging meals is switched off, so I could not add that."
          : image
            ? "That looks like **rice and chicken**."
            : /^log /i.test(message)
              ? `Logged: ${message.slice(4)}`
              : `You asked: ${message}`,
        createdAt: now,
      };
      const stored = {
        id: crypto.randomUUID(),
        fromUser: true,
        content: message,
        createdAt: now,
        imageUrl: image ? pixel : null,
      };
      target.messages.push(stored, reply);
      state.writes.push({ path, body: { message, image } });
      return json({
        threadId: target.id,
        title: target.title,
        message: stored,
        reply,
        performed: refused
          ? [
              {
                tool: "log_food",
                summary: "",
                succeeded: false,
                error:
                  "The user has not given permission to change their food and goals.",
              },
            ]
          : [],
        cards: [],
        consentRequired: refused
          ? [
              {
                errorCode: "ai_consent_required",
                axis: "nutrition",
                access: "write",
              },
            ]
          : [],
      } satisfies components["schemas"]["AiChatTurnDto"]);
    }
    if (path === "/api/Uploads/image" && method === "POST") {
      state.writes.push({ path, body: {} });
      return json({ key: "exercises/picture.png", url: pixel });
    }
    if (path === "/api/Uploads/model" && method === "POST") {
      const form = await request.formData();
      const file = form.get("file") as File | null;
      if (!file || !file.name.endsWith(".glb"))
        return json(
          {
            errorCode: "domain_validation_failed",
            error: "That file is not a glTF 2 binary (.glb) model.",
          },
          400,
        );
      state.writes.push({ path, body: { name: file.name, size: file.size } });
      return json({
        key: "models/figure.glb",
        url: `http://localhost:${port}/files/models/figure.glb`,
      });
    }
    if (path === "/api/Exercises" && method === "POST") {
      const body = (await request.json()) as Record<string, string>;
      const created: FixtureExercise = {
        id: crypto.randomUUID(),
        name: body.name,
        category: body.category,
        muscleGroup: body.muscleGroup ?? null,
        equipment: body.equipment ?? null,
        description: body.description ?? null,
        videoUrl: body.videoUrl ?? null,
        imageUrl: body.imageUrl ?? null,
        modelUrl: null,
        isCustom: true,
        isApproved: true,
      };
      state.exercises.unshift(created);
      state.writes.push({ path, body });
      return json({ id: created.id, name: created.name, success: true }, 201);
    }
    const exercise = path.match(
      /^\/api\/Exercises\/([^/]+)(?:\/(promote|model))?$/,
    );
    if (exercise) {
      const found = state.exercises.find((item) => item.id === exercise[1]);
      if (!found)
        return json(
          { errorCode: "not_found", error: "Exercise not found" },
          404,
        );
      const body =
        method === "POST" || method === "DELETE"
          ? {}
          : ((await request.json()) as Record<string, string | null>);
      if (exercise[2] === "promote" && method === "POST") {
        found.isCustom = false;
        state.writes.push({ path, body: {} });
        return json(null, 204);
      }
      if (exercise[2] === "model" && method === "PUT") {
        found.modelUrl = body.modelUrl ?? null;
        state.writes.push({ path, body });
        return json(null, 204);
      }
      if (!exercise[2] && method === "PUT") {
        Object.assign(found, {
          name: body.name,
          category: body.category,
          muscleGroup: body.muscleGroup,
          equipment: body.equipment,
          description: body.description,
          videoUrl: body.videoUrl,
          imageUrl: body.imageUrl,
        });
        state.writes.push({ path, body });
        return json(null, 204);
      }
      if (!exercise[2] && method === "DELETE") {
        state.exercises = state.exercises.filter((item) => item !== found);
        state.writes.push({ path, body: {} });
        return json(null, 204);
      }
    }
    const adminUser = path.match(
      /^\/api\/admin\/users\/([^/]+)(\/impersonate)?$/,
    );
    if (adminUser && state.role === "admin") {
      if (adminUser[2] && method === "POST") {
        state.impersonating = true;
        state.writes.push({ path, body: {} });
        return json({ ...user, id: adminUser[1] });
      }
      if (!adminUser[2] && method === "GET")
        return json({
          user: {
            id: adminUser[1],
            email: "sam@example.test",
            name: "Sam Rivera",
            role: "user",
            emailVerified: true,
            banned: false,
            createdAt: `${dateAgo(40)}T08:00:00Z`,
            updatedAt: `${dateAgo(2)}T08:00:00Z`,
          },
          activeSessionCount: 1,
          recentSessions: [],
        });
    }
    if (path === "/api/Auth/impersonation/stop" && method === "POST") {
      if (!state.impersonating)
        return json(
          {
            errorCode: "forbidden",
            error: "This is not an administrator's view of the site.",
          },
          403,
        );
      state.impersonating = false;
      state.writes.push({ path, body: {} });
      return json({ restored: true });
    }
    if (path === "/api/Notifications/unread-count")
      return json({
        unreadCount: state.notifications.filter((item) => !item.readAt).length,
      });
    if (path === "/api/Reminders" && method === "GET") {
      const active = state.reminders.filter((r) => r.enabled).length;
      return json({
        items: state.reminders,
        activeCount: active,
        activeLimit: state.pro ? 30 : 3,
        isPro: state.pro,
      });
    }
    if (path === "/api/Reminders" && method === "POST") {
      const body =
        (await request.json()) as components["schemas"]["CreateReminderRequest"];
      state.writes.push({
        path,
        body: body as Record<string, unknown>,
        key: request.headers.get("Idempotency-Key") ?? undefined,
      });
      const reminder = fixtureReminder(body);
      state.reminders.push(reminder);
      return json(reminder);
    }
    const reminderPath = /^\/api\/Reminders\/([0-9a-f-]+)$/.exec(path);
    if (reminderPath && (method === "PUT" || method === "DELETE")) {
      const reminder = state.reminders.find((r) => r.id === reminderPath[1]);
      if (!reminder)
        return json(
          { errorCode: "not_found", error: "Reminder not found." },
          404,
        );
      const body =
        method === "PUT"
          ? ((await request.json()) as components["schemas"]["UpdateReminderRequest"])
          : {};
      state.writes.push({
        path,
        body: { method, ...body },
        key: request.headers.get("Idempotency-Key") ?? undefined,
      });
      if (method === "DELETE") {
        state.reminders = state.reminders.filter((r) => r !== reminder);
        return json(null, 204);
      }
      if ("enabled" in body && typeof body.enabled === "boolean")
        reminder.enabled = reminder.active = body.enabled;
      return json(reminder);
    }
    if (path === "/api/Notifications/preferences" && method === "GET")
      return json(state.notificationPreferences);
    if (path === "/api/Notifications/preferences" && method === "PUT") {
      const body =
        (await request.json()) as components["schemas"]["UpdateNotificationPreferencesRequest"];
      const current = state.notificationPreferences;
      for (const change of body.kinds ?? []) {
        const kind = current.kinds.find((item) => item.kind === change.kind);
        if (!kind)
          return json({ errorCode: "validation", error: "Unknown kind" }, 400);
        kind.inApp = change.inApp ?? kind.inApp;
        kind.telegram = change.telegram ?? kind.telegram;
        kind.push = change.push ?? kind.push;
        kind.email = change.email ?? kind.email;
      }
      if (body.clearQuietHours) {
        current.quietHoursStart = null;
        current.quietHoursEnd = null;
      } else if (body.quietHoursStart && body.quietHoursEnd) {
        current.quietHoursStart = body.quietHoursStart;
        current.quietHoursEnd = body.quietHoursEnd;
      }
      state.writes.push({
        path,
        body: body as Record<string, unknown>,
        key: request.headers.get("Idempotency-Key") ?? undefined,
      });
      return json(current);
    }
    if (path === "/api/Notifications" && method === "GET")
      return json({
        ...page(state.notifications, url),
        unreadCount: state.notifications.filter((item) => !item.readAt).length,
      });
    const readNotification = path.match(
      /^\/api\/Notifications\/([^/]+)\/read$/,
    );
    if (readNotification && method === "POST") {
      const found = state.notifications.find(
        (item) => item.id === readNotification[1],
      );
      if (!found)
        return json({ errorCode: "not_found", error: "Not found" }, 404);
      found.readAt ??= new Date().toISOString();
      state.writes.push({ path, body: {} });
      return json(null, 204);
    }
    if (path === "/api/Notifications/read-all" && method === "POST") {
      const now = new Date().toISOString();
      for (const item of state.notifications) item.readAt ??= now;
      state.writes.push({ path, body: {} });
      return json(null, 204);
    }
    // The settings page reads these beside the price region.
    if (path === "/api/Auth/sessions" && method === "GET")
      return json([
        {
          id: "session-current",
          createdAt: `${dateAgo(2)}T08:00:00Z`,
          lastSeenAt: `${today}T08:00:00Z`,
          expiresAt: `${today}T23:00:00Z`,
          ipAddress: "127.0.0.1",
          userAgent: "Playwright",
          isCurrent: true,
        },
      ]);
    const profile = await handleUsers(
      request,
      url,
      {
        user: { ...user, id: state.userId },
        prices: state.prices,
        telegram: state.telegram,
        writes: state.writes,
      },
      json,
    );
    if (profile) return profile;
    if (path.startsWith("/api/admin/settings"))
      return handleAdminSettings(request, url, state, json);
    const priced = await handlePrices(
      request,
      url,
      {
        user: { ...user, id: state.userId },
        role: state.role,
        prices: state.prices,
        foods,
        writes: state.writes,
        unhandled: state.unhandled,
      },
      json,
    );
    if (priced) return priced;
    const planned = await handlePlanning(
      request,
      url,
      {
        userId: state.userId,
        role: state.role,
        planning: state.planning,
        writes: state.writes,
        unhandled: state.unhandled,
        recipes: state.recipes,
        meals: state.meals,
        foods,
        prices: state.prices,
      },
      today,
      json,
    );
    if (planned) return planned;
    if (path === "/api/Trainers/my-trainer")
      return json({ error: "No active trainer relationship found" }, 404);
    if (path === "/api/Goals")
      return state.empty ? json(null, 204) : json(goal);
    if (path === "/api/Goals/history") return json(state.empty ? [] : [goal]);
    if (path === "/api/Achievements/streak")
      return json({
        currentStreak: 4,
        longestStreak: 12,
        isActiveToday: !state.empty,
      });
    if (path === "/api/Achievements")
      return json({
        items: state.empty
          ? achievements.map((item) => ({
              ...item,
              isEarned: false,
              earnedAt: null,
            }))
          : achievements,
        totalCount: achievements.length,
        totalPages: 1,
        totalPoints: state.empty ? 0 : 10,
        earnedCount: state.empty ? 0 : 1,
        level: 1,
        levelName: state.empty ? "Rookie" : "Bronze",
        levelFloor: 0,
        nextLevelAt: state.empty ? 100 : 500,
      });
    if (path === "/api/Foods/search") {
      const query = parameter(url, "searchTerm")?.toLowerCase() ?? "";
      return json(
        page(
          foods.filter((item) => item.name.toLowerCase().includes(query)),
          url,
        ),
      );
    }
    if (path.startsWith("/api/Foods/"))
      return json(
        foods.find((item) => item.id === path.split("/").at(-1)) ?? null,
      );
    if (path === "/api/Recipes") {
      const query = parameter(url, "searchTerm")?.toLowerCase() ?? "";
      const favoritesOnly = parameter(url, "favoritesOnly") === "true";
      const frequent = parameter(url, "sortBy") === "frequent";
      const matching = state.recipes.filter(
        (item) =>
          String(item.title).toLowerCase().includes(query) &&
          (!favoritesOnly || item.isFavorited) &&
          (!frequent || Number(item.timesLogged) > 0),
      );
      return json(
        page(
          frequent
            ? matching.sort(
                (a, b) => Number(b.timesLogged) - Number(a.timesLogged),
              )
            : matching,
          url,
        ),
      );
    }
    if (path === "/api/Nutrition/log" && method === "POST") {
      const body = (await request.json()) as Record<string, unknown>;
      const selectedFood = foods.find((item) => item.id === body.foodId);
      const selectedRecipe = state.recipes.find(
        (item) => item.id === body.recipeId,
      );
      // LogFoodCommand: a food by amountGrams (or servings of its serving
      // size), a recipe by servings. The fixture converts, as the API does.
      const grams =
        selectedFood && body.amountGrams != null
          ? Number(body.amountGrams)
          : null;
      const servings =
        grams !== null && selectedFood
          ? grams / selectedFood.servingSize
          : Number(body.servings ?? 1);
      if (
        !(servings > 0) ||
        !body.entryDate ||
        (!selectedFood && !selectedRecipe) ||
        (selectedRecipe && body.amountGrams != null)
      )
        return json(
          { error: "Choose a food or recipe and a positive quantity" },
          400,
        );
      // Per 100 g for a food, per serving for a recipe.
      const multiplier = selectedFood
        ? (grams ?? servings * selectedFood.servingSize) / 100
        : servings;
      const nutrition = selectedRecipe?.nutrition as
        Record<string, number> | undefined;
      const entry: MealEntry = {
        id: crypto.randomUUID(),
        name: selectedFood?.name ?? String(selectedRecipe?.title),
        foodId: selectedFood?.id,
        recipeId: selectedRecipe ? String(selectedRecipe.id) : undefined,
        servings,
        amountGrams: selectedFood
          ? (grams ?? servings * selectedFood.servingSize)
          : null,
        mealType: String(body.mealType),
        loggedAt: `${body.entryDate}T12:00:00Z`,
        calories:
          (selectedFood?.caloriesPer100g ??
            nutrition?.caloriesPerServing ??
            0) * multiplier,
        proteinGrams:
          (selectedFood?.proteinPer100g ?? nutrition?.proteinGrams ?? 0) *
          multiplier,
        carbsGrams:
          (selectedFood?.carbsPer100g ?? nutrition?.carbsGrams ?? 0) *
          multiplier,
        fatGrams:
          (selectedFood?.fatPer100g ?? nutrition?.fatGrams ?? 0) * multiplier,
        fiberGrams:
          (selectedFood?.fiberPer100g ?? nutrition?.fiberGrams ?? 0) *
          multiplier,
      };
      state.meals.push(entry);
      state.writes.push({ path, body });
      return json({ id: entry.id, success: true, warnings: [] });
    }
    if (path === "/api/Recipes/promote" && method === "POST") {
      const body = (await request.json()) as Record<string, unknown>;
      const entries = state.meals.filter(
        (item) =>
          item.loggedAt.startsWith(String(body.entryDate)) &&
          item.mealType === body.mealType,
      );
      if (entries.length < 2 || !String(body.title ?? "").trim())
        return json(
          { error: "A recipe needs at least two meal entries and a title." },
          400,
        );
      const nutrition = totals(entries);
      const recipe = {
        id: crypto.randomUUID(),
        title: body.title,
        servings: 1,
        isOwner: true,
        isFavorited: false,
        nutrition: {
          caloriesPerServing: nutrition.calories,
          proteinGrams: nutrition.protein,
          carbsGrams: nutrition.carbs,
          fatGrams: nutrition.fat,
          fiberGrams: nutrition.fiber,
        },
      };
      state.recipes.push(recipe);
      state.writes.push({ path, body });
      return json({ recipeId: recipe.id, id: recipe.id, success: true });
    }
    const recipePath = path.match(
      /^\/api\/Recipes\/([^/]+)(?:\/(favorite|preparation))?$/,
    );
    if (recipePath) {
      const recipe = state.recipes.find((item) => item.id === recipePath[1]);
      if (!recipe) return json({ error: "Recipe not found" }, 404);
      if (method === "GET") return json(recipe);
      if (method === "POST" && recipePath[2] === "favorite") {
        recipe.isFavorited = !recipe.isFavorited;
        return json({ isFavorited: recipe.isFavorited });
      }
      const body = (await request.json()) as Record<string, unknown>;
      if (method === "PUT") {
        const ingredients = body.ingredients as {
          foodId: string;
          amount: number;
          ingredientText: string;
          unit: string;
        }[];
        if (!body.title || !(Number(body.servings) > 0) || !ingredients?.length)
          return json(
            { error: "Title, servings and ingredients are required" },
            400,
          );
        Object.assign(recipe, body, {
          ingredients: ingredients.map((ingredient) => ({
            ...ingredient,
            foodName:
              foods.find((item) => item.id === ingredient.foodId)?.name ??
              ingredient.ingredientText,
          })),
        });
        const nutrition = ingredients.reduce(
          (sum, ingredient) => {
            const item = foods.find(
              (candidate) => candidate.id === ingredient.foodId,
            )!;
            const factor = ingredient.amount / 100 / Number(body.servings);
            return {
              caloriesPerServing:
                sum.caloriesPerServing + item.caloriesPer100g * factor,
              proteinGrams: sum.proteinGrams + item.proteinPer100g * factor,
              carbsGrams: sum.carbsGrams + item.carbsPer100g * factor,
              fatGrams: sum.fatGrams + item.fatPer100g * factor,
            };
          },
          {
            caloriesPerServing: 0,
            proteinGrams: 0,
            carbsGrams: 0,
            fatGrams: 0,
          },
        );
        recipe.nutrition = nutrition;
        state.writes.push({ path, body });
        return json({ success: true });
      }
      if (method === "POST" && recipePath[2] === "preparation") {
        if (!(Number(body.yieldGrams) > 0))
          return json({ error: "Finished batch weight is required" }, 400);
        state.writes.push({ path, body });
        return json({
          foodId: "44444444-4444-4444-8444-444444444444",
          success: true,
        });
      }
    }
    if (path === "/api/Meals/range") {
      const endDate = parameter(url, "endDate") ?? today;
      const days = Number(parameter(url, "days") ?? 7);
      return json({
        days: Array.from({ length: days }, (_, index) => {
          const date = new Date(`${endDate}T12:00:00Z`);
          date.setUTCDate(date.getUTCDate() - days + index + 1);
          const day = date.toISOString().slice(0, 10);
          return {
            date: day,
            ...totals(
              state.meals.filter((meal) => meal.loggedAt.startsWith(day)),
            ),
          };
        }).filter((day) =>
          state.meals.some((entry) => entry.loggedAt.startsWith(day.date)),
        ),
      });
    }
    if (path === "/api/Meals" && method === "GET") {
      const date = parameter(url, "date") ?? today;
      const entries = state.meals.filter((meal) =>
        meal.loggedAt.startsWith(date),
      );
      const mealTypes = [
        ...new Set(entries.map((entry) => entry.mealType.toUpperCase())),
      ];
      const groupIds = [
        ...new Set(entries.flatMap((entry) => entry.groupId ?? [])),
      ];
      return json({
        date,
        entries,
        totals: totals(entries),
        meals: mealTypes.map((mealType) => ({
          mealType,
          totals: subtotal(
            entries.filter(
              (entry) => entry.mealType.toUpperCase() === mealType,
            ),
          ),
        })),
        groups: groupIds.map((groupId) => ({
          groupId,
          totals: subtotal(
            entries.filter((entry) => entry.groupId === groupId),
          ),
        })),
      });
    }
    // PreviewMealQuery: a food by grams, a recipe by servings, an estimate by
    // its per-100 g figures, a custom line as typed. A read, so not a write.
    if (path === "/api/Meals/preview" && method === "POST") {
      const body = (await request.json()) as { lines: MealPreviewLine[] };
      const cents = (value: number) => Math.round(value * 100) / 100;
      const lines = body.lines.map((line) => {
        const food = foods.find((item) => item.id === line.foodId);
        const recipe = state.recipes.find((item) => item.id === line.recipeId);
        const perServing = (
          recipe?.nutrition as Record<string, number | null> | undefined
        )?.caloriesPerServing;
        const proteinPerServing = (
          recipe?.nutrition as Record<string, number | null> | undefined
        )?.proteinGrams;
        if (line.foodId)
          return food && line.grams != null
            ? {
                key: line.key,
                calories: cents((food.caloriesPer100g * line.grams) / 100),
                proteinGrams: cents((food.proteinPer100g * line.grams) / 100),
              }
            : { key: line.key, calories: null, proteinGrams: null };
        if (line.recipeId)
          return perServing != null && line.servings != null
            ? {
                key: line.key,
                calories: cents(perServing * line.servings),
                proteinGrams: cents((proteinPerServing ?? 0) * line.servings),
              }
            : { key: line.key, calories: null, proteinGrams: null };
        if (line.per100g && line.grams != null)
          return {
            key: line.key,
            calories: cents((line.per100g.calories * line.grams) / 100),
            proteinGrams: cents((line.per100g.proteinGrams * line.grams) / 100),
          };
        return {
          key: line.key,
          calories: line.calories ?? null,
          proteinGrams: line.calories != null ? (line.proteinGrams ?? 0) : null,
        };
      });
      const known = lines.filter((line) => line.calories != null);
      return json({
        lines,
        calories:
          Math.round(
            known.reduce((sum, l) => sum + (l.calories ?? 0), 0) * 10,
          ) / 10,
        proteinGrams:
          Math.round(
            known.reduce((sum, l) => sum + (l.proteinGrams ?? 0), 0) * 10,
          ) / 10,
        complete: known.length === lines.length,
      } satisfies MealPreviewResult);
    }
    if (path === "/api/Meals" && method === "POST") {
      const body = (await request.json()) as Record<string, unknown>;
      if (
        !body.name ||
        !(Number(body.servings) > 0) ||
        !/^\d{4}-\d{2}-\d{2}$/.test(String(body.entryDate))
      )
        return json(
          { error: "Name, servings and entryDate are required" },
          400,
        );
      const entry: MealEntry = {
        ...(body as unknown as MealEntry),
        id: crypto.randomUUID(),
        loggedAt: `${body.entryDate}T12:00:00Z`,
      };
      state.meals.push(entry);
      state.writes.push({ path, body });
      return json({ id: entry.id, success: true, warnings: [] });
    }
    if (path === "/api/BodyMeasurements" && method === "GET")
      return json(
        page(
          state.measurements.filter((item) => inRange(item.date, url)),
          url,
        ),
      );
    if (path === "/api/BodyMeasurements" && method === "POST") {
      const body = (await request.json()) as Record<string, unknown>;
      if (
        !body.date ||
        !Object.entries(body).some(
          ([key, value]) =>
            key !== "date" && key !== "notes" && Number(value) > 0,
        )
      )
        return json(
          { error: "Date and a positive measurement are required" },
          400,
        );
      const entry = {
        ...(body as unknown as BodyMeasurement),
        id: crypto.randomUUID(),
      };
      state.measurements.unshift(entry);
      state.writes.push({ path, body });
      return json({ id: entry.id, success: true });
    }
    if (path === "/api/Workouts/draft") {
      if (method === "GET")
        return state.draft
          ? json(state.draft)
          : json({ error: "No draft" }, 404);
      if (method === "PUT") {
        state.draft = {
          ...((await request.json()) as { payload: string }),
          updatedAt: new Date().toISOString(),
        };
        return json(null, 204);
      }
      if (method === "DELETE") {
        state.draft = null;
        return json(null, 204);
      }
    }
    if (path === "/api/Workouts" && method === "GET")
      return json(
        page(
          state.workouts.filter((item) => inRange(item.workoutDate, url)),
          url,
        ),
      );
    if (path === "/api/Workouts" && method === "POST") {
      const body = (await request.json()) as {
        name: string;
        workoutDate: string;
        exercises: {
          exerciseId: string;
          sets: WorkoutSummary["exercises"][number]["sets"];
        }[];
      };
      if (!body.workoutDate || !body.exercises?.length)
        return json({ error: "A workout date and exercise are required" }, 400);
      const workout: WorkoutSummary = {
        id: crypto.randomUUID(),
        name: body.name,
        workoutDate: body.workoutDate,
        durationMinutes: 1,
        caloriesBurned: null,
        notes: null,
        createdAt: new Date().toISOString(),
        exercises: body.exercises.map((exercise) => ({
          id: crypto.randomUUID(),
          exerciseId: exercise.exerciseId,
          exerciseName: "Bench press",
          category: "Strength",
          muscleGroup: "Chest",
          sortOrder: 0,
          sets: exercise.sets.map((set, index) => ({
            ...set,
            setNumber: index + 1,
          })),
        })),
      };
      state.workouts.unshift(workout);
      state.writes.push({ path, body });
      return json({
        id: workout.id,
        totalExercises: workout.exercises.length,
        totalSets: workout.exercises.reduce(
          (sum, exercise) =>
            sum + exercise.sets.filter((set) => set.completed).length,
          0,
        ),
        personalRecords: [],
      });
    }
    if (path === "/api/Exercises" && method === "GET") {
      const query = parameter(url, "searchTerm")?.toLowerCase() ?? "";
      return json(
        page(
          state.exercises.filter((item) =>
            item.name.toLowerCase().includes(query),
          ),
          url,
        ),
      );
    }
    if (path === "/api/WorkoutTemplates")
      return json([
        {
          id: "template-strength",
          name: "Simple strength",
          programName: "Weekly training",
          exercises: [],
          isBuiltIn: true,
          sessionOrder: 0,
          sortOrder: 0,
        },
      ]);
    if (path === "/api/WorkoutTemplates/template-strength/next-session")
      return json({
        templateId: "template-strength",
        name: "Simple strength",
        exercises: [
          {
            exerciseId: "exercise-bench",
            name: "Bench press",
            category: "Strength",
            supersetWithNext: false,
            restSecondsMin: 60,
            restSecondsMax: 120,
            sets: [{ targetReps: 8, weightKg: 55 }],
          },
        ],
      });
    if (path === "/api/Social/profile")
      return json({ error: "No social profile" }, 404);
    if (method === "DELETE") {
      const id = path.split("/").at(-1);
      if (path.startsWith("/api/Meals/"))
        state.meals = state.meals.filter((item) => item.id !== id);
      else if (path.startsWith("/api/Workouts/"))
        state.workouts = state.workouts.filter((item) => item.id !== id);
      else if (path.startsWith("/api/BodyMeasurements/"))
        state.measurements = state.measurements.filter(
          (item) => item.id !== id,
        );
      else {
        state.unhandled.push(`${method} ${path}`);
        return json({ error: "Unsupported delete" }, 501);
      }
      state.writes.push({ path, body: {} });
      return json(null, 204);
    }
    state.unhandled.push(`${method} ${path}`);
    return json({ error: `Fixture does not implement ${method} ${path}` }, 501);
  },
});
console.log(`UI fixture API listening on http://localhost:${server.port}`);
