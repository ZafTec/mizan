import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";
async function signIn(context: BrowserContext, options: { userId?: string; empty?: boolean; unread?: number; failures?: string[]; writeFailures?: Record<string, number> } = {}) {
	const result = await context.request.post(`${apiURL}/__fixture/session`, { data: options });
	expect(result.ok()).toBeTruthy();
	return await result.json() as { today: string };
}
async function noOverflow(page: Page) {
	expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}
async function fixtureState(context: BrowserContext) {
	return (await context.request.get(`${apiURL}/__fixture/state`)).json();
}

test("signed-out spine pages require sign-in", async ({ page }) => {
	await page.goto("/today");
	await expect(page).toHaveURL(/\/login/);
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
});

test("More keeps secondary features accessible", async ({ context, page }) => {
	await signIn(context);
	await page.goto("/more");
	await expect(page.getByRole("heading", { name: "More", exact: true })).toBeVisible();
	await expect(page.getByRole("link", { name: /Recipes/ }).first()).toBeVisible();
	await expect(page.getByRole("link", { name: /Billing/ }).first()).toBeVisible();
	await page.getByRole("searchbox", { name: "Search features" }).fill("billing");
	await expect(page.getByRole("link", { name: /Billing/ }).first()).toBeVisible();
	await expect(page.getByRole("link", { name: /Recipes/ })).toHaveCount(0);
	await page.getByRole("searchbox", { name: "Search features" }).fill("nonexistent feature");
	await expect(page.getByRole("heading", { name: "No matching features" })).toBeVisible();
	await page.getByRole("button", { name: "Clear search", exact: true }).last().click();
	await expect(page.getByRole("link", { name: /Recipes/ }).first()).toBeVisible();
	await noOverflow(page);
});

test("Today presents all three logs and the four spine destinations", async ({ context, page }, testInfo) => {
	await signIn(context);
	const errors: string[] = [];
	page.on("pageerror", (error) => errors.push(error.message));
	await page.goto("/today");
	for (const name of ["Today", "Meals", "Training", "Measurements"]) await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
	await expect(page.getByText("Greek yogurt", { exact: true })).toBeVisible();
	await expect(page.getByText("Upper body", { exact: true })).toBeVisible();
	await expect(page.getByRole("meter", { name: "Calories", exact: true })).toHaveAttribute("aria-valuenow", "389");
	for (const route of ["today", "history", "progress", "more"]) await expect(page.locator(`nav a[href='/${route}']:visible`).first()).toBeVisible();
	await expect(page.getByRole("link", { name: /notifications/i })).toHaveCount(0);
	await noOverflow(page);
	expect(errors).toEqual([]);
	expect((await fixtureState(context)).unhandled).toEqual([]);
	await page.screenshot({ path: testInfo.outputPath("today-desktop.png"), fullPage: true });
});

test("History opens a dated log and logging preserves that date", async ({ context, page }) => {
	await signIn(context);
	await page.goto("/history");
	await expect(page.getByRole("heading", { name: "History", exact: true })).toBeVisible();
	await page.getByRole("link", { name: /View log for/ }).nth(1).click();
	await expect(page).toHaveURL(/\/today\?date=/);
	const selectedDate = new URL(page.url()).searchParams.get("date");
	expect(selectedDate).toMatch(/^\d{4}-\d{2}-\d{2}$/);
	await expect(page.getByText("Chicken and rice", { exact: true })).toBeVisible();
	await page.getByRole("button", { name: "Add measurement", exact: true }).click();
	const dialog = page.getByRole("dialog", { name: "Log a measurement" });
	await expect(dialog.getByLabel("Date", { exact: true })).toHaveValue(selectedDate!);
	await dialog.getByLabel("Weight (kg)").fill("73.8");
	await dialog.getByRole("button", { name: "Log measurement", exact: true }).click();
	await expect(dialog).not.toBeVisible();
	await expect(page).toHaveURL(new RegExp(`/today\\?date=${selectedDate}`));
	await expect(page.getByText("73.8", { exact: false }).first()).toBeVisible();
	expect((await fixtureState(context)).writes.at(-1)).toMatchObject({ path: "/api/BodyMeasurements", body: { date: selectedDate, weightKg: 73.8 } });
});

