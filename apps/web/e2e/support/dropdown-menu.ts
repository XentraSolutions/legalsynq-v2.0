import { expect, type Locator, type Page } from '@playwright/test';

/**
 * Clicks a Radix DropdownMenu trigger (ActionMenu's "Actions menu" button,
 * or contact-detail-shell's "Actions" button) and clicks the named menuitem
 * inside it, retrying the whole open-then-click sequence if the menu closes
 * out from under us — observed intermittently on both the row-level and
 * detail-page dropdowns: the trigger click opens the menu, but it can close
 * again a moment later (before or right as the item click lands), leaving
 * the menuitem click waiting on a now-detached element until it times out.
 * Re-clicking the trigger and trying again is cheap and sidesteps needing to
 * pin down why Radix (or a background re-render) closes it.
 */
export async function clickMenuItem(
  page: Page,
  trigger: Locator,
  itemName: string | RegExp,
  attempts = 3,
): Promise<void> {
  let lastError: unknown;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      await trigger.click();
      await expect(page.getByRole('menu')).toBeVisible({ timeout: 5_000 });
      await page
        .getByRole('menuitem', { name: itemName })
        .click({ timeout: 5_000 });
      return;
    } catch (err) {
      lastError = err;
    }
  }
  throw lastError;
}
