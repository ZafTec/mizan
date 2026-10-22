import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { expect, test, type FrameLocator, type Page } from "@playwright/test";

const appsDir = resolve(__dirname, "../../backend/Mizan.Mcp.Server/Apps");
const bridgeMarker = "<!-- mizan:bridge -->";
const bridge = readFileSync(resolve(appsDir, "_bridge.js"), "utf8");
// The same inlining MizanApps.Resources() does when the server serves a page.
const page_ = (name: string) => {
  const html = readFileSync(resolve(appsDir, `${name}.html`), "utf8");
  if (!html.includes(bridgeMarker)) {
    throw new Error(`${name}.html has no ${bridgeMarker} marker`);
  }
  return html.replace(bridgeMarker, bridge);
};

type Call = { name: string; arguments: Record<string, unknown> };
type ToolReply = {
  content: { type: "text"; text: string }[];
  isError?: boolean;
};

const text = (value: unknown): ToolReply => ({
  content: [
    {
      type: "text",
      text: typeof value === "string" ? value : JSON.stringify(value),
    },
  ],
});

/**
 * A stand-in for the chat host. It does what a real one does: answers the app's
 * ui/initialize, sends the tool result once the app says it is ready, and answers
 * the tools the app calls. It records those calls so a test can say what the app asked for.
 */
async function openApp(
  page: Page,
  name: string,
  initial: ToolReply,
  reply: (call: Call) => ToolReply = () => text({}),
): Promise<FrameLocator> {
  await page.exposeFunction("hostReply", (call: Call) => reply(call));
  await page.setContent(
    `<!doctype html><body style="margin:0"><iframe id="app" style="width:520px;height:700px;border:0"></iframe></body>`,
  );
  await page.evaluate(
    ({ html, initial }) => {
      const frame = document.getElementById("app") as HTMLIFrameElement;
      const w = window as unknown as {
        calls: Call[];
        sizes: unknown[];
        hostReply: (c: Call) => Promise<ToolReply>;
      };
      w.calls = [];
      w.sizes = [];
      window.addEventListener("message", async (event) => {
        if (event.source !== frame.contentWindow) return;
        const m = event.data;
        const post = (data: unknown) =>
          frame.contentWindow!.postMessage(data, "*");
        if (m.method === "ui/initialize") {
          post({
            jsonrpc: "2.0",
            id: m.id,
            result: {
              protocolVersion: "2025-06-18",
              hostInfo: { name: "test", version: "1" },
              hostCapabilities: {},
              hostContext: { theme: "light" },
            },
          });
        } else if (m.method === "ui/notifications/initialized") {
          post({
            jsonrpc: "2.0",
            method: "ui/notifications/tool-result",
            params: initial,
          });
        } else if (m.method === "ui/notifications/size-changed") {
          w.sizes.push(m.params);
        } else if (m.method === "tools/call") {
          w.calls.push(m.params);
          post({
            jsonrpc: "2.0",
            id: m.id,
            result: await w.hostReply(m.params),
          });
        }
      });
      frame.srcdoc = html;
    },
    { html: page_(name), initial },
  );
  return page.frameLocator("#app");
}
const calls = (page: Page) =>
  page.evaluate(() => (window as unknown as { calls: Call[] }).calls);

