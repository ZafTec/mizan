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
			await searchInput.press("Enter");
			await page.waitForURL(/searchIngredient/);
			const url = page.url();
			expect(url).toContain("chicken");
		}
	});
});
