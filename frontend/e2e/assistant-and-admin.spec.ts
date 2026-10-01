import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type Options = { empty?: boolean; role?: "admin" | "user"; pro?: boolean };
type State = { writes: { path: string; body: Record<string, unknown> }[]; impersonating: boolean };

async function signIn(context: BrowserContext, options: Options = {}) {
	const result = await context.request.post(`${apiURL}/__fixture/session`, { data: options });
	expect(result.ok()).toBeTruthy();
}
async function state(context: BrowserContext) {
	return (await context.request.get(`${apiURL}/__fixture/state`)).json() as Promise<State>;
}
async function noOverflow(page: Page) {
	expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}
const writesTo = (s: State, path: string) => s.writes.filter((w) => w.path === path);

async function openMealLog(page: Page) {
	await page.goto("/today");
	await page.getByRole("button", { name: "Log an entry" }).first().click();
	await page.getByRole("button", { name: /^Meal/ }).click();
	await expect(page.getByRole("region", { name: "Describe your meal" })).toBeVisible();
}

test.describe("logging a meal with the assistant", () => {
	test("a sentence becomes a meal to check, and only what is confirmed is saved", async ({ context, page }) => {
		await signIn(context, { pro: true });
		await openMealLog(page);

		await page.getByLabel("Tell me what you ate").fill("two eggs and toast with butter");
		await page.getByRole("button", { name: "Estimate meal" }).click();

		const meal = page.getByRole("region", { name: "Items in this meal" });
		await expect(meal.locator(`input[value="Scrambled eggs"]`)).toBeVisible();
		await expect(meal.locator(`input[value="Toast with butter"]`)).toBeVisible();
		await expect(meal).toContainText("70% confidence");
		expect(writesTo(await state(context), "/api/Meals")).toHaveLength(0);

		// Eat half the eggs, drop the toast.
		await meal.getByLabel("Scrambled eggs quantity in grams").fill("60");
		await meal.getByRole("button", { name: "Remove Toast with butter" }).click();
		await page.getByRole("button", { name: "Log meal" }).click();

		await expect(page.getByRole("dialog")).toBeHidden();
		const saved = writesTo(await state(context), "/api/Meals");
		expect(saved).toHaveLength(1);
		expect(saved[0].body).toMatchObject({ name: "Scrambled eggs", amountGrams: 60, calories: 90, proteinGrams: 6, mealType: expect.any(String) });
	});

	test("a free account is walled when it tries, and can still search", async ({ context, page }) => {
		await signIn(context, { pro: false });
		await openMealLog(page);

		await page.getByLabel("Tell me what you ate").fill("a bowl of oats");
		await page.getByRole("button", { name: "Estimate meal" }).click();

		await expect(page.getByRole("note")).toContainText("Log a meal in plain words");
		expect(writesTo(await state(context), "/api/Nutrition/ai/analyze-text")).toHaveLength(0);
		await page.getByRole("button", { name: "Not now" }).click();

		await page.getByRole("button", { name: "Search foods" }).click();
		await page.getByRole("button", { name: "Add Greek yogurt" }).click();
		await expect(page.getByRole("region", { name: "Items in this meal" })).toContainText("Greek yogurt");
	});

	test("recipes open in their own view, most useful first, and return to the same meal", async ({ context, page }) => {
		await signIn(context, { pro: true });
		await openMealLog(page);
		await page.getByRole("button", { name: "Search foods" }).click();
		await page.getByRole("button", { name: "Add Greek yogurt" }).click();

		await page.getByRole("button", { name: "Log a recipe" }).click();

		const view = page.getByRole("region", { name: "Log from a recipe" });
		await expect(view.getByRole("group", { name: "Favorites" })).toContainText("Yogurt and oats");
		await expect(view.getByRole("group", { name: "All recipes" })).toContainText("Imported lentil stew");
		await view.getByRole("button", { name: "Add Yogurt and oats" }).click();

		await expect(view).toBeHidden();
		await expect(page.getByRole("status").filter({ hasText: "Added Yogurt and oats" })).toBeVisible();
		const meal = page.getByRole("region", { name: "Items in this meal" });
		await expect(meal).toContainText("Greek yogurt");
		await expect(meal).toContainText("Yogurt and oats");
		await page.getByRole("button", { name: "Log meal" }).click();
		await expect(page.getByRole("dialog")).toBeHidden();
		expect(writesTo(await state(context), "/api/Nutrition/log")).toHaveLength(2);
	});

	test("the sheet works on a narrow screen with the keyboard", async ({ context, page }) => {
		await page.setViewportSize({ width: 360, height: 640 });
		await signIn(context, { pro: true });
		await openMealLog(page);

		await noOverflow(page);
		await page.getByLabel("Tell me what you ate").fill("oats");
		await page.keyboard.press("Control+Enter");
		await expect(page.getByRole("region", { name: "Items in this meal" })).toBeVisible();
		await expect(page.getByRole("button", { name: "Log meal" })).toBeInViewport();
		await noOverflow(page);
	});
});

