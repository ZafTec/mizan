import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import {
  ACTIVE_PLAN_ID,
  CLIENT_ID,
  COACH_ID,
  DRAFT_PLAN_ID,
  LIST_ID,
  TEMPLATE_ID,
  type PlanningOptions,
} from "./fixture-planning";
import type { components } from "../types/api.generated";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type Options = {
  empty?: boolean;
  role?: string;
  userId?: string;
  planning?: PlanningOptions;
};
type State = {
  unhandled: string[];
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
};

async function signIn(context: BrowserContext, options: Options = {}) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: options,
  });
  expect(result.ok()).toBeTruthy();
  return (await result.json()) as { today: string };
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
const writesTo = (s: State, pattern: RegExp) =>
  s.writes.filter((w) => pattern.test(w.path));

test("a person creates an empty plan from the form and lands on its detail", async ({
  page,
  context,
}) => {
  const { today } = await signIn(context);
  await page.goto("/meal-plan/new");
  await expect(page.getByTestId("meal-plan-new-page")).toBeVisible();
  await page.getByRole("radio", { name: "Empty plan" }).check();
  await page.getByLabel("Name").fill("Cut week");
  await page.getByLabel("Start date").fill(today);
  await page.getByLabel("Length (days)").fill("5");
  await page.getByLabel("Calories (kcal)").fill("1900");
  await page.getByLabel("Daily budget").fill("150");
  await page.getByLabel("Currency").fill("etb");
  await page.getByRole("button", { name: "Create plan" }).click();

  await page.waitForURL(/\/meal-plan\/[0-9a-f-]{36}$/);
  await expect(page.getByRole("heading", { name: "Cut week" })).toBeVisible();
  await expect(page.getByTestId("plan-facts")).toContainText("Active");
  await expect(page.getByRole("tab", { name: /, today$/ })).toBeVisible();
  await expect(
    page.getByRole("tablist", { name: "Days" }).getByRole("tab"),
  ).toHaveCount(5);

  const s = await state(context);
  const [write] = writesTo(s, /^\/api\/MealPlans$/);
  expect(write.body).toMatchObject({
    name: "Cut week",
    startDate: today,
    durationDays: 5,
    rules: {
      targetCalories: 1900,
      macroMode: "None",
      prepStyle: "Daily",
      dailyBudget: 150,
      currency: "ETB",
    },
    entries: [],
  });
  expect(s.unhandled).toEqual([]);
});

test("the rules form refuses a half-open eating window without calling the API", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto("/meal-plan/new");
  await page.getByRole("radio", { name: "Empty plan" }).check();
  await page.getByLabel("Eating window starts").fill("08:00");
  await page.getByRole("button", { name: "Create plan" }).click();
  await expect(page.getByText(/both a start and an end/)).toBeVisible();
  await expect(page).toHaveURL(/\/meal-plan\/new$/);
  const s = await state(context);
  expect(writesTo(s, /^\/api\/MealPlans/)).toEqual([]);
  expect(s.unhandled).toEqual([]);
});

test("a person logs a planned entry from the day view", async ({
  page,
  context,
}) => {
  const { today } = await signIn(context);
  await page.goto(`/meal-plan/${ACTIVE_PLAN_ID}`);
  await expect(
    page.getByRole("heading", { name: "Steady week" }),
  ).toBeVisible();
  // Today is day 3 of the plan; the view opens there.
  const todayTab = page.getByRole("tab", { name: /, today$/ });
  await expect(todayTab).toHaveAttribute("aria-selected", "true");
  const panel = page.getByRole("tabpanel");
  await expect(panel).toContainText("Greek yogurt");
  await expect(panel).toContainText("150 g");
  await expect(panel.getByText("Not loggable")).toBeVisible();
  // Each slot shows the total the API sent for it; the page adds nothing up.
  const plan = (await (
    await context.request.get(`${apiURL}/api/MealPlans/${ACTIVE_PLAN_ID}`)
  ).json()) as components["schemas"]["MealPlanDetailDto"];
  const day = plan.days.find((d) => d.date === today)!;
  expect(day.slots.length).toBeGreaterThan(0);
  await expect(panel.getByTestId("slot-calories")).toHaveText(
    day.slots.map(
      (slot) => `${Math.round(slot.totals.calories).toLocaleString()} kcal`,
    ),
  );

  await panel.getByRole("button", { name: "Log Greek yogurt" }).click();
  await expect(panel.getByLabel("Greek yogurt logged")).toBeVisible();
  await expect(
    panel.getByRole("button", { name: "Log Greek yogurt" }),
  ).toHaveCount(0);

  const s = await state(context);
  const logs = writesTo(s, /\/entries\/[^/]+\/log$/);
  expect(logs).toHaveLength(1);
  expect(logs[0].path).toContain(`/api/MealPlans/${ACTIVE_PLAN_ID}/entries/`);
  expect(s.unhandled).toEqual([]);
});

