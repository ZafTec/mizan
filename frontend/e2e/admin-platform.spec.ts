import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { readFile } from "node:fs/promises";
import { GREEN_GATE_ID, SAM_ID } from "./fixture-admin-ops";

/**
 * The platform administration pages (docs/ADMIN.md): feature flags with
 * household overrides, allowance overrides, and usage with its CSV export,
 * plus the plan page showing the API's message when its feature is off. The
 * fixture is synthetic; the rules are covered by the backend's FeatureFlagTests,
 * UsageLimitTests and PlatformUsageTests.
 */
const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type State = { unhandled: string[] };
type AdminState = { writes: { method: string; path: string; body: unknown }[] };

async function signIn(context: BrowserContext, data: Record<string, unknown>) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data,
  });
  expect(result.ok()).toBeTruthy();
}
async function state(context: BrowserContext) {
  return (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json() as Promise<State>;
}
async function adminState(context: BrowserContext) {
  return (
    await context.request.get(`${apiURL}/__fixture/admin-flags`)
  ).json() as Promise<AdminState>;
}
async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
}

test("an administrator turns plan drafting off for one household and prices off by default", async ({
  page,
  context,
}) => {
  await signIn(context, { role: "admin" });
  await page.goto("/admin/feature-flags");
  await expect(
    page.getByRole("heading", { name: "Feature flags", level: 1 }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Feature flags" }).first(),
  ).toBeVisible();

  const planning = page.getByTestId("flag-planning.generate");
  await planning.getByRole("button", { name: "Overrides (0)" }).click();
  await expect(
    planning.getByText("Every household follows the default."),
  ).toBeVisible();

  // An empty search says what to type; then the household is found by name.
  await planning.getByRole("button", { name: "Search" }).click();
  await expect(planning.getByRole("alert")).toHaveText(
    "Type part of a household's name.",
  );
  await planning
    .getByRole("textbox", { name: "Find a household" })
    .fill("green");
  await planning
    .getByRole("textbox", { name: "Find a household" })
    .press("Enter");
  await planning
    .getByRole("button", { name: "Turn off for Green gate" })
    .click();

  const overrides = planning.getByRole("list", {
    name: "Overrides for planning.generate",
  });
  await expect(overrides.getByText("Green gate")).toBeVisible();
  await expect(
    overrides.getByRole("switch", { name: "planning.generate for Green gate" }),
  ).toHaveAttribute("aria-checked", "false");
  await expect(planning).toContainText(
    "1 household overrides it (0 on, 1 off)",
  );

  // The default switch works from the keyboard.
  const prices = page.getByRole("switch", { name: "Default for prices" });
  await prices.focus();
  await page.keyboard.press("Space");
  await expect(prices).toHaveAttribute("aria-checked", "false");
  await expect(page.getByTestId("flag-prices")).toContainText("ships on");

  // Removing the override puts the household back on the default.
  await overrides
    .getByRole("button", { name: "Remove the override for Green gate" })
    .click();
  await expect(
    planning.getByText("Every household follows the default."),
  ).toBeVisible();

  const writes = (await adminState(context)).writes;
  expect(writes.map((w) => `${w.method} ${w.path}`)).toEqual([
    `PUT /api/admin/feature-flags/planning.generate/overrides/${GREEN_GATE_ID}`,
    "PUT /api/admin/feature-flags/prices",
    `DELETE /api/admin/feature-flags/planning.generate/overrides/${GREEN_GATE_ID}`,
  ]);
  expect(writes[0].body).toEqual({ enabled: false });
  expect(writes[1].body).toEqual({ enabled: false });

  await page.setViewportSize({ width: 375, height: 800 });
  await noOverflow(page);
  expect((await state(context)).unhandled).toEqual([]);
});

test("the plan page shows the API's message when plan drafting is off for the household", async ({
  page,
  context,
}) => {
  await signIn(context, { aiPlanning: { consented: true } });
  const configured = await context.request.post(
    `${apiURL}/__fixture/admin-flags`,
    { data: { off: ["planning.generate"] } },
  );
  expect(configured.ok()).toBeTruthy();

  await page.goto("/meal-plan/new");
  const section = page.getByRole("region", {
    name: "Draft with the assistant",
  });
  await section
    .getByRole("button", { name: "Draft with the assistant" })
    .click();
  await section.getByRole("button", { name: "Draft plan" }).click();

  await expect(section.getByRole("alert")).toHaveText(
    "This feature is turned off for your household.",
  );
  await expect(section.getByTestId("draft-preview")).toHaveCount(0);
  expect((await state(context)).unhandled).toEqual([]);
});

test("an administrator caps one user's daily requests and sees where each limit comes from", async ({
  page,
  context,
}) => {
  await signIn(context, { role: "admin" });
  await page.goto("/admin/usage-limits");

  // Plans are listed first, each against the Pro defaults.
  await expect(
    page
      .getByRole("navigation", { name: "Limit scopes" })
      .getByRole("link", { name: "Plans" }),
  ).toHaveAttribute("aria-current", "page");
  await expect(page.getByText("Pro monthly")).toBeVisible();

  const scopes = page.getByRole("navigation", { name: "Limit scopes" });
  await scopes.getByRole("link", { name: "Users" }).click();
  await expect(page.getByText("No user has an override.")).toBeVisible();
  await page.getByLabel("Name or email").fill("sam");
  await page.getByRole("button", { name: "Search" }).click();
  await page.getByRole("button", { name: "Set limits for Sam Rivera" }).click();

  const row = page.getByTestId(`limits-${SAM_ID}`);
  const form = row.getByRole("form", { name: "Override for Sam Rivera" });
  await form.getByRole("button", { name: "Save override" }).click();
  await expect(form.getByRole("alert")).toContainText("Set at least one cap.");

  await form.getByLabel("Line").selectOption("Personal");
  await form.getByLabel("Requests a day").fill("3");
  await form.getByLabel("Note (optional)").fill("Support ticket 41");
  await form.getByRole("button", { name: "Save override" }).click();

  const personal = row.getByRole("row", { name: /^Personal/ });
  await expect(personal.getByRole("cell").first()).toContainText("3User");
  await expect(
    row
      .getByRole("row", { name: /^Meal planning/ })
      .getByRole("cell")
      .first(),
  ).toContainText("1Default");
  await expect(
    row.getByRole("list", { name: "Overrides for Sam Rivera" }),
  ).toContainText("Requests a day: 3 · Support ticket 41");

  const [write] = (await adminState(context)).writes;
  expect(write.path).toBe(`/api/admin/usage-limits/User/${SAM_ID}`);
  expect(write.body).toEqual({
    line: "Personal",
    dailyRequests: 3,
    dailyTokens: null,
    dailyCostMicros: null,
    monthlyCostMicros: null,
    note: "Support ticket 41",
  });

  await page.setViewportSize({ width: 375, height: 800 });
  await noOverflow(page);
  expect((await state(context)).unhandled).toEqual([]);
});

test("an administrator reads usage by plan and downloads it as CSV", async ({
  page,
  context,
}) => {
  await signIn(context, { role: "admin" });
  await page.goto("/admin/usage?from=2026-10-01&to=2026-10-25");
  await expect(
    page.getByRole("heading", { name: "Usage", level: 1 }),
  ).toBeVisible();
  await expect(
    page.getByRole("cell", { name: "Blue door", exact: true }),
  ).toBeVisible();

  await page.getByLabel("Group by").selectOption("plan");
  await page.getByRole("button", { name: "Show" }).click();
  await expect(page).toHaveURL(/groupBy=plan/);
  await expect(page.getByRole("cell", { name: "Pro monthly" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Free" })).toBeVisible();

  const downloading = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export CSV" }).click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe(
    "mizan-usage-plan-2026-10-01-2026-10-25.csv",
  );
  const text = await readFile((await download.path())!, "utf8");
  expect(text.split("\r\n").filter(Boolean)).toEqual([
    "group,key,label,requests,tokens,cost_micros,refusals",
    "plan,bbbbbbbb-0000-4000-8000-000000000001,Pro monthly,40,80000,24000,0",
    "plan,free,Free,9,11200,3360,4",
  ]);

  await page.setViewportSize({ width: 375, height: 800 });
  await noOverflow(page);
  expect((await state(context)).unhandled).toEqual([]);
});

test("a user's admin page links to their usage this month", async ({
  page,
  context,
}) => {
  await signIn(context, { role: "admin" });
  await page.goto(`/admin/users/${SAM_ID}`);
  const block = page.getByRole("region", { name: "Usage this month" });
  await expect(block).toContainText("Requests");
  await block.getByRole("link", { name: "See usage in detail" }).click();
  await expect(page).toHaveURL(new RegExp(`userId=${SAM_ID}`));
  await expect(page.getByText("Showing one user only.")).toBeVisible();
  expect((await state(context)).unhandled).toEqual([]);
});