test.describe("the assistant conversation", () => {
	test("a past photo opens full size and closes with Escape", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/ai");

		await page.getByRole("complementary", { name: "Conversations" }).getByRole("button", { name: "Protein ideas", exact: true }).click();
		await expect(page.getByText("You averaged")).toBeVisible();
		await page.getByRole("button", { name: "Open photo" }).click();

		const dialog = page.getByRole("dialog");
		await expect(dialog.getByRole("img", { name: "Photo you sent" })).toBeVisible();
		await page.keyboard.press("Escape");
		await expect(dialog).toBeHidden();
	});

	test("a message gets an answer, and a refused one gives the words back", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/ai");
		const box = page.getByLabel("Message the assistant");

		await box.fill("how was my week");
		await box.press("Enter");
		await expect(page.getByText("You asked: how was my week")).toBeVisible();

		await box.fill("fail please");
		await box.press("Enter");
		await expect(page.locator("p[role=alert]")).toContainText("allowance");
		await expect(box).toHaveValue("fail please");
	});

	test("a photo is previewed before it is sent, then sent", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/ai");

		await page.getByLabel("Choose a photo").setInputFiles({ name: "lunch.png", mimeType: "image/png", buffer: Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==", "base64") });
		await expect(page.getByText("lunch.png")).toBeVisible();
		await page.getByRole("button", { name: "Send", exact: true }).click();

		await expect(page.getByText("rice and chicken")).toBeVisible();
		await expect(page.getByRole("button", { name: "Open photo" })).toBeVisible();
	});

	test("on a phone the list is behind a button, the composer stays in view, and nothing overflows", async ({ context, page }) => {
		await page.setViewportSize({ width: 375, height: 700 });
		await signIn(context);
		await page.goto("/ai");

		await expect(page.getByRole("complementary", { name: "Conversations" })).toBeHidden();
		await expect(page.getByLabel("Message the assistant")).toBeInViewport();
		await noOverflow(page);

		await page.getByRole("button", { name: "Conversations" }).click();
		await page.getByRole("dialog").getByRole("button", { name: "Protein ideas", exact: true }).click();
		await expect(page.getByText("You averaged")).toBeVisible();
		await expect(page.getByRole("dialog")).toBeHidden();
		await noOverflow(page);
		await page.screenshot({ path: "test-results/ai-phone.png" });
	});

	test("deleting a conversation asks first", async ({ context, page }) => {
		await signIn(context);
		await page.goto("/ai");

		await page.getByRole("button", { name: "Delete Protein ideas" }).click();
		await page.locator(".modal-pop-in").getByRole("button", { name: "Cancel" }).click();
		expect(writesTo(await state(context), "/api/Ai/threads/thread-1")).toHaveLength(0);

		await page.getByRole("button", { name: "Delete Protein ideas" }).click();
		await page.locator(".modal-pop-in").getByRole("button", { name: "Delete" }).click();
		await expect(page.getByRole("button", { name: "Delete Protein ideas" })).toHaveCount(0);
	});
});

test.describe("administrators", () => {
	test("an exercise gets a picture and a 3D figure from the edit form", async ({ context, page }) => {
		await signIn(context, { role: "admin" });
		await page.goto("/admin/exercises");

		await page.getByRole("button", { name: "Edit Barbell row" }).click();
		const form = page.getByRole("form", { name: "Edit Barbell row" });
		await form.getByLabel("Name").fill("Barbell row (strict)");

		await form.getByLabel("3D figure file").setInputFiles({ name: "notes.txt", mimeType: "text/plain", buffer: Buffer.from("no") });
		await expect(form.getByRole("alert")).toContainText(".glb");

		await form.getByLabel("3D figure file").setInputFiles({ name: "row.glb", mimeType: "model/gltf-binary", buffer: Buffer.from([0x67, 0x6c, 0x54, 0x46, 2, 0, 0, 0, 12, 0, 0, 0]) });
		await expect(form.getByTestId("model-state")).toHaveText("row.glb");
		await form.getByRole("button", { name: "Save" }).click();

		await expect(form).toBeHidden();
		const s = await state(context);
		expect(writesTo(s, "/api/Uploads/model")).toHaveLength(1);
		expect(writesTo(s, "/api/Exercises/exercise-row")[0].body).toMatchObject({ name: "Barbell row (strict)" });
		expect(writesTo(s, "/api/Exercises/exercise-row/model")[0].body).toEqual({ modelUrl: expect.stringContaining("models/figure.glb") });
		await expect(page.getByRole("row", { name: /Barbell row \(strict\)/ })).toContainText("3D");
	});

	test("a new system exercise is created and promoted in one step", async ({ context, page }) => {
		await signIn(context, { role: "admin" });
		await page.goto("/admin/exercises");

		await page.getByRole("button", { name: "New exercise" }).click();
		const form = page.getByRole("form", { name: "New exercise" });
		await expect(form.getByRole("button", { name: "Create exercise" })).toBeDisabled();
		await form.getByLabel("Name").fill("Goblet squat");
		await form.getByLabel("Muscle group").fill("Legs");
		await form.getByRole("button", { name: "Create exercise" }).click();

		await expect(form).toBeHidden();
		const s = await state(context);
		expect(writesTo(s, "/api/Exercises")[0].body).toMatchObject({ name: "Goblet squat", muscleGroup: "Legs", category: "Strength" });
		expect(s.writes.some((w) => /\/api\/Exercises\/[^/]+\/promote$/.test(w.path))).toBe(true);
		await expect(page.getByRole("row", { name: /Goblet squat/ })).toBeVisible();
	});

	test("viewing as a user shows a banner on every screen, with one way back", async ({ context, page }) => {
		await signIn(context, { role: "admin" });
		await page.goto("/admin/users/cccccccc-cccc-4ccc-8ccc-cccccccccccc");

		await page.getByRole("button", { name: "View the site as this user" }).click();

		await page.waitForURL("**/today");
		const banner = page.getByRole("status").filter({ hasText: "Viewing as" });
		await expect(banner).toContainText("recorded under your name");
		await expect(banner).toContainText("min left");
		await page.goto("/history");
		await expect(banner).toBeVisible();
		expect((await state(context)).impersonating).toBe(true);

		await page.getByRole("button", { name: "Back to admin" }).click();
		await page.waitForURL("**/admin/users");
		expect((await state(context)).impersonating).toBe(false);
	});
});
