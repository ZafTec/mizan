import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { ACTIVE_PLAN_ID, LIST_ID } from "./fixture-planning";
import {
  OATS_ALIAS_ID,
  OPEN_REPORT_ID,
  type PricesOptions,
} from "./fixture-prices";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";
const YOGURT_ID = "11111111-1111-4111-8111-111111111111";
const OATS_ID = "22222222-2222-4222-8222-222222222222";

type Options = { role?: string; prices?: PricesOptions };
type State = {
  unhandled: string[];
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
};

async function signIn(context: BrowserContext, options: Options = {}) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: options,
  });
  expect(result.ok()).toBeTruthy();
}
async function state(context: BrowserContext) {
  return (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json() as Promise<State>;
}
async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
}
const writesTo = (s: State, pattern: RegExp) =>
  s.writes.filter((w) => pattern.test(w.path));

test("a person sees a food's price and reports a wrong one", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto(`/ingredients/${YOGURT_ID}`);
  const block = page.getByRole("region", { name: "Price" });
  await expect(block).toContainText("ETB 180.00 for 500 g");
  await expect(block).toContainText("ETB 360.00 per kg");
  await expect(block).toContainText("Observed");
  await expect(block).toContainText("Arada Mart");

  await block.getByRole("button", { name: "Report a wrong price" }).click();
  const dialog = page.getByRole("dialog", { name: "Report a wrong price" });
  await dialog.getByRole("button", { name: "Send report" }).click();
  await expect(dialog.getByText("Price is a number")).toBeVisible();
  expect(writesTo(await state(context), /price-reports$/)).toEqual([]);

  await dialog.getByLabel("Price (ETB)").fill("200");
  await expect(dialog.getByLabel("Quantity")).toHaveValue("500");
  await expect(dialog.getByLabel("Unit")).toHaveValue("g");
  await dialog.getByLabel("Note (optional)").fill("Shola market");
  await dialog.getByRole("button", { name: "Send report" }).click();
  await expect(
    dialog.getByText("Thanks, an administrator will check it."),
  ).toBeVisible();

  const s = await state(context);
  const [report] = writesTo(s, /price-reports$/);
  expect(report.path).toBe(`/api/Foods/${YOGURT_ID}/price-reports`);
  expect(report.body).toEqual({
    suggestedPrice: 200,
    quantity: 500,
    unit: "g",
    note: "Shola market",
  });
  expect(report.key).toBeTruthy();
  expect(s.unhandled).toEqual([]);
});

test("a plan shows its weekly cost over budget and its unpriced items", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto(`/meal-plan/${ACTIVE_PLAN_ID}`);
  const cost = page.getByRole("region", { name: "Plan cost" });
  await expect(cost.getByTestId("plan-weekly-cost")).toContainText(
    "ETB 665.50",
  );
  await expect(cost).toContainText("of ETB 600.00 budget");
  await expect(cost).toContainText("Over budget");
  await expect(cost.getByTestId("plan-cost-coverage")).toContainText("80%");
  await expect(cost).toContainText("Estimated, a rough guess");
  // Today is day 3 of the plan: breakfast, the yogurt lunch and a meal out,
  // over the 120 daily budget, which the API marks on the day.
  await expect(page.getByRole("tabpanel").getByTestId("day-cost")).toHaveText(
    "Cost of the day ETB 138.50 of ETB 120.00 · Over the daily budget",
  );
  await expect(page.getByRole("tabpanel")).toContainText(
    "ETB 138.50 this day is over the ETB 120.00 daily budget.",
  );
  const unpriced = page.getByRole("region", { name: "Without a price" });
  await expect(unpriced.getByRole("listitem")).toHaveCount(1);
  await expect(unpriced).toContainText("Eat out");
  await expect(unpriced).toContainText("No catalogue food is linked.");
  const s = await state(context);
  expect(s.unhandled).toEqual([]);
});

test("a shopping list shows per-item costs and the total", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto(`/shopping-lists/${LIST_ID}`);
  await expect(page.getByTestId("list-cost")).toHaveText(
    "Total ETB 665.50 · Estimated, a rough guess · 1 item without a price",
  );
  const row = (name: RegExp) =>
    page.getByRole("listitem").filter({ has: page.getByLabel(name) });
  await expect(row(/Greek yogurt/).getByTestId("item-cost")).toHaveText(
    "ETB 558.00",
  );
  await expect(row(/Rolled oats/).getByTestId("item-cost")).toHaveText(
    "ETB 107.50",
  );
  await expect(row(/Eat out/).getByTestId("item-cost")).toHaveCount(0);
  const s = await state(context);
  expect(s.unhandled).toEqual([]);
});

