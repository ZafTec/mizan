import { z } from "zod";
import type { GenerateMealPlanRequest } from "@/lib/api/mealPlans";

/**
 * The "Draft with the assistant" form as typed: strings from inputs, checked
 * here and shaped into the generate request. The API validates again and owns
 * every rule; this only names what is wrong before the metered round trip. A
 * weekly budget becomes the one budget ceiling and a daily budget the ceiling
 * for each day, both in the price region's currency.
 */
export type DraftFormValues = {
  name: string;
  startDate: string;
  durationDays: string;
  weeklyBudget: string;
  dailyBudget: string;
  preference: string;
};

export type DraftFormErrors = Partial<Record<keyof DraftFormValues, string>>;

export const MAX_DRAFT_DAYS = 14;

const draftSchema = z.object({
  name: z.string().trim().max(120, "Keep the name under 120 characters"),
  startDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "Pick a start date"),
  durationDays: z
    .string()
    .trim()
    .min(1, "Length is a number of days")
    .transform(Number)
    .refine(Number.isInteger, "Length is a whole number of days")
    .refine(
      (days) => days >= 1 && days <= MAX_DRAFT_DAYS,
      `A drafted plan runs 1 to ${MAX_DRAFT_DAYS} days`,
    ),
  weeklyBudget: z
    .string()
    .trim()
    .transform((value) => (value === "" ? null : Number(value)))
    .refine(
      (value) => value === null || Number.isFinite(value),
      "Weekly budget must be a number",
    )
    .refine(
      (value) => value === null || (value > 0 && value <= 1_000_000),
      "Weekly budget must be above zero",
    ),
  dailyBudget: z
    .string()
    .trim()
    .transform((value) => (value === "" ? null : Number(value)))
    .refine(
      (value) => value === null || Number.isFinite(value),
      "Daily budget must be a number",
    )
    .refine(
      (value) => value === null || (value > 0 && value <= 1_000_000),
      "Daily budget must be above zero",
    ),
  preference: z
    .string()
    .trim()
    .max(1000, "Keep what you would like under 1,000 characters"),
});

export function emptyDraftForm(today: string): DraftFormValues {
  return {
    name: "",
    startDate: today,
    durationDays: "7",
    weeklyBudget: "",
    dailyBudget: "",
    preference: "",
  };
}

export function parseDraftForm(
  values: DraftFormValues,
):
  | { request: GenerateMealPlanRequest; errors: null }
  | { request: null; errors: DraftFormErrors } {
  const result = draftSchema.safeParse(values);
  if (!result.success) {
    const errors: DraftFormErrors = {};
    for (const issue of result.error.issues) {
      const key = issue.path[0] as keyof DraftFormValues | undefined;
      if (key && !errors[key]) errors[key] = issue.message;
    }
    return { request: null, errors };
  }
  const value = result.data;
  return {
    errors: null,
    request: {
      name: value.name || null,
      startDate: value.startDate,
      durationDays: value.durationDays,
      preference: value.preference || null,
      budgetCeilings: value.weeklyBudget === null ? [] : [value.weeklyBudget],
      dailyBudget: value.dailyBudget,
    },
  };
}

/** How a plan the assistant drafted is labelled wherever it is listed or shown. */
export const DRAFTED_BY_ASSISTANT = "Drafted by the assistant";

/** Whether the plan came from generation: the API sets `generatedFrom` only then. */
export function isGenerated(plan: { generatedFrom?: unknown }): boolean {
  return plan.generatedFrom != null;
}
