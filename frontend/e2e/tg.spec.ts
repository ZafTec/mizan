import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { ACTIVE_PLAN_ID, LIST_ID } from "./fixture-planning";
import type { TelegramOpening, TelegramOutcome } from "./fixture-tg";

/**
 * The Telegram Mini App: the bot's Open buttons land on /tg pages, which sign
 * in from Telegram's initData through the bot service (played by the fixture)
 * and show the plan day or the list without the app's shell.
 */
const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";
const TELEGRAM_SCRIPT = "https://telegram.org/js/telegram-web-app.js";

type State = {
  unhandled: string[];
  writes: { path: string; body: Record<string, unknown> }[];
};
type Opening = { initData: string; token: string; hash: string };

/**
 * What Telegram's WebView provides: window.Telegram.WebApp with the signed
 * initData. The real script is answered with nothing, so no test reaches
 * telegram.org; `__telegram` records what the page asked of Telegram.
 */
async function openTelegram(
  page: Page,
  context: BrowserContext,
  outcome: TelegramOutcome,
  { inTelegram = true }: { inTelegram?: boolean } = {},
): Promise<Opening> {
  const result = await context.request.post(`${apiURL}/__fixture/telegram`, {
    data: { outcome },
  });
  expect(result.ok()).toBeTruthy();
  const { initData, token } = (await result.json()) as {
    initData: string;
    token: string;
  };
  await context.route(TELEGRAM_SCRIPT, (route) =>
    route.fulfill({ contentType: "application/javascript", body: "" }),
  );
  await page.addInitScript(
    ({ initData }) => {
      const calls: string[] = [];
      const opened: string[] = [];
      Object.assign(window, {
        __telegram: { calls, opened },
        Telegram: {
          WebApp: {
            initData,
            colorScheme: "light",
            themeParams: { bg_color: "#ffffff", text_color: "#000000" },
            ready: () => calls.push("ready"),
            expand: () => calls.push("expand"),
            openLink: (url: string) => opened.push(url),
          },
        },
      });
    },
    { initData: inTelegram ? initData : "" },
  );
  return {
    initData,
    token,
    hash: new URLSearchParams(initData).get("hash")!,
  };
}

async function state(context: BrowserContext, opening: Opening) {
  return (
    await context.request.get(`${apiURL}/__fixture/state`, {
      headers: { cookie: `mizan_session=${opening.token}` },
    })
  ).json() as Promise<State>;
}
async function exchanges(context: BrowserContext, opening: Opening) {
  const result = await context.request.get(
    `${apiURL}/__fixture/telegram?hash=${opening.hash}`,
  );
  return ((await result.json()) as TelegramOpening).exchanges;
}
async function telegramCalls(page: Page) {
  return page.evaluate(
    () =>
      (
        window as unknown as {
          __telegram: { calls: string[]; opened: string[] };
        }
      ).__telegram,
  );
}
async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
}
async function sessionCookie(context: BrowserContext) {
  return (await context.cookies()).find(
    (cookie) => cookie.name === "mizan_session",
  )?.value;
}
const tomorrow = () => {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
};

test("a person opens the plan day from the bot's button and logs an entry", async ({
  page,
  context,
}) => {
  const opening = await openTelegram(page, context, "signed_in");
  // The /plan tomorrow card's Open button.
  const response = await page.goto(
    `/tg/plan?planId=${ACTIVE_PLAN_ID}&day=${tomorrow()}`,
  );
  expect(response?.status()).toBe(200);
  expect(new URL(page.url()).pathname).toBe("/tg/plan");

  await expect(
    page.getByRole("heading", { name: "Steady week" }),
  ).toBeVisible();
  await expect(page.getByRole("navigation")).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Meal plans" })).toHaveCount(0);
  await expect(page.getByRole("tab", { name: /, today$/ })).toHaveAttribute(
    "aria-selected",
    "false",
  );
  const panel = page.getByRole("tabpanel");
  await expect(panel).toContainText("Rolled oats");
  await panel.getByRole("button", { name: "Log Rolled oats" }).click();
  await expect(panel.getByLabel("Rolled oats logged")).toBeVisible();

  expect(await sessionCookie(context)).toBe(opening.token);
  expect(await exchanges(context, opening)).toEqual([
    { initData: opening.initData, authorization: null },
  ]);
  expect((await telegramCalls(page)).calls).toEqual(["ready", "expand"]);
  const s = await state(context, opening);
  const logs = s.writes.filter((write) =>
    /\/entries\/[^/]+\/log$/.test(write.path),
  );
  expect(logs).toHaveLength(1);
  expect(logs[0].path).toBe(
    `/api/MealPlans/${ACTIVE_PLAN_ID}/entries/e2000004-0000-4000-8000-000000000004/log`,
  );
  expect(s.unhandled).toEqual([]);
});

