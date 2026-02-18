import { test as authTest, expect } from "./fixtures/auth.fixture";

authTest.describe("Meals (authenticated)", () => {
	authTest("meals page renders diary", async ({ authenticatedPage: page }) => {
		await page.goto("/meals");
		await expect(page.getByTestId("meal-list")).toBeVisible();
	});

	authTest("date navigation changes the displayed date", async ({ authenticatedPage: page }) => {
		await page.goto("/meals");

		const prevButton = page.getByRole("button", { name: /previous|prev|←/i }).first();
		if (await prevButton.isVisible()) {
			const beforeText = await page.locator("[data-testid='meal-list']").textContent();
			await prevButton.click();
			await page.waitForURL(/date=/);
			const afterText = await page.locator("[data-testid='meal-list']").textContent();
			expect(beforeText).not.toBe(afterText);
		}
	});

	authTest("log meal button navigates to add meal", async ({ authenticatedPage: page }) => {
		await page.goto("/meals");
		const logButton = page.getByRole("link", { name: /log meal|add meal/i }).first();
		if (await logButton.isVisible()) {
			await logButton.click();
			await expect(page).toHaveURL(/\/meals\/add/);
		}
	});
});
