import { test, expect } from "@playwright/test";

test.describe("Landing page", () => {
	test("renders hero section for unauthenticated users", async ({ page }) => {
		await page.goto("/");
		const hero = page.getByTestId("hero-section");
		await expect(hero).toBeVisible();
		await expect(page.getByRole("heading", { name: /Find Your Balance/i })).toBeVisible();
		await expect(hero.getByRole("link", { name: /Get Started Free/i })).toBeVisible();
	});

	test("feature section is visible", async ({ page }) => {
		await page.goto("/");
		await expect(page.getByTestId("feature-section")).toBeVisible();
	});

	test("testimonial section is visible", async ({ page }) => {
		await page.goto("/");
		await expect(page.getByTestId("testimonial-section")).toBeVisible();
	});

	test("CTA section is visible", async ({ page }) => {
		await page.goto("/");
		await expect(page.getByTestId("cta-section")).toBeVisible();
	});

	test("hero Get Started CTA navigates to register", async ({ page }) => {
		await page.goto("/");
		await page.getByTestId("hero-section").getByRole("link", { name: /Get Started Free/i }).click();
		await expect(page).toHaveURL(/\/register/);
	});

	test("sign in link is visible in navbar", async ({ page }) => {
		await page.goto("/");
		await expect(page.getByRole("link", { name: /Sign In|Login/i }).first()).toBeVisible();
	});

	test("page title is set", async ({ page }) => {
		await page.goto("/");
		await expect(page).toHaveTitle(/Mizan|MacroChef/i);
	});
});
