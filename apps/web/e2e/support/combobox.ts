import type { Locator, Page } from '@playwright/test';

/**
 * Locates the trigger button of this repo's custom `<Combobox>`
 * (src/components/ui/combobox.tsx) for the field labeled `labelText` — the
 * trigger is a plain sibling of its `<label>`, not a `for`/wrapping
 * association, so `getByLabel()` doesn't match it. Exposed separately from
 * `selectComboboxOption` below so callers can also assert on the trigger
 * itself (e.g. that it's disabled, or shows a particular selected value)
 * without opening it.
 */
export function comboboxTrigger(page: Page, labelText: string): Locator {
  return page
    .locator('label')
    .filter({ hasText: labelText })
    .locator('xpath=following-sibling::button[1]');
}

/**
 * Selects an option from the Combobox labeled `labelText` — a Radix Popover,
 * so `page.selectOption()` doesn't apply either. Opens the trigger, then
 * types into the popover's auto-focused search box to filter down to the
 * option and clicks it.
 *
 * `searchTerm` only needs to be a distinctive substring of the option's label
 * (matching is case-insensitive, per the Combobox's own filter) — exact
 * backend copy isn't guaranteed. Prefer a stem shared by singular/plural
 * forms (e.g. "facilit" not "facility") since tenant-configured lookup names
 * aren't guaranteed to match either.
 */
export async function selectComboboxOption(
  page: Page,
  labelText: string,
  searchTerm: string,
): Promise<void> {
  await comboboxTrigger(page, labelText).click();
  await page.keyboard.type(searchTerm);
  await page
    .locator('[data-radix-popper-content-wrapper]')
    .getByRole('button')
    .first()
    .click();
}
