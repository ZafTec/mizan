import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { choose } from "./select";
import { ADMIN_EMAIL, SMTP_TEST_JOB_ID } from "./fixture-admin-settings";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type State = {
  unhandled: string[];
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
};

async function signIn(context: BrowserContext, role = "admin") {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: { role },
  });
  expect(result.ok()).toBeTruthy();
}
async function state(context: BrowserContext) {
  return (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json() as Promise<State>;
}
const settingsWrites = (s: State) =>
  s.writes.filter((w) => w.path.startsWith("/api/admin/settings/"));
const row = (page: Page, key: string) => page.getByTestId(`setting-${key}`);

test("an administrator edits a setting, is told why a value is refused, and clears it", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto("/admin/exercises");
  await page.locator(`a[href="/admin/settings"]`).first().click();
  await expect(page).toHaveURL(/\/admin\/settings$/);
  await expect(page.getByRole("heading", { name: "Email" })).toBeVisible();
  for (const section of [
    "AI provider",
    "AI limits",
    "Storage",
    "Telegram",
    "Push",
    "Billing",
  ])
    await expect(page.getByRole("heading", { name: section })).toBeVisible();

  const port = row(page, "Smtp.Port");
  await expect(port.getByTestId("setting-value")).toHaveText("587");
  await expect(port).toContainText("Default");
  await expect(page.getByTestId("settings-notice-storage")).toHaveText(
    "Storage is not configured. Image uploads are refused.",
  );

  await port.getByRole("button", { name: "Edit SMTP port" }).click();
  const field = port.getByLabel("SMTP port", { exact: true });
  await expect(field).toBeFocused();
  await field.fill("70000");
  await field.press("Enter");
  await expect(port.getByRole("alert")).toHaveText(
    "SMTP port must be a whole number from 1 to 65535.",
  );
  await expect(field).toHaveAttribute("aria-invalid", "true");

  await field.fill("2525");
  await port.getByRole("button", { name: "Save" }).click();
  await expect(port.getByTestId("setting-value")).toHaveText("2525");
  await expect(port).toContainText("Set here");
  await expect(port.getByRole("alert")).toHaveCount(0);

  await port.getByRole("button", { name: "Clear SMTP port" }).click();
  await expect(port.getByTestId("setting-value")).toHaveText("587");
  await expect(port).toContainText("Default");
  await expect(page.getByText("SMTP port uses the default again")).toBeVisible();

  const s = await state(context);
  expect(settingsWrites(s).map((w) => [w.path, w.body])).toEqual([
    ["/api/admin/settings/Smtp.Port", { value: "70000" }],
    ["/api/admin/settings/Smtp.Port", { value: "2525" }],
    ["/api/admin/settings/Smtp.Port", {}],
  ]);
  expect(settingsWrites(s).every((w) => w.key)).toBe(true);
  expect(s.unhandled).toEqual([]);
});

test("a secret is entered masked and never shown, and the test buttons report back", async ({
  page,
  context,
}) => {
  await signIn(context);
  await page.goto("/admin/settings");

  const password = row(page, "Smtp.Password");
  await expect(password.getByTestId("setting-value")).toHaveText("Not set");
  await password.getByRole("button", { name: "Edit Password" }).click();
  const field = password.getByLabel("New password", { exact: true });
  await expect(field).toHaveAttribute("type", "password");
  await field.fill("fixture-smtp-secret");
  await field.press("Enter");
  await expect(password.getByTestId("setting-value")).toHaveText("Set");
  await expect(password).toContainText("Set here");
  await expect(page.locator("body")).not.toContainText("fixture-smtp-secret");

  const key = row(page, "Ai.ApiKey");
  await expect(key.getByTestId("setting-value")).toHaveText("Set");
  await expect(key).toContainText("Set here");

  const email = page.getByRole("region", { name: "Email" });
  await email.getByRole("button", { name: "Send test email" }).click();
  await expect(email.getByRole("status")).toContainText(
    `Queued for ${ADMIN_EMAIL}. Job ${SMTP_TEST_JOB_ID}`,
  );
  await expect(email.getByRole("link", { name: "see Jobs" })).toHaveAttribute(
    "href",
    "/admin/jobs?type=email",
  );

  const provider = page.getByRole("region", { name: "AI provider" });
  await provider.getByRole("button", { name: "Test provider" }).click();
  await expect(provider.getByRole("status")).toHaveText(
    "Reachable: fixture-model answered in 412 ms.",
  );

  const s = await state(context);
  expect(s.writes.map((w) => w.path)).toEqual([
    "/api/admin/settings/Smtp.Password",
    "/api/admin/settings/smtp/test",
    "/api/admin/settings/ai/test",
  ]);
  expect(s.unhandled).toEqual([]);
});

test("the page works from the keyboard on a narrow screen", async ({
  page,
  context,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(context);
  await page.goto("/admin/settings");

  const notice = page.getByTestId("settings-notice-email");
  await expect(notice).toHaveText(
    "Email is not configured. Verification and password emails are not sent.",
  );

  const host = row(page, "Smtp.Host");
  await host.getByRole("button", { name: "Edit SMTP host" }).focus();
  await page.keyboard.press("Enter");
  const field = host.getByLabel("SMTP host", { exact: true });
  await expect(field).toBeFocused();
  await page.keyboard.type("smtp.example.test");
  await page.keyboard.press("Escape");
  await expect(field).toHaveCount(0);
  await expect(host.getByTestId("setting-value")).toHaveText("Not set");

  await host.getByRole("button", { name: "Edit SMTP host" }).focus();
  await page.keyboard.press("Enter");
  await page.keyboard.type("smtp.example.test");
  await page.keyboard.press("Enter");
  await expect(host.getByTestId("setting-value")).toHaveText(
    "smtp.example.test",
  );
  // With a host set, the section no longer says email is off.
  await expect(notice).toHaveCount(0);

  const security = row(page, "Smtp.Security");
  await security.getByRole("button", { name: "Edit Security" }).click();
  // The dropdown takes focus and drives by keyboard: Enter opens it, typing
  // jumps to the first match, and Enter picks it.
  const securityValue = security.getByLabel("Security", { exact: true });
  await expect(securityValue).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.getByRole("listbox")).toBeVisible();
  await page.keyboard.type("st");
  await page.keyboard.press("Enter");
  await expect(page.getByRole("listbox")).toHaveCount(0);
  await expect(securityValue).toHaveText("StartTls");
  await expect(securityValue).toBeFocused();
  // The dropdown keeps Enter for itself; Tab reaches Save.
  await page.keyboard.press("Tab");
  await expect(security.getByRole("button", { name: "Save" })).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(security.getByTestId("setting-value")).toHaveText("StartTls");

  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  expect((await state(context)).unhandled).toEqual([]);
});

test("someone who is not an administrator never sees the page", async ({
  page,
  context,
}) => {
  await signIn(context, "user");
  await page.goto("/admin/settings");
  await expect(page).not.toHaveURL(/\/admin/);
  expect((await state(context)).unhandled).toEqual([]);
});
