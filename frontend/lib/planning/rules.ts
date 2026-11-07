import { z } from "zod";
import type { PlanEntry, PlanRules } from "@/lib/api/mealPlans";

/**
 * The rules form as a person types it: strings from inputs, validated here
 * and shaped into the PlanRulesDto the API expects. The API validates again;
 * this only tells the person what is wrong before the round trip.
 */
export const MACRO_MODES = ["None", "Keto", "HighProtein", "Custom"] as const;
export const PREP_STYLES = ["Daily", "Batch"] as const;

export const MACRO_MODE_LABELS: Record<(typeof MACRO_MODES)[number], string> = {
  None: "No macro rule",
  Keto: "Keto",
  HighProtein: "High protein",
  Custom: "Custom targets",
};

export type FoodChoice = { id: string; name: string };

export type RulesFormValues = {
  macroMode: string;
  targetCalories: string;
  targetProtein: string;
  maxCarbs: string;
  targetFat: string;
  eatingWindowStart: string;
  eatingWindowEnd: string;
  weeklyBudget: string;
  dailyBudget: string;
  currency: string;
  prepStyle: string;
  batchDays: number[];
  allowedFoods: FoodChoice[];
  forbiddenFoods: FoodChoice[];
};

const optionalNumber = (label: string, max: number) =>
  z
    .string()
    .trim()
    .transform((value) => (value === "" ? null : Number(value)))
    .refine((value) => value === null || Number.isFinite(value), {
      message: `${label} must be a number`,
    })
    .refine((value) => value === null || (value > 0 && value <= max), {
      message: `${label} must be between 1 and ${max}`,
    });

const timeText = z
  .string()
  .trim()
  .transform((value) => (value === "" ? null : value))
  .refine((value) => value === null || /^\d{2}:\d{2}$/.test(value), {
    message: "Use a time like 08:00",
  });

export const rulesSchema = z
  .object({
    macroMode: z.enum(MACRO_MODES, { message: "Choose a macro mode" }),
    targetCalories: optionalNumber("Calories", 10000),
    targetProtein: optionalNumber("Protein", 1000),
    maxCarbs: optionalNumber("Carbs", 1000),
    targetFat: optionalNumber("Fat", 1000),
    eatingWindowStart: timeText,
    eatingWindowEnd: timeText,
    weeklyBudget: optionalNumber("Weekly budget", 1_000_000),
    dailyBudget: optionalNumber("Daily budget", 1_000_000),
    currency: z
      .string()
      .trim()
      .toUpperCase()
      .transform((value) => (value === "" ? null : value))
      .refine((value) => value === null || /^[A-Z]{3}$/.test(value), {
        message: "Currency is a 3-letter code like ETB",
      }),
    prepStyle: z.enum(PREP_STYLES, { message: "Choose a prep style" }),
    batchDays: z.array(z.number().int().min(0)),
    allowedFoods: z.array(z.object({ id: z.string(), name: z.string() })),
    forbiddenFoods: z.array(z.object({ id: z.string(), name: z.string() })),
  })
  .superRefine((value, context) => {
    if ((value.eatingWindowStart === null) !== (value.eatingWindowEnd === null))
      context.addIssue({
        code: "custom",
        path: ["eatingWindowEnd"],
        message: "An eating window needs both a start and an end",
      });
    if (
      (value.weeklyBudget !== null || value.dailyBudget !== null) &&
      value.currency === null
    )
      context.addIssue({
        code: "custom",
        path: ["currency"],
        message: "A budget needs a currency",
      });
    if (value.prepStyle === "Batch" && value.batchDays.length === 0)
      context.addIssue({
        code: "custom",
        path: ["batchDays"],
        message: "Pick at least one cooking day",
      });
    const allowed = new Set(value.allowedFoods.map((food) => food.id));
    if (value.forbiddenFoods.some((food) => allowed.has(food.id)))
      context.addIssue({
        code: "custom",
        path: ["forbiddenFoods"],
        message: "A food cannot be both allowed and forbidden",
      });
  });

