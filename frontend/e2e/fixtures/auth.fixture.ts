import { test as base, type Page } from "@playwright/test";

type AuthFixtures = {
    authenticatedPage: Page;
    adminPage: Page;
};

async function createVerifiedUser(page: Page, role = "user"): Promise<{ email: string; password: string }> {
    const email = `test-${Date.now()}-${Math.random().toString(36).slice(2, 7)}@example.com`;
    const password = "TestPassword123!";

    const signupRes = await page.request.post("/api/auth/sign-up/email", {
        data: { email, password, name: "Test User" },
    });

    if (!signupRes.ok() && signupRes.status() !== 422) {
        const body = await signupRes.text();
        throw new Error(`Auth fixture: sign-up failed (${signupRes.status()}): ${body}`);
    }

    const verifyRes = await page.request.post("/api/test/verify-user", {
        data: { email, role },
    });

    if (!verifyRes.ok()) {
        const body = await verifyRes.text();
        throw new Error(`Auth fixture: verify-user failed (${verifyRes.status()}): ${body}`);
    }

    const loginRes = await page.request.post("/api/auth/sign-in/email", {
        data: { email, password },
    });

    if (!loginRes.ok()) {
        const body = await loginRes.text();
        throw new Error(`Auth fixture: sign-in failed (${loginRes.status()}): ${body}`);
    }

    return { email, password };
}

export const test = base.extend<AuthFixtures>({
    authenticatedPage: async ({ page }, use) => {
        await createVerifiedUser(page, "user");
        await page.goto("/");
        await use(page);
    },

    adminPage: async ({ page }, use) => {
        await createVerifiedUser(page, "admin");
        await page.goto("/");
        await use(page);
    },
});

export { expect } from "@playwright/test";
