import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import { createTestCompany, findCompanyRow, searchCompanies } from '../../../support/selling-companies';
import { clickMenuItem } from '../../../support/dropdown-menu';

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`Selling contacts — delete company [${env.name}]`, () => {
  test('deletes a company from the row Actions menu, warns what it removes, and it disappears from the list', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    const name = await createTestCompany(page);

    await searchCompanies(page, name);
    const row = findCompanyRow(page, name);
    await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Delete');

    await expect(page.getByRole('heading', { name: 'Delete Company' })).toBeVisible();
    await expect(page.getByText('All associated contact persons')).toBeVisible();
    await expect(page.getByText('All case associations')).toBeVisible();
    await expect(page.getByText('All activity history')).toBeVisible();

    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Delete Company' })).toBeHidden();

    await searchCompanies(page, name);
    await expect(row).toBeHidden();
  });

  test('deletes a company from its detail page via Manage Company', async ({ page, credentials }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    const name = await createTestCompany(page);

    await searchCompanies(page, name);
    await findCompanyRow(page, name).getByRole('link', { name, exact: true }).click();
    await expect(page.getByRole('heading', { name })).toBeVisible();

    await clickMenuItem(page, page.getByRole('button', { name: 'Manage Company' }), 'Delete');
    await expect(page.getByRole('heading', { name: 'Delete Company' })).toBeVisible();
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Delete Company' })).toBeHidden();

    // Deactivation doesn't navigate away — the detail page just flips to Inactive in place.
    await expect(page.getByText('Inactive')).toBeVisible();

    await page.getByRole('link', { name: 'Back to Contacts' }).click();
    await expect(page).toHaveURL(/\/selling\/contacts$/);
    await searchCompanies(page, name);
    // The list only ever queries isActive:true, so the now-inactive company drops out of it —
    // this test deliberately doesn't reactivate/clean up further, since a stray inactive test
    // company has no visible footprint anywhere the app queries by default.
    await expect(findCompanyRow(page, name)).toBeHidden();
  });
});