export type RulesFormErrors = Partial<Record<keyof RulesFormValues, string>>;

/** Validates the form and shapes it into the wire rules, or names the first problem per field. */
export function parseRules(
  values: RulesFormValues,
):
  | { rules: PlanRules; errors: null }
  | { rules: null; errors: RulesFormErrors } {
  const result = rulesSchema.safeParse(values);
  if (!result.success) {
    const errors: RulesFormErrors = {};
    for (const issue of result.error.issues) {
      const key = issue.path[0] as keyof RulesFormValues | undefined;
      if (key && !errors[key]) errors[key] = issue.message;
    }
    return { rules: null, errors };
  }
  const value = result.data;
  return {
    errors: null,
    rules: {
      macroMode: value.macroMode,
      targetCalories: value.targetCalories,
      targetProtein: value.targetProtein,
      maxCarbs: value.maxCarbs,
      targetFat: value.targetFat,
      eatingWindowStart: value.eatingWindowStart,
      eatingWindowEnd: value.eatingWindowEnd,
      weeklyBudget: value.weeklyBudget,
      dailyBudget: value.dailyBudget,
      currency: value.currency,
      prepStyle: value.prepStyle,
      batchDays: value.prepStyle === "Batch" ? value.batchDays : [],
      allowedFoodIds: value.allowedFoods.map((food) => food.id),
      forbiddenFoodIds: value.forbiddenFoods.map((food) => food.id),
    },
  };
}

export function emptyRulesForm(): RulesFormValues {
  return {
    macroMode: "None",
    targetCalories: "",
    targetProtein: "",
    maxCarbs: "",
    targetFat: "",
    eatingWindowStart: "",
    eatingWindowEnd: "",
    weeklyBudget: "",
    dailyBudget: "",
    currency: "",
    prepStyle: "Daily",
    batchDays: [],
    allowedFoods: [],
    forbiddenFoods: [],
  };
}

/** The form for rules the API already holds. Food names come from the caller, since the DTO carries ids. */
export function rulesToForm(
  rules: PlanRules,
  foodNames: Record<string, string> = {},
): RulesFormValues {
  const text = (value: number | null | undefined) =>
    value == null ? "" : String(value);
  const time = (value: string | null | undefined) =>
    value ? value.slice(0, 5) : "";
  const foods = (ids: string[]) =>
    ids.map((id) => ({ id, name: foodNames[id] ?? "Food" }));
  return {
    macroMode: rules.macroMode,
    targetCalories: text(rules.targetCalories),
    targetProtein: text(rules.targetProtein),
    maxCarbs: text(rules.maxCarbs),
    targetFat: text(rules.targetFat),
    eatingWindowStart: time(rules.eatingWindowStart),
    eatingWindowEnd: time(rules.eatingWindowEnd),
    weeklyBudget: text(rules.weeklyBudget),
    dailyBudget: text(rules.dailyBudget),
    currency: rules.currency ?? "",
    prepStyle: rules.prepStyle,
    batchDays: rules.batchDays ?? [],
    allowedFoods: foods(rules.allowedFoodIds ?? []),
    forbiddenFoods: foods(rules.forbiddenFoodIds ?? []),
  };
}

/** Name, start date and length of a plan. Length is 1 to 31 days. */
export const planSchema = z.object({
  name: z.string().trim().max(120, "Keep the name under 120 characters"),
  startDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "Pick a start date"),
  durationDays: z.preprocess(
    (value) => (typeof value === "string" && !value.trim() ? undefined : value),
    z.coerce
      .number({ message: "Length is a number of days" })
      .int("Length is a whole number of days")
      .min(1, "A plan lasts at least one day")
      .max(31, "A plan lasts at most 31 days"),
  ),
});

export type PlanFormErrors = Partial<
  Record<"name" | "startDate" | "durationDays", string>
>;