test("Today shows the plan for the day and logs an entry in place", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto("/today");
  const plan = page.getByRole("region", { name: "Today's plan" });
  await expect(plan).toBeVisible();
  await expect(plan.getByRole("link", { name: "Steady week" })).toHaveAttribute(
    "href",
    `/meal-plan/${ACTIVE_PLAN_ID}`,
  );
  await expect(plan).toContainText("0 of 3 logged");
  await expect(plan).toContainText("eating window closes at 20:00");

  await plan.getByRole("button", { name: "Log Yogurt and oats" }).click();
  await expect(plan.getByLabel("Yogurt and oats logged")).toBeVisible();
  await expect(plan).toContainText("1 of 3 logged");
  // The diary below picks up the logged meal.
  await expect(page.getByText("Yogurt and oats").first()).toBeVisible();

  const s = await state(context);
  expect(writesTo(s, /\/entries\/[^/]+\/log$/)).toHaveLength(1);
  expect(s.unhandled).toEqual([]);
});

test("Today shows nothing about plans when there is no active plan", async ({
  page,
  context,
}) => {
  await signIn(context, { planning: { none: true } });
  await page.goto("/today");
  await expect(page.getByRole("heading", { name: "Today" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Today's plan" })).toHaveCount(
    0,
  );
  const s = await state(context);
  expect(s.unhandled).toEqual([]);
});

test("a person makes a shopping list from the plan and ticks an item", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto(`/meal-plan/${ACTIVE_PLAN_ID}`);
  await page.getByRole("button", { name: "Make shopping list" }).click();
  await page.waitForURL(/\/shopping-lists\/[0-9a-f-]{36}$/);
  await expect(
    page.getByRole("heading", { name: "Steady week shopping" }),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: /Steady week/ })).toBeVisible();

  // Items group by cooking session, since the plan has one.
  await expect(
    page.getByRole("region", { name: "Sunday batch" }),
  ).toBeVisible();
  await expect(page.getByRole("region", { name: "Any time" })).toBeVisible();
  await expect(page.getByText("Check", { exact: true })).toBeVisible();

  await expect(page.locator("[data-hydrated]")).toBeVisible();
  const left = page.getByTestId("items-left");
  const before = await left.textContent();
  // The plan's own 150 g of yogurt sits outside the batch session.
  const box = page
    .getByRole("region", { name: "Any time" })
    .getByRole("checkbox", { name: /Greek yogurt/ });
  await box.check();
  await expect(box).toBeChecked();
  await expect(left).not.toHaveText(before ?? "");

  const s = await state(context);
  const generated = writesTo(s, /\/shopping-list$/);
  expect(generated).toHaveLength(1);
  const toggles = writesTo(s, /\/api\/ShoppingLists\/items\/[^/]+\/toggle$/);
  expect(toggles).toHaveLength(1);
  expect(toggles[0].body).toEqual({ isChecked: true });
  expect(s.unhandled).toEqual([]);
});

