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
      schedule: { repeat: "daily", time: "07:00" },
    },
  ]);
});