test("a person opens the shopping list from the bot's button and ticks an item", async ({
  page,
  context,
}) => {
  const opening = await openTelegram(page, context, "signed_in");
  await page.goto(`/tg/shopping-list?listId=${LIST_ID}`);
  const list = page.getByTestId("shopping-list-page");
  await expect(list).toHaveAttribute("data-hydrated", "true");
  await expect(
    page.getByRole("heading", { name: "Steady week shopping" }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Delete list" })).toHaveCount(
    0,
  );
  const before = await page.getByTestId("items-left").textContent();

  await page.getByRole("checkbox", { name: "Greek yogurt" }).check();
  await expect(
    page.getByRole("checkbox", { name: "Greek yogurt" }),
  ).toBeChecked();
  await expect(page.getByTestId("items-left")).not.toHaveText(before ?? "");

  const s = await state(context, opening);
  const toggles = s.writes.filter((write) => write.path.endsWith("/toggle"));
  expect(toggles).toHaveLength(1);
  expect(toggles[0].body).toEqual({ isChecked: true });
  expect(s.unhandled).toEqual([]);
});

test("a chat that is not linked is told to link the account", async ({
  page,
  context,
}) => {
  const opening = await openTelegram(page, context, "not_linked");
  await page.goto(`/tg/plan?planId=${ACTIVE_PLAN_ID}`);
  await expect(
    page.getByText("Link your Telegram account first."),
  ).toBeVisible();
  await page.getByRole("button", { name: "Link account" }).click();
  expect((await telegramCalls(page)).opened).toEqual([
    `${new URL(page.url()).origin}/profile/settings/telegram`,
  ]);
  expect(await sessionCookie(context)).toBeUndefined();
  expect(await exchanges(context, opening)).toHaveLength(1);
  expect((await state(context, opening)).unhandled).toEqual([]);
});

test("a Free account is shown the upgrade", async ({ page, context }) => {
  const opening = await openTelegram(page, context, "upgrade_required");
  await page.goto(`/tg/shopping-list?listId=${LIST_ID}`);
  await expect(
    page.getByText("The Telegram bot is part of Mizan Pro."),
  ).toBeVisible();
  await page.getByRole("button", { name: "Upgrade to Pro" }).click();
  expect((await telegramCalls(page)).opened).toEqual([
    `${new URL(page.url()).origin}/billing`,
  ]);
  expect(await sessionCookie(context)).toBeUndefined();
  expect((await state(context, opening)).unhandled).toEqual([]);
});

test("a stale opening shows the bot service's reason", async ({
  page,
  context,
}) => {
  const opening = await openTelegram(page, context, "invalid_init_data");
  await page.goto("/tg/plan");
  await expect(
    page.getByText("initData has expired; open the Mini App again."),
  ).toBeVisible();
  expect(await sessionCookie(context)).toBeUndefined();
  expect((await state(context, opening)).unhandled).toEqual([]);
});

test("the page opened outside Telegram says to open it from the bot", async ({
  page,
  context,
}) => {
  const opening = await openTelegram(page, context, "signed_in", {
    inTelegram: false,
  });
  const response = await page.goto(`/tg/plan?planId=${ACTIVE_PLAN_ID}`);
  // Signed out and still here: the Mini App signs itself in.
  expect(new URL(page.url()).pathname).toBe("/tg/plan");
  expect(response?.headers()["x-frame-options"]).toBeUndefined();
  expect(response?.headers()["content-security-policy"]).toContain(
    "https://web.telegram.org",
  );
  await expect(
    page.getByText("Open this page from the Telegram bot."),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: "Open Mizan" })).toHaveAttribute(
    "href",
    "/today",
  );
  expect(await exchanges(context, opening)).toEqual([]);
  expect((await state(context, opening)).unhandled).toEqual([]);
});

test.describe("on a phone", () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test("today's plan fits the screen without the shell", async ({
    page,
    context,
  }) => {
    const opening = await openTelegram(page, context, "signed_in");
    await page.goto("/tg/plan");
    await expect(
      page.getByRole("heading", { name: "Steady week" }),
    ).toBeVisible();
    await expect(page.getByRole("tab", { name: /, today$/ })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    // The first entry's Log button is on the first screen.
    await expect(
      page.getByRole("tabpanel").getByRole("button", { name: /^Log / }).first(),
    ).toBeInViewport();
    await noOverflow(page);
    await page.screenshot({
      path: "test-results/ui/tg-plan-mobile.png",
      fullPage: true,
    });
    expect((await state(context, opening)).unhandled).toEqual([]);
  });
});
