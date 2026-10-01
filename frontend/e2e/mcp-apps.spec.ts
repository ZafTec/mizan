import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { expect, test, type FrameLocator, type Page } from "@playwright/test";

const appsDir = resolve(__dirname, "../../backend/Mizan.Mcp.Server/Apps");
const page_ = (name: string) => readFileSync(resolve(appsDir, `${name}.html`), "utf8");

type Call = { name: string; arguments: Record<string, unknown> };
type ToolReply = { content: { type: "text"; text: string }[]; isError?: boolean };

const text = (value: unknown): ToolReply => ({ content: [{ type: "text", text: typeof value === "string" ? value : JSON.stringify(value) }] });

/**
 * A stand-in for the chat host. It does what a real one does: answers the app's
 * ui/initialize, sends the tool result once the app says it is ready, and answers
 * the tools the app calls. It records those calls so a test can say what the app asked for.
 */
async function openApp(page: Page, name: string, initial: ToolReply, reply: (call: Call) => ToolReply = () => text({})): Promise<FrameLocator> {
	await page.exposeFunction("hostReply", (call: Call) => reply(call));
	await page.setContent(`<!doctype html><body style="margin:0"><iframe id="app" style="width:520px;height:700px;border:0"></iframe></body>`);
	await page.evaluate(
		({ html, initial }) => {
			const frame = document.getElementById("app") as HTMLIFrameElement;
			const w = window as unknown as { calls: Call[]; sizes: unknown[]; hostReply: (c: Call) => Promise<ToolReply> };
			w.calls = [];
			w.sizes = [];
			window.addEventListener("message", async (event) => {
				if (event.source !== frame.contentWindow) return;
				const m = event.data;
				const post = (data: unknown) => frame.contentWindow!.postMessage(data, "*");
				if (m.method === "ui/initialize") {
					post({ jsonrpc: "2.0", id: m.id, result: { protocolVersion: "2025-06-18", hostInfo: { name: "test", version: "1" }, hostCapabilities: {}, hostContext: { theme: "light" } } });
				} else if (m.method === "ui/notifications/initialized") {
					post({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: initial });
				} else if (m.method === "ui/notifications/size-changed") {
					w.sizes.push(m.params);
				} else if (m.method === "tools/call") {
					w.calls.push(m.params);
					post({ jsonrpc: "2.0", id: m.id, result: await w.hostReply(m.params) });
				}
			});
			frame.srcdoc = html;
		},
		{ html: page_(name), initial },
	);
	return page.frameLocator("#app");
}
const calls = (page: Page) => page.evaluate(() => (window as unknown as { calls: Call[] }).calls);

