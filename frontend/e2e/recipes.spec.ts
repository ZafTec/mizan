import { test, expect } from "@playwright/test";
import { test as authTest } from "./fixtures/auth.fixture";

test.describe("Recipes (unauthenticated)", () => {
	test("recipes list page renders", async ({ page }) => {
		await page.goto("/recipes");
		await expect(page.getByTestId("recipe-list")).toBeVisible();
		await expect(page.getByRole("heading", { name: /Recipes/i })).toBeVisible();
	});

	test("popular recipes section on landing", async ({ page }) => {
		await page.goto("/");
		await expect(page.getByRole("heading", { name: /Popular Recipes/i })).toBeVisible();
	});
});

authTest.describe("Recipes (authenticated)", () => {
	authTest("create recipe page loads", async ({ authenticatedPage: page }) => {
		await page.goto("/recipes/add");
		await expect(page.getByRole("heading", { name: /Create Recipe/i })).toBeVisible();
	});

	authTest("recipe collections visible", async ({ authenticatedPage: page }) => {
		await page.goto("/recipes");
		await expect(page.getByText(/Favorites/i)).toBeVisible();
		await expect(page.getByText(/My Recipes/i)).toBeVisible();
	});
});