test("a shopping list reloads with the tick kept and clears what is checked", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto(`/shopping-lists/${LIST_ID}`);
  await expect(page.getByTestId("items-left")).toHaveText("2 left of 3");
  await expect(page.locator("[data-hydrated]")).toBeVisible();
  await page.getByRole("checkbox", { name: /Greek yogurt/ }).check();
  await expect(page.getByTestId("items-left")).toHaveText("1 left of 3");
  await page.reload();
  await expect(page.getByTestId("items-left")).toHaveText("1 left of 3");
  await expect(page.locator("[data-hydrated]")).toBeVisible();
  await page.getByRole("button", { name: "Clear checked" }).click();
  await expect(page.getByTestId("items-left")).toHaveText("1 left of 1");
  await expect(
    page.getByRole("checkbox", { name: /Greek yogurt/ }),
  ).toHaveCount(0);

  await page.goto("/meal-plan/shopping-list");
  await expect(page.getByTestId("shopping-lists-page")).toBeVisible();
  await expect(
    page.getByRole("link", { name: /Steady week shopping/ }),
  ).toHaveAttribute("href", `/shopping-lists/${LIST_ID}`);
  const s = await state(context);
  expect(writesTo(s, /\/clear-checked$/)).toHaveLength(1);
  expect(s.unhandled).toEqual([]);
});

test("a person starts a plan from a template", async ({ page, context }) => {
  const { today } = await signIn(context);
  await page.goto("/meal-plan/new");
  await expect(
    page.getByRole("radio", { name: "From a template" }),
  ).toBeChecked();
  const templates = page.getByRole("radiogroup", { name: "Templates" });
  await expect(templates).toContainText("Simple week");
  await expect(templates).toContainText("7 days · 7 entries");
  await templates.getByRole("radio", { name: /Simple week/ }).check();
  await page.getByLabel("Name").fill("Next week");
  await page.getByLabel("Start date").fill(today);
  await page.getByRole("button", { name: "Create plan" }).click();

  await page.waitForURL(/\/meal-plan\/[0-9a-f-]{36}$/);
  await expect(page.getByRole("heading", { name: "Next week" })).toBeVisible();
  await expect(page.getByRole("tabpanel")).toContainText("Yogurt and oats");

  const s = await state(context);
  const [applied] = writesTo(s, /\/templates\/[^/]+\/apply$/);
  expect(applied.path).toBe(`/api/MealPlans/templates/${TEMPLATE_ID}/apply`);
  expect(applied.body).toMatchObject({ startDate: today, name: "Next week" });
  expect(s.unhandled).toEqual([]);
});

test("a coach's draft offers Accept, and accepting makes it the active plan", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto("/meal-plan");
  await expect(page.getByTestId("meal-plan-page")).toBeVisible();
  const active = page.getByRole("region", { name: "Active" });
  await expect(active).toContainText("Steady week");
  const drafts = page.getByRole("region", { name: "From your coach" });
  await expect(drafts).toContainText("Coach's cut");
  await expect(drafts).toContainText("Dana Coach");

  await page.goto(`/meal-plan/${DRAFT_PLAN_ID}`);
  await expect(page.getByTestId("plan-facts")).toContainText("Draft");
  await expect(page.getByTestId("plan-facts")).toContainText("From Dana Coach");
  await expect(page.getByRole("link", { name: "Edit" })).toHaveCount(0);
  await page.getByRole("button", { name: "Accept Coach's cut" }).click();
  await expect(page.getByTestId("plan-facts")).toContainText("Active");
  await expect(
    page.getByRole("button", { name: "Accept Coach's cut" }),
  ).toHaveCount(0);

  await page.goto("/meal-plan");
  await expect(page.getByRole("region", { name: "Active" })).toContainText(
    "Coach's cut",
  );
  await expect(page.getByRole("region", { name: "Past plans" })).toContainText(
    "Steady week",
  );
  await expect(
    page.getByRole("region", { name: "From your coach" }),
  ).toHaveCount(0);

  const s = await state(context);
  // Accepting is a write a person may retry, so it carries an Idempotency-Key.
  expect(writesTo(s, /\/accept$/)).toEqual([
    {
      path: `/api/MealPlans/${DRAFT_PLAN_ID}/accept`,
      body: {},
      key: expect.any(String),
    },
  ]);
  expect(s.unhandled).toEqual([]);
});

