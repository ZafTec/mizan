import { test as authTest, expect } from "./fixtures/auth.fixture";

authTest.describe("Meal Plan (authenticated)", () => {
	authTest("meal plan page renders", async ({ authenticatedPage: page }) => {
		await page.goto("/meal-plan");
		await expect(page.getByTestId("meal-plan-page")).toBeVisible();
		await expect(page.getByRole("heading", { name: /Meal Planning/i })).toBeVisible();
	});

	authTest("Create Meal Plan link is visible", async ({ authenticatedPage: page }) => {
		await page.goto("/meal-plan");
		await expect(page.getByRole("link", { name: /Create Meal Plan/i })).toBeVisible();
	});

	authTest("Shopping List link is visible", async ({ authenticatedPage: page }) => {
		await page.goto("/meal-plan");
		await expect(page.getByRole("link", { name: /Shopping List/i })).toBeVisible();
	});

	authTest("empty state shows create prompt", async ({ authenticatedPage: page }) => {
		await page.goto("/meal-plan");
		// Either shows existing plans or the empty state with a create link
		const hasPlans = await page.getByText(/Weekly Plan|No meal plans yet/i).isVisible({ timeout: 5000 });
		expect(hasPlans).toBeTruthy();
	});

	authTest("create meal plan page renders with calendar", async ({ authenticatedPage: page }) => {
		await page.goto("/meal-plan/create");
		await expect(page.getByRole("heading", { name: /Create Meal Plan|New Meal Plan/i })).toBeVisible();
		// Week calendar should show day columns
		await expect(page.getByText(/Mon|Tue|Wed|Thu|Fri|Sat|Sun/i).first()).toBeVisible();
		// Meal type rows
		await expect(page.getByText(/Breakfast/i).first()).toBeVisible();
		await expect(page.getByText(/Lunch/i).first()).toBeVisible();
		await expect(page.getByText(/Dinner/i).first()).toBeVisible();
	});

	authTest("create meal plan → redirects to meal plan list on success", async ({ authenticatedPage: page }) => {
		const planName = `E2E Plan ${Date.now()}`;

		await page.goto("/meal-plan/create");
		const nameInput = page.getByPlaceholder(/plan name|name/i);
		if (await nameInput.isVisible()) {
			await nameInput.fill(planName);
		}

		// Submit the plan (even if empty - backend validates)
		const saveButton = page.getByRole("button", { name: /Save|Create/i }).last();
		await saveButton.click();

		// Should redirect to /meal-plan after creation
		await page.waitForURL(/\/meal-plan$/, { timeout: 10000 });
		await expect(page.getByTestId("meal-plan-page")).toBeVisible();
	});

	authTest("shopping list page renders", async ({ authenticatedPage: page }) => {
		await page.goto("/meal-plan/shopping-list");
		await expect(page.getByRole("heading", { name: /Shopping List/i })).toBeVisible();
	});
});
