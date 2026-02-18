import { test as base, type Page } from "@playwright/test";

type AuthFixtures = {
	authenticatedPage: Page;
};

export const test = base.extend<AuthFixtures>({
	authenticatedPage: async ({ page }, use) => {
		const email = `test-${Date.now()}@example.com`;
		const password = "TestPassword123!";

		// Create test user via BetterAuth signup endpoint
		const signupRes = await page.request.post("/api/auth/sign-up/email", {
			data: { email, password, name: "Test User" },
		});

		if (!signupRes.ok()) {
			// User may already exist; try login directly
		}

		// Sign in
		const loginRes = await page.request.post("/api/auth/sign-in/email", {
			data: { email, password },
		});

		if (!loginRes.ok()) {
			const body = await loginRes.text();
			throw new Error(`Auth fixture: sign-in failed (${loginRes.status()}): ${body}`);
		}

		// Cookies are auto-stored by the page context
		await page.goto("/");
		await use(page);
	},
});

export { expect } from "@playwright/test";
