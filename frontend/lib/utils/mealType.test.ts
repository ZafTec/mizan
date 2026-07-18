import { describe, expect, it } from "vitest";
import { defaultMealTypeForHour, formatMealType } from "./mealType";

describe("meal type utilities", () => {
  it("selects canonical local meal types", () => {
    expect(defaultMealTypeForHour(8)).toBe("BREAKFAST");
    expect(defaultMealTypeForHour(13)).toBe("LUNCH");
    expect(defaultMealTypeForHour(19)).toBe("DINNER");
  });

  it("formats canonical and legacy values", () => {
    expect(formatMealType("drink")).toBe("Drink");
    expect(formatMealType("MEAL")).toBe("Meal");
  });
});
