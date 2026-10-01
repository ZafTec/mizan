import { expect, test, type BrowserContext, type Locator, type Page } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

async function signIn(context: BrowserContext, options: { empty?: boolean } = {}) {
	const result = await context.request.post(`${apiURL}/__fixture/session`, { data: options });
	expect(result.ok()).toBeTruthy();
}
async function fixtureState(context: BrowserContext) {
	return (await context.request.get(`${apiURL}/__fixture/state`)).json() as Promise<{ writes: { path: string; body: Record<string, unknown> }[]; connections: unknown[] }>;
}
async function noOverflow(page: Page) {
	expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}
/** People click the label, because the radio itself is visually hidden. */
async function choose(scope: Page | Locator, group: string, option: string) {
	await scope.getByRole("radiogroup", { name: group }).locator("label").filter({ hasText: new RegExp(`^${option}$`) }).click();
}
/** The app's own address does not exist here. Answer it so the browser has somewhere to land. */
async function catchAppRedirect(page: Page) {
	await page.route("http://localhost:8123/**", (route) => route.fulfill({ status: 200, body: "app" }));
}

test.describe("Connected apps", () => {
	test("lists each app with what it may do and where", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/profile/mcp");

		await expect(page.getByRole("heading", { name: "Connected apps", level: 1 })).toBeVisible();
		const claude = page.getByRole("listitem").filter({ hasText: "Claude" }).first();
		await expect(claude).toContainText("claude.ai");
		await expect(claude).toContainText("Food and nutrition: read and change");
		await expect(claude).toContainText("Training: read");
		await expect(claude).toContainText("Mizan assistant");
		await expect(claude).toContainText("Home");
		await expect(claude).toContainText("42 calls in 30 days");

		const gemini = page.getByRole("listitem").filter({ hasText: "Gemini CLI" });
		await expect(gemini).toContainText("Unverified");
		await expect(gemini).toContainText("Personal data only");
		await expect(gemini).toContainText("Never");
		await noOverflow(page);
	});

	test("access can be reduced but never raised", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/profile/mcp");
		await page.getByRole("listitem").filter({ hasText: "Claude" }).getByRole("button", { name: "Edit access" }).click();

		const dialog = page.getByRole("dialog");
		await expect(dialog.getByRole("heading", { name: "Edit access for Claude" })).toBeVisible();

		// Training is held at read, so "Read and change" cannot be chosen.
		const training = dialog.getByRole("radiogroup", { name: "Training" });
		await expect(training.getByLabel("Read and change")).toBeDisabled();
		// Planning is not held at all, so nothing in it can be chosen.
		await expect(dialog.getByRole("radiogroup", { name: "Meal plans and shopping" }).getByLabel("Read", { exact: true })).toBeDisabled();

		await choose(dialog, "Food and nutrition", "Read");
		await choose(dialog, "Mizan assistant", "No access");
		await dialog.getByRole("button", { name: "Save" }).click();

		await expect(page.getByRole("listitem").filter({ hasText: "Claude" })).toContainText("Food and nutrition: read");
		const { writes } = await fixtureState(context);
		expect(writes.at(-1)).toMatchObject({
			path: expect.stringContaining("/api/McpConnections/c0000000-0000-4000-8000-000000000001"),
			body: { scopes: ["nutrition:read", "training:read"] },
		});
	});

	test("an app cannot be left with nothing", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/profile/mcp");
		await page.getByRole("listitem").filter({ hasText: "Gemini CLI" }).getByRole("button", { name: "Edit access" }).click();
		const dialog = page.getByRole("dialog");
		await choose(dialog, "Food and nutrition", "No access");
		await expect(dialog.getByRole("button", { name: "Keep at least one permission" })).toBeDisabled();
	});

	test("disconnecting asks first, then removes the app", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/profile/mcp");
		await page.getByRole("listitem").filter({ hasText: "Gemini CLI" }).getByRole("button", { name: "Disconnect" }).click();
		await expect(page.getByText("It loses access on its next request.")).toBeVisible();
		await page.locator(".modal-pop-in").getByRole("button", { name: "Disconnect" }).click();

		await expect(page.getByRole("listitem").filter({ hasText: "Gemini CLI" })).toHaveCount(0);
		expect((await fixtureState(context)).connections).toHaveLength(1);
	});

	test("an empty account points to how to add an app", async ({ context, page }) => {
		await signIn(context, { empty: true });
		await page.goto("/profile/mcp");
		await expect(page.getByText("No apps connected yet")).toBeVisible();
		await page.getByRole("tab", { name: "Add an app" }).click();
		await expect(page.getByText("There is no key to copy")).toBeVisible();
		await expect(page.locator("code").first()).toContainText("/mcp");

		await page.getByRole("tab", { name: "Claude Code" }).click();
		await expect(page.getByText("claude mcp add --transport http mizan")).toBeVisible();
	});

	test("usage shows calls by app and tool", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/profile/mcp");
		await page.getByRole("tab", { name: "Usage" }).click();

		await expect(page.getByText("95.2% succeeded")).toBeVisible();
		await expect(page.getByRole("heading", { name: "By app" })).toBeVisible();
		await expect(page.getByText("log_food")).toBeVisible();
		await noOverflow(page);
	});

	test("fits a phone and works from the keyboard", async ({ context, page }) => {
		await page.setViewportSize({ width: 375, height: 800 });
		await signIn(context);
		await page.goto("/profile/mcp");
		await noOverflow(page);

		await page.getByRole("listitem").filter({ hasText: "Claude" }).getByRole("button", { name: "Edit access" }).focus();
		await page.keyboard.press("Enter");
		const food = page.getByRole("radiogroup", { name: "Food and nutrition" });
		await food.getByLabel("Read and change").focus();
		await page.keyboard.press("ArrowLeft");
		await expect(food.getByLabel("Read", { exact: true })).toBeChecked();
		await page.keyboard.press("Escape");
		await expect(page.getByRole("dialog")).toHaveCount(0);
	});
});

