import { test, expect } from "@playwright/test";

test.describe("Landing page", () => {
	test("renders hero section for unauthenticated users", async ({ page }) => {
		await page.goto("/");

		await expect(page.getByTestId("hero-section")).toBeVisible();
		await expect(page.getByRole("heading", { name: /Find Your Balance/i })).toBeVisible();
		await expect(page.getByRole("link", { name: /Get Started Free/i })).toBeVisible();
	});

	test("feature and CTA sections are visible", async ({ page }) => {
		await page.goto("/");

		await expect(page.getByTestId("feature-section")).toBeVisible();
		await expect(page.getByTestId("testimonial-section")).toBeVisible();
		await expect(page.getByTestId("cta-section")).toBeVisible();
	});

	test("CTA links navigate to register", async ({ page }) => {
		await page.goto("/");

		await page.getByTestId("hero-section").getByRole("link", { name: /Get Started Free/i }).click();
		await expect(page).toHaveURL(/\/register/);
	});
});
