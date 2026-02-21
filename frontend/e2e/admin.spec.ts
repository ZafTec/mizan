import { test, expect } from "@playwright/test";
import { test as authTest } from "./fixtures/auth.fixture";

test.describe("Admin access control (unauthenticated)", () => {
	test("unauthenticated user redirected from /admin", async ({ page }) => {
		await page.goto("/admin");
		await expect(page).toHaveURL(/\/login/);
	});
});

authTest.describe("Admin access control (authenticated non-admin)", () => {
	authTest("regular user redirected from /admin to home", async ({ authenticatedPage: page }) => {
		await page.goto("/admin");
		// Non-admin users get redirected to /
		await expect(page).not.toHaveURL(/\/admin/);
	});

	authTest("regular user cannot access admin users page", async ({ authenticatedPage: page }) => {
		await page.goto("/admin/users");
		await expect(page).not.toHaveURL(/\/admin/);
	});
});

authTest.describe("Admin dashboard (admin user)", () => {
	authTest("admin can access dashboard", async ({ adminPage: page }) => {
		await page.goto("/admin");
		await expect(page.getByRole("heading", { name: /Admin Dashboard/i })).toBeVisible();
	});

	authTest("admin dashboard shows stats", async ({ adminPage: page }) => {
		await page.goto("/admin");
		await expect(page.getByText(/Total Users/i)).toBeVisible();
		await expect(page.getByText(/Active Trainers/i)).toBeVisible();
	});

	authTest("admin can access user management page", async ({ adminPage: page }) => {
		await page.goto("/admin/users");
		await expect(page.getByRole("heading", { name: /User Management|Users/i })).toBeVisible();
	});

	authTest("admin user list has table with headers", async ({ adminPage: page }) => {
		await page.goto("/admin/users");
		await expect(page.getByRole("columnheader", { name: /^User$/i })).toBeVisible();
		await expect(page.getByRole("columnheader", { name: /^Role$/i })).toBeVisible();
	});

	authTest("admin can access ingredients management", async ({ adminPage: page }) => {
		await page.goto("/admin/ingredients");
		await expect(page.getByRole("heading", { name: /Ingredients/i })).toBeVisible();
	});

	authTest("Manage users link on dashboard navigates to user list", async ({ adminPage: page }) => {
		await page.goto("/admin");
		await page.getByRole("link", { name: "Manage users →" }).click();
		await expect(page).toHaveURL(/\/admin\/users/);
	});
});
