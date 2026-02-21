import { test, expect } from "@playwright/test";
import { test as authTest } from "./fixtures/auth.fixture";

test.describe("Navigation (unauthenticated)", () => {
	test("desktop navbar is visible with key links", async ({ page }) => {
		await page.goto("/");
		const navbar = page.getByTestId("navbar");
		await expect(navbar).toBeVisible();
		await expect(navbar.getByRole("link", { name: /Foods/i })).toBeVisible();
		await expect(navbar.getByRole("link", { name: /Recipes/i })).toBeVisible();
	});

	test("mobile hamburger opens menu", async ({ page }) => {
		await page.setViewportSize({ width: 375, height: 667 });
		await page.goto("/");

		const toggle = page.getByTestId("nav-mobile-toggle");
		await expect(toggle).toBeVisible();

		await toggle.click();
		await expect(page.getByTestId("nav-mobile-menu")).toBeVisible();
	});

	test("mobile menu closes on second toggle click", async ({ page }) => {
		await page.setViewportSize({ width: 375, height: 667 });
		await page.goto("/");

		const toggle = page.getByTestId("nav-mobile-toggle");
		await toggle.click();
		await expect(page.getByTestId("nav-mobile-menu")).toBeVisible();

		await toggle.click();
		await expect(page.getByTestId("nav-mobile-menu")).not.toBeVisible();
	});

	test("navbar logo links to homepage", async ({ page }) => {
		await page.goto("/recipes");
		const navbar = page.getByTestId("navbar");
		const logo = navbar.getByRole("link").first();
		await logo.click();
		await expect(page).toHaveURL(/^\//);
	});

	test("Recipes nav link navigates to recipes page", async ({ page }) => {
		await page.goto("/");
		const navbar = page.getByTestId("navbar");
		await navbar.getByRole("link", { name: /Recipes/i }).click();
		await expect(page).toHaveURL(/\/recipes/);
	});

	test("Foods nav link navigates to ingredients page", async ({ page }) => {
		await page.goto("/");
		const navbar = page.getByTestId("navbar");
		await navbar.getByRole("link", { name: /Foods/i }).click();
		// Unauthenticated → redirected to login
		await expect(page).toHaveURL(/\/login|\/ingredients/);
	});
});

authTest.describe("Navigation (authenticated)", () => {
	authTest("user menu is visible when authenticated", async ({ authenticatedPage: page }) => {
		await page.goto("/");
		await expect(page.getByTestId("nav-user-menu")).toBeVisible();
	});

	authTest("user menu is not visible when unauthenticated", async ({ page }) => {
		await page.goto("/");
		await expect(page.getByTestId("nav-user-menu")).not.toBeVisible();
	});

	authTest("dashboard links are accessible from navbar when authenticated", async ({ authenticatedPage: page }) => {
		await page.goto("/");
		const navbar = page.getByTestId("navbar");
		// Authenticated nav should have Meals / Diary link
		await expect(navbar.getByRole("link", { name: /Meals|Diary/i }).first()).toBeVisible();
	});
});