test.describe("MCP apps, inside a stand-in host", () => {
  test("the nutrition day shows the totals, and the day buttons ask the host for the next day", async ({
    page,
  }) => {
    const day = {
      date: "2026-10-01",
      totalCalories: 1850,
      targetCalories: 2200,
      totalProtein: 120,
      targetProtein: 150,
      totalCarbs: 200,
      targetCarbs: 250,
      totalFat: 60,
      targetFat: 70,
    };
    const app = await openApp(page, "nutrition-day", text(day), (call) =>
      text({ ...day, date: call.arguments.date, totalCalories: 900 }),
    );

    await expect(app.getByText("2026-10-01")).toBeVisible();
    await expect(
      app.getByRole("img", { name: /1,850 calories of 2,200/ }),
    ).toBeVisible();
    await expect(app.getByText("120 / 150 g")).toBeVisible();

    await app.getByRole("button", { name: "Next day" }).click();
    await expect(app.getByText("2026-10-02")).toBeVisible();
    expect(await calls(page)).toEqual([
      { name: "get_nutrition_summary", arguments: { date: "2026-10-02" } },
    ]);
  });

  test("a failed tool result is shown as a message, not a blank page", async ({
    page,
  }) => {
    const app = await openApp(page, "nutrition-day", {
      ...text("[MONTHLY LIMIT REACHED] Upgrade."),
      isError: true,
    });

    await expect(app.getByRole("status")).toContainText(
      "MONTHLY LIMIT REACHED",
    );
  });

  test("the app reports its size to the host and keeps within a narrow frame", async ({
    page,
  }) => {
    await page.setViewportSize({ width: 360, height: 700 });
    const app = await openApp(
      page,
      "body-trend",
      text({
        items: [
          { date: "2026-09-01", weightKg: 82 },
          { date: "2026-09-20", weightKg: 80.5 },
        ],
      }),
    );

    await expect(
      app.getByRole("img", { name: /Weight from 82 to 80.5 kg/ }),
    ).toBeVisible();
    expect(
      await page.evaluate(
        () => (window as unknown as { sizes: unknown[] }).sizes.length,
      ),
    ).toBeGreaterThan(0);
  });

  test("the body trend draws a chart, and its tabs switch the measurement", async ({
    page,
  }) => {
    const items = [
      { date: "2026-09-01", weightKg: 82, waistCm: 90 },
      { date: "2026-09-10", weightKg: 81, waistCm: 89 },
      { date: "2026-09-20", weightKg: 80.5, waistCm: 88 },
    ];
    const app = await openApp(page, "body-trend", text({ items }));

    await expect(
      app.getByText("Weight: 80.5 kg now, -1.5 kg since 2026-09-01"),
    ).toBeVisible();
    await app.getByRole("button", { name: "Waist" }).click();
    await expect(
      app.getByText("Waist: 88 cm now, -2 cm since 2026-09-01"),
    ).toBeVisible();
    await expect(app.getByRole("button", { name: "Waist" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    await expect(app.getByRole("button", { name: "Body fat" })).toHaveCount(0);
  });

  test("the photo review logs each ticked item once, with the grams the person set", async ({
    page,
  }) => {
    const analysis = {
      confidence: 0.8,
      foods: [
        {
          name: "Rice",
          portionGrams: 100,
          calories: 130,
          protein: 2.7,
          carbs: 28,
          fat: 0.3,
          per100g: {
            calories: 130,
            proteinGrams: 2.7,
            carbsGrams: 28,
            fatGrams: 0.3,
          },
        },
        {
          name: "Chicken",
          portionGrams: 100,
          calories: 165,
          protein: 31,
          carbs: 0,
          fat: 3.6,
          per100g: {
            calories: 165,
            proteinGrams: 31,
            carbsGrams: 0,
            fatGrams: 3.6,
          },
        },
        {
          name: "Salad",
          portionGrams: 50,
          calories: 20,
          protein: 1,
          carbs: 3,
          fat: 0,
          per100g: {
            calories: 40,
            proteinGrams: 2,
            carbsGrams: 6,
            fatGrams: 0,
          },
        },
      ],
    };
    const app = await openApp(page, "food-photo", text(analysis));

    await expect(app.getByText("Estimate (80% confident).")).toBeVisible();
    await app.getByLabel("Include Salad").uncheck();
    await app.getByLabel("Grams of Rice").fill("200");
    await expect(app.locator("#total")).toContainText("2 items, 425 kcal");
    await app.getByRole("button", { name: "Log selected" }).click();

    await expect(app.getByRole("status")).toContainText("2 items logged");
    const made = await calls(page);
    expect(made.map((c) => c.name)).toEqual([
      "log_meal_manual",
      "log_meal_manual",
    ]);
    expect(made.map((c) => c.arguments.name)).toEqual(["Rice", "Chicken"]);
    // The grams and the per-100 g figures go to the server; no total is computed here.
    expect(made[0].arguments.amountGrams).toBe(200);
    expect(made[0].arguments.caloriesPer100g).toBe(130);
    expect(made[0].arguments.calories).toBeUndefined();
    await expect(
      app.getByRole("button", { name: "Log selected" }),
    ).toBeDisabled();
  });

  test("the photo review stops at the first failure, so a retry cannot log anything twice", async ({
    page,
  }) => {
    const analysis = {
      foods: [
        {
          name: "Rice",
          portionGrams: 100,
          calories: 130,
          protein: 2.7,
          carbs: 28,
          fat: 0.3,
          per100g: {
            calories: 130,
            proteinGrams: 2.7,
            carbsGrams: 28,
            fatGrams: 0.3,
          },
        },
        {
          name: "Chicken",
          portionGrams: 100,
          calories: 165,
          protein: 31,
          carbs: 0,
          fat: 3.6,
          per100g: {
            calories: 165,
            proteinGrams: 31,
            carbsGrams: 0,
            fatGrams: 3.6,
          },
        },
        {
          name: "Beans",
          portionGrams: 100,
          calories: 120,
          protein: 8,
          carbs: 20,
          fat: 1,
          per100g: {
            calories: 120,
            proteinGrams: 8,
            carbsGrams: 20,
            fatGrams: 1,
          },
        },
      ],
    };
    let attempts = 0;
    const app = await openApp(page, "food-photo", text(analysis), (call) =>
      ++attempts === 2
        ? { ...text(`Could not log ${call.arguments.name}`), isError: true }
        : text({ ok: true }),
    );

    await app.getByRole("button", { name: "Log selected" }).click();

    await expect(app.getByRole("status")).toContainText(
      "1 logged. Chicken failed",
    );
    expect((await calls(page)).map((c) => c.arguments.name)).toEqual([
      "Rice",
      "Chicken",
    ]);

    // Rice is logged and unticked. Retrying sends Chicken and Beans, never Rice again.
    await expect(app.getByLabel("Include Rice")).not.toBeChecked();
    await app.getByRole("button", { name: "Log selected" }).click();
    await expect(app.getByRole("status")).toContainText("2 items logged");
    expect((await calls(page)).map((c) => c.arguments.name)).toEqual([
      "Rice",
      "Chicken",
      "Chicken",
      "Beans",
    ]);
  });

  test("the meal plan shows a day with its totals and violations, logs an entry once, and asks for the shopping list", async ({
    page,
  }) => {
    const planId = "11111111-1111-4111-8111-111111111111";
    const entry = (
      id: string,
      slot: string,
      extra: Record<string, unknown>,
    ) => ({
      id,
      dayIndex: 0,
      date: "2026-10-12",
      slot,
      logged: false,
      notes: null,
      prepSessionId: null,
      ...extra,
    });
    const plan = {
      id: planId,
      name: "Week of the 12th",
      status: "Active",
      startDate: "2026-10-12",
      endDate: "2026-10-13",
      durationDays: 2,
      rules: { targetCalories: 2000 },
      prepSessions: [
        {
          id: "s1",
          dayIndex: 0,
          date: "2026-10-12",
          title: "Sunday batch",
          notes: null,
          entryCount: 1,
        },
      ],
      violations: [
        {
          dayIndex: 0,
          slot: null,
          rule: "calories",
          message: "Day 1 is 1,205 kcal under target",
        },
      ],
      days: [
        {
          dayIndex: 0,
          date: "2026-10-12",
          entryCount: 3,
          loggedCount: 0,
          unresolvedCount: 1,
          totals: {
            calories: 795,
            proteinGrams: 30,
            carbsGrams: 100,
            fatGrams: 20,
            fiberGrams: 5,
          },
          violations: [
            {
              dayIndex: 0,
              slot: null,
              rule: "calories",
              message: "Day 1 is 1,205 kcal under target",
            },
          ],
          slots: [
            {
              slot: "BREAKFAST",
              entries: [
                entry("e1", "BREAKFAST", {
                  kind: "Text",
                  text: "Coffee from the cafe",
                  nutrition: null,
                }),
              ],
            },
            {
              slot: "LUNCH",
              entries: [
                entry("e2", "LUNCH", {
                  kind: "Food",
                  foodId: "f1",
                  foodName: "Rice",
                  grams: 150,
                  nutrition: {
                    calories: 195,
                    proteinGrams: 4,
                    carbsGrams: 42,
                    fatGrams: 0.5,
                    fiberGrams: 0,
                  },
                }),
              ],
            },
            {
              slot: "DINNER",
              entries: [
                entry("e3", "DINNER", {
                  kind: "Recipe",
                  recipeId: "r1",
                  recipeTitle: "Stew",
                  servings: 1,
                  prepSessionId: "s1",
                  nutrition: {
                    calories: 600,
                    proteinGrams: 26,
                    carbsGrams: 58,
                    fatGrams: 19.5,
                    fiberGrams: 5,
                  },
                }),
              ],
            },
          ],
        },
        {
          dayIndex: 1,
          date: "2026-10-13",
          entryCount: 0,
          loggedCount: 0,
          unresolvedCount: 0,
          totals: {
            calories: 0,
            proteinGrams: 0,
            carbsGrams: 0,
            fatGrams: 0,
            fiberGrams: 0,
          },
          violations: [],
          slots: [],
        },
      ],
    };
    const app = await openApp(page, "meal-plan", text(plan), (call) =>
      call.name === "generate_shopping_list"
        ? text({ listId: "l1", itemCount: 3, needsReviewCount: 1 })
        : text({
            diaryEntryIds: ["d1"],
            calories: 195,
            date: "2026-10-12",
            mealType: "LUNCH",
          }),
    );

    await expect(
      app.getByRole("heading", { name: "Week of the 12th" }),
    ).toBeVisible();
    await expect(
      app.getByText("2026-10-12 to 2026-10-13 · Active · 0 of 3 logged"),
    ).toBeVisible();
    await expect(app.getByText("Stew")).toBeVisible();
    await expect(app.getByText("600 kcal · P 26 · C 58 · F 20")).toBeVisible();
    await expect(app.getByText("No nutrition for this line")).toBeVisible();
    await expect(app.getByText("1 line without nutrition")).toBeVisible();
    await expect(app.getByText("Day 1 is 1,205 kcal under target")).toHaveCount(
      1,
    );
    await expect(app.getByText("Sunday batch")).toBeVisible();

    // Logging goes through the host with the plan id and the ENTRY id, and the button cannot be pressed twice.
    await app.getByRole("button", { name: "Log Rice" }).click();
    await expect(app.getByRole("status")).toContainText(
      "Logged Rice on 2026-10-12",
    );
    await expect(
      app.getByRole("button", { name: "Logged Rice" }),
    ).toBeDisabled();
    await expect(
      app.getByText("2026-10-12 to 2026-10-13 · Active · 1 of 3 logged"),
    ).toBeVisible();

    await app.getByRole("button", { name: "Shopping list" }).click();
    await expect(app.getByRole("status")).toContainText(
      "Shopping list ready: 3 items, 1 to check",
    );
    expect(await calls(page)).toEqual([
      {
        name: "log_plan_entry",
        arguments: { mealPlanId: planId, entryId: "e2" },
      },
      { name: "generate_shopping_list", arguments: { mealPlanId: planId } },
    ]);

    // The second day is empty, and says so.
    await app.getByRole("button", { name: /Tue 13/ }).click();
    await expect(app.getByText("Nothing planned for this day.")).toBeVisible();
    await expect(app.getByRole("button", { name: /Tue 13/ })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  test("the plan day view renders one day, and a plain 'no active plan' answer is a message", async ({
    page,
  }) => {
    const app = await openApp(
      page,
      "meal-plan",
      text({
        planId: "22222222-2222-4222-8222-222222222222",
        planName: "Cutting week",
        date: "2026-10-14",
        rules: {},
        day: {
          dayIndex: 2,
          date: "2026-10-14",
          entryCount: 1,
          loggedCount: 1,
          unresolvedCount: 0,
          totals: {
            calories: 400,
            proteinGrams: 30,
            carbsGrams: 40,
            fatGrams: 10,
            fiberGrams: 2,
          },
          violations: [],
          slots: [
            {
              slot: "LUNCH",
              entries: [
                {
                  id: "e9",
                  dayIndex: 2,
                  date: "2026-10-14",
                  slot: "LUNCH",
                  kind: "Food",
                  foodName: "Eggs",
                  grams: 120,
                  logged: true,
                  nutrition: {
                    calories: 400,
                    proteinGrams: 30,
                    carbsGrams: 40,
                    fatGrams: 10,
                    fiberGrams: 2,
                  },
                },
              ],
            },
          ],
        },
      }),
    );

    await expect(
      app.getByRole("heading", { name: "Cutting week" }),
    ).toBeVisible();
    await expect(app.getByText("2026-10-14 · 1 of 1 logged")).toBeVisible();
    await expect(
      app.getByRole("button", { name: "Logged Eggs" }),
    ).toBeDisabled();
    await expect(app.getByRole("button", { name: /Wed 14/ })).toHaveCount(0);

    const second = await page.context().newPage();
    const none = await openApp(
      second,
      "meal-plan",
      text("No active plan covers that date."),
    );
    await expect(none.getByRole("status")).toContainText(
      "No active plan covers that date.",
    );
  });

  test("a hostile food name is shown as text and never runs", async ({
    page,
  }) => {
    const evil = `<img src=x onerror="window.top.pwned = true">`;
    const app = await openApp(
      page,
      "food-photo",
      text({
        foods: [
          {
            name: evil,
            portionGrams: 100,
            calories: 10,
            protein: 0,
            carbs: 0,
            fat: 0,
            per100g: {
              calories: 10,
              proteinGrams: 0,
              carbsGrams: 0,
              fatGrams: 0,
            },
          },
        ],
      }),
    );

    await expect(app.getByLabel("Food name")).toHaveValue(evil);
    await expect(app.locator("img")).toHaveCount(0);
    expect(
      await page.evaluate(
        () => (window as unknown as { pwned?: boolean }).pwned,
      ),
    ).toBeUndefined();
  });
});
