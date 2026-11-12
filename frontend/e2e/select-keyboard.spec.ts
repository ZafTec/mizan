import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type State = {
  unhandled: string[];
  writes: { path: string; body: Record<string, unknown> }[];
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

test.afterEach(async ({ context }) => {
  expect((await state(context)).unhandled).toEqual([]);
});

/** The Remind me dialog on the measurements page: its "How often" is a short dropdown. */
async function openReminderForm(page: Page) {
  await page.goto("/body-measurements");
  await page.getByRole("button", { name: "Remind me" }).click();
  const dialog = page.getByRole("dialog", { name: "Remind me to measure" });
  return dialog.getByRole("combobox", { name: "How often" });
}

test("a person picks from a dropdown with the keyboard alone", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.setViewportSize({ width: 1280, height: 800 });
  const select = await openReminderForm(page);
  await expect(select).toHaveText("Every day");
  await expect(select).toHaveAttribute("aria-haspopup", "listbox");

  await select.focus();
  await page.keyboard.press("ArrowDown");
  await expect(select).toHaveAttribute("aria-expanded", "true");
  const list = page.getByRole("listbox", { name: "How often" });
  await expect(list).toBeVisible();
  // Focus stays on the button; the active option is announced through it.
  await expect(select).toBeFocused();
  const active = async () =>
    page
      .locator(`[id="${await select.getAttribute("aria-activedescendant")}"]`)
      .textContent();
  await expect(list.getByRole("option", { selected: true })).toHaveText(
    "Every day",
  );
  expect(await active()).toBe("Every day");

  // A letter moves to the next option that starts with it; again, to the one after.
  await page.keyboard.press("w");
  await expect.poll(active).toBe("Weekdays");
  await page.waitForTimeout(600);
  await page.keyboard.press("w");
  await expect.poll(active).toBe("Weekends");
  await page.keyboard.press("End");
  await expect.poll(active).toBe("Every few hours");
  await page.keyboard.press("ArrowUp");
  await expect.poll(active).toBe("Once");
  await page.keyboard.press("Enter");
  await expect(list).toBeHidden();
  await expect(select).toBeFocused();
  await expect(select).toHaveText("Once");

  // Escape closes without changing anything.
  await page.keyboard.press("Home");
  await expect(list).toBeVisible();
  await expect.poll(active).toBe("Every day");
  await page.keyboard.press("Escape");
  await expect(list).toBeHidden();
  await expect(select).toBeFocused();
  await expect(select).toHaveText("Once");
});

test("on a phone the list is a sheet from the bottom that takes the keyboard too", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.setViewportSize({ width: 375, height: 740 });
  const select = await openReminderForm(page);
  await expect(select).toHaveAttribute("aria-haspopup", "dialog");

  await select.click();
  const sheet = page.getByRole("dialog", { name: "How often" });
  await expect(sheet).toBeVisible();
  const list = sheet.getByRole("listbox", { name: "How often" });
  await expect(list).toBeFocused();
  // The sheet sits on the bottom edge and spans the screen.
  await expect
    .poll(async () => {
      const box = await sheet.boundingBox();
      return Math.round((box?.y ?? 0) + (box?.height ?? 0));
    })
    .toBe(740);
  expect((await sheet.boundingBox())?.width).toBe(375);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);

  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  await expect(sheet).toBeHidden();
  await expect(select).toBeFocused();
  await expect(select).toHaveText("Weekends");

  // A tap on an option picks it; Escape closes and gives focus back.
  await select.click();
  await sheet.getByRole("option", { name: "Once a month" }).click();
  await expect(sheet).toBeHidden();
  await expect(select).toHaveText("Once a month");
  await select.click();
  await expect(sheet).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(sheet).toBeHidden();
  // Only the sheet closed; the dialog it opened from is still there.
  await expect(select).toBeVisible();
  await expect(select).toBeFocused();
});
