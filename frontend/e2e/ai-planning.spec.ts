import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { PLAN_PROMPT, type AiPlanningOptions } from "./fixture-ai-planning";
import { LIST_ID } from "./fixture-planning";

/**
 * The web surface of assistant planning (docs/PLANNING.md#web): drafting a
 * plan from /meal-plan/new, keeping or discarding it, the allowance and the
 * provider failing, consent, typed cards in a chat turn and the planning line
 * of the usage panel. The fixture is synthetic; the API's own rules are
 * covered by the backend's MealPlanGenerationTests.
 */
const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type State = {
  unhandled: string[];
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
};

async function signIn(context: BrowserContext, aiPlanning: AiPlanningOptions) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: { aiPlanning },
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
// Every page here renders the same on the server and in the browser: a
// hydration error in the console or on the page fails the story.
let hydration: string[] = [];
test.beforeEach(({ page }) => {
  hydration = [];
  page.on("console", (message) => {
    if (message.type() === "error" && /hydrat/i.test(message.text()))
      hydration.push(message.text());
  });
  page.on("pageerror", (error) => {
    if (/hydrat/i.test(error.message)) hydration.push(error.message);
  });
});
test.afterEach(() => {
  expect(hydration).toEqual([]);
});

const writesTo = (s: State, pattern: RegExp) =>
  s.writes.filter((w) => pattern.test(w.path));
const assistant = (page: Page) =>
  page.getByRole("region", { name: "Draft with the assistant" });

async function openDraftForm(page: Page) {
  await page.goto("/meal-plan/new");
  await assistant(page)
    .getByRole("button", { name: "Draft with the assistant" })
    .click();
  await expect(assistant(page).getByLabel("Start date")).toBeFocused();
}

test("a person drafts a week with the assistant and keeps it", async ({
  page,
  context,
}) => {
  const { today } = await signIn(context, { consented: true });
  await openDraftForm(page);
  const section = assistant(page);
  await section.getByLabel("Days").fill("5");
  await section.getByLabel("Weekly budget (optional)").fill("1500");
  await section.getByLabel("Daily budget (optional)").fill("250");
  await section.getByLabel("What you would like (optional)").fill("no pork");
  await section.getByRole("button", { name: "Draft plan" }).click();

  const preview = section.getByTestId("draft-preview");
  await expect(
    preview.getByRole("heading", { name: "High-protein week" }),
  ).toBeFocused();
  await expect(preview.getByTestId("draft-facts")).toContainText(
    "Drafted by the assistant",
  );
  await expect(preview).toContainText("Yogurt and oats");
  await expect(
    preview.getByRole("region", { name: "To review" }),
  ).toContainText("Shiro wat with injera");
  const cost = preview.getByTestId("plan-cost-table");
  await expect(cost).toContainText("ETB 1,180.00");
  await expect(cost).toContainText("of ETB 1,500.00 budget");
  await expect(cost).toContainText("67%");
  await expect(cost.getByRole("link", { name: "Rolled oats" })).toBeVisible();

  const s = await state(context);
  const [write] = writesTo(s, /^\/api\/MealPlans\/generate$/);
  // The form sends what was typed; the weekly budget is the one ceiling and
  // the daily one goes with it for the API to check each day against.
  expect(write.body).toMatchObject({
    startDate: today,
    durationDays: 5,
    preference: "no pork",
    budgetCeilings: [1500],
    dailyBudget: 250,
  });
  expect(write.key).toEqual(expect.any(String));

  await preview.getByRole("link", { name: "Keep High-protein week" }).click();
  await page.waitForURL(/\/meal-plan\/[0-9a-f-]{36}$/);
  await expect(page.getByTestId("plan-facts")).toContainText("Draft");
  await expect(page.getByTestId("plan-facts")).toContainText(
    "Drafted by the assistant",
  );
  await expect(
    page.getByRole("button", { name: "Accept High-protein week" }),
  ).toBeVisible();

  await page.goto("/meal-plan");
  await expect(
    page.getByRole("link", { name: /High-protein week/ }),
  ).toContainText("Drafted by the assistant");
  expect((await state(context)).unhandled).toEqual([]);
});