test.describe("MCP apps, inside a stand-in host", () => {
	test("the nutrition day shows the totals, and the day buttons ask the host for the next day", async ({ page }) => {
		const day = { date: "2026-10-01", totalCalories: 1850, targetCalories: 2200, totalProtein: 120, targetProtein: 150, totalCarbs: 200, targetCarbs: 250, totalFat: 60, targetFat: 70 };
		const app = await openApp(page, "nutrition-day", text(day), (call) =>
			text({ ...day, date: call.arguments.date, totalCalories: 900 }));

		await expect(app.getByText("2026-10-01")).toBeVisible();
		await expect(app.getByRole("img", { name: /1,850 calories of 2,200/ })).toBeVisible();
		await expect(app.getByText("120 / 150 g")).toBeVisible();

		await app.getByRole("button", { name: "Next day" }).click();
		await expect(app.getByText("2026-10-02")).toBeVisible();
		expect(await calls(page)).toEqual([{ name: "get_nutrition_summary", arguments: { date: "2026-10-02" } }]);
	});

	test("a failed tool result is shown as a message, not a blank page", async ({ page }) => {
		const app = await openApp(page, "nutrition-day", { ...text("[MONTHLY LIMIT REACHED] Upgrade."), isError: true });

		await expect(app.getByRole("status")).toContainText("MONTHLY LIMIT REACHED");
	});

	test("the app reports its size to the host and keeps within a narrow frame", async ({ page }) => {
		await page.setViewportSize({ width: 360, height: 700 });
		const app = await openApp(page, "body-trend", text({ items: [{ date: "2026-09-01", weightKg: 82 }, { date: "2026-09-20", weightKg: 80.5 }] }));

		await expect(app.getByRole("img", { name: /Weight from 82 to 80.5 kg/ })).toBeVisible();
		expect(await page.evaluate(() => (window as unknown as { sizes: unknown[] }).sizes.length)).toBeGreaterThan(0);
	});

	test("the body trend draws a chart, and its tabs switch the measurement", async ({ page }) => {
		const items = [
			{ date: "2026-09-01", weightKg: 82, waistCm: 90 },
			{ date: "2026-09-10", weightKg: 81, waistCm: 89 },
			{ date: "2026-09-20", weightKg: 80.5, waistCm: 88 },
		];
		const app = await openApp(page, "body-trend", text({ items }));

		await expect(app.getByText("Weight: 80.5 kg now, -1.5 kg since 2026-09-01")).toBeVisible();
		await app.getByRole("button", { name: "Waist" }).click();
		await expect(app.getByText("Waist: 88 cm now, -2 cm since 2026-09-01")).toBeVisible();
		await expect(app.getByRole("button", { name: "Waist" })).toHaveAttribute("aria-pressed", "true");
		await expect(app.getByRole("button", { name: "Body fat" })).toHaveCount(0);
	});

	test("the photo review logs each ticked item once, with the grams the person set", async ({ page }) => {
		const analysis = { confidence: 0.8, foods: [
			{ name: "Rice", portionGrams: 100, calories: 130, protein: 2.7, carbs: 28, fat: 0.3 },
			{ name: "Chicken", portionGrams: 100, calories: 165, protein: 31, carbs: 0, fat: 3.6 },
			{ name: "Salad", portionGrams: 50, calories: 20, protein: 1, carbs: 3, fat: 0 },
		] };
		const app = await openApp(page, "food-photo", text(analysis));

		await expect(app.getByText("Estimate (80% confident).")).toBeVisible();
		await app.getByLabel("Include Salad").uncheck();
		await app.getByLabel("Grams of Rice").fill("200");
		await expect(app.locator("#total")).toContainText("2 items, 425 kcal");
		await app.getByRole("button", { name: "Log selected" }).click();

		await expect(app.getByRole("status")).toContainText("2 items logged");
		const made = await calls(page);
		expect(made.map((c) => c.name)).toEqual(["log_meal_manual", "log_meal_manual"]);
		expect(made.map((c) => c.arguments.name)).toEqual(["Rice", "Chicken"]);
		expect(made[0].arguments.calories).toBe(260);
		await expect(app.getByRole("button", { name: "Log selected" })).toBeDisabled();
	});

	test("the photo review stops at the first failure, so a retry cannot log anything twice", async ({ page }) => {
		const analysis = { foods: [
			{ name: "Rice", portionGrams: 100, calories: 130, protein: 2.7, carbs: 28, fat: 0.3 },
			{ name: "Chicken", portionGrams: 100, calories: 165, protein: 31, carbs: 0, fat: 3.6 },
			{ name: "Beans", portionGrams: 100, calories: 120, protein: 8, carbs: 20, fat: 1 },
		] };
		let attempts = 0;
		const app = await openApp(page, "food-photo", text(analysis), (call) =>
			++attempts === 2 ? { ...text(`Could not log ${call.arguments.name}`), isError: true } : text({ ok: true }));

		await app.getByRole("button", { name: "Log selected" }).click();

		await expect(app.getByRole("status")).toContainText("1 logged. Chicken failed");
		expect((await calls(page)).map((c) => c.arguments.name)).toEqual(["Rice", "Chicken"]);

		// Rice is logged and unticked. Retrying sends Chicken and Beans, never Rice again.
		await expect(app.getByLabel("Include Rice")).not.toBeChecked();
		await app.getByRole("button", { name: "Log selected" }).click();
		await expect(app.getByRole("status")).toContainText("2 items logged");
		expect((await calls(page)).map((c) => c.arguments.name)).toEqual(["Rice", "Chicken", "Chicken", "Beans"]);
	});

	test("a hostile food name is shown as text and never runs", async ({ page }) => {
		const evil = `<img src=x onerror="window.top.pwned = true">`;
		const app = await openApp(page, "food-photo", text({ foods: [{ name: evil, portionGrams: 100, calories: 10, protein: 0, carbs: 0, fat: 0 }] }));

		await expect(app.getByLabel("Food name")).toHaveValue(evil);
		await expect(app.locator("img")).toHaveCount(0);
		expect(await page.evaluate(() => (window as unknown as { pwned?: boolean }).pwned)).toBeUndefined();
	});
});
