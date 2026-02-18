import { test, expect } from "@playwright/test";

test.describe("Navigation", () => {
	test("desktop nav links are visible", async ({ page }) => {
		await page.goto("/");

		const navbar = page.getByTestId("navbar");
		await expect(navbar).toBeVisible();
		await expect(navbar.getByRole("link", { name: /Foods/i })).toBeVisible();
		await expect(navbar.getByRole("link", { name: /Recipes/i })).toBeVisible();
	});

	test("mobile hamburger opens and closes menu", async ({ page }) => {
		await page.setViewportSize({ width: 375, height: 667 });
		await page.goto("/");

		const toggle = page.getByTestId("nav-mobile-toggle");
		await expect(toggle).toBeVisible();

		await toggle.click();
		await expect(page.getByTestId("nav-mobile-menu")).toBeVisible();

		await toggle.click();
		await expect(page.getByTestId("nav-mobile-menu")).not.toBeVisible();
	});
});
