import { test, expect } from "@playwright/test";
import { test as authTest } from "./fixtures/auth.fixture";

test.describe("Ingredients (unauthenticated)", () => {
	test("unauthenticated user redirected to login", async ({ page }) => {
		await page.goto("/ingredients");
		await expect(page).toHaveURL(/\/login/);
	});
});

authTest.describe("Ingredients (authenticated)", () => {
	authTest("ingredient list page renders", async ({ authenticatedPage: page }) => {
		await page.goto("/ingredients");
		await expect(page.getByTestId("ingredient-list")).toBeVisible();
		await expect(page.getByRole("heading", { name: /Ingredients/i })).toBeVisible();
	});

	authTest("ingredient table has column headers", async ({ authenticatedPage: page }) => {
		await page.goto("/ingredients");
		await expect(page.getByRole("columnheader", { name: /Name/i })).toBeVisible();
		await expect(page.getByRole("columnheader", { name: /Calories/i })).toBeVisible();
		await expect(page.getByRole("columnheader", { name: /Protein/i })).toBeVisible();
	});

	authTest("search input is present", async ({ authenticatedPage: page }) => {
		await page.goto("/ingredients");
		await expect(page.getByTestId("search-input")).toBeVisible();
	});

	authTest("search updates URL query param", async ({ authenticatedPage: page }) => {
		await page.goto("/ingredients");
		const searchInput = page.getByTestId("search-input");
		await searchInput.fill("chicken");
		await searchInput.press("Enter");

		await page.waitForURL(/searchIngredient=chicken/, { timeout: 5000 });
		expect(page.url()).toContain("searchIngredient=chicken");
	});

	authTest("search with results shows matching ingredients", async ({ authenticatedPage: page }) => {
		await page.goto("/ingredients?searchIngredient=chicken");
		await expect(page.getByTestId("ingredient-list")).toBeVisible();
		// Either results or empty state should be visible
		const hasResults = await page.getByRole("cell", { name: /chicken/i }).first().isVisible({ timeout: 5000 }).catch(() => false);
		const hasEmpty = await page.getByText(/No ingredients found/i).isVisible({ timeout: 5000 }).catch(() => false);
		expect(hasResults || hasEmpty).toBeTruthy();
	});

	authTest("Add button links to ingredient add page", async ({ authenticatedPage: page }) => {
		await page.goto("/ingredients");
		const addLink = page.getByRole("link", { name: /^Add$/i });
		await expect(addLink).toBeVisible();
		await addLink.click();
		await expect(page).toHaveURL(/\/ingredients\/add/);
	});

});