export function parsePlanForm(values: {
  name: string;
  startDate: string;
  durationDays: string | number;
}):
  | { plan: z.infer<typeof planSchema>; errors: null }
  | { plan: null; errors: PlanFormErrors } {
  const result = planSchema.safeParse(values);
  if (result.success) return { plan: result.data, errors: null };
  const errors: PlanFormErrors = {};
  for (const issue of result.error.issues) {
    const key = issue.path[0] as keyof PlanFormErrors | undefined;
    if (key && !errors[key]) errors[key] = issue.message;
  }
  return { plan: null, errors };
}

/** Name and length of a template. A template has no dates, and the API needs its name. */
export const templateSchema = planSchema.pick({ durationDays: true }).extend({
  name: z
    .string()
    .trim()
    .min(1, "Give the template a name")
    .max(120, "Keep the name under 120 characters"),
});

export type TemplateFormErrors = Partial<
  Record<"name" | "durationDays", string>
>;

export function parseTemplateForm(values: {
  name: string;
  durationDays: string | number;
}):
  | { template: z.infer<typeof templateSchema>; errors: null }
  | { template: null; errors: TemplateFormErrors } {
  const result = templateSchema.safeParse(values);
  if (result.success) return { template: result.data, errors: null };
  const errors: TemplateFormErrors = {};
  for (const issue of result.error.issues) {
    const key = issue.path[0] as keyof TemplateFormErrors | undefined;
    if (key && !errors[key]) errors[key] = issue.message;
  }
  return { template: null, errors };
}

/** What an entry is called in a list: the recipe, the food with its weight, or the line of text. */
export function entryTitle(entry: PlanEntry): string {
  if (entry.kind === "Recipe") return entry.recipeTitle ?? "Recipe";
  if (entry.kind === "Food") return entry.foodName ?? "Food";
  return entry.text ?? "";
}

export function entryQuantity(entry: PlanEntry): string | null {
  if (entry.kind === "Recipe" && entry.servings != null)
    return `${entry.servings} ${entry.servings === 1 ? "serving" : "servings"}`;
  if (entry.kind === "Food" && entry.grams != null)
    return `${Math.round(entry.grams)} g`;
  return null;
}

export const SLOT_LABELS: Record<string, string> = {
  BREAKFAST: "Breakfast",
  LUNCH: "Lunch",
  DINNER: "Dinner",
  SNACK: "Snack",
};

export function slotLabel(slot: string): string {
  return (
    SLOT_LABELS[slot.toUpperCase()] ??
    slot.charAt(0).toUpperCase() + slot.slice(1).toLowerCase()
  );
}

export function formatPlanDate(
  iso: string | null | undefined,
  options: Intl.DateTimeFormatOptions = { day: "numeric", month: "short" },
): string {
  if (!iso) return "";
  return new Date(`${iso}T00:00:00Z`).toLocaleDateString(undefined, {
    timeZone: "UTC",
    ...options,
  });
}

export function planDates(plan: {
  startDate?: string | null;
  endDate?: string | null;
  durationDays: number;
}): string {
  if (!plan.startDate) return `${plan.durationDays} days`;
  return `${formatPlanDate(plan.startDate)} – ${formatPlanDate(plan.endDate)}`;
}

export const STATUS_LABELS: Record<string, string> = {
  Draft: "Draft",
  Active: "Active",
  Archived: "Archived",
};

/** The index of the day that covers `today`, or the first day. */
export function dayIndexFor(
  plan: { startDate?: string | null; durationDays: number },
  today: string,
): number {
  if (!plan.startDate) return 0;
  const start = Date.UTC(
    Number(plan.startDate.slice(0, 4)),
    Number(plan.startDate.slice(5, 7)) - 1,
    Number(plan.startDate.slice(8, 10)),
  );
  const now = Date.UTC(
    Number(today.slice(0, 4)),
    Number(today.slice(5, 7)) - 1,
    Number(today.slice(8, 10)),
  );
  const index = Math.round((now - start) / 86_400_000);
  return index >= 0 && index < plan.durationDays ? index : 0;
}
