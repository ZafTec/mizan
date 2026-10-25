import { describe, expect, it } from "vitest";
import {
  confidenceLabel,
  formatCoverage,
  formatMoney,
  formatPriceDate,
  itemCount,
} from "./format";

describe("formatMoney", () => {
  it.each([
    [665.5, "ETB", "ETB 665.50"],
    [0, "ETB", "ETB 0.00"],
    [1250, "KES", "KES 1,250.00"],
    [1234567.891, "ETB", "ETB 1,234,567.89"],
    [0.005, "ETB", "ETB 0.01"],
    [12.3, "etb", "ETB 12.30"],
    [-40, "ETB", "-ETB 40.00"],
  ])("formats %s %s as %s", (amount, currency, expected) => {
    expect(formatMoney(amount, currency)).toBe(expected);
  });
});

describe("the other price words", () => {
  it.each([
    ["Observed", "Observed"],
    ["Reported", "Reported"],
    ["Estimated", "Estimated, a rough guess"],
    ["Unknown", "Unknown"],
    [null, ""],
  ])("names confidence %s as %s", (confidence, expected) => {
    expect(confidenceLabel(confidence)).toBe(expected);
  });

  it.each([
    [0, "0%"],
    [0.8, "80%"],
    [0.667, "67%"],
    [1, "100%"],
  ])("shows a coverage of %s as %s", (share, expected) => {
    expect(formatCoverage(share)).toBe(expected);
  });

  it.each([
    [1, "1 item"],
    [3, "3 items"],
    [0, "0 items"],
  ])("counts %s as %s", (count, expected) => {
    expect(itemCount(count)).toBe(expected);
  });

  it("prints the UTC day", () => {
    expect(formatPriceDate("2026-10-24T23:30:00Z")).toBe("24 Oct 2026");
  });
});
