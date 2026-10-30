import { expect, test, type BrowserContext } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

async function signIn(context: BrowserContext) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: {},
  });
  expect(result.ok()).toBeTruthy();
}
async function fixtureState(context: BrowserContext) {
  return (await (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json()) as { unhandled: string[]; writes: { path: string }[] };
}

test("a person deletes a check-in and then their goal, confirming each", async ({
  context,
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(context);
  await page.goto("/goal");

  const record = page.getByRole("region", {
    name: "Current goal and check-ins",
  });
  await expect(record.getByRole("listitem")).toHaveCount(2);

  // Cancelling keeps the check-in.
  await record.getByRole("button", { name: /Delete check-in for/ }).first().click();
  const dialog = page.getByRole("alertdialog", { name: "Delete this check-in?" });
  await expect(dialog).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await expect(record.getByRole("listitem")).toHaveCount(2);

  await record.getByRole("button", { name: /Delete check-in for/ }).first().click();
  await dialog.getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Check-in deleted")).toBeVisible();
  await expect(record.getByRole("listitem")).toHaveCount(1);

  await record.getByRole("button", { name: "Delete goal" }).click();
  const goalDialog = page.getByRole("alertdialog", { name: "Delete this goal?" });
  await expect(goalDialog).toContainText("no daily targets");
  await goalDialog.getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Goal deleted")).toBeVisible();
  await expect(record).toBeHidden();
  await expect(page.locator('input[name="calories"]')).toHaveValue("");

  const state = await fixtureState(context);
  expect(state.writes.map((w) => w.path)).toEqual([
    expect.stringMatching(/^\/api\/Goals\/progress\//),
    "/api/Goals/goal-1",
  ]);
  expect(state.unhandled).toEqual([]);
});
