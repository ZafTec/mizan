import { test, expect } from "@playwright/test";

test.describe("Navigation", () => {
	test("desktop nav links are visible", async ({ page }) => {
		await page.goto("/");

		await expect(page.getByTestId("navbar")).toBeVisible();
		await expect(page.getByRole("link", { name: /Foods/i })).toBeVisible();
		await expect(page.getByRole("link", { name: /Recipes/i })).toBeVisible();
	});

	test("mobile hamburger opens and closes menu", async ({ page }) => {
		await page.goto("/");

		const toggle = page.getByTestId("nav-mobile-toggle");
		await expect(toggle).toBeVisible();

		await toggle.click();
		await expect(page.getByTestId("nav-mobile-menu")).toBeVisible();

		await toggle.click();
		await expect(page.getByTestId("nav-mobile-menu")).not.toBeVisible();
	});
});
