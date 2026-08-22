import { describe, expect, it } from "vitest";
import { safeRedirectPath } from "./safe-redirect";

describe("safeRedirectPath", () => {
	it("keeps same-origin paths", () => {
		expect(safeRedirectPath("/dashboard")).toBe("/dashboard");
		expect(safeRedirectPath("/meals?date=2026-08-22")).toBe("/meals?date=2026-08-22");
		expect(safeRedirectPath("/goal/dashboard#trend")).toBe("/goal/dashboard#trend");
	});

	it("rejects anything that leaves the origin", () => {
		for (const hostile of [
			"https://evil.example/steal",
			"//evil.example/steal",
			"/\\evil.example",
			"javascript:alert(1)",
			"dashboard",
		]) {
			expect(safeRedirectPath(hostile)).toBe("/dashboard");
		}
	});

	it("falls back when nothing was supplied", () => {
		expect(safeRedirectPath(null)).toBe("/dashboard");
		expect(safeRedirectPath(undefined)).toBe("/dashboard");
		expect(safeRedirectPath("")).toBe("/dashboard");
	});

	it("honours a caller-supplied fallback", () => {
		expect(safeRedirectPath("https://evil.example", "/")).toBe("/");
	});
});
