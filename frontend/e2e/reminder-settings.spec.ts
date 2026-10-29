import { expect, test, type BrowserContext } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type State = {
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
  unhandled: string[];
};

async function signIn(context: BrowserContext) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: {},
  });
  expect(result.ok()).toBeTruthy();
}
async function state(context: BrowserContext) {
  return (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json() as Promise<State>;
}
const reminderWrites = (s: State) =>
  s.writes.filter((w) => w.path.startsWith("/api/Reminders"));

test.afterEach(async ({ context }) => {
  expect((await state(context)).unhandled).toEqual([]);
});

test("a person adds a weekday workout reminder in Settings, turns it off and deletes it", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto("/profile/settings#reminders");
  const section = page.getByRole("region", { name: "Reminders", exact: true });
  await expect(section).toContainText("No reminders yet.");

  await section.getByRole("button", { name: "Add a reminder" }).click();
  await section.getByRole("combobox", { name: "How often" }).click();
  await page.getByRole("option", { name: "Weekdays" }).click();
  await section.getByRole("combobox", { name: "At" }).click();
  await page.getByRole("option", { name: "07:00", exact: true }).click();
  await section.getByRole("button", { name: "Set reminder" }).click();

  const row = section.getByRole("switch", { name: /Time for your workout/ });
  await expect(row).toHaveAttribute("aria-checked", "true");
  await expect(row).toContainText(
    "Weekdays at 07:00. Next: Mon 2 Nov at 07:00.",
  );

  await row.click();
  await expect(row).toHaveAttribute("aria-checked", "false");

  await section
    .getByRole("button", { name: "Delete Time for your workout" })
    .click();
  await page.getByRole("button", { name: "Delete", exact: true }).click();
  await expect(section).toContainText("No reminders yet.");

  const writes = reminderWrites(await state(context));
  expect(writes.map((w) => w.body)).toEqual([
    {
      kind: "workout",
      title: null,
      channels: ["inApp", "telegram", "push", "email"],
      schedule: {
        repeat: "days",
        time: "07:00",
        days: ["mon", "tue", "wed", "thu", "fri"],
      },
    },
    { method: "PUT", enabled: false },
    { method: "DELETE" },
  ]);
  expect(writes.every((w) => w.key)).toBeTruthy();
});

test("a person sets a reminder for the first Sunday of every month", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto("/profile/settings#reminders");
  const section = page.getByRole("region", { name: "Reminders", exact: true });

  await section.getByRole("button", { name: "Add a reminder" }).click();
  await section.getByRole("combobox", { name: "How often" }).click();
  await page.getByRole("option", { name: "Once a month" }).click();
  await expect(section.getByRole("combobox", { name: "On" })).toContainText(
    "The first Sunday",
  );
  await section.getByRole("combobox", { name: "At" }).click();
  await page.getByRole("option", { name: "08:00", exact: true }).click();
  await section.getByRole("button", { name: "Set reminder" }).click();

  await expect(
    section.getByRole("switch", { name: /Time for your workout/ }),
  ).toContainText("First Sunday of the month at 08:00.");

  const writes = reminderWrites(await state(context));
  expect(writes.map((w) => w.body)).toEqual([
    {
      kind: "workout",
      title: null,
      channels: ["inApp", "telegram", "push", "email"],
      schedule: {
        repeat: "monthly",
        time: "08:00",
        week: "first",
        days: ["sun"],
      },
    },
  ]);
});

test("Remind me on the measurements page sets a measurement reminder", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto("/body-measurements");
  await page.getByRole("button", { name: "Remind me" }).click();
  const dialog = page.getByRole("dialog", { name: "Remind me to measure" });
  await dialog.getByRole("button", { name: "Set reminder" }).click();
  await expect(dialog).toBeHidden();

  const writes = reminderWrites(await state(context));
  expect(writes.map((w) => w.body)).toEqual([
    {
      kind: "measurement",
      title: null,
      channels: ["inApp", "telegram", "push", "email"],
      schedule: { repeat: "daily", time: "07:00" },
    },
  ]);
});

test("a person chooses where a reminder goes, in Settings and from Remind me", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto("/profile/settings#reminders");
  const section = page.getByRole("region", { name: "Reminders", exact: true });
  await section.getByRole("button", { name: "Add a reminder" }).click();

  const sendTo = section.getByRole("group", { name: "Send it to" });
  const email = sendTo.getByRole("switch", { name: /^Email/ });
  await expect(email).toHaveAttribute("aria-checked", "true");
  await email.click();
  await expect(email).toHaveAttribute("aria-checked", "false");
  // The keyboard flips a channel too.
  const push = sendTo.getByRole("switch", { name: /^Push/ });
  await push.focus();
  await page.keyboard.press("Space");
  await expect(push).toHaveAttribute("aria-checked", "false");
  await section.getByRole("button", { name: "Set reminder" }).click();
  await expect(
    section.getByRole("switch", { name: /Time for your workout/ }),
  ).toBeVisible();

  await page.goto("/body-measurements");
  await page.getByRole("button", { name: "Remind me" }).click();
  const dialog = page.getByRole("dialog", { name: "Remind me to measure" });
  await dialog
    .getByRole("group", { name: "Send it to" })
    .getByRole("switch", { name: /^Telegram/ })
    .click();
  await dialog.getByRole("button", { name: "Set reminder" }).click();
  await expect(dialog).toBeHidden();

  const writes = reminderWrites(await state(context));
  expect(writes.map((w) => w.body.channels)).toEqual([
    ["inApp", "telegram"],
    ["inApp", "push", "email"],
  ]);
});

test("on a phone, the channel switches fit the Remind me dialog", async ({
  context,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 740 });
  await signIn(context);
  await page.goto("/body-measurements");
  await page.getByRole("button", { name: "Remind me" }).click();
  const dialog = page.getByRole("dialog", { name: "Remind me to measure" });
  const email = dialog.getByRole("switch", { name: /^Email/ });
  await email.scrollIntoViewIfNeeded();
  const box = await email.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.x + box!.width).toBeLessThanOrEqual(360);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});
