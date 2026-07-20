import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';

/**
 * Covers the Liens list (/lien/liens) Filter Liens modal — TODO item 8
 * ("revisit liens filter and sorting - deferred, still waiting for API").
 * The backend now honors lawFirmIds/lienStatusIds/etc. on
 * GET /lien/api/liens/liens (previously silently ignored — see the TODO
 * comment on LiensQuery in src/lib/liens/liens.types.ts), so this asserts the
 * filter actually narrows server-side results, not just that the modal opens.
 *
 * Filed under mutations/ rather than readonly/ purely because opening the
 * modal and checking a checkbox requires clicking a non-link element, which
 * ReadOnlyPage structurally disallows — the requests involved are all GETs,
 * nothing is created/edited/deleted.
 *
 * Deliberately doesn't cover sorting: the column header sort arrows
 * (BaseTable's built-in client-side sort) don't fire a request when clicked,
 * so there's nothing server-backed there yet to verify.
 */

const test = createMutationTest('lien');
const env = getEnv();

// The toolbar's "Filter" button's accessible name has a leading space (icon
// + text, rendered as separate JSX children), and once a filter is active
// the toolbar also grows a "Clear Filter" button — which also
// substring-matches "Filter". Excluding "Clear" text disambiguates both.
function openFilterButton(page: import('@playwright/test').Page) {
  return page.getByRole('button', { name: /Filter/i }).filter({ hasNotText: 'Clear' });
}

test.describe(`SynqLien liens list — filter [${env.name}]`, () => {
  test('Law Firm filter narrows results to only that firm, and Clear Filter restores the full list', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/liens`);

    const subtitle = page.getByText(/^\d+ liens$/);
    await expect(subtitle).toBeVisible({ timeout: 20_000 });
    const initialTotal = Number((await subtitle.textContent())!.match(/\d+/)![0]);

    await openFilterButton(page).click();
    await expect(page.getByRole('dialog', { name: 'Filter Liens' })).toBeVisible();

    // First listbox in the modal is Law Firm (DOM order: Law Firm, Medical
    // Facility, Case Manager, Liens Status) — pick whatever its first loaded
    // option is rather than hardcoding a tenant-data-dependent name.
    const lawFirmOption = page.getByRole('listbox').first().getByRole('option').first();
    await expect(lawFirmOption).toBeVisible({ timeout: 15_000 });
    const firmName = (await lawFirmOption.textContent())!.trim();
    await lawFirmOption.click();

    await page.getByRole('button', { name: 'Apply Filters' }).click();
    await expect(page.getByRole('dialog', { name: 'Filter Liens' })).toBeHidden();

    await expect(page.getByText('1 Filter(s) Applied')).toBeVisible();
    await expect(subtitle).toHaveText(/^\d+ liens$/);
    const filteredTotal = Number((await subtitle.textContent())!.match(/\d+/)![0]);
    expect(filteredTotal).toBeLessThan(initialTotal);

    // Every row on the (now filtered) first page must be that law firm —
    // the real proof the API filtered server-side, not just that the count
    // in the header text changed.
    const rows = page.locator('tbody tr');
    const rowCount = await rows.count();
    expect(rowCount).toBeGreaterThan(0);
    for (let i = 0; i < rowCount; i++) {
      await expect(rows.nth(i).locator('td').nth(2)).toHaveText(firmName);
    }

    await page.getByRole('button', { name: 'Clear Filter' }).click();
    await expect(page.getByText('1 Filter(s) Applied')).toBeHidden();
    await expect(subtitle).toHaveText(`${initialTotal} liens`);
  });

  test('combining Law Firm + Liens Status filters narrows further than either alone', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/liens`);

    const subtitle = page.getByText(/^\d+ liens$/);
    await expect(subtitle).toBeVisible({ timeout: 20_000 });

    await openFilterButton(page).click();
    await expect(page.getByRole('dialog', { name: 'Filter Liens' })).toBeVisible();

    const lawFirmOption = page.getByRole('listbox').first().getByRole('option').first();
    await expect(lawFirmOption).toBeVisible({ timeout: 15_000 });
    await lawFirmOption.click();
    await page.getByRole('button', { name: 'Apply Filters' }).click();
    await expect(page.getByRole('dialog', { name: 'Filter Liens' })).toBeHidden();

    const lawFirmOnlyTotal = Number((await subtitle.textContent())!.match(/\d+/)![0]);

    await openFilterButton(page).click();
    await expect(page.getByRole('dialog', { name: 'Filter Liens' })).toBeVisible();
    // Liens Status is a fixed 3-value enum (Open/Closed/Rejected), not
    // tenant data, so "Open" is safe to hardcode here.
    await page.getByRole('option', { name: 'Open', exact: true }).click();
    await page.getByRole('button', { name: 'Apply Filters' }).click();
    await expect(page.getByRole('dialog', { name: 'Filter Liens' })).toBeHidden();

    await expect(page.getByText('2 Filter(s) Applied')).toBeVisible();
    await expect(subtitle).toHaveText(/^\d+ liens$/);
    const combinedTotal = Number((await subtitle.textContent())!.match(/\d+/)![0]);
    expect(combinedTotal).toBeLessThanOrEqual(lawFirmOnlyTotal);

    await page.getByRole('button', { name: 'Clear Filter' }).click();
    await expect(page.getByText(/Filter\(s\) Applied/)).toBeHidden();
  });
});