test("the day tabs move with the arrow keys", async ({ page, context }) => {
  await signIn(context);
  await page.goto(`/meal-plan/${ACTIVE_PLAN_ID}`);
  const tabs = page.getByRole("tablist", { name: "Days" }).getByRole("tab");
  await expect(tabs).toHaveCount(7);
  const selected = page.getByRole("tab", { name: /, today$/ });
  await selected.focus();
  await page.keyboard.press("ArrowRight");
  const next = tabs.nth(3);
  await expect(next).toHaveAttribute("aria-selected", "true");
  await expect(next).toBeFocused();
  await expect(page.getByRole("tabpanel")).toContainText("Rolled oats");
  await page.keyboard.press("Home");
  await expect(tabs.nth(0)).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("tabpanel")).toContainText("Yogurt and oats");
  const s = await state(context);
  expect(s.unhandled).toEqual([]);
});

test("the plan pages fit a phone without sideways scrolling", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/meal-plan");
  await expect(page.getByTestId("meal-plan-page")).toBeVisible();
  await expect(page.getByRole("link", { name: "New plan" })).toBeVisible();
  await noOverflow(page);
  await page.goto(`/meal-plan/${ACTIVE_PLAN_ID}`);
  await expect(page.getByTestId("meal-plan-detail-page")).toBeVisible();
  await noOverflow(page);
  await page.goto(`/shopping-lists/${LIST_ID}`);
  await expect(page.getByTestId("shopping-list-page")).toBeVisible();
  await noOverflow(page);
  await page.goto("/meal-plan/new");
  await expect(page.getByTestId("meal-plan-new-page")).toBeVisible();
  await noOverflow(page);
  const s = await state(context);
  expect(s.unhandled).toEqual([]);
});

test("a person edits a plan: a new entry and a cooking session", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto(`/meal-plan/${ACTIVE_PLAN_ID}/edit`);
  await expect(page.getByTestId("meal-plan-edit-page")).toBeVisible();

  await page.getByRole("button", { name: "Add session" }).click();
  await page.getByLabel("Title").fill("Wednesday top-up");
  await page.getByRole("button", { name: "Save session" }).click();
  await expect(page.getByText("Wednesday top-up")).toBeVisible();

  await page.getByRole("button", { name: "Add entry" }).click();
  await page.getByRole("radio", { name: "Just text" }).check();
  await page.getByLabel("What to eat").fill("Leftovers");
  await page.getByRole("button", { name: "Save entry" }).click();
  await expect(page.getByText("Leftovers")).toBeVisible();
  await page.getByRole("button", { name: "Remove Leftovers" }).click();
  await expect(page.getByText("Leftovers")).toHaveCount(0);

  const s = await state(context);
  expect(writesTo(s, /\/prep-sessions$/)).toHaveLength(1);
  const entries = writesTo(s, /\/entries(\/[^/]+)?$/);
  expect(entries.map((w) => w.body)).toContainEqual(
    expect.objectContaining({ kind: "Text", text: "Leftovers" }),
  );
  expect(s.unhandled).toEqual([]);
});

