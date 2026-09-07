import { chromium, expect } from "@playwright/test";
import { mkdir, stat } from "node:fs/promises";
import { resolve } from "node:path";

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? "http://localhost:3100";
const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";
const matrix = process.argv.includes("--matrix");
type Capture = {
  route: string;
  name?: string;
  mobile?: boolean;
  dark?: boolean;
  empty?: boolean;
  dialog?: "meal" | "measurement";
  workout?: boolean;
};
const captures: Capture[] = matrix
  ? [
      { route: "/today", name: "today-desktop" },
      { route: "/today", name: "today-mobile", mobile: true },
      {
        route: "/today",
        name: "today-mobile-empty",
        mobile: true,
        empty: true,
      },
      { route: "/today", name: "today-desktop-dark", dark: true },
      { route: "/history", name: "history-desktop" },
      { route: "/history", name: "history-mobile", mobile: true },
      { route: "/progress", name: "progress-desktop" },
      { route: "/progress", name: "progress-mobile", mobile: true },
      { route: "/more", name: "more-desktop" },
      { route: "/more", name: "more-mobile", mobile: true },
      { route: "/recipes", name: "recipes-desktop" },
      { route: "/recipes", name: "recipes-mobile", mobile: true },
      {
        route: "/recipes/33333333-3333-4333-8333-333333333333",
        name: "recipe-desktop",
      },
      {
        route: "/recipes/33333333-3333-4333-8333-333333333333",
        name: "recipe-mobile",
        mobile: true,
      },
      { route: "/today", name: "meal-desktop", dialog: "meal" },
      { route: "/today", name: "meal-mobile", mobile: true, dialog: "meal" },
      {
        route: "/today",
        name: "measurement-mobile",
        mobile: true,
        dialog: "measurement",
      },
      { route: "/today", name: "measurement-desktop", dialog: "measurement" },
      { route: "/workout/active", name: "workout-desktop", workout: true },
      {
        route: "/workout/active",
        name: "workout-mobile",
        mobile: true,
        workout: true,
      },
    ]
  : [
      {
        route: process.argv[2] ?? "/today",
        mobile: process.argv.includes("--mobile"),
        dark: process.argv.includes("--dark"),
        empty: process.argv.includes("--empty"),
      },
    ];
const browser = await chromium.launch();
const output = resolve(
  process.env.CAPTURE_OUTPUT ??
    (matrix ? "../.impeccable/review" : "test-results/ui"),
);
if (!(await stat(output).catch(() => null))?.isDirectory()) {
  await mkdir(output, { recursive: true });
}
for (const capture of captures) {
  const viewport = capture.mobile
    ? { width: 390, height: 844 }
    : { width: 1440, height: 1000 };
  const context = await browser.newContext({
    baseURL,
    viewport,
    colorScheme: capture.dark ? "dark" : "light",
    timezoneId: "UTC",
  });
  await context.request.post(`${apiURL}/__fixture/session`, {
    data: { empty: capture.empty },
  });
  const page = await context.newPage();
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(capture.route, {
    waitUntil: "networkidle",
    timeout: 120_000,
  });
  await expect(
    page.getByRole("heading", { name: "Something went wrong" }),
  ).toHaveCount(0);
  await page.evaluate(() => document.fonts.ready);
  if (capture.dark)
    await page.evaluate(() => {
      document.documentElement.classList.add("dark");
      document.documentElement.style.colorScheme = "dark";
    });
  if (capture.dialog) {
    await page
      .getByRole("button", {
        name: capture.dialog === "meal" ? "Add meal" : "Add measurement",
        exact: true,
      })
      .click();
    await expect(page.getByRole("dialog")).toBeVisible();
    if (capture.dialog === "meal") {
      await page
        .getByRole("button", { name: "Add Greek yogurt", exact: true })
        .click();
      await page.getByLabel("Greek yogurt quantity in grams").fill("150");
      await expect(
        page.getByRole("button", { name: "Log meal", exact: true }),
      ).toBeInViewport({ ratio: 1 });
    } else await page.getByLabel("Weight (kg)").fill("74.2");
  }
  if (capture.workout) {
    await page.getByRole("button", { name: /Repeat last/ }).click();
    await expect(page.getByLabel("Workout name")).toBeVisible();
    if (capture.mobile) {
      const firstSet = page.getByLabel("Weight (kg) for set 1", {
        exact: true,
      });
      await expect(firstSet).toBeInViewport({ ratio: 1 });
      const bounds = await firstSet.boundingBox();
      const navigation = await page
        .getByRole("navigation", { name: "Primary mobile" })
        .boundingBox();
      if (!bounds || !navigation || bounds.y + bounds.height > navigation.y) {
        throw new Error(
          "The first set must be fully visible above mobile navigation.",
        );
      }
    }
  }
  await page.addStyleTag({
    content: "nextjs-portal { display: none !important; }",
  });
  const name =
    capture.name ??
    `${capture.route.replace(/[^a-z0-9]/gi, "-").replace(/^-/, "")}-${capture.mobile ? "mobile" : "desktop"}${capture.dark ? "-dark" : ""}${capture.empty ? "-empty" : ""}`;
  const file = `${output}/${name}.png`;
  await page.screenshot({ path: file, fullPage: true, animations: "disabled" });
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth > innerWidth,
  );
  if (!capture.dialog) {
    const extra = await page
      .locator("#app-content")
      .evaluate((element) =>
        Math.max(0, element.scrollHeight - element.clientHeight),
      );
    if (extra > 2) {
      await page.setViewportSize({
        ...viewport,
        height: Math.ceil(viewport.height + extra),
      });
      await page.screenshot({
        path: `${output}/${name}-full.png`,
        fullPage: true,
        animations: "disabled",
      });
    }
  }
  const state = await context.request.get(`${apiURL}/__fixture/state`);
  console.log(
    JSON.stringify({
      file,
      url: page.url(),
      overflow,
      headings: await page.locator("h1, h2").allTextContents(),
      errors,
      unhandled: (await state.json()).unhandled,
    }),
  );
  await context.close();
}
await browser.close();
