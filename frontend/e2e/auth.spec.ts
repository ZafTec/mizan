import { test, expect } from "@playwright/test";
import { test as authTest } from "./fixtures/auth.fixture";

test.describe("Auth forms (unauthenticated)", () => {
	test("login form renders all fields", async ({ page }) => {
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

		await expect(page.getByTestId("error-message")).toBeVisible({ timeout: 8000 });
	});

	test("register form renders all fields", async ({ page }) => {
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
		await expect(page.getByRole("button", { name: /Send/i })).toBeVisible();
	});

	test("password toggle reveals input text", async ({ page }) => {
		await page.goto("/login");

		const passwordInput = page.getByTestId("login-password");
		await expect(passwordInput).toHaveAttribute("type", "password");

		await page.getByTestId("password-toggle").first().click();
		await expect(passwordInput).toHaveAttribute("type", "text");
	});

	test("register shows error when passwords do not match", async ({ page }) => {
		await page.goto("/register");

		await page.getByTestId("register-email").fill("test@example.com");
		await page.getByTestId("register-password").fill("Password123!");
		await page.getByTestId("register-confirm-password").fill("Different456!");
		await page.getByRole("checkbox").check();
		await page.getByTestId("register-submit").click();

		await expect(page.getByTestId("error-message")).toBeVisible({ timeout: 8000 });
	});
});

test.describe("Full auth flow", () => {
	test("register → verify → login → logout", async ({ page }) => {
		const email = `e2e-${Date.now()}@example.com`;
		const password = "TestPassword123!";

		// Register
		await page.goto("/register");
		await page.getByTestId("register-email").fill(email);
		await page.getByTestId("register-password").fill(password);
		await page.getByTestId("register-confirm-password").fill(password);
		await page.getByRole("checkbox").check();
		await page.getByTestId("register-submit").click();

		// Wait for success state (redirect to login or success message)
		await page.waitForURL(/\/(login|verifyemail)/, { timeout: 10000 });

		// Verify email via test endpoint
		const verifyRes = await page.request.post("/api/test/verify-user", {
			data: { email },
		});
		expect(verifyRes.ok()).toBeTruthy();

		// Sign in via login form
		await page.goto("/login");
		await page.getByTestId("login-email").fill(email);
		await page.getByTestId("login-password").fill(password);
		await page.getByTestId("login-submit").click();

		// Should be redirected to authenticated area
		await page.waitForURL(/^(?!.*\/login).*$/, { timeout: 10000 });
		await expect(page).not.toHaveURL(/\/login/);

		// Sign out
		const userMenuTrigger = page.getByTestId("nav-user-menu");
		if (await userMenuTrigger.isVisible()) {
			await userMenuTrigger.click();
			const signOutButton = page.getByRole("button", { name: /sign out|log out/i });
			if (await signOutButton.isVisible()) {
				await signOutButton.click();
				await page.waitForURL(/\/(login|)$/, { timeout: 8000 });
			}
		}
	});
});

authTest.describe("Authenticated auth actions", () => {
	authTest("authenticated user is redirected away from login", async ({ authenticatedPage: page }) => {
		await page.goto("/login");
		await expect(page).not.toHaveURL(/\/login/);
	});

	authTest("authenticated user is redirected away from register", async ({ authenticatedPage: page }) => {
		await page.goto("/register");
		await expect(page).not.toHaveURL(/\/register/);
	});
});
