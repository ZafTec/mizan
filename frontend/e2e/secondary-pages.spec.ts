import { test, expect, type Page } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

async function signIn(page: Page) {
	await page.request.post(`${apiURL}/__fixture/session`, { data: {} });
}

test.describe("the pages under More read like the daily log", () => {
	test.beforeEach(async ({ page }) => {
		await signIn(page);
	});

	test("Body shows the latest values to one decimal, with the change in words", async ({ page }) => {
		await page.goto("/body-measurements");
		const facts = page.getByRole("region", { name: "Latest measurements" });
		await expect(facts).toContainText("74.2");
		await expect(facts).toContainText("kg since last");
		await expect(page.getByTestId("body-measurements-page")).not.toContainText("74.6000000000000");
		await expect(page.getByRole("heading", { name: "History" })).toBeVisible();
		await page.getByRole("button", { name: "Add measurement", exact: true }).click();
		await expect(page.getByRole("dialog", { name: "Log a measurement" })).toBeVisible();
	});

	test("Exercises filter as you type, without a search button", async ({ page }) => {
		await page.goto("/exercises");
		await expect(page.getByRole("button", { name: "Search", exact: true })).toHaveCount(0);
		await expect(page.getByText("Strength · Chest · Barbell")).toBeVisible();
		await page.getByPlaceholder("Search exercises").fill("row");
		await expect(page).toHaveURL(/search=row/);
		await expect(page.getByText("Barbell row")).toBeVisible();
		await expect(page.getByText("Bench press")).toHaveCount(0);
	});

	test("Achievements lead with the streak and list what counts", async ({ page }) => {
		await page.goto("/achievements");
		const facts = page.getByRole("region", { name: "Streak and level" });
		await expect(facts).toContainText("Streak");
		await expect(facts).toContainText("Longest");
		await expect(page.getByRole("heading", { name: "Earned" })).toBeVisible();
	});

	test("Profile is the account, not a second More", async ({ page }) => {
		await page.goto("/profile");
		await expect(page.getByRole("heading", { name: "Profile" })).toBeVisible();
		await expect(page.getByText("Time zone", { exact: true })).toBeVisible();
		await expect(page.getByRole("link", { name: /Settings/ })).toBeVisible();
		await expect(page.getByRole("link", { name: /Connected apps/ })).toBeVisible();
		await expect(page.getByText("Current read")).toHaveCount(0);
		await expect(page.getByRole("link", { name: /Food diary/ })).toHaveCount(0);
	});

	test("Meal plans say what to do when there are none", async ({ page }) => {
		await page.goto("/meal-plan");
		await expect(page.getByText("No plans yet.")).toBeVisible();
		await expect(page.getByRole("link", { name: "New plan" })).toBeVisible();
	});
});
