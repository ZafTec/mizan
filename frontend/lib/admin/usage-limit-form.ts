import { z } from "zod";

/**
 * The allowance override form as an administrator types it: an empty box
 * inherits, a number is a cap (zero included). Checked here before the round
 * trip; the API validates again with the same rules.
 */
const cap = (label: string) =>
  z.preprocess(
    (value) => (typeof value === "string" && !value.trim() ? null : value),
    z.coerce
      .number({ message: `${label} is a whole number` })
      .int(`${label} is a whole number`)
      .min(0, `${label} cannot be negative`)
      .max(Number.MAX_SAFE_INTEGER, `${label} is too large`)
      .nullable(),
  );

export const usageLimitFormSchema = z
  .object({
    line: z
      .string()
      .transform((value) => (value === "" || value === "all" ? null : value)),
    dailyRequests: cap("Daily requests"),
    dailyTokens: cap("Daily tokens"),
    dailyCostMicros: cap("Daily cost"),
    monthlyCostMicros: cap("Monthly cost"),
    note: z
      .string()
      .trim()
      .max(500, "Keep the note under 500 characters")
      .transform((value) => (value === "" ? null : value)),
  })
  .refine(
    (form) =>
      form.dailyRequests !== null ||
      form.dailyTokens !== null ||
      form.dailyCostMicros !== null ||
      form.monthlyCostMicros !== null,
    {
      message:
        "Set at least one cap. To inherit every cap, remove the override.",
      path: ["dailyRequests"],
    },
  );

export type UsageLimitFormValues = z.input<typeof usageLimitFormSchema>;