test.describe("a coach drafts for a client", () => {
  test("the form sends the draft when the client allows planning", async ({
    page,
    context,
  }) => {
    const { today } = await signIn(context, {
      role: "trainer",
      userId: COACH_ID,
    });
    await page.goto(`/trainer/clients/${CLIENT_ID}/plan`);
    await expect(page.getByTestId("trainer-client-plan-page")).toBeVisible();
    // The global template is offered first; this draft starts empty.
    await expect(
      page.getByRole("radio", { name: "From a template" }),
    ).toBeChecked();
    await page.getByRole("radio", { name: "Empty plan" }).check();
    await page.getByLabel("Name").fill("Sam's cut");
    await page.getByLabel("Length (days)").fill("10");
    await page.getByRole("button", { name: "Send draft" }).click();
    await expect(page.getByRole("status")).toContainText(
      "Draft sent to Sam Rivera",
    );
    const s = await state(context);
    const [write] = writesTo(s, /\/api\/Trainers\/clients\/[^/]+\/meal-plans$/);
    expect(write.path).toBe(`/api/Trainers/clients/${CLIENT_ID}/meal-plans`);
    expect(write.body).toMatchObject({
      name: "Sam's cut",
      startDate: today,
      durationDays: 10,
    });
    expect(s.unhandled).toEqual([]);
  });

  test("the page says so when the client has not granted planning", async ({
    page,
    context,
  }) => {
    await signIn(context, {
      role: "trainer",
      userId: COACH_ID,
      planning: { clientGrantsPlanning: false },
    });
    await page.goto(`/trainer/clients/${CLIENT_ID}/plan`);
    await expect(
      page.getByText("Sam Rivera has not granted meal planning."),
    ).toBeVisible();
    await expect(page.getByRole("button", { name: "Send draft" })).toHaveCount(
      0,
    );
    const s = await state(context);
    expect(s.unhandled).toEqual([]);
  });
});

