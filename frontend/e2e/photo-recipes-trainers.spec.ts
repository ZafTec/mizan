import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";
const plateUrl = "https://files.fixture.test/meals/plate.jpg";
const yogurtAndOats = "33333333-3333-4333-8333-333333333333";

type State = {
  writes: { path: string; body: Record<string, unknown> }[];
  unhandled: string[];
  deletedPhotos: string[];
};

async function signIn(context: BrowserContext, options: { pro?: boolean }) {
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
async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
}
async function openMealLog(page: Page) {
  await page.goto("/today");
  await page.getByRole("button", { name: "Log an entry" }).first().click();
  await page.getByRole("button", { name: /^Meal/ }).click();
  await expect(
    page.getByRole("region", { name: "Describe your meal" }),
  ).toBeVisible();
}
// A small JPEG header is enough: the fixture answers any image with the same estimate.
const photo = {
  name: "plate.jpg",
  mimeType: "image/jpeg",
  buffer: Buffer.from([0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10]),
};

test("a photo estimate logs the matched recipe as that recipe, and its photo can be deleted now", async ({
  context,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 780 });
  await signIn(context, { pro: true });
  await openMealLog(page);

  await page.getByLabel("Meal photo").setInputFiles(photo);

  const meal = page.getByRole("region", { name: "Items in this meal" });
  await expect(meal).toContainText("Estimated from your photo");
  await expect(
    meal.getByLabel("Yogurt and oats quantity in servings"),
  ).toHaveValue("1.5");
  await expect(
    meal.getByLabel("Toast with butter quantity in grams"),
  ).toHaveValue("60");
  await expect(meal).toContainText("The photo is kept for 30 days.");
  await noOverflow(page);

  const remove = meal.getByRole("button", { name: "Delete it now" });
  await remove.focus();
  await page.keyboard.press("Enter");
  await expect(meal).toContainText("Photo deleted.");
  await expect(remove).toBeHidden();
  expect((await state(context)).deletedPhotos).toEqual([plateUrl]);

  await page.getByRole("button", { name: "Log meal" }).click();
  await expect(page.getByRole("dialog")).toBeHidden();
  const s = await state(context);
  expect(
    s.writes.filter((w) => w.path === "/api/Nutrition/log").map((w) => w.body),
  ).toEqual([
    expect.objectContaining({ recipeId: yogurtAndOats, servings: 1.5 }),
  ]);
  expect(s.unhandled).toEqual([]);
});

test("the recipe view starts with what fits what is left today, in the API's words", async ({
  context,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 780 });
  await signIn(context, { pro: true });
  await openMealLog(page);
  await page.getByRole("button", { name: "Log a recipe" }).click();

  const view = page.getByRole("region", { name: "Log from a recipe" });
  const fits = view.getByRole("group", { name: "Fits what is left" });
  await expect(view.getByRole("group").first()).toHaveAttribute(
    "aria-label",
    "Fits what is left",
  );
  await expect(fits).toContainText("Yogurt and oats");
  await expect(fits).toContainText("389 kcal / serving · leaves 251 kcal");
  await noOverflow(page);

  // A search already knows what it wants, so the ranking steps aside.
  await view.getByLabel("Search recipes").fill("lentil");
  await expect(fits).toBeHidden();
  await view.getByLabel("Search recipes").fill("");

  await fits.getByRole("button", { name: "Add Yogurt and oats" }).focus();
  await page.keyboard.press("Enter");
  await expect(view).toBeHidden();
  await expect(
    page
      .getByRole("region", { name: "Items in this meal" })
      .getByLabel("Yogurt and oats quantity in servings"),
  ).toHaveValue("1");
  expect((await state(context)).unhandled).toEqual([]);
});

test("the trainers to ask show no email, and an unnamed trainer is named as such", async ({
  context,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 780 });
  await signIn(context, {});
  await page.goto("/trainers");

  const list = page.getByTestId("trainers-page");
  await expect(
    list.getByRole("heading", { name: "Sam Rivera", level: 3 }),
  ).toBeVisible();
  await expect(
    list.getByRole("heading", { name: "Unnamed trainer", level: 3 }),
  ).toBeVisible();
  await expect(list).not.toContainText("@");
  await noOverflow(page);

  await page.getByTestId("search-input").focus();
  await page.keyboard.type("strength");
  await expect(list.getByRole("heading", { level: 3 })).toHaveText([
    "Sam Rivera",
  ]);
  expect((await state(context)).unhandled).toEqual([]);
});
