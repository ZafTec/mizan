import { describe, expect, it } from "vitest";
import {
  parseAliasMapping,
  parseManualPrice,
  parsePriceReport,
  type ManualPriceForm,
  type PriceReportForm,
} from "./schema";

const report = (overrides: Partial<PriceReportForm> = {}): PriceReportForm => ({
  price: "200",
  quantity: "500",
  unit: "g",
  note: "",
  ...overrides,
});

describe("the price report form", () => {
  it("shapes typed values into the request fields", () => {
    expect(
      parsePriceReport(report({ price: "199.5", note: "  Shola market " })),
    ).toEqual({
      errors: null,
      value: { price: 199.5, quantity: 500, unit: "g", note: "Shola market" },
    });
  });

  it.each([
    [{ price: "" }, "price", "Price is a number"],
    [{ price: "abc" }, "price", "Price is a number"],
    [{ price: "0" }, "price", "Price must be above zero"],
    [{ price: "-5" }, "price", "Price must be above zero"],
    [{ price: "10000000" }, "price", "Price is too large"],
    [{ quantity: "" }, "quantity", "Quantity is a number"],
    [{ quantity: "0" }, "quantity", "Quantity must be above zero"],
    [{ quantity: "10000" }, "quantity", "Quantity is too large"],
    [{ unit: "cup" }, "unit", "Choose g, kg, ml, l or piece"],
    [
      { note: "x".repeat(1001) },
      "note",
      "Keep the note under 1,000 characters",
    ],
  ] as const)("refuses %o: %s says %s", (overrides, field, message) => {
    const result = parsePriceReport(report(overrides));
    expect(result.value).toBeNull();
    expect(result.errors?.[field]).toBe(message);
  });

  it.each(["g", "kg", "ml", "l", "piece"])("accepts the unit %s", (unit) => {
    expect(parsePriceReport(report({ unit })).errors).toBeNull();
  });

  it("turns an empty note into null", () => {
    expect(parsePriceReport(report({ note: "   " })).value?.note).toBeNull();
  });
});

const manual = (overrides: Partial<ManualPriceForm> = {}): ManualPriceForm => ({
  foodId: "food-1",
  regionCode: "ET-AA",
  price: "250",
  quantity: "1",
  unit: "kg",
  confidence: "Estimated",
  ...overrides,
});

describe("the manual price form", () => {
  it("shapes typed values into the request fields", () => {
    expect(parseManualPrice(manual({ confidence: "Observed" }))).toEqual({
      errors: null,
      value: {
        foodId: "food-1",
        regionCode: "ET-AA",
        price: 250,
        quantity: 1,
        unit: "kg",
        confidence: "Observed",
      },
    });
  });

  it.each([
    [{ foodId: "" }, "foodId", "Choose a food"],
    [{ regionCode: "" }, "regionCode", "Choose a region"],
    [{ price: "0" }, "price", "Price must be above zero"],
    [{ confidence: "Reported" }, "confidence", "Choose Estimated or Observed"],
  ] as const)("refuses %o: %s says %s", (overrides, field, message) => {
    expect(parseManualPrice(manual(overrides)).errors?.[field]).toBe(message);
  });
});

describe("mapping a queued product", () => {
  it.each([
    [
      { quantity: "500", unit: "g" },
      { quantity: 500, unit: "g" },
    ],
    [
      { quantity: "", unit: "" },
      { quantity: null, unit: null },
    ],
  ])("takes %o", (amount, expected) => {
    expect(parseAliasMapping({ foodId: "food-1", ...amount })).toEqual({
      errors: null,
      value: { foodId: "food-1", ...expected },
    });
  });

  it.each([
    [{ foodId: "", quantity: "", unit: "" }, "foodId", "Choose a food"],
    [
      { foodId: "food-1", quantity: "500", unit: "" },
      "quantity",
      "Give a quantity with its unit, or neither",
    ],
    [
      { foodId: "food-1", quantity: "", unit: "kg" },
      "quantity",
      "Give a quantity with its unit, or neither",
    ],
    [
      { foodId: "food-1", quantity: "0", unit: "kg" },
      "quantity",
      "Quantity must be above zero",
    ],
  ] as const)("refuses %o: %s says %s", (values, field, message) => {
    expect(parseAliasMapping(values).errors?.[field]).toBe(message);
  });
});