test("a meal can be composed and saved over the current page", async ({ context, page }) => {
	const { today } = await signIn(context);
	await page.goto("/today");
	await page.getByRole("button", { name: "Add meal", exact: true }).click();
	const dialog = page.getByRole("dialog", { name: "Log a meal", exact: true });
	await dialog.getByLabel("Meal", { exact: true }).selectOption("LUNCH");
	await dialog.getByRole("button", { name: "Add Greek yogurt", exact: true }).click();
	await dialog.getByLabel("Greek yogurt quantity in grams").fill("150");
	await dialog.getByRole("button", { name: "Add Yogurt and oats", exact: true }).click();
	await dialog.getByLabel("Yogurt and oats quantity in servings").fill("0.5");
	await dialog.getByRole("button", { name: "Log meal", exact: true }).click();
	await expect(dialog).not.toBeVisible();
	await expect(page).toHaveURL(/\/today$/);
	await expect(page.getByRole("heading", { name: "Lunch", exact: true })).toBeVisible();
	await page.reload();
	await expect(page.getByText("Yogurt and oats", { exact: true })).toBeVisible();
	const state = await fixtureState(context);
	expect(state.writes).toHaveLength(2);
	expect(state.writes[0]).toMatchObject({ path: "/api/Nutrition/log", body: { servings: 1.5, mealType: "LUNCH", entryDate: today } });
	expect(state.writes[1]).toMatchObject({ path: "/api/Nutrition/log", body: { servings: 0.5, mealType: "LUNCH", entryDate: today } });
	expect(state.unhandled).toEqual([]);
});

test("a multi-item meal becomes a reusable recipe", async ({ context, page }) => {
	await signIn(context);
	await page.goto("/today");
	await page.getByRole("button", { name: "Save as recipe", exact: true }).click();
	await page.getByRole("dialog").getByLabel("Recipe name").fill("My morning bowl");
	await page.getByRole("button", { name: "Save recipe", exact: true }).click();
	await expect(page.getByRole("dialog")).not.toBeVisible();
	await page.getByRole("button", { name: "Add meal", exact: true }).click();
	await page.getByLabel("Find food or a recipe").fill("My morning bowl");
	await expect(page.getByRole("button", { name: "Add My morning bowl", exact: true })).toBeVisible();
	expect((await fixtureState(context)).writes.at(-1)).toMatchObject({ path: "/api/Recipes/promote", body: { title: "My morning bowl", mealType: "BREAKFAST" } });
});

test("Progress offers nutrition, bodyweight and exercise trends with readable data", async ({ context, page }, testInfo) => {
	await signIn(context);
	await page.goto("/progress");
	for (const name of ["Progress", "Nutrition", "Bodyweight", "Strength"]) await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
	await expect(page.getByLabel("Exercise", { exact: true })).toHaveValue("exercise-bench");
	await page.getByText("View data", { exact: true }).first().click();
	await expect(page.getByRole("columnheader", { name: "Calories (kcal)" })).toBeVisible();
	await page.getByRole("link", { name: "7 days", exact: true }).click();
	await expect(page).toHaveURL(/days=7/);
	await expect(page.getByRole("link", { name: "7 days", exact: true })).toHaveAttribute("aria-current", "page");
	await noOverflow(page);
	await page.screenshot({ path: testInfo.outputPath("progress-desktop.png"), fullPage: true });
});

test("mobile logging fits the viewport and Escape restores focus", async ({ context, page }, testInfo) => {
	await signIn(context, { empty: true });
	await page.setViewportSize({ width: 390, height: 844 });
	await page.goto("/today");
	await expect(page.getByText("Your meals go here.")).toBeVisible();
	const trigger = page.getByRole("button", { name: "Log an entry", exact: true });
	await trigger.click();
	await expect(page.getByRole("dialog")).toBeVisible();
	await page.getByRole("button", { name: /^Measurement Weight/ }).click();
	const dialog = page.getByRole("dialog", { name: "Log a measurement" });
	await noOverflow(page);
	const box = await dialog.boundingBox();
	expect(box!.width).toBeLessThanOrEqual(390);
	await page.screenshot({ path: testInfo.outputPath("measurement-mobile.png"), fullPage: true });
	await page.keyboard.press("Escape");
	await expect(dialog).not.toBeVisible();
	await expect(trigger).toBeFocused();
	await noOverflow(page);
});

