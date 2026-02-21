import { test as authTest, expect } from "./fixtures/auth.fixture";

authTest.describe("Profile (authenticated)", () => {
	authTest("profile page renders with user info", async ({ authenticatedPage: page }) => {
		await page.goto("/profile");
		await expect(page.getByTestId("profile-page")).toBeVisible();
		await expect(page.getByRole("heading", { name: /Profile/i }).first()).toBeVisible();
		// User email should be displayed
		await expect(page.getByText(/@example\.com/i)).toBeVisible();
	});

	authTest("Edit Profile button opens modal", async ({ authenticatedPage: page }) => {
		await page.goto("/profile");
		await page.getByRole("button", { name: /Edit Profile/i }).click();

		// Modal should appear with name and image fields
		await expect(page.getByRole("heading", { name: /Edit Profile/i })).toBeVisible();
		await expect(page.getByPlaceholder("Enter your name")).toBeVisible();
		await expect(page.getByRole("button", { name: /Cancel/i })).toBeVisible();
		await expect(page.getByRole("button", { name: /Save Changes/i })).toBeVisible();
	});

	authTest("Edit Profile modal closes on cancel", async ({ authenticatedPage: page }) => {
		await page.goto("/profile");
		await page.getByRole("button", { name: /Edit Profile/i }).click();
		await expect(page.getByRole("heading", { name: /Edit Profile/i })).toBeVisible();

		await page.getByRole("button", { name: /Cancel/i }).click();
		await expect(page.getByRole("heading", { name: /Edit Profile/i })).not.toBeVisible();
	});

	authTest("profile page links to key sections", async ({ authenticatedPage: page }) => {
		await page.goto("/profile");
		await expect(page.getByRole("link", { name: /Nutrition Goals/i })).toBeVisible();
		await expect(page.getByRole("link", { name: /Food Diary/i })).toBeVisible();
		await expect(page.getByRole("link", { name: /Meal Plan/i })).toBeVisible();
		await expect(page.getByRole("link", { name: /My Recipes/i })).toBeVisible();
	});

	authTest("account settings section has action buttons", async ({ authenticatedPage: page }) => {
		await page.goto("/profile");
		// Account settings buttons
		await expect(page.getByRole("button", { name: /Notifications/i })).toBeVisible();
		await expect(page.getByRole("button", { name: /Privacy & Security/i })).toBeVisible();
		await expect(page.getByRole("button", { name: /Appearance/i })).toBeVisible();
	});

	authTest("sign out button is present", async ({ authenticatedPage: page }) => {
		await page.goto("/profile");
		// Sign out in the danger zone
		await expect(page.getByRole("button", { name: /Sign Out/i })).toBeVisible();
	});

	authTest("sign out navigates to login", async ({ authenticatedPage: page }) => {
		await page.goto("/profile");
		await page.getByRole("button", { name: /Sign Out/i }).click();
		await page.waitForURL(/\/login/, { timeout: 8000 });
		await expect(page).toHaveURL(/\/login/);
	});
});
