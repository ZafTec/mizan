import { z } from "zod";

/**
 * The price forms as a person types them: strings from inputs, checked here
 * before the round trip. The API validates again with the same limits.
 */
export const PRICE_UNITS = ["g", "kg", "ml", "l", "piece"] as const;
export type PriceUnit = (typeof PRICE_UNITS)[number];
export const MANUAL_CONFIDENCES = ["Estimated", "Observed"] as const;

const amount = (label: string, max: number) =>
  z.preprocess(
    (value) => (typeof value === "string" && !value.trim() ? undefined : value),
    z.coerce
      .number({ message: `${label} is a number` })
      .gt(0, `${label} must be above zero`)
      .lt(max, `${label} is too large`),
  );

const unit = z.enum(PRICE_UNITS, { message: "Choose g, kg, ml, l or piece" });

export const priceReportSchema = z.object({
  price: amount("Price", 10_000_000),
  quantity: amount("Quantity", 10_000),
  unit,
  note: z
    .string()
    .trim()
    .max(1000, "Keep the note under 1,000 characters")
    .transform((value) => (value === "" ? null : value)),
});

export const manualPriceSchema = z.object({
  foodId: z.string().min(1, "Choose a food"),
  regionCode: z.string().min(1, "Choose a region"),
  price: amount("Price", 10_000_000),
  quantity: amount("Quantity", 10_000),
  unit,
  confidence: z.enum(MANUAL_CONFIDENCES, {
    message: "Choose Estimated or Observed",
  }),
});

/** Mapping a queued product: a food, and a quantity with its unit or neither. */
export const aliasMappingSchema = z
  .object({
    foodId: z.string().min(1, "Choose a food"),
    quantity: z.preprocess(
      (value) => (typeof value === "string" && !value.trim() ? null : value),
      z.coerce
        .number({ message: "Quantity is a number" })
        .gt(0, "Quantity must be above zero")
        .lt(10_000, "Quantity is too large")
        .nullable(),
    ),
    unit: z
      .string()
      .transform((value) => (value === "" ? null : value))
      .refine(
        (value) => value === null || PRICE_UNITS.includes(value as PriceUnit),
        { message: "Choose g, kg, ml, l or piece" },
      ),
  })
  .superRefine((value, context) => {
    if ((value.quantity === null) !== (value.unit === null))
      context.addIssue({
        code: "custom",
        path: ["quantity"],
        message: "Give a quantity with its unit, or neither",
      });
  });

export type PriceReportForm = {
  price: string;
  quantity: string;
  unit: string;
  note: string;
};
export type ManualPriceForm = {
  foodId: string;
  regionCode: string;
  price: string;
  quantity: string;
  unit: string;
  confidence: string;
};

export type AliasMappingForm = {
  foodId: string;
  quantity: string;
  unit: string;
};

type Parsed<Schema extends z.ZodType, Form> =
  | { value: z.infer<Schema>; errors: null }
  | { value: null; errors: Partial<Record<keyof Form, string>> };

function parse<Schema extends z.ZodType, Form>(
  schema: Schema,
  values: Form,
): Parsed<Schema, Form> {
  const result = schema.safeParse(values);
  if (result.success) return { value: result.data, errors: null };
  const errors: Partial<Record<keyof Form, string>> = {};
  for (const issue of result.error.issues) {
    const key = issue.path[0] as keyof Form | undefined;
    if (key && !errors[key]) errors[key] = issue.message;
  }
  return { value: null, errors };
}

/** The report form, or the first problem per field. */
export const parsePriceReport = (values: PriceReportForm) =>
  parse(priceReportSchema, values);

/** The manual price form, or the first problem per field. */
export const parseManualPrice = (values: ManualPriceForm) =>
  parse(manualPriceSchema, values);

/** The review queue's mapping form, or the first problem per field. */
export const parseAliasMapping = (values: AliasMappingForm) =>
  parse(aliasMappingSchema, values);
