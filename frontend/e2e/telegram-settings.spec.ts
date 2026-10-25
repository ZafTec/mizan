import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import type { TelegramOptions } from "./fixture-users";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type State = {
  unhandled: string[];
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
};

async function signIn(context: BrowserContext, telegram: TelegramOptions) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: { pro: true, telegram },
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
const profileWrites = (s: State) =>
  s.writes.filter((w) => w.path === "/api/Users/me");
const notificationsSwitch = (page: Page) =>
  page.getByRole("switch", { name: /Send notifications to Telegram/ });

test("a linked person sends their notifications to Telegram, and disconnecting stops them", async ({
  page,
  context,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(context, { linked: true });
  await page.goto("/profile/settings/telegram");

  const toggle = notificationsSwitch(page);
  await expect(toggle).toHaveAttribute("aria-checked", "false");
  await expect(toggle).toBeEnabled();
  await noOverflow(page);

  // From the keyboard: Space flips the switch.
  await toggle.focus();
  await page.keyboard.press("Space");
  await expect(toggle).toHaveAttribute("aria-checked", "true");
  await expect(
    page.getByText("Notifications will come to Telegram"),
  ).toBeVisible();

  let s = await state(context);
  const [turnedOn] = profileWrites(s);
  expect(turnedOn.body).toEqual({ notifyByTelegram: true });
  expect(turnedOn.key).toBeTruthy();

  // The server kept it.
  await page.reload();
  await expect(notificationsSwitch(page)).toHaveAttribute(
    "aria-checked",
    "true",
  );

  // Disconnecting turns it off, and the switch waits for a new link.
  await page.getByRole("button", { name: "Disconnect" }).click();
  // The confirmation's own Disconnect button opens over the page.
  await page.getByRole("button", { name: "Disconnect" }).last().click();
  await expect(page.getByText("Telegram disconnected")).toBeVisible();
  await expect(notificationsSwitch(page)).toHaveAttribute(
    "aria-checked",
    "false",
  );
  await expect(notificationsSwitch(page)).toBeDisabled();
  await noOverflow(page);

  s = await state(context);
  expect(profileWrites(s)).toHaveLength(1);
  expect(s.unhandled).toEqual([]);
});

test("a person whose chat is not linked sees the switch off, disabled, with a hint", async ({
  page,
  context,
}) => {
  await signIn(context, { linked: false });
  await page.goto("/profile/settings/telegram");

  await expect(
    page.getByRole("button", { name: "Connect Telegram" }),
  ).toBeVisible();
  const toggle = notificationsSwitch(page);
  await expect(toggle).toHaveAttribute("aria-checked", "false");
  await expect(toggle).toBeDisabled();
  await expect(toggle).toContainText(
    "Connect Telegram above first, then you can turn this on.",
  );
  await toggle.click({ force: true });

  const s = await state(context);
  expect(profileWrites(s)).toEqual([]);
  expect(s.unhandled).toEqual([]);
});
