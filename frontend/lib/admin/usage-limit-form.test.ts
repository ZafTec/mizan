import { describe, expect, it } from "vitest";
import { usageLimitFormSchema } from "./usage-limit-form";

const blank = {
  line: "all",
  dailyRequests: "",
  dailyTokens: "",
  dailyCostMicros: "",
  monthlyCostMicros: "",
  note: "",
};

describe("usageLimitFormSchema", () => {
  it.each([
    [
      "one cap on every line",
      { ...blank, dailyRequests: "20" },
      {
        line: null,
        dailyRequests: 20,
        dailyTokens: null,
        dailyCostMicros: null,
        monthlyCostMicros: null,
        note: null,
      },
    ],
    [
      "zero is a cap, and the line and note are kept",
      { ...blank, line: "Planning", monthlyCostMicros: "0", note: " trial " },
      {
        line: "Planning",
        dailyRequests: null,
        dailyTokens: null,
        dailyCostMicros: null,
        monthlyCostMicros: 0,
        note: "trial",
      },
    ],
  ])("accepts %s", (_, input, expected) => {
    expect(usageLimitFormSchema.parse(input)).toEqual(expected);
  });

  it.each([
    ["nothing set", blank, "Set at least one cap"],
    ["a negative cap", { ...blank, dailyTokens: "-1" }, "cannot be negative"],
    ["a fraction", { ...blank, dailyRequests: "1.5" }, "whole number"],
    ["text", { ...blank, dailyCostMicros: "lots" }, "whole number"],
    [
      "a long note",
      { ...blank, dailyRequests: "1", note: "x".repeat(501) },
      "under 500",
    ],
  ])("refuses %s", (_, input, message) => {
    const result = usageLimitFormSchema.safeParse(input);
    expect(result.success).toBe(false);
    expect(
      result.error?.issues.map((issue) => issue.message).join(" "),
    ).toContain(message);
  });
});
