import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";

async function signIn(context: BrowserContext) {
  const result = await context.request.post(`${apiURL}/__fixture/session`, {
    data: {},
  });
  expect(result.ok()).toBeTruthy();
}
async function unhandled(context: BrowserContext) {
  const state = (await (
    await context.request.get(`${apiURL}/__fixture/state`)
  ).json()) as { unhandled: string[] };
  return state.unhandled;
}
async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
}

test("a person finds how to add Mizan to Claude, downloads the plugin for this server and copies its address", async ({
  context,
  page,
}) => {
  await context.grantPermissions(["clipboard-read", "clipboard-write"]);
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(context);
  await page.goto("/profile/mcp");

  await page.getByRole("tab", { name: "Add an app" }).click();
  await page.getByRole("link", { name: "Add Mizan to Claude" }).click();
  await expect(
    page.getByRole("heading", { name: "Add Mizan to Claude", level: 1 }),
  ).toBeVisible();

  // The download and the address are this deployment's own MCP server.
  const download = page.getByRole("link", { name: "Download plugin" });
  await expect(download).toHaveAttribute("href", /\/mcp\/plugin\.zip$/);
  await expect(download).toHaveAttribute("download", "");
  for (const title of [
    "claude.ai and Claude Desktop",
    "Claude Code",
    "Only the connector",
  ])
    await expect(page.getByRole("heading", { name: title })).toBeVisible();
  await expect(page.getByText("No access, Read or Read and change")).toBeVisible();

  // From the keyboard: the first Copy button copies the connector address.
  const copyAddress = page.getByRole("button", { name: "Copy" }).first();
  await copyAddress.focus();
  await page.keyboard.press("Enter");
  await expect(page.getByText("Copied")).toBeVisible();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toMatch(
    /\/mcp$/,
  );
  await noOverflow(page);

  expect(await unhandled(context)).toEqual([]);
});
