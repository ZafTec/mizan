import { describe, expect, it } from "vitest";
import { emptyDraftForm, parseDraftForm, type DraftFormValues } from "./draft";

const form = (values: Partial<DraftFormValues> = {}): DraftFormValues => ({
  ...emptyDraftForm("2026-10-26"),
  ...values,
});

describe("parseDraftForm", () => {
  it("sends a week with no budget and no words as the bare request", () => {
    expect(parseDraftForm(form())).toEqual({
      errors: null,
      request: {
        name: null,
        startDate: "2026-10-26",
        durationDays: 7,
        preference: null,
        budgetCeilings: [],
        dailyBudget: null,
      },
    });
  });

  it("sends a typed budget as the one ceiling and keeps the words", () => {
    const parsed = parseDraftForm(
      form({
        name: " Lean week ",
        durationDays: "5",
        weeklyBudget: " 1500.5 ",
        dailyBudget: " 250 ",
        preference: " no pork ",
      }),
    );
    expect(parsed.request).toEqual({
      name: "Lean week",
      startDate: "2026-10-26",
      durationDays: 5,
      preference: "no pork",
      budgetCeilings: [1500.5],
      dailyBudget: 250,
    });
  });

  it.each([
    [{ startDate: "" }, "startDate", "Pick a start date"],
    [{ durationDays: "" }, "durationDays", "Length is a number of days"],
    [{ durationDays: "0" }, "durationDays", "A drafted plan runs 1 to 14 days"],
    [
      { durationDays: "15" },
      "durationDays",
      "A drafted plan runs 1 to 14 days",
    ],
    [
      { durationDays: "2.5" },
      "durationDays",
      "Length is a whole number of days",
    ],
    [
      { weeklyBudget: "cheap" },
      "weeklyBudget",
      "Weekly budget must be a number",
    ],
    [{ weeklyBudget: "0" }, "weeklyBudget", "Weekly budget must be above zero"],
    [
      { weeklyBudget: "-20" },
      "weeklyBudget",
      "Weekly budget must be above zero",
    ],
    [{ dailyBudget: "0" }, "dailyBudget", "Daily budget must be above zero"],
    [{ name: "x".repeat(121) }, "name", "Keep the name under 120 characters"],
    [
      { preference: "x".repeat(1001) },
      "preference",
      "Keep what you would like under 1,000 characters",
    ],
  ] as const)("%j is refused on %s", (values, key, message) => {
    const parsed = parseDraftForm(form(values));
    expect(parsed.request).toBeNull();
    expect(parsed.errors?.[key]).toBe(message);
  });
});
