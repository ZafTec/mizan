import { describe, expect, it } from "vitest";
import { typedCard } from "./cards";

const data = { planId: "p1" };

describe("typedCard", () => {
  it.each([
    ["plan_preview", data, "plan_preview"],
    ["day_plan", data, "day_plan"],
    ["cost_table", data, "cost_table"],
    ["shopping_list", { listId: "l1" }, "shopping_list"],
    ["recipe_card", data, null],
    ["Plan_Preview", data, null],
    ["plan_preview", null, null],
    ["plan_preview", "a model's markup", null],
  ])("%s with %j is %s", (type, cardData, expected) => {
    expect(typedCard({ type, data: cardData })?.type ?? null).toBe(expected);
  });
});
