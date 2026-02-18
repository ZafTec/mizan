import { test as authTest, expect } from "./fixtures/auth.fixture";

authTest.describe("Meal Plan (authenticated)", () => {
	authTest("meal plan page renders", async ({ authenticatedPage: page }) => {
		await page.goto("/meal-plan");
		await expect(page.getByTestId("meal-plan-page")).toBeVisible();
	});

	authTest("create meal plan link is visible", async ({ authenticatedPage: page }) => {
		await page.goto("/meal-plan");
		await expect(
			page.getByRole("link", { name: /create meal plan|new meal plan/i })
		).toBeVisible();
	});
});