test("a person drafts with the keyboard on a phone and discards the draft", async ({
  page,
  context,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(context, { consented: true });
  await page.goto("/meal-plan/new");
  const section = assistant(page);
  const open = section.getByRole("button", {
    name: "Draft with the assistant",
  });
  await open.focus();
  await page.keyboard.press("Enter");
  await expect(section.getByLabel("Start date")).toBeFocused();
  await section.getByLabel("Days").focus();
  await page.keyboard.press("ControlOrMeta+a");
  await page.keyboard.type("3");
  await page.keyboard.press("Enter");

  const preview = section.getByTestId("draft-preview");
  await expect(preview).toBeVisible();
  await noOverflow(page);
  const discard = preview.getByRole("button", {
    name: "Discard High-protein week",
  });
  await discard.focus();
  await page.keyboard.press("Enter");

  const status = section.getByRole("status");
  await expect(status).toBeAttached();
  await expect(status).toHaveText("Draft discarded.");
  await expect(status).toBeFocused({ timeout: 10_000 });
  await expect(preview).toBeHidden();
  const s = await state(context);
  const [generated] = writesTo(s, /^\/api\/MealPlans\/generate$/);
  expect(generated.body).toMatchObject({ durationDays: 3, budgetCeilings: [] });
  const declines = writesTo(s, /^\/api\/MealPlans\/[0-9a-f-]{36}\/decline$/);
  expect(declines).toHaveLength(1);
  expect(declines[0].key).toEqual(expect.any(String));

  await page.goto("/meal-plan");
  const list = page.getByTestId("meal-plan-page");
  await expect(list.getByRole("heading", { name: "Drafts" })).toHaveCount(0);
  await expect(
    list
      .getByRole("region", { name: "Past plans" })
      .getByText("High-protein week"),
  ).toBeVisible();
  expect((await state(context)).unhandled).toEqual([]);
});

test("a spent planning allowance says so and when it resets", async ({
  page,
  context,
}) => {
  await signIn(context, { consented: true, plansUsed: 1 });
  await openDraftForm(page);
  const section = assistant(page);
  await section.getByRole("button", { name: "Draft plan" }).click();

  const alert = section.getByRole("alert");
  await expect(alert).toContainText(
    "You have used your AI allowance for now. It resets at 00:00 UTC.",
  );
  await expect(alert).toContainText("Your planning allowance resets at");
  await expect(section.getByTestId("draft-preview")).toHaveCount(0);
  await expect(
    section.getByRole("button", { name: "Draft plan" }),
  ).toBeEnabled();
  expect((await state(context)).unhandled).toEqual([]);
});

test("an unavailable assistant shows a hint to try again", async ({
  page,
  context,
}) => {
  await signIn(context, { consented: true, providerDown: true });
  await openDraftForm(page);
  const section = assistant(page);
  await section.getByRole("button", { name: "Draft plan" }).click();

  const alert = section.getByRole("alert");
  await expect(alert).toContainText(
    "The assistant could not draft a plan right now.",
  );
  await expect(alert).toContainText("Try again in a moment.");
  await section.getByRole("button", { name: "Try again" }).click();
  await expect(alert).toContainText("Try again in a moment.");

  // The same request tried again carries the same key, so a lost answer replays.
  const keys = writesTo(
    await state(context),
    /^\/api\/MealPlans\/generate$/,
  ).map((w) => w.key);
  expect(keys).toHaveLength(2);
  expect(keys[0]).toBe(keys[1]);
  expect((await state(context)).unhandled).toEqual([]);
});

test("without consent the assistant's way to grant it replaces the action", async ({
  page,
  context,
}) => {
  await signIn(context, { consented: false });
  await page.goto("/meal-plan/new");
  const section = assistant(page);
  await expect(section.getByTestId("ai-consent-prompt")).toBeVisible();
  await expect(
    section.getByRole("button", { name: "Draft with the assistant" }),
  ).toHaveCount(0);
  await section
    .getByRole("link", { name: "Choose what the assistant can see" })
    .click();
  await page.waitForURL(/\/profile\/settings$/);
  expect(
    writesTo(await state(context), /^\/api\/MealPlans\/generate$/),
  ).toHaveLength(0);
  expect((await state(context)).unhandled).toEqual([]);
});

test("a planning chat turn shows its plan, day, cost and list as cards", async ({
  page,
  context,
}) => {
  await signIn(context, { consented: true });
  await page.goto("/ai");
  await page.getByLabel("Message the assistant").fill(`Please ${PLAN_PROMPT}`);
  await page.keyboard.press("Enter");

  const reply = page
    .getByRole("article", { name: "The assistant replied" })
    .last();
  await expect(reply).toContainText("Here is a draft for next week.");
  const plan = reply.getByRole("region", { name: "Plan High-protein week" });
  await expect(plan).toContainText("Draft");
  await expect(plan).toContainText("Cost a week ETB 1,180.00");
  await expect(plan).toContainText("1 to review");
  await expect(plan).toContainText("Two lines have no price");
  const planHref = await plan
    .getByRole("link", { name: "Open the plan" })
    .getAttribute("href");
  expect(planHref).toMatch(/^\/meal-plan\/[0-9a-f-]{36}$/);

  const day = reply.getByRole("region", { name: /^High-protein week, / });
  await expect(day).toContainText("Yogurt and oats");
  await expect(
    day.getByRole("link", { name: "Open the plan" }),
  ).toHaveAttribute("href", planHref!);
  await expect(reply.getByRole("region", { name: "Plan cost" })).toContainText(
    "ETB 1,500.00 budget",
  );
  const list = reply.getByRole("region", { name: "Shopping list" });
  await expect(list).toContainText("6 items · 1 to check");
  await expect(
    list.getByRole("link", { name: "Open the list" }),
  ).toHaveAttribute("href", `/shopping-lists/${LIST_ID}`);
  // An unknown card type is left out; the text stands alone.
  await expect(reply).not.toContainText("Not rendered");

  await plan.getByRole("link", { name: "Open the plan" }).click();
  await page.waitForURL(planHref!);
  await expect(page.getByTestId("plan-facts")).toContainText(
    "Drafted by the assistant",
  );
  expect((await state(context)).unhandled).toEqual([]);
});

test("a week drafted in chat is listed as a conversation after a reload", async ({
  page,
  context,
}) => {
  await signIn(context, { consented: true });
  await page.goto("/ai");
  await page.getByLabel("Message the assistant").fill(`Please ${PLAN_PROMPT}`);
  await page.keyboard.press("Enter");
  await expect(
    page.getByRole("region", { name: "Plan High-protein week" }),
  ).toBeVisible();

  // The panel and its conversation come back after a reload.
  await page.reload();
  await expect(
    page.getByRole("article", { name: "You said" }).last(),
  ).toContainText(`Please ${PLAN_PROMPT}`);
  await page.getByRole("button", { name: "Past conversations" }).click();
  const conversations = page.getByRole("region", {
    name: "Conversations",
  });
  const thread = conversations.getByRole("button", {
    name: "Plan my week",
    exact: true,
  });
  // Newest first, above the conversation the person already had.
  await expect(conversations.getByRole("listitem").first()).toContainText(
    "Plan my week",
  );
  // The conversation the reload restored is the one marked current.
  await expect(thread).toHaveAttribute("aria-current", "true");
  await thread.click();
  await expect(
    page.getByRole("article", { name: "You said" }).last(),
  ).toContainText(`Please ${PLAN_PROMPT}`);
  await expect(
    page.getByRole("article", { name: "The assistant replied" }).last(),
  ).toContainText("Here is a draft for next week.");

  // The draft the turn made waits on the plans page.
  await page.goto("/meal-plan");
  await expect(
    page.getByRole("link", { name: /High-protein week/ }),
  ).toContainText("Drafted by the assistant");
  expect((await state(context)).unhandled).toEqual([]);
});

test("the usage panel shows the planning allowance", async ({
  page,
  context,
}) => {
  await signIn(context, { consented: true, plansUsed: 1 });
  await page.goto("/profile/settings#assistant");
  const line = page.getByTestId("ai-usage-planning");
  await expect(line).toContainText("Meal plans drafted");
  await expect(line).toContainText("1 / 1");
  expect((await state(context)).unhandled).toEqual([]);
});
