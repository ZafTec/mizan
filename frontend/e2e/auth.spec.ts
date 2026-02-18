import { test, expect } from "@playwright/test";

test.describe("Authentication", () => {
	test("login page renders form elements", async ({ page }) => {
		await page.goto("/login");

		await expect(page.getByTestId("login-form")).toBeVisible();
		await expect(page.getByTestId("login-email")).toBeVisible();
		await expect(page.getByTestId("login-password")).toBeVisible();
		await expect(page.getByTestId("login-submit")).toBeVisible();
		await expect(page.getByRole("link", { name: /Forgot password/i })).toBeVisible();
	});

	test("login shows error on invalid credentials", async ({ page }) => {
		await page.goto("/login");

		await page.getByTestId("login-email").fill("notreal@example.com");
		await page.getByTestId("login-password").fill("wrongpassword");
		await page.getByTestId("login-submit").click();

		await expect(page.getByTestId("error-message")).toBeVisible({ timeout: 5000 });
	});

	test("register page renders form elements", async ({ page }) => {
		await page.goto("/register");

		await expect(page.getByTestId("register-form")).toBeVisible();
		await expect(page.getByTestId("register-email")).toBeVisible();
		await expect(page.getByTestId("register-password")).toBeVisible();
		await expect(page.getByTestId("register-confirm-password")).toBeVisible();
		await expect(page.getByTestId("register-submit")).toBeVisible();
	});

	test("forgot password page renders form", async ({ page }) => {
		await page.goto("/forgot-password");

		await expect(page.getByTestId("forgot-password-form")).toBeVisible();
		await expect(page.getByRole("button", { name: /Send reset link/i })).toBeVisible();
	});

	test("password toggle reveals input text", async ({ page }) => {
		await page.goto("/login");

		const passwordInput = page.getByTestId("login-password");
		await expect(passwordInput).toHaveAttribute("type", "password");

		await page.getByTestId("password-toggle").first().click();
		await expect(passwordInput).toHaveAttribute("type", "text");
	});
});
