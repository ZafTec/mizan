import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";
const OATS_ID = "22222222-2222-4222-8222-222222222222";

type State = { unhandled: string[]; writes: { path: string }[] };

async function signIn(context: BrowserContext, options: object = {}) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: options,
  });
  expect(result.ok()).toBeTruthy();
}
async function state(context: BrowserContext) {
  return (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json() as Promise<State>;
}

/** The element's box lies wholly inside the viewport. */
async function inView(page: Page, selector: ReturnType<Page["locator"]>) {
  const box = await selector.boundingBox();
  const viewport = page.viewportSize();
  expect(box).not.toBeNull();
  expect(box!.y).toBeGreaterThanOrEqual(0);
  expect(box!.y + box!.height).toBeLessThanOrEqual(viewport!.height);
}

test("a long form keeps its title and actions in view while its fields scroll", async ({
  context,
  page,
}) => {
  await signIn(context);
  // A short phone screen, as when the keyboard is up.
  await page.setViewportSize({ width: 375, height: 480 });
  await page.goto(`/ingredients/${OATS_ID}`);
  await page
    .getByRole("region", { name: "Price" })
    .getByRole("button", { name: "Report a wrong price" })
    .click();
  const dialog = page.getByRole("dialog", { name: "Report a wrong price" });
  await expect(dialog).toBeVisible();

  const body = dialog.locator("[data-modal-body]");
  expect(await body.evaluate((el) => el.scrollHeight > el.clientHeight)).toBe(
    true,
  );
  const title = dialog.getByRole("heading", { name: "Report a wrong price" });
  const send = dialog.getByRole("button", { name: "Send report" });
  await inView(page, title);
  await inView(page, send);

  await body.evaluate((el) => el.scrollTo({ top: el.scrollHeight }));
  await expect
    .poll(() => body.evaluate((el) => el.scrollTop))
    .toBeGreaterThan(0);
  await inView(page, title);
  await inView(page, send);
  // The page behind does not scroll sideways or move.
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);

  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  expect((await state(context)).unhandled).toEqual([]);
});

test("signing out asks first, and Cancel keeps the person signed in", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto("/today");
  await page.getByRole("button", { name: "Account" }).click();
  const menuItem = page.getByRole("button", { name: "Sign out" });
  await menuItem.click();

  const dialog = page.getByRole("alertdialog", { name: "Sign out?" });
  await expect(dialog).toHaveAttribute("aria-modal", "true");
  const cancel = dialog.getByRole("button", { name: "Cancel" });
  await expect(cancel).toBeFocused();
  // Enter on the focused Cancel closes the dialog and changes nothing.
  await page.keyboard.press("Enter");
  await expect(dialog).toBeHidden();
  await expect(page).toHaveURL(/\/today$/);

  const s = await state(context);
  expect(s.writes.filter((w) => /sign-?out/i.test(w.path))).toEqual([]);
  expect(s.unhandled).toEqual([]);
});

test("a person opens an earned achievement to see its badge", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto("/achievements");

  const earned = page.getByRole("region", { name: "Earned" });
  const row = earned.getByRole("button", { name: /First Meal/ });
  await row.click();
  const dialog = page.getByRole("dialog", { name: "First Meal" });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByTestId("achievement-badge")).toHaveAttribute(
    "src",
    "/badges/512/achievement-first-meal.png",
  );
  await expect(dialog).toContainText("Log your first meal");
  await expect(dialog).toContainText("10");
  await expect(dialog).toContainText("nutrition");
  await inView(page, dialog.getByRole("heading", { name: "First Meal" }));

  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await expect(row).toBeFocused();

  // What is still to earn is a plain row, not a button.
  const toEarn = page.getByRole("region", { name: "Still to earn" });
  await expect(toEarn.getByText("Getting Started")).toBeVisible();
  await expect(toEarn.getByRole("button")).toHaveCount(0);

  await page.setViewportSize({ width: 1280, height: 800 });
  await row.click();
  await expect(dialog).toBeVisible();
  await inView(page, dialog);
  await dialog.getByRole("button", { name: "Close" }).click();
  await expect(dialog).toBeHidden();
  expect((await state(context)).unhandled).toEqual([]);
});
