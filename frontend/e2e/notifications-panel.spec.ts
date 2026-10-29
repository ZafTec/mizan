import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type State = {
  writes: { path: string; body: Record<string, unknown> }[];
  unhandled: string[];
};

async function signIn(context: BrowserContext, unread: number) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: { unread, aiPlanning: { consented: true } },
  });
  expect(result.ok()).toBeTruthy();
}
async function state(context: BrowserContext) {
  return (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json() as Promise<State>;
}
const writesTo = (s: State, path: RegExp) =>
  s.writes.filter((w) => path.test(w.path));
const bell = (page: Page) =>
  page.getByRole("button", { name: /^Notifications/ });

test.afterEach(async ({ context }) => {
  expect((await state(context)).unhandled).toEqual([]);
});

test("a person opens notifications from the bell and reads one", async ({
  context,
  page,
}) => {
  await signIn(context, 2);
  await page.goto("/today");
  await expect(bell(page)).toHaveAccessibleName("Notifications, 2 unread");
  await expect(bell(page)).toHaveAttribute("aria-expanded", "false");
  await expect(bell(page)).toHaveAttribute(
    "aria-controls",
    "notifications-panel",
  );
  await bell(page).click();

  const panel = page.getByRole("complementary", { name: "Notifications" });
  await expect(bell(page)).toHaveAttribute("aria-expanded", "true");
  await expect(panel.getByText("2 unread")).toBeVisible();
  const newest = panel.getByRole("button", { name: /Achievement 2 unlocked/ });
  await expect(newest).toContainText("(unread)");
  await expect(newest).toBeFocused();
  await expect(
    panel.getByRole("button", { name: /You joined the Abebe household/ }),
  ).not.toContainText("(unread)");

  await newest.click();
  await expect(newest).not.toContainText("(unread)");
  await expect(panel.getByText("1 unread")).toBeVisible();
  await expect(bell(page)).toHaveAccessibleName("Notifications, 1 unread");
  expect(
    writesTo(await state(context), /^\/api\/Notifications\/[^/]+\/read$/),
  ).toHaveLength(1);
});

test("mark all read clears the count and the bell", async ({
  context,
  page,
}) => {
  await signIn(context, 3);
  await page.goto("/today");
  await bell(page).click();
  const panel = page.getByRole("complementary", { name: "Notifications" });
  await panel.getByRole("button", { name: "Mark all read" }).click();

  await expect(panel.getByText("All read")).toBeVisible();
  await expect(
    panel.getByRole("button", { name: "Mark all read" }),
  ).toHaveCount(0);
  await expect(panel.getByText("(unread)")).toHaveCount(0);
  await expect(bell(page)).toHaveAccessibleName("Notifications");
  expect(
    writesTo(await state(context), /^\/api\/Notifications\/read-all$/),
  ).toHaveLength(1);
});

test("Escape closes the panel and returns focus to the bell", async ({
  context,
  page,
}) => {
  await signIn(context, 1);
  await page.goto("/today");
  await bell(page).click();
  const panel = page.getByRole("complementary", { name: "Notifications" });
  await expect(
    panel.getByRole("button", { name: /Achievement 1/ }),
  ).toBeFocused();

  await page.keyboard.press("Escape");
  await expect(panel).toBeHidden();
  await expect(bell(page)).toBeFocused();
  await expect(bell(page)).toHaveAttribute("aria-expanded", "false");
});

test("an empty inbox says what will arrive there", async ({
  context,
  page,
}) => {
  await signIn(context, 0);
  await page.goto("/today");
  await bell(page).click();
  const panel = page.getByRole("complementary", { name: "Notifications" });
  await expect(
    panel.getByText(
      "Achievements, coach messages and household invites will land here.",
    ),
  ).toBeVisible();
  await expect(
    panel.getByRole("button", { name: "Close notifications" }),
  ).toBeFocused();
});

test("the /notifications link opens the panel over Today", async ({
  context,
  page,
}) => {
  await signIn(context, 1);
  await page.goto("/notifications");
  await page.waitForURL(/\/today$/);
  const panel = page.getByRole("complementary", { name: "Notifications" });
  await expect(
    panel.getByRole("button", { name: /Achievement 1/ }),
  ).toBeVisible();
  await expect(bell(page)).toHaveAttribute("aria-expanded", "true");
});

test("opening the assistant closes notifications, and the other way round", async ({
  context,
  page,
}) => {
  await signIn(context, 1);
  await page.goto("/today");
  await bell(page).click();
  const notifications = page.getByRole("complementary", {
    name: "Notifications",
  });
  await expect(notifications).toBeVisible();

  await page.getByRole("button", { name: "Ask Mizan" }).click();
  const assistant = page.getByRole("complementary", {
    name: "Mizan assistant",
  });
  await expect(assistant).toBeVisible();
  await expect(notifications).toBeHidden();
  await expect(bell(page)).toHaveAttribute("aria-expanded", "false");

  await bell(page).click();
  await expect(notifications).toBeVisible();
  await expect(assistant).toBeHidden();
});

test("on a phone the notifications are a sheet that holds focus", async ({
  context,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 740 });
  await signIn(context, 1);
  await page.goto("/today");
  await bell(page).click();

  const sheet = page.getByRole("dialog", { name: "Notifications" });
  await expect(sheet).toBeVisible();
  const box = await sheet.boundingBox();
  expect(box?.width).toBe(360);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  const row = sheet.getByRole("button", { name: /Achievement 1/ });
  await expect(row).toBeFocused();

  // Tab stays inside the sheet.
  for (let step = 0; step < 4; step += 1) await page.keyboard.press("Tab");
  expect(
    await page.evaluate(
      () => !!document.activeElement?.closest("#notifications-panel"),
    ),
  ).toBe(true);

  await sheet.getByRole("button", { name: "Close notifications" }).click();
  await expect(sheet).toBeHidden();
  await expect(bell(page)).toBeFocused();
});
