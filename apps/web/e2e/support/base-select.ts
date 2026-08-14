import type { Locator, Page } from '@playwright/test';

/**
 * Locates the trigger button of this repo's `<BaseSelect>` (and everything
 * built on it, e.g. `<ContactEntitySelect>` —
 * src/components/lien/contact-entity-select.tsx) for the field labeled
 * `labelText`. Same shape as `comboboxTrigger` in combobox.ts: the trigger
 * is a plain sibling of its `<label>`, not a `for`/wrapping association, so
 * `getByLabel()` doesn't match it.
 */
export function baseSelectTrigger(page: Page, labelText: string): Locator {
  return page
    .locator('label')
    .filter({ hasText: labelText })
    .locator('xpath=following-sibling::button[1]');
}

/**
 * Selects an option from the BaseSelect labeled `labelText` — a Radix
 * Popover, so `page.selectOption()` doesn't apply. Opens the trigger, then
 * types into the popover's auto-focused search box to filter down to the
 * option and clicks it.
 *
 * Rows render as `role="option"` (not the implicit "button" role
 * `selectComboboxOption` in combobox.ts looks for), since BaseSelect exposes
 * proper listbox/option semantics — that's the one behavioral difference
 * from the plain `<Combobox>` helper.
 */
export async function selectBaseSelectOption(
  page: Page,
  labelText: string,
  searchTerm: string,
): Promise<void> {
  await baseSelectTrigger(page, labelText).click();
  await page.keyboard.type(searchTerm);
  await page
    .locator('[data-radix-popper-content-wrapper]')
    .getByRole('option')
    .first()
    .click();
}
