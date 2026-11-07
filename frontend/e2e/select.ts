import type { Locator } from "@playwright/test";

/**
 * Picks an option from the app's dropdown (components/ui/select.tsx): opens
 * it with a click and clicks the option by its accessible name. A string
 * matches the option's whole label; a pattern also covers a hint beside it.
 */
export async function choose(select: Locator, option: string | RegExp) {
  await select.click();
  await select
    .page()
    .getByRole("listbox")
    .getByRole("option", {
      name: option,
      exact: typeof option === "string" ? true : undefined,
    })
    .click();
}