test.describe("Consent screen", () => {
	test("sends a signed-out visitor to sign in and back", async ({ page }) => {
		await page.goto("/oauth/consent?request=fixture-request");
		await expect(page).toHaveURL(/\/login\?callbackUrl=%2Foauth%2Fconsent%3Frequest%3Dfixture-request/);
	});

	test("shows who is asking, and says when it cannot vouch for them", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/oauth/consent?request=fixture-request");

		await expect(page.getByRole("heading", { name: "Claude wants to connect to Mizan" })).toBeVisible();
		await expect(page.getByText("Mizan cannot confirm who made it")).toBeVisible();
		await expect(page.getByText("localhost", { exact: true })).toBeVisible();
		await noOverflow(page);
	});

	test("nothing is granted until the person chooses, and what they choose is what is sent", async ({ context, page }) => {
		await signIn(context);
		await catchAppRedirect(page);
		await page.goto("/oauth/consent?request=fixture-request");

		await choose(page, "Food and nutrition", "Read and change");
		await choose(page, "Training", "No access");
		await choose(page, "Mizan assistant", "Allowed");
		await page.getByLabel("Only households I choose").check();
		await page.getByLabel("Home", { exact: true }).check();
		await page.getByRole("button", { name: "Allow" }).click();

		await expect(page).toHaveURL(/localhost:8123\/callback\?code=fixture-code&state=s1/);
		const { writes } = await fixtureState(context);
		expect(writes.at(-1)).toMatchObject({
			path: "/api/oauth/authorization-requests/decision",
			body: {
				request: "fixture-request", approve: true,
				scopes: ["nutrition:read", "nutrition:write", "planning:read", "ai:use"],
				householdMode: "selected",
			},
		});
	});

	test("Allow is off until something is chosen", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/oauth/consent?request=fixture-request");

		for (const group of ["Food and nutrition", "Training", "Meal plans and shopping"]) {
			await choose(page, group, "No access");
		}
		await expect(page.getByRole("button", { name: "Allow" })).toBeDisabled();
	});

	test("an app can only be given what it asked for", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/oauth/consent?request=asks-for-little");

		await expect(page.getByRole("radiogroup", { name: "Food and nutrition" }).getByLabel("Read", { exact: true })).toBeChecked();
		await expect(page.getByRole("radiogroup", { name: "Food and nutrition" }).getByLabel("Read and change")).toBeDisabled();
		await expect(page.getByRole("radiogroup", { name: "Training" }).getByLabel("Read", { exact: true })).toBeDisabled();
	});

	test("denying goes back to the app with an error", async ({ context, page }) => {
		await signIn(context);
		await catchAppRedirect(page);
		await page.goto("/oauth/consent?request=fixture-request");
		await page.getByRole("button", { name: "Deny" }).click();

		await expect(page).toHaveURL(/localhost:8123\/callback\?error=access_denied/);
	});

	test("a Mizan app gets one plain question", async ({ context, page }) => {
		await signIn(context);
		await catchAppRedirect(page);
		await page.goto("/oauth/consent?request=first-party");

		await expect(page.getByText("A Mizan app")).toBeVisible();
		await expect(page.getByRole("radiogroup")).toHaveCount(0);
		await page.getByRole("button", { name: "Allow" }).click();
		await expect(page).toHaveURL(/code=fixture-code/);
		expect((await fixtureState(context)).writes.at(-1)).toMatchObject({ body: { scopes: ["full"], householdMode: "none" } });
	});

	test("an expired request says so and offers a way out", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/oauth/consent?request=old");

		await expect(page.locator("p[role=alert]")).toContainText("expired");
		await expect(page.getByRole("link", { name: "Go to connected apps" })).toBeVisible();
	});

	test("fits a phone", async ({ context, page }) => {
		await page.setViewportSize({ width: 375, height: 800 });
		await signIn(context);
		await page.goto("/oauth/consent?request=fixture-request");
		await expect(page.getByRole("heading", { name: /wants to connect/ })).toBeVisible();
		await noOverflow(page);
		await expect(page.getByRole("button", { name: "Allow" })).toBeVisible();
	});
});
