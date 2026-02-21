import { test as authTest, expect } from "./fixtures/auth.fixture";

authTest.describe("Meals (authenticated)", () => {
	authTest("food diary page renders", async ({ authenticatedPage: page }) => {
		await page.goto("/meals");
		await expect(page.getByTestId("meal-list")).toBeVisible();
		await expect(page.getByRole("heading", { name: /Food Diary/i })).toBeVisible();
	});

	authTest("date navigation buttons are present", async ({ authenticatedPage: page }) => {
		await page.goto("/meals");
		// Prev and next day buttons
		const prevButton = page.getByRole("button").filter({ has: page.locator(".ri-arrow-left-s-line") }).first();
		const nextButton = page.getByRole("button").filter({ has: page.locator(".ri-arrow-right-s-line") }).first();
		await expect(prevButton).toBeVisible();
		await expect(nextButton).toBeVisible();
	});

	authTest("previous day navigation updates URL", async ({ authenticatedPage: page }) => {
		await page.goto("/meals");
		const prevButton = page.getByRole("button").filter({ has: page.locator(".ri-arrow-left-s-line") }).first();
		await prevButton.click();
		await page.waitForURL(/date=/, { timeout: 5000 });
		expect(page.url()).toContain("date=");
	});

	authTest("Log Meal link navigates to add page", async ({ authenticatedPage: page }) => {
		await page.goto("/meals");
		await page.getByRole("link", { name: /Log Meal/i }).first().click();
		await expect(page).toHaveURL(/\/meals\/add/);
	});

	authTest("log meal form has all fields", async ({ authenticatedPage: page }) => {
		await page.goto("/meals/add");
		await expect(page.getByRole("heading", { name: /Log Meal/i })).toBeVisible();
		await expect(page.getByLabel("Meal Name")).toBeVisible();
		await expect(page.locator("#mealType")).toBeVisible();
		await expect(page.locator("#calories")).toBeVisible();
		await expect(page.locator("#protein")).toBeVisible();
		await expect(page.locator("#carbs")).toBeVisible();
		await expect(page.locator("#fat")).toBeVisible();
		await expect(page.getByRole("button", { name: /Log Meal/i })).toBeVisible();
	});

	authTest("log meal → appears in diary", async ({ authenticatedPage: page }) => {
		const mealName = `E2E Meal ${Date.now()}`;

		await page.goto("/meals/add");
		await page.getByLabel("Meal Name").fill(mealName);
		await page.locator("#calories").fill("450");
		await page.locator("#protein").fill("35");
		await page.locator("#carbs").fill("40");
		await page.locator("#fat").fill("12");

		await page.getByRole("button", { name: /Log Meal/i }).click();

		// Should redirect to /meals after success
		await page.waitForURL(/\/meals/, { timeout: 10000 });
		await expect(page.getByTestId("meal-list")).toBeVisible();
		await expect(page.getByText(mealName)).toBeVisible({ timeout: 8000 });
	});

	authTest("delete meal from diary", async ({ authenticatedPage: page }) => {
		const mealName = `E2E Delete ${Date.now()}`;

		// Log a meal first
		await page.goto("/meals/add");
		await page.getByLabel("Meal Name").fill(mealName);
		await page.locator("#calories").fill("200");
		await page.getByRole("button", { name: /Log Meal/i }).click();
		await page.waitForURL(/\/meals/, { timeout: 10000 });
		await expect(page.getByText(mealName)).toBeVisible({ timeout: 8000 });

		// Click the delete button for that meal
		const mealRow = page.locator("div").filter({ hasText: mealName }).last();
		await mealRow.getByTitle("Delete meal").click();

		// Confirm deletion in the modal
		await expect(page.getByTestId("delete-confirm-modal")).toBeVisible();
		await page.getByTestId("delete-confirm-modal").getByRole("button", { name: /Delete/i }).click();

		// Meal should be removed
		await expect(page.getByText(mealName)).not.toBeVisible({ timeout: 8000 });
	});

	authTest("Browse Recipes link in add page navigates to recipes", async ({ authenticatedPage: page }) => {
		await page.goto("/meals/add");
		await page.getByRole("link", { name: /Browse Recipes/i }).click();
		await expect(page).toHaveURL(/\/recipes/);
	});
});
