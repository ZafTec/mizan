import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { choose } from "./select";
import { WELCOME_ID } from "./fixture-admin-notifications";

/**
 * Notification campaigns in the admin console (docs/ADMIN.md#notification-campaigns):
 * the list and its report, the editor with its live per-channel preview and
 * audience count, the test send, and starting with a confirmed count. The
 * fixture is synthetic; the rules are covered by NotificationCampaignStoryTests
 * and CampaignRulesTests in the backend.
 */
const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type Write = {
  method: string;
  path: string;
  body: unknown;
  key: string | null;
};

async function signIn(context: BrowserContext, data: Record<string, unknown>) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data,
  });
  expect(result.ok()).toBeTruthy();
}
async function unhandled(context: BrowserContext) {
  return (
    (await (await context.request.get(`${apiURL}/__fixture/state`)).json()) as {
      unhandled: string[];
    }
  ).unhandled;
}
async function writes(context: BrowserContext) {
  return (
    (await (
      await context.request.get(`${apiURL}/__fixture/admin-notifications`)
    ).json()) as { writes: Write[] }
  ).writes;
}
async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
}

test("an administrator writes a campaign, previews it, tests it and starts it for the confirmed count", async ({
  page,
  context,
}) => {
  await signIn(context, { role: "admin" });
  await page.goto("/admin/notifications");
  await expect(
    page.getByRole("heading", { name: "Notifications", level: 1 }),
  ).toBeVisible();

  // The history: the live welcome and its delivery report.
  await page.getByRole("link", { name: /Welcome/ }).click();
  await expect(page).toHaveURL(new RegExp(WELCOME_ID));
  const report = page.getByTestId("campaign-report");
  await expect(report.getByRole("row", { name: /In the app/ })).toContainText(
    "7",
  );
  await expect(
    page.getByText("Pause the campaign to change it."),
  ).toBeVisible();

  await page.goto("/admin/notifications/new");
  await page.getByLabel("Name").fill("Ramadan recipes");
  await page.getByRole("radio", { name: /News and offers/ }).check();
  await page.getByLabel("Title").fill("New for you, {firstName}");
  await page.getByLabel("Body").fill("Twenty iftar recipes at {appLink}");
  await page.getByRole("checkbox", { name: "Email" }).check();

  // The preview fills the placeholders, per channel, as the API renders them.
  await expect(page.getByTestId("preview-inApp")).toContainText(
    "New for you, Ada",
  );
  await expect(page.getByTestId("preview-email")).toContainText(
    "Turn them off",
  );
  await expect(page.getByTestId("campaign-reachable")).toHaveText("3");

  await choose(page.getByLabel("Sends"), "As soon as it is started");
  await page.getByRole("button", { name: "Save draft" }).click();
  await expect(
    page.getByRole("heading", { name: "Ramadan recipes", level: 1 }),
  ).toBeVisible();

  await page.getByRole("button", { name: "Send a test to me" }).click();
  await expect(page.getByText("Test sent to you on 1 channel")).toBeVisible();

  // The confirmation names the count, and the start sends it back.
  await page.getByRole("button", { name: "Start…" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByTestId("confirm-count")).toHaveText("3 people");
  await dialog.getByRole("button", { name: "Start for 3" }).click();
  await expect(dialog).toBeHidden();

  const written = await writes(context);
  const start = written.find((w) => w.path.endsWith("/activate"));
  expect(start?.body).toEqual({ audienceCount: 3 });
  for (const w of written.filter((w) => !/\/(audience|preview)$/.test(w.path)))
    expect(w.key, `${w.method} ${w.path}`).toBeTruthy();

  await page.setViewportSize({ width: 375, height: 800 });
  await page.goto("/admin/notifications/new");
  await expect(page.getByTestId("campaign-editor")).toBeVisible();
  await noOverflow(page);
  await page.goto(`/admin/notifications/${WELCOME_ID}`);
  await noOverflow(page);
  expect(await unhandled(context)).toEqual([]);
});

test("the opt-out link in a marketing email turns news and offers off with one press", async ({
  page,
  context,
}) => {
  await signIn(context, { role: "user" });
  await page.setViewportSize({ width: 375, height: 800 });
  await page.goto("/notifications/opt-out?token=valid-token");
  await page.getByRole("button", { name: "Turn off news and offers" }).click();
  await expect(page.getByTestId("opt-out-done")).toContainText(
    "News and offers are off",
  );
  await noOverflow(page);
  expect(await unhandled(context)).toEqual([]);
});
