import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import type { PageOf } from '../../../support/test-types';

/**
 * Covers the status filter shared by the Portfolio Liens page
 * (portfolio.tsx) and a case's Liens tab (case-liens-tab.tsx) via the
 * extracted LiensTableCard component (liens-table-card.tsx): both render the
 * same STATUS_OPTIONS list, including "All Statuses" (previously missing on
 * the Portfolio page — it only existed on the case tab). Also covers the one
 * bit of contextual behavior LiensTableCard keeps per-caller: "Add Single
 * Lien" scopes its navigation to the current case via `?caseId=`.
 *
 * Built on createMutationTest rather than createReadOnlyTest because
 * selecting an option in the (Radix) status dropdown requires clicking a
 * non-link element, which ReadOnlyPage's type doesn't allow — see
 * read-only-page.ts.
 */

const test = createMutationTest('lien');
const env = getEnv();

type TestPage = PageOf<typeof test>;

// The status Select is the only combobox next to the Search input on either
// page (the other combobox on the Portfolio Liens page, "Rows per page", has
// its own aria-label) — scoping this way avoids depending on Tailwind class
// names or on which page rendered it.
function statusFilterTrigger(page: TestPage) {
  return page.getByPlaceholder('Search').locator('xpath=../..').getByRole('combobox');
}

test.describe(`Selling Portfolio Liens — status filter [${env.name}]`, () => {
  test('the dropdown includes "All Statuses" alongside Pending/Internal/Sold/Archived, and selecting it clears the tab filter', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/portfolio/lien`);
    await expect(page.getByRole('heading', { name: 'All Liens' })).toBeVisible();

    const trigger = statusFilterTrigger(page);
    await expect(trigger).toHaveText('Pending');
    await trigger.click();

    const listbox = page.getByRole('listbox');
    for (const label of ['All Statuses', 'Pending', 'Internal', 'Sold', 'Archived']) {
      await expect(listbox.getByRole('option', { name: label, exact: true })).toBeVisible();
    }

    await listbox.getByRole('option', { name: 'All Statuses', exact: true }).click();
    await expect(trigger).toHaveText('All Statuses');

    // Re-fetching with the tab filter cleared still succeeds.
    await expect(page.getByText('Something went wrong.')).not.toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Lien ID' })).toBeVisible();
  });
});

test.describe(`Selling case Liens tab — shares the status filter and keeps contextual navigation [${env.name}]`, () => {
  test('defaults to "All Statuses" and scopes "Add Single Lien" to the case', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/portfolio/cases`);

    await page.waitForFunction(
      () => {
        const rows = document.querySelectorAll('table tbody tr');
        return rows.length > 0 && !(rows[0].textContent ?? '').includes('Loading...');
      },
      { timeout: 15_000 },
    );

    const firstRow = page.locator('table tbody tr').first();
    const hasCase = !(await firstRow.locator('td[colspan]').isVisible().catch(() => false));
    test.skip(!hasCase, 'No cases exist in this environment yet.');

    await firstRow.locator('td').first().locator('a').click();
    await page.waitForURL(/\/selling\/portfolio\/cases\/[^/]+$/, { timeout: 20_000 });
    const caseId = page.url().split('/').pop() as string;

    await page.getByRole('button', { name: 'Liens', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Liens', exact: true })).toBeVisible();

    // Unlike the Portfolio page, the case tab has no "Pending" default — it
    // starts unfiltered.
    await expect(statusFilterTrigger(page)).toHaveText('All Statuses');

    await page.getByRole('button', { name: 'Add New Lien' }).click();
    await page.getByRole('menuitem', { name: 'Add Single Lien' }).click();
    await page.waitForURL(/\/selling\/portfolio\/lien\/add\?caseId=/, { timeout: 20_000 });
    expect(page.url()).toContain(`caseId=${caseId}`);
  });
});
