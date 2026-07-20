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