test("a region without prices shows no cost", async ({ page, context }) => {
  await signIn(context, { prices: { regionCode: "ET-DD" } });
  await page.goto(`/meal-plan/${ACTIVE_PLAN_ID}`);
  await expect(page.getByTestId("meal-plan-detail-page")).toBeVisible();
  await expect(page.getByRole("region", { name: "Plan cost" })).toHaveCount(0);
  await expect(page.getByTestId("day-cost")).toHaveCount(0);
  await expect(
    page.getByRole("region", { name: "Without a price" }),
  ).toHaveCount(0);

  await page.goto(`/shopping-lists/${LIST_ID}`);
  await expect(page.getByTestId("items-left")).toBeVisible();
  await expect(page.getByTestId("list-cost")).toHaveCount(0);
  await expect(page.getByTestId("item-cost")).toHaveCount(0);

  await page.goto(`/ingredients/${YOGURT_ID}`);
  const block = page.getByRole("region", { name: "Price" });
  await expect(block).toContainText("No price for your region yet.");
  await expect(
    block.getByRole("button", { name: "Report a wrong price" }),
  ).toBeVisible();
  const s = await state(context);
  expect(s.unhandled).toEqual([]);
});

test("switching region changes the currency shown", async ({
  page,
  context,
}) => {
  await signIn(context);
  // The settings page renders the same on the server and in the browser.
  const hydration: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error" && /hydrat/i.test(message.text()))
      hydration.push(message.text());
  });
  page.on("pageerror", (error) => {
    if (/hydrat/i.test(error.message)) hydration.push(error.message);
  });
  await page.goto(`/ingredients/${YOGURT_ID}`);
  await expect(page.getByRole("region", { name: "Price" })).toContainText(
    "ETB 180.00",
  );

  await page.goto("/profile/settings");
  const zone = page.getByLabel("Time zone");
  await expect(zone).toHaveValue("UTC");
  // The browser's own list arrives after hydration, with each zone's offset.
  await expect(
    zone.getByRole("option", { name: "Africa/Addis Ababa (GMT+3)" }),
  ).toHaveCount(1);
  const select = page.getByLabel("Price region");
  await expect(select).toHaveValue("ET-AA");
  await expect(select.getByRole("option")).toHaveText([
    "Addis Ababa (ETB)",
    "Dire Dawa (ETB)",
    "Nairobi (KES)",
  ]);
  await select.selectOption("KE-NBO");
  await expect(
    page.getByText("Prices now come from Nairobi, in KES"),
  ).toBeVisible();

  await page.goto(`/ingredients/${YOGURT_ID}`);
  await expect(page.getByRole("region", { name: "Price" })).toContainText(
    "KES 220.00 for 500 g",
  );
  const s = await state(context);
  expect(writesTo(s, /^\/api\/Users\/me$/).map((w) => w.body)).toEqual([
    { priceRegionCode: "KE-NBO" },
  ]);
  expect(hydration).toEqual([]);
  expect(s.unhandled).toEqual([]);
});

