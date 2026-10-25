import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import {
  BLUE_DOOR_ID,
  FAILED_MAIL_ID,
  LIDYA_ID,
  NEW_CONTACT_ID,
} from "./fixture-admin-ops";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

type Write = { method: string; path: string; body: Record<string, unknown> };

async function signIn(
  context: BrowserContext,
  role: "admin" | "user" = "admin",
) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: { role },
  });
  expect(result.ok()).toBeTruthy();
}
async function writes(context: BrowserContext, path?: string | RegExp) {
  const state = (await (
    await context.request.get(`${apiURL}/__fixture/admin-ops`)
  ).json()) as { writes: Write[] };
  return state.writes.filter(
    (w) =>
      !path || (typeof path === "string" ? w.path === path : path.test(w.path)),
  );
}
async function noUnhandled(context: BrowserContext) {
  const state = (await (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json()) as { unhandled: string[] };
  expect(state.unhandled).toEqual([]);
}
async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
}

test("the overview shows system health, and a check with nothing set up says so and links to its fix", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto("/admin");

  const grid = page.getByRole("region", { name: "System health" });
  const database = grid.getByRole("listitem", { name: "Database" });
  await expect(database).toContainText("OK");
  await expect(database).toContainText("3 ms round trip");

  const email = grid.getByRole("listitem", { name: "Email" });
  await expect(email).toContainText("Not configured");
  await expect(email).toContainText("No SMTP host is set.");
  await expect(email).not.toContainText(/\d/);
  await expect(
    email.getByRole("link", { name: "Set up in Mail log" }),
  ).toHaveAttribute("href", "/admin/mail");
  await expect(
    grid.getByRole("listitem", { name: "Background jobs" }).getByRole("link"),
  ).toHaveAttribute("href", "/admin/jobs");
  await expect(page.getByText("Total Users")).toBeVisible();

  // Mail and Contact sit next to each other in the admin tabs.
  const tabs = await page
    .locator("a[href^='/admin/']")
    .evaluateAll((links) => links.map((a) => a.getAttribute("href")));
  const mail = tabs.indexOf("/admin/mail");
  expect(mail).toBeGreaterThan(-1);
  expect(tabs[mail + 1]).toBe("/admin/contact");

  await page.setViewportSize({ width: 390, height: 844 });
  await noOverflow(page);
  await noUnhandled(context);
});

test("the mail log filters, shows why a message failed, retries it, and sends an announcement after a confirmation", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto("/admin/mail");
  await expect(page.getByRole("heading", { name: "Mail" })).toBeVisible();
  await expect(page.getByText("Confirm your email")).toBeVisible();

  await page.getByLabel("Any status").selectOption("Failed");
  await expect(page).toHaveURL(/status=Failed/);
  await expect(page.getByText("Confirm your email")).toHaveCount(0);

  await page.getByRole("link", { name: /Reset your password/ }).click();
  const drawer = page.getByRole("dialog", { name: "Reset your password" });
  await expect(drawer).toContainText("Why it failed");
  await expect(drawer).toContainText("mailbox unavailable");
  await drawer.getByRole("button", { name: "Retry" }).click();
  await expect(drawer).toContainText("Queued");
  await expect(drawer.getByRole("button", { name: "Retry" })).toHaveCount(0);
  expect(
    await writes(context, `/api/admin/mail/${FAILED_MAIL_ID}/retry`),
  ).toHaveLength(1);

  await page.keyboard.press("Escape");
  await expect(drawer).toHaveCount(0);
  await expect(page).not.toHaveURL(/open=/);

  await page.getByRole("button", { name: "Compose announcement" }).click();
  const compose = page.getByRole("dialog", { name: "Compose announcement" });
  await compose.getByRole("button", { name: "Review" }).click();
  await expect(compose.getByText("Subject is required")).toBeVisible();
  await compose.getByLabel("Subject").fill("Kitchen closed");
  await compose
    .getByLabel("Message")
    .fill("Closed on Friday.\n\nBack on Monday.");
  await compose.getByLabel("One household").check();
  await compose.getByRole("button", { name: "Review" }).click();
  await expect(compose.getByText("Choose who receives it")).toBeVisible();
  await compose.getByLabel("Find a household").fill("blue");
  await compose.getByRole("button", { name: /Blue door/ }).click();
  await compose.getByRole("button", { name: "Review" }).click();

  const confirm = page.getByRole("dialog", { name: "Send this announcement?" });
  await expect(confirm.getByRole("status")).toContainText("2 recipients");
  await expect(confirm.getByRole("status")).toContainText("Blue door");
  expect(await writes(context, "/api/admin/mail/announcements")).toHaveLength(
    0,
  );

  await confirm.getByRole("button", { name: "Send to 2" }).click();
  await expect(confirm).toHaveCount(0);
  const [sent] = await writes(context, "/api/admin/mail/announcements");
  expect(sent.body).toMatchObject({
    subject: "Kitchen closed",
    body: "Closed on Friday.\n\nBack on Monday.",
    audience: `household:${BLUE_DOOR_ID}`,
  });
  expect(sent.body.announcementId).toBeTruthy();

  await page.getByLabel("Any status").selectOption("Queued");
  await expect(page.getByRole("link", { name: /Kitchen closed/ })).toHaveCount(
    2,
  );

  await page.setViewportSize({ width: 390, height: 844 });
  await noOverflow(page);
  await noUnhandled(context);
});

