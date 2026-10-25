import { describe, expect, it } from "vitest";
import {
  dayIndexFor,
  emptyRulesForm,
  parsePlanForm,
  parseRules,
  parseTemplateForm,
  rulesToForm,
  type RulesFormValues,
} from "./rules";

const form = (overrides: Partial<RulesFormValues> = {}): RulesFormValues => ({
  ...emptyRulesForm(),
  ...overrides,
});

describe("the rules form", () => {
  it("turns an empty form into rules with no targets", () => {
    const result = parseRules(form());
    expect(result.errors).toBeNull();
    expect(result.rules).toMatchObject({
      macroMode: "None",
      targetCalories: null,
      maxCarbs: null,
      eatingWindowStart: null,
      weeklyBudget: null,
      currency: null,
      prepStyle: "Daily",
      batchDays: [],
      allowedFoodIds: [],
      forbiddenFoodIds: [],
    });
  });

  it("shapes typed values into the wire rules", () => {
    const result = parseRules(
      form({
        macroMode: "Custom",
        targetCalories: "1800",
        targetProtein: "140",
        maxCarbs: "120",
        eatingWindowStart: "08:00",
        eatingWindowEnd: "20:00",
        weeklyBudget: "2500",
        dailyBudget: "400",
        currency: "etb",
        prepStyle: "Batch",
        batchDays: [0, 3],
        allowedFoods: [{ id: "a", name: "Oats" }],
        forbiddenFoods: [{ id: "b", name: "Sugar" }],
      }),
    );
    expect(result.errors).toBeNull();
    expect(result.rules).toEqual({
      macroMode: "Custom",
      targetCalories: 1800,
      targetProtein: 140,
      maxCarbs: 120,
      targetFat: null,
      eatingWindowStart: "08:00",
      eatingWindowEnd: "20:00",
      weeklyBudget: 2500,
      dailyBudget: 400,
      currency: "ETB",
      prepStyle: "Batch",
      batchDays: [0, 3],
      allowedFoodIds: ["a"],
      forbiddenFoodIds: ["b"],
    });
  });

  it.each<[string, Partial<RulesFormValues>, keyof RulesFormValues, RegExp]>([
    [
      "a non-number target",
      { targetCalories: "lots" },
      "targetCalories",
      /number/,
    ],
    ["a zero target", { targetProtein: "0" }, "targetProtein", /between/],
    [
      "half an eating window",
      { eatingWindowStart: "08:00" },
      "eatingWindowEnd",
      /both a start and an end/,
    ],
    [
      "a budget without a currency",
      { weeklyBudget: "100" },
      "currency",
      /currency/,
    ],
    [
      "a daily budget without a currency",
      { dailyBudget: "80" },
      "currency",
      /currency/,
    ],
    [
      "a bad currency code",
      { weeklyBudget: "100", currency: "birr" },
      "currency",
      /3-letter/,
    ],
    [
      "batch cooking with no days",
      { prepStyle: "Batch" },
      "batchDays",
      /cooking day/,
    ],
    [
      "a food both allowed and forbidden",
      {
        allowedFoods: [{ id: "a", name: "Oats" }],
        forbiddenFoods: [{ id: "a", name: "Oats" }],
      },
      "forbiddenFoods",
      /both allowed and forbidden/,
    ],
  ])("refuses %s", (_, overrides, field, message) => {
    const result = parseRules(form(overrides));
    expect(result.rules).toBeNull();
    expect(result.errors?.[field]).toMatch(message);
  });

  it("drops batch days when cooking is daily", () => {
    const result = parseRules(form({ prepStyle: "Daily", batchDays: [1, 2] }));
    expect(result.rules?.batchDays).toEqual([]);
  });

  it("round-trips rules the API holds, naming foods it knows", () => {
    const values = rulesToForm(
      {
        macroMode: "Keto",
        targetCalories: 1700,
        targetProtein: null,
        maxCarbs: 30,
        targetFat: null,
        eatingWindowStart: "09:00:00",
        eatingWindowEnd: "19:00:00",
        allowedFoodIds: [],
        forbiddenFoodIds: ["b"],
        weeklyBudget: null,
        currency: null,
        prepStyle: "Daily",
        batchDays: [],
      },
      { b: "Sugar" },
    );
    expect(values).toMatchObject({
      macroMode: "Keto",
      targetCalories: "1700",
      maxCarbs: "30",
      eatingWindowStart: "09:00",
      eatingWindowEnd: "19:00",
      forbiddenFoods: [{ id: "b", name: "Sugar" }],
    });
    expect(parseRules(values).rules?.maxCarbs).toBe(30);
  });
});

describe("the plan form", () => {
  it.each([
    ["0", /at least one day/],
    ["32", /at most 31/],
    ["2.5", /whole number/],
    ["", /number of days/],
  ])("refuses a length of %s", (durationDays, message) => {
    const result = parsePlanForm({
      name: "",
      startDate: "2026-10-20",
      durationDays,
    });
    expect(result.errors?.durationDays).toMatch(message);
  });

  it("needs a start date and accepts seven days", () => {
    expect(
      parsePlanForm({ name: "Week", startDate: "", durationDays: "7" }).errors
        ?.startDate,
    ).toMatch(/start date/);
    expect(
      parsePlanForm({
        name: " Week ",
        startDate: "2026-10-20",
        durationDays: 7,
      }).plan,
    ).toEqual({ name: "Week", startDate: "2026-10-20", durationDays: 7 });
  });
});

describe("the template form", () => {
  it.each([
    [{ name: "", durationDays: "7" }, "name", /a name/],
    [{ name: "   ", durationDays: "7" }, "name", /a name/],
    [{ name: "Keto week", durationDays: "0" }, "durationDays", /at least/],
    [{ name: "Keto week", durationDays: "40" }, "durationDays", /at most 31/],
  ] as const)("refuses %o", (values, field, message) => {
    expect(parseTemplateForm(values).errors?.[field]).toMatch(message);
  });

  it("needs no start date", () => {
    expect(
      parseTemplateForm({ name: " Keto week ", durationDays: "5" }).template,
    ).toEqual({ name: "Keto week", durationDays: 5 });
  });
});

describe("the day that covers today", () => {
  it.each([
    ["2026-10-20", 0],
    ["2026-10-22", 2],
    ["2026-10-26", 6],
    ["2026-10-27", 0],
    ["2026-10-19", 0],
  ])("on %s selects day %i of a week from 20 Oct", (today, index) => {
    expect(
      dayIndexFor({ startDate: "2026-10-20", durationDays: 7 }, today),
    ).toBe(index);
  });
});
