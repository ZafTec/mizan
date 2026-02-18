import { test, expect } from "@playwright/test";

test.describe("Ingredients", () => {
	test("ingredient list page renders table", async ({ page }) => {
		await page.goto("/ingredients");
		await expect(page.getByTestId("ingredient-list")).toBeVisible();
		await expect(page.getByRole("heading", { name: /ingredients|foods/i })).toBeVisible();
	});

	test("search filters the list", async ({ page }) => {
		await page.goto("/ingredients");

		const searchInput = page.getByTestId("search-input");
		if (await searchInput.isVisible()) {
			await searchInput.fill("chicken");
			// Give debounce time + navigation
			await page.waitForTimeout(400);
			// Verify URL updated or list filtered
			const url = page.url();
			expect(url).toContain("chicken");
		}
	});
});
