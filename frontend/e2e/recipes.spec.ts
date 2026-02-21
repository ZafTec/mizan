import { test, expect } from "@playwright/test";
import { test as authTest } from "./fixtures/auth.fixture";

test.describe("Recipes (unauthenticated)", () => {
	test("recipe list page renders", async ({ page }) => {
		await page.goto("/recipes");
		await expect(page.getByTestId("recipe-list")).toBeVisible();
		await expect(page.getByRole("heading", { name: /^Recipes$/i })).toBeVisible();
	});

	test("create recipe button not visible when unauthenticated", async ({ page }) => {
		await page.goto("/recipes");
		await expect(page.getByRole("link", { name: /Create Recipe/i })).not.toBeVisible();
	});

	test("recipe list shows collections section", async ({ page }) => {
		await page.goto("/recipes");
		await expect(page.getByText(/My Recipes/i)).toBeVisible();
	});
});

authTest.describe("Recipes (authenticated)", () => {
	authTest("create recipe button is visible", async ({ authenticatedPage: page }) => {
		await page.goto("/recipes");
		await expect(page.getByRole("link", { name: /Create Recipe/i })).toBeVisible();
	});

	authTest("recipe create form has all required fields", async ({ authenticatedPage: page }) => {
		await page.goto("/recipes/add");
		await expect(page.getByRole("heading", { name: /Create Recipe/i })).toBeVisible();
		await expect(page.getByLabel("Recipe Name")).toBeVisible();
		await expect(page.getByLabel("Description")).toBeVisible();
		await expect(page.getByLabel("Instructions")).toBeVisible();
		await expect(page.getByLabel("Servings")).toBeVisible();
		await expect(page.getByLabel("Visibility")).toBeVisible();
		await expect(page.getByRole("button", { name: /Add Ingredient/i })).toBeVisible();
		await expect(page.getByRole("button", { name: /Create Recipe/i })).toBeVisible();
	});

	authTest("adding ingredient row shows search input", async ({ authenticatedPage: page }) => {
		await page.goto("/recipes/add");
		await page.getByRole("button", { name: /Add Ingredient/i }).click();
		await expect(page.getByPlaceholder("Search ingredient...")).toBeVisible();
		await expect(page.getByPlaceholder("Amount")).toBeVisible();
		await expect(page.getByRole("button", { name: /Remove ingredient/i })).toBeVisible();
	});

	authTest("multiple ingredient rows can be added", async ({ authenticatedPage: page }) => {
		await page.goto("/recipes/add");
		await page.getByRole("button", { name: /Add Ingredient/i }).click();
		await page.getByRole("button", { name: /Add Ingredient/i }).click();
		const searchInputs = page.getByPlaceholder("Search ingredient...");
		await expect(searchInputs).toHaveCount(2);
	});

	authTest("create recipe → redirects to recipe list on success", async ({ authenticatedPage: page }) => {
		const recipeName = `E2E Recipe ${Date.now()}`;

		await page.goto("/recipes/add");
		await page.getByLabel("Recipe Name").fill(recipeName);
		await page.getByLabel("Description").fill("E2E test recipe description");
		await page.getByLabel("Instructions").fill("Step 1: Test\nStep 2: Verify");

		// Add an ingredient via search (backend-dependent)
		await page.getByRole("button", { name: /Add Ingredient/i }).click();
		const searchInput = page.getByPlaceholder("Search ingredient...");
		await searchInput.fill("chicken");

		// Wait for dropdown (ingredient search requires backend with seed data)
		const dropdownItem = page.getByRole("button").filter({ hasText: /chicken/i }).first();
		await dropdownItem.waitFor({ state: "visible", timeout: 5000 });
		await dropdownItem.click();
		await page.getByPlaceholder("Amount").first().fill("200");

		await page.getByRole("button", { name: /Create Recipe/i }).last().click();

		// Should redirect to /recipes
		await page.waitForURL(/\/recipes$/, { timeout: 10000 });
		await expect(page.getByTestId("recipe-list")).toBeVisible();
		await expect(page.getByText(recipeName)).toBeVisible();
	});

	authTest("favorites page renders when authenticated", async ({ authenticatedPage: page }) => {
		await page.goto("/recipes/favorites");
		await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
	});
});