test.describe("templates", () => {
  test("an administrator creates and deletes a global template", async ({
    page,
    context,
  }) => {
    await signIn(context, { role: "admin" });
    await page.goto("/admin/meal-plans");
    await expect(
      page.getByRole("link", { name: "Meal plans", exact: true }).first(),
    ).toHaveAttribute("href", "/admin/meal-plans");
    const list = page.getByRole("region", { name: "Templates" });
    await expect(
      list.getByRole("listitem", { name: "Simple week" }),
    ).toContainText("7 days · 7 entries");

    await page.getByRole("button", { name: "New template" }).click();
    const form = page.getByRole("form", { name: "New template" });
    await expect(form.getByLabel("Name")).toBeFocused();
    await form.getByRole("button", { name: "Create template" }).click();
    await expect(form.getByText("Give the template a name")).toBeVisible();
    await form.getByLabel("Name").fill("Keto starter");
    await form.getByLabel("Length (days)").fill("5");
    await form.getByRole("button", { name: "Create template" }).click();

    await page.waitForURL(/\/admin\/meal-plans\/[0-9a-f-]{36}$/);
    await expect(
      page.getByRole("heading", { name: "Edit Keto starter" }),
    ).toBeVisible();
    await expect(page.getByLabel("Start date")).toHaveCount(0);
    await page.getByRole("button", { name: "Add entry" }).click();
    const entry = page.getByRole("form", { name: "New entry" });
    await entry.getByRole("radio", { name: "Just text" }).check();
    await entry.getByLabel("What to eat").fill("Eggs and greens");
    await entry.getByRole("button", { name: "Save entry" }).click();
    await expect(page.getByText("Eggs and greens")).toBeVisible();
    await page.getByLabel("Description").fill("Five low-carb days.");
    await page.getByRole("button", { name: "Save template" }).click();
    await expect(page.getByText("Template saved")).toBeVisible();

    await page
      .getByRole("link", { name: "Meal plans", exact: true })
      .last()
      .click();
    await page.waitForURL(/\/admin\/meal-plans$/);
    const keto = list.getByRole("listitem", { name: "Keto starter" });
    await expect(keto).toContainText("Five low-carb days.");
    await expect(keto).toContainText("5 days · 1 entry");
    await page.screenshot({
      path: "test-results/admin-meal-plans.png",
      fullPage: true,
    });
    await keto.getByRole("button", { name: "Delete Keto starter" }).click();
    const dialog = page.getByRole("dialog", { name: "Delete Keto starter?" });
    await expect(dialog.getByRole("button", { name: "Cancel" })).toBeFocused();
    await dialog.getByRole("button", { name: "Delete" }).click();
    await expect(keto).toHaveCount(0);

    const s = await state(context);
    const [created] = writesTo(s, /^\/api\/MealPlans\/templates$/);
    expect(created.body).toMatchObject({
      name: "Keto starter",
      durationDays: 5,
      global: true,
    });
    const id = s.writes
      .map((w) => w.path.match(/^\/api\/MealPlans\/([0-9a-f-]{36})\/entries$/))
      .find(Boolean)?.[1];
    expect(id).toEqual(expect.any(String));
    expect(
      writesTo(s, new RegExp(`^/api/MealPlans/templates/${id}$`)).map(
        (w) => w.body,
      ),
    ).toEqual([
      expect.objectContaining({
        name: "Keto starter",
        description: "Five low-carb days.",
      }),
      {},
    ]);
    expect(s.unhandled).toEqual([]);
  });

  test("a coach creates a template and drafts a client's plan from it", async ({
    page,
    context,
  }) => {
    const { today } = await signIn(context, {
      role: "trainer",
      userId: COACH_ID,
    });
    await page.goto("/trainer");
    await page.getByRole("link", { name: "Meal plan templates" }).click();
    await page.waitForURL(/\/trainer\/templates$/);
    await expect(page.getByText("You have no templates yet.")).toBeVisible();
    await page.getByRole("button", { name: "New template" }).click();
    const form = page.getByRole("form", { name: "New template" });
    await form.getByLabel("Name").fill("Lean three days");
    await form.getByLabel("Length (days)").fill("3");
    await form.getByLabel("Calories (kcal)").fill("2100");
    await form.getByRole("button", { name: "Create template" }).click();

    await page.waitForURL(/\/trainer\/templates\/[0-9a-f-]{36}$/);
    await page.getByRole("button", { name: "Add entry" }).click();
    const entry = page.getByRole("form", { name: "New entry" });
    await entry.getByRole("radio", { name: "Just text" }).check();
    await entry.getByLabel("What to eat").fill("Chicken and rice");
    await entry.getByRole("button", { name: "Save entry" }).click();
    await expect(page.getByText("Chicken and rice")).toBeVisible();
    await page.getByRole("link", { name: "Templates", exact: true }).click();
    await expect(
      page.getByRole("listitem", { name: "Lean three days" }),
    ).toContainText("3 days · 1 entry");

    await page.goto(`/trainer/clients/${CLIENT_ID}/plan`);
    const templates = page.getByRole("radiogroup", { name: "Templates" });
    await expect(templates.getByRole("radio")).toHaveCount(2);
    await templates.getByRole("radio", { name: /Lean three days/ }).check();
    await page.getByLabel("Start date").fill(today);
    await page.getByRole("button", { name: "Send draft" }).click();
    await expect(page.getByRole("status")).toContainText(
      "Draft sent to Sam Rivera",
    );

    const s = await state(context);
    const [kept] = writesTo(s, /^\/api\/MealPlans\/templates$/);
    expect(kept.body).toMatchObject({
      name: "Lean three days",
      durationDays: 3,
      global: false,
      rules: expect.objectContaining({ targetCalories: 2100 }),
    });
    const [draft] = writesTo(s, /\/meal-plans\/from-template$/);
    expect(draft.path).toBe(
      `/api/Trainers/clients/${CLIENT_ID}/meal-plans/from-template`,
    );
    expect(draft.body).toMatchObject({ startDate: today, name: null });
    expect(draft.body.templateId).toMatch(/^[0-9a-f-]{36}$/);
    expect(s.unhandled).toEqual([]);
  });

  test("a coach's templates fit a phone", async ({ page, context }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await signIn(context, { role: "trainer", userId: COACH_ID });
    await page.goto("/trainer/templates");
    await page.getByRole("button", { name: "New template" }).click();
    await expect(
      page.getByRole("form", { name: "New template" }).getByLabel("Name"),
    ).toBeFocused();
    await noOverflow(page);
    await page.screenshot({
      path: "test-results/trainer-templates-phone.png",
      fullPage: true,
    });
    expect((await state(context)).unhandled).toEqual([]);
  });
});