test("an administrator maps a queued alias", async ({ page, context }) => {
  await signIn(context, { role: "admin" });
  await page.goto("/admin/prices");
  await expect(
    page.getByRole("link", { name: "Prices", exact: true }).first(),
  ).toHaveAttribute("href", "/admin/prices");
  const queue = page.getByRole("region", { name: "Review queue" });
  const oats = queue.getByRole("listitem", { name: "Quaker oats 500g" });
  await expect(oats).toContainText("Arada Mart · ETB 145.00");
  await expect(
    oats.getByRole("link", { name: "Listing for Quaker oats 500g" }),
  ).toHaveAttribute(
    "href",
    "https://aradamart.net/product-page/quaker-oats-500g",
  );

  await oats.getByRole("button", { name: "Map Quaker oats 500g" }).click();
  const form = page.getByRole("form", { name: "Map Quaker oats 500g" });
  await form.getByRole("button", { name: "Save" }).click();
  await expect(form.getByText("Choose a food")).toBeVisible();
  await form.getByRole("searchbox", { name: "Food" }).fill("oats");
  await form.getByRole("button", { name: "Add Rolled oats" }).click();
  await expect(form.getByLabel("Quantity")).toHaveValue("500");
  await expect(form.getByLabel("Unit")).toHaveValue("g");
  await form.getByRole("button", { name: "Save" }).click();
  await expect(
    queue.getByRole("listitem", { name: "Quaker oats 500g" }),
  ).toHaveCount(0);

  await queue
    .getByRole("button", { name: "Ignore Reusable shopping bag" })
    .click();
  await expect(queue.getByText("Every product is mapped.")).toBeVisible();

  const s = await state(context);
  const updates = writesTo(s, /^\/api\/admin\/prices\/aliases\//);
  expect(updates.map((w) => w.body)).toEqual([
    { foodId: OATS_ID, quantity: 500, unit: "g", ignored: false },
    { foodId: null, quantity: null, unit: null, ignored: true },
  ]);
  expect(updates[0].path).toBe(`/api/admin/prices/aliases/${OATS_ALIAS_ID}`);
  expect(updates.every((w) => w.key)).toBe(true);
  expect(s.unhandled).toEqual([]);
});

test("an administrator opens a price report from the notifications", async ({
  page,
  context,
}) => {
  await signIn(context, { role: "admin" });
  await page.goto("/notifications");
  const row = page.getByRole("button", {
    name: /Price report for Greek yogurt/,
  });
  await expect(row).toContainText("200 ETB for 500 g.");
  await expect(row).toContainText("(unread)");
  await row.click();

  await page.waitForURL(/\/admin\/prices#reports$/);
  const reports = page.getByRole("region", { name: "Reports" });
  await expect(
    reports.getByRole("listitem", { name: "Report on Greek yogurt" }),
  ).toBeInViewport();
  const s = await state(context);
  expect(writesTo(s, /^\/api\/Notifications\/[^/]+\/read$/)).toHaveLength(1);
  expect(s.unhandled).toEqual([]);
});

test("an administrator accepts a report", async ({ page, context }) => {
  await signIn(context, { role: "admin" });
  await page.goto("/admin/prices#reports");
  const reports = page.getByRole("region", { name: "Reports" });
  const report = reports.getByRole("listitem", {
    name: "Report on Greek yogurt",
  });
  await expect(report).toContainText(
    "Shown ETB 360.00 per kg · Suggested ETB 400.00 per kg",
  );
  await expect(report).toContainText("ETB 200.00 for 500 g");
  await expect(report).toContainText("Shola market price this week.");

  await report
    .getByRole("button", { name: "Accept the report on Greek yogurt" })
    .click();
  await expect(reports.getByText("No open reports.")).toBeVisible();

  await reports.getByRole("link", { name: "Accepted" }).click();
  await expect(page).toHaveURL(/reports=Accepted/);
  await expect(
    reports.getByRole("listitem", { name: "Report on Greek yogurt" }),
  ).toContainText("Accepted");
  await expect(
    reports.getByRole("listitem", { name: "Report on Rolled oats" }),
  ).toBeVisible();

  const s = await state(context);
  const accepted = writesTo(s, /\/reports\/[^/]+\/accept$/);
  expect(accepted.map((w) => w.path)).toEqual([
    `/api/admin/prices/reports/${OPEN_REPORT_ID}/accept`,
  ]);
  expect(accepted[0].key).toBeTruthy();
  expect(s.unhandled).toEqual([]);
});

test("Run now shows 202, and the API's message when the worker is unset or down", async ({
  page,
  context,
}) => {
  const runArada = async () => {
    const sources = page.getByRole("region", { name: "Sources" });
    const arada = sources.getByRole("listitem", { name: "Arada Mart, ET-AA" });
    await expect(arada).toContainText("succeeded · 312 prices");
    await arada.getByRole("button", { name: "Run Arada Mart now" }).click();
    return arada;
  };

  await signIn(context, { role: "admin" });
  await page.goto("/admin/prices");
  const sources = page.getByRole("region", { name: "Sources" });
  await expect(
    sources.getByRole("listitem", { name: "Asbeza, ET-AA" }),
  ).toContainText("The store renders prices in the browser");
  let arada = await runArada();
  await expect(arada.getByRole("status")).toContainText("Run started.");
  expect(writesTo(await state(context), /\/run$/)[0].key).toBeTruthy();
  expect((await state(context)).unhandled).toEqual([]);

  await signIn(context, {
    role: "admin",
    prices: { runOutcome: "unconfigured" },
  });
  await page.reload();
  arada = await runArada();
  await expect(arada.getByRole("alert")).toHaveText(
    "The prices worker is not configured. Set Prices:WorkerUrl on the API to use Run now.",
  );
  expect((await state(context)).unhandled).toEqual([]);

  await signIn(context, {
    role: "admin",
    prices: { runOutcome: "unreachable" },
  });
  await page.reload();
  arada = await runArada();
  await expect(arada.getByRole("alert")).toHaveText(
    "The prices worker did not answer. Check that it is running.",
  );
  expect((await state(context)).unhandled).toEqual([]);
});

test("the admin prices page and the food page fit a phone", async ({
  page,
  context,
}) => {
  await signIn(context, { role: "admin" });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/admin/prices");
  await expect(page.getByTestId("admin-prices-page")).toBeVisible();
  await noOverflow(page);
  await page.getByRole("button", { name: "Map Quaker oats 500g" }).click();
  await expect(
    page.getByRole("form", { name: "Map Quaker oats 500g" }),
  ).toBeVisible();
  await noOverflow(page);

  await page.goto(`/ingredients/${OATS_ID}`);
  const block = page.getByRole("region", { name: "Price" });
  await expect(block).toContainText("Estimated, a rough guess");
  await noOverflow(page);
  await block.getByRole("button", { name: "Report a wrong price" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await noOverflow(page);
  const s = await state(context);
  expect(s.unhandled).toEqual([]);
});
