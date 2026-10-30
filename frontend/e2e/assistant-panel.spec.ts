import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type State = {
  writes: { path: string; body: Record<string, unknown> }[];
  unhandled: string[];
};

async function signIn(context: BrowserContext, consented: boolean) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: { aiPlanning: { consented } },
  });
  expect(result.ok()).toBeTruthy();
}
async function state(context: BrowserContext) {
  return (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json() as Promise<State>;
}
const consentWrites = (s: State) =>
  s.writes.filter((w) => w.path === "/api/Ai/consent");
const launcher = (page: Page) =>
  page.getByRole("button", { name: "Ask Mizan" });

test.afterEach(async ({ context }) => {
  expect((await state(context)).unhandled).toEqual([]);
});

// #119: someone who never chose anything is asked in the panel, allows, and gets an answer.
test("a person who has not allowed the assistant is asked first, allows, and the first message is answered", async ({
  context,
  page,
}) => {
  await signIn(context, false);
  await page.goto("/today");
  await launcher(page).click();

  const panel = page.getByRole("complementary", { name: "Mizan assistant" });
  await expect(
    panel.getByRole("heading", { name: "What can the assistant use?" }),
  ).toBeVisible();
  await expect(
    panel.getByRole("switch", { name: /Meals and meal plans/ }),
  ).toHaveAttribute("aria-checked", "true");
  await expect(
    panel.getByRole("switch", { name: /Body measurements/ }),
  ).toHaveAttribute("aria-checked", "false");
  await expect(panel.getByLabel("Message the assistant")).toHaveCount(0);

  await panel.getByRole("button", { name: "Allow and start" }).click();
  const box = panel.getByLabel("Message the assistant");
  await expect(box).toBeFocused();
  expect(consentWrites(await state(context))).toHaveLength(1);
  expect(consentWrites(await state(context))[0].body).toMatchObject({
    enabled: true,
    shareNutrition: true,
    shareTraining: true,
    shareBody: false,
    allowWrites: true,
  });

  await box.fill("log two eggs");
  await box.press("Enter");
  await expect(panel.getByText("Logged: two eggs")).toBeVisible();
  await expect(
    panel.getByRole("group", { name: "Permission needed" }),
  ).toHaveCount(0);
});

test("Not now keeps the chat usable and says the log is out of reach", async ({
  context,
  page,
}) => {
  await signIn(context, false);
  await page.goto("/today");
  await launcher(page).click();

  await page.getByRole("button", { name: "Not now" }).click();
  await expect(
    page.getByText(
      "I can't see your log yet. Allow access to get answers about your data.",
      { exact: true },
    ),
  ).toBeVisible();
  const box = page.getByLabel("Message the assistant");
  await box.fill("is oatmeal a good breakfast");
  await box.press("Enter");
  await expect(
    page.getByText("You asked: is oatmeal a good breakfast"),
  ).toBeVisible();
  expect(consentWrites(await state(context))).toHaveLength(0);
});

test("a refused write offers to allow just that, then sends the question again", async ({
  context,
  page,
}) => {
  await signIn(context, false);
  await page.goto("/today");
  await launcher(page).click();
  await page.getByRole("button", { name: "Not now" }).click();

  const box = page.getByLabel("Message the assistant");
  await box.fill("log two eggs");
  await box.press("Enter");

  const needed = page.getByRole("group", { name: "Permission needed" });
  await expect(needed).toContainText("Logging meals and plans is switched off");
  await needed
    .getByRole("button", { name: "Allow logging meals and plans" })
    .click();
  await needed.getByRole("button", { name: "Send again" }).click();
  await expect(page.getByText("Logged: two eggs")).toBeVisible();

  const [grant] = consentWrites(await state(context));
  expect(grant.body).toMatchObject({
    allowWrites: true,
    writeNutrition: true,
    writeTraining: false,
    writeBody: false,
    enabled: false,
  });
});

test("the launcher opens a docked panel that leaves the page usable, and Escape closes it back to the launcher", async ({
  context,
  page,
}) => {
  await signIn(context, true);
  await page.goto("/today");

  await launcher(page).click();
  await expect(launcher(page)).toHaveAttribute("aria-expanded", "true");
  const panel = page.getByRole("complementary", { name: "Mizan assistant" });
  await expect(panel.getByLabel("Message the assistant")).toBeFocused();

  // Not modal: the page beside it still takes clicks.
  await page.getByRole("link", { name: "History" }).first().click();
  await page.waitForURL(/\/history/);
  await expect(panel).toBeVisible();

  await panel.getByLabel("Message the assistant").focus();
  await page.keyboard.press("Escape");
  await expect(panel).toBeHidden();
  await expect(launcher(page)).toBeFocused();
  await expect(launcher(page)).toHaveAttribute("aria-expanded", "false");
});

test("the conversation stays open across pages", async ({ context, page }) => {
  await signIn(context, true);
  await page.goto("/today");
  await launcher(page).click();
  const box = page.getByLabel("Message the assistant");
  await box.fill("how was my week");
  await box.press("Enter");
  await expect(page.getByText("You asked: how was my week")).toBeVisible();

  await page.getByRole("link", { name: "Progress" }).first().click();
  await page.waitForURL(/\/progress/);
  await expect(page.getByText("You asked: how was my week")).toBeVisible();
});

test("/ai opens the panel, and /ai?thread= opens that conversation", async ({
  context,
  page,
}) => {
  await signIn(context, true);
  await page.goto("/ai");
  await page.waitForURL(/\/today$/);
  await expect(
    page.getByRole("complementary", { name: "Mizan assistant" }),
  ).toBeVisible();

  await page.goto("/ai?thread=thread-1");
  await page.waitForURL(/\/today$/);
  await expect(page.getByText("You averaged")).toBeVisible();
});

test("on a 360px phone the panel is a sheet that holds focus, and nothing overflows", async ({
  context,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 720 });
  await signIn(context, true);
  await page.goto("/today");
  await launcher(page).click();

  await expect(
    page.getByRole("dialog", { name: "Mizan assistant" }),
  ).toHaveAttribute("aria-modal", "true");
  const sheet = page.getByRole("dialog", { name: "Mizan assistant" });
  const box = sheet.getByLabel("Message the assistant");
  await expect(box).toBeFocused();
  await expect(box).toBeInViewport();

  // Tab cycles inside the sheet rather than reaching the page behind it.
  for (let i = 0; i < 12; i++) {
    await page.keyboard.press("Tab");
    expect(
      await page.evaluate(() =>
        Boolean(document.activeElement?.closest("#assistant-panel")),
      ),
    ).toBe(true);
  }

  await sheet.getByRole("button", { name: "Past conversations" }).click();
  await sheet
    .getByRole("region", { name: "Conversations" })
    .getByRole("button", { name: "Protein ideas", exact: true })
    .click();
  await expect(sheet.getByText("You averaged")).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({ path: "test-results/assistant-phone.png" });
  await expect(sheet.getByLabel("Message the assistant")).toBeFocused();

  await page.keyboard.press("Escape");
  await expect(sheet).toBeHidden();
  await expect(launcher(page)).toBeFocused();
});
