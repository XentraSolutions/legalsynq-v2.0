import { expect, type Locator, type Page } from '@playwright/test';

/**
 * Searches /lien/contacts for `name` and returns the resulting row, having
 * first waited out the "Refreshing..." indicator (contacts/page.tsx) that's
 * absolutely positioned over the top-right of the table — directly over the
 * right-aligned Actions column — while the debounced search re-query is in
 * flight. Clicking a row's Actions menu (or anything else in that corner)
 * before it clears can silently land on that overlay instead of the button
 * underneath, which looks like the click did nothing.
 *
 * `tabLabel`, when given, selects that contact-type tab (e.g. "Law Firms",
 * "Medical Facilities") before searching. There's no "All" tab on this page
 * — it defaults to preselecting the first tab — so searching for a contact
 * of a different type without switching tabs would filter it out of the
 * results entirely.
 */
export async function findContactRow(
  page: Page,
  name: string,
  tabLabel?: string,
): Promise<Locator> {
  if (tabLabel) {
    await page.getByRole('button', { name: tabLabel, exact: true }).click();
  }
  await page
    .getByPlaceholder('Search contacts by name, org, or email...')
    .fill(name);
  await expect(page.getByText('Refreshing...')).toBeHidden();
  const row = page.getByRole('row', { name });
  await expect(row.getByRole('link', { name, exact: true })).toBeVisible();
  return row;
}