test("an unavailable log displays a retry state", async ({ context, page }) => {
	await signIn(context, { failures: ["/api/Meals"] });
	await page.goto("/today");
	await expect(page.getByRole("heading", { name: "Something went wrong" })).toBeVisible();
	await expect(page.getByRole("button", { name: /Try again/i })).toBeVisible();
	await expect(page.getByText("Your meals go here.")).toHaveCount(0);
});

test("a workout draft survives a reload", async ({ context, page }) => {
	await signIn(context, { empty: true });
	await page.goto("/workout/active");
	await page.getByRole("button", { name: /Empty workout/ }).click();
	await page.getByLabel("Workout name").fill("Evening session");
	await page.getByPlaceholder("Search exercises").fill("Bench");
	await page.getByRole("button", { name: "Search", exact: true }).click();
	await page.getByRole("button", { name: /Bench press.*Add/ }).click();
	await expect(page.getByText("Session saved", { exact: true })).toBeVisible();
	await page.reload();
	await page.getByRole("button", { name: "Resume", exact: true }).click();
	await expect(page.getByLabel("Workout name")).toHaveValue("Evening session");
	await expect(page.getByRole("heading", { name: "Bench press", exact: true })).toBeVisible();
	expect((await fixtureState(context)).draft).not.toBeNull();
	await page.getByLabel("Weight (kg) for set 1", { exact: true }).fill("60");
	await page.getByRole("button", { name: "Cycle set 1", exact: true }).click();
	await page.getByRole("button", { name: "Finish workout", exact: true }).click();
	await expect(page.getByText("Workout finished", { exact: true })).toBeVisible();
	const state = await fixtureState(context);
	expect(state.draft).toBeNull();
	expect(state.workouts[0]).toMatchObject({ name: "Evening session", exercises: [{ sets: [{ weightKg: 60, completed: true }, {}, {}] }] });
});

test("a custom meal accepts entered nutrition without leaving More", async ({ context, page }) => {
	await signIn(context, { empty: true });
	await page.goto("/more");
	await page.getByRole("button", { name: "Log an entry", exact: true }).click();
	await page.getByRole("button", { name: /^Meal Foods/ }).click();
	await page.getByRole("button", { name: "Enter a food manually" }).click();
	await page.getByLabel("Food name", { exact: true }).fill("Homemade smoothie");
	for (const [name, value] of [["Calories (kcal)", "310"], ["Protein (g)", "24"], ["Carbs (g)", "40"], ["Fat (g)", "6"]]) await page.getByLabel(name, { exact: true }).fill(value);
	await page.getByRole("button", { name: "Add to meal", exact: true }).click();
	await page.getByRole("button", { name: "Log meal", exact: true }).click();
	await expect(page.getByRole("dialog")).not.toBeVisible();
	await expect(page).toHaveURL(/\/more$/);
	await page.goto("/today");
	await expect(page.getByText("Homemade smoothie", { exact: true })).toBeVisible();
	expect((await fixtureState(context)).writes.at(-1)).toMatchObject({ path: "/api/Meals", body: { name: "Homemade smoothie", calories: 310, proteinGrams: 24, servings: 1 } });
});

test("failed measurement writes keep entered values for a successful retry", async ({ context, page }) => {
	await signIn(context, { empty: true, writeFailures: { "/api/BodyMeasurements": 1 } });
	await page.goto("/today");
	await page.getByRole("button", { name: "Add measurement", exact: true }).click();
	const dialog = page.getByRole("dialog", { name: "Log a measurement" });
	await dialog.getByLabel("Weight (kg)").fill("75.1");
	await dialog.getByRole("button", { name: "Log measurement", exact: true }).click();
	await expect(dialog.getByRole("alert")).toContainText("Please try again");
	await expect(dialog.getByLabel("Weight (kg)")).toHaveValue("75.1");
	await dialog.getByRole("button", { name: "Log measurement", exact: true }).click();
	await expect(dialog).not.toBeVisible();
	await expect(page.getByText("75.1", { exact: false }).first()).toBeVisible();
	expect((await fixtureState(context)).writes).toHaveLength(1);
});