test("the contact inbox opens a message, replies by email and marks it handled", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto("/admin/contact?status=New");
  await expect(page.getByText("Thanks", { exact: true })).toHaveCount(0);

  await page.getByRole("link", { name: /Billing question/ }).click();
  const drawer = page.getByRole("dialog", { name: "Billing question" });
  await expect(drawer).toContainText("Can I pay for a year at once?");
  await drawer.getByRole("button", { name: "Send reply" }).click();
  await expect(drawer.getByText("Reply is required")).toBeVisible();
  await drawer
    .getByLabel("Reply to Sam Rivera")
    .fill("Yes, choose yearly on the billing page.");
  await drawer.getByRole("button", { name: "Send reply" }).click();

  await expect(drawer).toContainText("Handled by Alex Morgan");
  await expect(drawer.getByRole("link", { name: "Queued" })).toBeVisible();
  const [reply] = await writes(
    context,
    `/api/admin/contact/${NEW_CONTACT_ID}/reply`,
  );
  expect(reply.body).toEqual({
    body: "Yes, choose yearly on the billing page.",
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await noOverflow(page);
  await noUnhandled(context);
});

test("the contact form fills in a signed-in person, checks the fields, and never sends the hidden field", async ({
  context,
  page,
}) => {
  await signIn(context, "user");
  await page.goto("/contact");
  await expect(page.getByLabel("Name")).toHaveValue("Alex Morgan");
  await expect(page.getByLabel("Email")).toHaveValue("alex@example.test");
  // The hidden field is out of the accessibility tree.
  await expect(page.getByRole("textbox", { name: "Website" })).toHaveCount(0);

  await page.getByRole("button", { name: "Send message" }).click();
  await expect(page.getByText("Subject is required")).toBeVisible();
  await expect(page.getByText("Message is required")).toBeVisible();
  expect(await writes(context, "/api/Contact")).toHaveLength(0);

  await page.getByLabel("Subject").fill("Export");
  await page.getByLabel("Subject").press("Tab");
  await expect(page.getByLabel("Message")).toBeFocused();
  await page.keyboard.type("Can I export my log?");
  await page.keyboard.press("Tab");
  await expect(
    page.getByRole("button", { name: "Send message" }),
  ).toBeFocused();
  await page.keyboard.press("Enter");

  await expect(page.getByRole("status")).toContainText(
    "Thanks, your message is on its way",
  );
  const [sent] = await writes(context, "/api/Contact");
  expect(sent.body).toEqual({
    name: "Alex Morgan",
    email: "alex@example.test",
    subject: "Export",
    message: "Can I export my log?",
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await noOverflow(page);
  await noUnhandled(context);
});

test("a signed-out visitor can write through the contact form, linked from the footer", async ({
  page,
}) => {
  const failures: string[] = [];
  page.on("response", (response) => {
    if (response.url().startsWith(apiURL) && response.status() >= 500)
      failures.push(`${response.status()} ${response.url()}`);
  });
  await page.goto("/");
  await page
    .getByRole("contentinfo")
    .getByRole("link", { name: "Contact" })
    .click();
  await expect(page).toHaveURL(/\/contact$/);

  await page.getByLabel("Name").fill("Pat Lee");
  await page.getByLabel("Email").fill("pat");
  await page.getByLabel("Subject").fill("Hello");
  await page.getByLabel("Message").fill("A question about households.");
  await page.getByRole("button", { name: "Send message" }).click();
  await expect(
    page.getByText("Enter an email address we can reply to"),
  ).toBeVisible();

  await page.getByLabel("Email").fill("pat@example.test");
  const posted = page.waitForRequest(
    (r) => r.url() === `${apiURL}/api/Contact` && r.method() === "POST",
  );
  await page.getByRole("button", { name: "Send message" }).click();
  const body = (await posted).postDataJSON() as Record<string, unknown>;
  expect(body).not.toHaveProperty("website");
  await expect(page.getByRole("status")).toContainText(
    "We reply to pat@example.test",
  );
  expect(failures).toEqual([]);
});

test("household search finds a household by a member's email, and its detail keeps support notes", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto("/admin/households");
  await page.getByLabel("Household, member name or email…").fill("lidya.blue");
  await expect(page).toHaveURL(/search=lidya.blue/);
  await expect(page.getByText("Green gate")).toHaveCount(0);
  await expect(page.getByText("Blue door")).toBeVisible();

  await page.goto(`/admin/households/${BLUE_DOOR_ID}`);
  await expect(page.getByRole("heading", { name: "Blue door" })).toBeVisible();
  await expect(page.getByText("Owner", { exact: true }).first()).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Lidya Bekele" }).first(),
  ).toBeVisible();
  await expect(
    page.getByText(/sam@example.test · member · joined/),
  ).toBeVisible();

  const notes = page.getByRole("region", { name: "Support notes" });
  await expect(notes).toContainText("Asked about sharing one shopping list.");
  await expect(notes).toContainText("Alex Morgan");
  await notes.getByRole("button", { name: "Add note" }).click();
  await expect(notes.getByText("Note is required")).toBeVisible();
  await notes.getByLabel("New note").fill("Called about the shared list.");
  await notes.getByRole("button", { name: "Add note" }).click();
  // Newest first, so the new note leads the list.
  const added = notes.getByRole("listitem").first();
  await expect(added).toContainText("Called about the shared list.");

  await added.getByRole("button", { name: "Edit" }).click();
  await added.getByLabel("Edit note").fill("Called, list is shared now.");
  await added.getByRole("button", { name: "Save" }).click();
  await expect(notes).toContainText("Called, list is shared now.");

  page.once("dialog", (dialog) => dialog.accept());
  await notes
    .getByRole("listitem")
    .filter({ hasText: "Called, list is shared now." })
    .getByRole("button", { name: "Delete" })
    .click();
  await expect(notes).not.toContainText("Called, list is shared now.");

  const noteWrites = await writes(context, /^\/api\/admin\/support-notes/);
  expect(noteWrites.map((w) => w.method)).toEqual(["POST", "PUT", "DELETE"]);
  expect(noteWrites[0].body).toEqual({
    subjectType: "Household",
    subjectId: BLUE_DOOR_ID,
    body: "Called about the shared list.",
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await noOverflow(page);
  await noUnhandled(context);
});

test("an administrator suspends an account with a reason and an end, then lifts it", async ({
  context,
  page,
}) => {
  await signIn(context);
  await page.goto(`/admin/users/${LIDYA_ID}`);
  await expect(
    page.getByRole("region", { name: "Support notes" }),
  ).toContainText("No notes yet.");

  await page.getByRole("button", { name: "Suspend account" }).click();
  await page.getByLabel("Reason").fill("Chargeback under review");
  await page.getByLabel("Ends (optional)").fill("2030-01-15T12:00");
  await page.getByRole("button", { name: "Suspend", exact: true }).click();

  const suspension = page.getByRole("region", { name: "Suspended" });
  await expect(suspension).toContainText("Chargeback under review");
  await expect(suspension).toContainText("2030");
  const [suspend] = await writes(context, `/api/admin/users/${LIDYA_ID}`);
  expect(suspend.body).toMatchObject({
    banned: true,
    banReason: "Chargeback under review",
  });
  expect(suspend.body.banExpires).toBe("2030-01-15T12:00:00.000Z");

  await page.getByRole("button", { name: "Lift suspension" }).click();
  await expect(suspension).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Suspend account" }),
  ).toBeVisible();
  const patches = await writes(context, `/api/admin/users/${LIDYA_ID}`);
  expect(patches.at(-1)?.body).toEqual({ banned: false });
  await noUnhandled(context);
});
