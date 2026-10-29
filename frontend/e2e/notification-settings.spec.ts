import { expect, test, type BrowserContext } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type State = {
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
  unhandled: string[];
};

async function signIn(context: BrowserContext, telegram?: object) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: { telegram },
  });
  expect(result.ok()).toBeTruthy();
}
async function state(context: BrowserContext) {
  return (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json() as Promise<State>;
}
const preferenceWrites = (s: State) =>
  s.writes.filter((w) => w.path === "/api/Notifications/preferences");

test.afterEach(async ({ context }) => {
  expect((await state(context)).unhandled).toEqual([]);
});

test("a person turns news on in the app and sets quiet hours", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto("/profile/settings#notifications");
  const section = page.getByRole("region", { name: "Notifications", exact: true });

  const news = section.getByRole("switch", { name: /News and offers/ });
  await expect(news).toHaveAttribute("aria-checked", "false");
  await news.click();
  await expect(news).toHaveAttribute("aria-checked", "true");
  await expect(section.getByRole("status").first()).toContainText("Saved");

  await section.getByRole("combobox", { name: "Quiet hours from" }).click();
  await page.getByRole("option", { name: "22:00" }).click();
  await expect(section.getByRole("combobox", { name: "Until" })).toContainText(
    "07:00",
  );

  const writes = preferenceWrites(await state(context));
  expect(writes.map((w) => w.body)).toEqual([
    { kinds: [{ kind: "marketing", inApp: true }] },
    { quietHoursStart: "22:00", quietHoursEnd: "07:00" },
  ]);
  expect(writes.every((w) => w.key)).toBeTruthy();
});

test("choosing Telegram before linking it says so and still saves the choice", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto("/profile/settings#notifications");
  const section = page.getByRole("region", { name: "Notifications", exact: true });

  await section.getByRole("combobox", { name: "Channel" }).focus();
  await page.keyboard.press("Enter");
  await page.getByRole("option", { name: "Telegram" }).click();
  await expect(section).toContainText("Telegram is not linked");
  await expect(
    section.getByRole("link", { name: "Link Telegram" }),
  ).toHaveAttribute("href", "/profile/settings/telegram");

  await section.getByRole("switch", { name: /Meal plans/ }).click();
  await expect
    .poll(async () => preferenceWrites(await state(context)).map((w) => w.body))
    .toEqual([{ kinds: [{ kind: "planning", telegram: false }] }]);
});