test("delete requires confirmation and updates the day", async ({ context, page }) => {
	await signIn(context);
	await page.goto("/today");
	await page.getByRole("button", { name: "Delete Rolled oats", exact: true }).click();
	await page.getByRole("button", { name: "Keep entry", exact: true }).click();
	await expect(page.getByText("Rolled oats", { exact: true })).toBeVisible();
	await page.getByRole("button", { name: "Delete Rolled oats", exact: true }).click();
	await page.getByRole("button", { name: "Delete entry", exact: true }).click();
	await expect(page.getByText("Rolled oats", { exact: true })).toHaveCount(0);
	await expect(page.getByRole("button", { name: "Save as recipe", exact: true })).toHaveCount(0);
	await expect(page.getByRole("meter", { name: "Calories", exact: true })).toHaveAttribute("aria-valuenow", "194");
});

test("recipe detail preselects the recipe and saves quantities inline", async ({ context, page }) => {
	await signIn(context);
	const route = "/recipes/33333333-3333-4333-8333-333333333333";
	await page.goto(route);
	await expect(page.getByRole("heading", { name: "Yogurt and oats", exact: true })).toBeVisible();
	await page.getByRole("button", { name: "Adjust quantities" }).click();
	await page.getByLabel("Greek yogurt", { exact: true }).fill("250");
	await page.getByRole("button", { name: "Save quantities", exact: true }).click();
	await expect(page.getByText("250 g", { exact: true })).toBeVisible();
	await page.getByRole("button", { name: "Log this recipe", exact: true }).first().click();
	const dialog = page.getByRole("dialog", { name: "Log a meal", exact: true });
	await expect(dialog.getByLabel("Yogurt and oats quantity in servings")).toHaveValue("1");
	await dialog.getByRole("button", { name: "Log meal", exact: true }).click();
	await expect(dialog).not.toBeVisible();
	await expect(page).toHaveURL(route);
	await page.getByRole("button", { name: "Use as ingredient", exact: true }).click();
	await page.getByLabel("Finished batch weight (g)").fill("300");
	await page.getByRole("button", { name: "Save ingredient", exact: true }).click();
	await expect(page.getByText("Ingredient saved", { exact: true }).first()).toBeVisible();
	const state = await fixtureState(context);
	expect(state.writes[0]).toMatchObject({ path: "/api/Recipes/33333333-3333-4333-8333-333333333333", body: { ingredients: [{ amount: 250 }, { amount: 50 }] } });
	expect(state.writes.at(-1)).toMatchObject({ body: { yieldGrams: 300 } });
	expect(state.unhandled).toEqual([]);
});

test("historical repeat and template workouts retain the selected date", async ({ context, page }) => {
	const { today } = await signIn(context);
	const date = new Date(`${today}T12:00:00Z`);
	date.setUTCDate(date.getUTCDate() - 3);
	const selectedDate = date.toISOString().slice(0, 10);
	await page.goto(`/workout/active?date=${selectedDate}`);
	await page.getByRole("button", { name: /Repeat last/ }).click();
	await expect(page.getByLabel("Date", { exact: true })).toHaveValue(selectedDate);
	await expect(page.getByRole("heading", { name: "Bench press", exact: true })).toBeVisible();
	await signIn(context, { userId: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb" });
	await page.goto(`/workout/active?date=${selectedDate}`);
	await page.getByRole("button", { name: /Simple strength/ }).click();
	await expect(page.getByLabel("Date", { exact: true })).toHaveValue(selectedDate);
	await expect(page.getByLabel("Workout name")).toHaveValue("Simple strength");
});

test("a shared browser does not offer another account's workout draft", async ({ context, page }) => {
	await signIn(context, { empty: true });
	await page.goto("/workout/active");
	await page.getByRole("button", { name: /Empty workout/ }).click();
	await page.getByLabel("Workout name").fill("Private session for Alex");
	await expect(page.getByText("Session saved", { exact: true })).toBeVisible();
	await page.evaluate(() => localStorage.setItem("mizan-workout-draft", localStorage.getItem("mizan-workout-draft:aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")!));
	await signIn(context, { empty: true, userId: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb" });
	const resolvedDraft = page.waitForResponse((response) => response.url().includes("/api/Workouts/draft"));
	await page.goto("/workout/active");
	await resolvedDraft;
	await expect(page.getByRole("button", { name: /Empty workout/ })).toBeVisible();
	await expect(page.getByRole("heading", { name: "Resume workout?" })).toHaveCount(0);
	await expect(page.getByText("Private session for Alex", { exact: true })).toHaveCount(0);
});
