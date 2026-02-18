import { test as authTest, expect } from "./fixtures/auth.fixture";

authTest.describe("Profile (authenticated)", () => {
	authTest("profile page renders", async ({ authenticatedPage: page }) => {
		await page.goto("/profile");
		await expect(page.getByTestId("profile-page")).toBeVisible();
	});

	authTest("profile shows user information section", async ({ authenticatedPage: page }) => {
		await page.goto("/profile");
		// Profile card with avatar or initials
		await expect(page.getByRole("heading", { name: /profile|account/i }).first()).toBeVisible();
	});
});
