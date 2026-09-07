import { describe, expect, it } from "vitest";
import { dateInTimeZone, isLogDate, shiftDate } from "./log-date";

describe("log calendar dates", () => {
  it("uses the configured day across the UTC boundary", () => {
    const now = new Date("2026-09-07T22:30:00Z");
    expect(dateInTimeZone("Africa/Addis_Ababa", now)).toBe("2026-09-08");
    expect(dateInTimeZone("America/Los_Angeles", now)).toBe("2026-09-07");
  });
  it("rejects rolled-over dates and crosses month and leap-day boundaries", () => {
    expect(isLogDate("2026-02-30")).toBe(false);
    expect(isLogDate("2024-02-29")).toBe(true);
    expect(isLogDate("2026-9-7")).toBe(false);
    expect(shiftDate("2024-03-01", -1)).toBe("2024-02-29");
    expect(shiftDate("2026-01-01", -1)).toBe("2025-12-31");
  });
});
