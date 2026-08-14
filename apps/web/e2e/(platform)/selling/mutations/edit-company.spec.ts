import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import { createTestCompany, deleteTestCompany, findCompanyRow, searchCompanies } from '../../../support/selling-companies';
import { clickMenuItem } from '../../../support/dropdown-menu';

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`Selling contacts — edit company [${env.name}]`, () => {
  test('edits a company name via the row Actions menu and the change is reflected in the list', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    const originalName = await createTestCompany(page);
    const updatedName = `${originalName} (edited)`;

    await searchCompanies(page, originalName);
    const row = findCompanyRow(page, originalName);
    await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Edit');

    await expect(page.getByRole('heading', { name: 'Edit Company' })).toBeVisible();
    const nameInput = page
      .locator('label', { hasText: 'Name' })
      .locator('xpath=following-sibling::input[1]');
    await nameInput.fill(updatedName);
    await page.getByRole('button', { name: 'Save Changes' }).click();
    await expect(page.getByRole('heading', { name: 'Edit Company' })).toBeHidden();

    await searchCompanies(page, updatedName);
    await expect(findCompanyRow(page, updatedName)).toBeVisible();

    await deleteTestCompany(page, updatedName);
  });

  test('edits a company from its detail page via Manage Company > Edit Company', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    const originalName = await createTestCompany(page);
    const updatedName = `${originalName} (edited)`;

    await searchCompanies(page, originalName);
    await findCompanyRow(page, originalName).getByRole('link', { name: originalName, exact: true }).click();
    await expect(page.getByRole('heading', { name: originalName })).toBeVisible();

    await clickMenuItem(page, page.getByRole('button', { name: 'Manage Company' }), 'Edit');
    await expect(page.getByRole('heading', { name: 'Edit Company' })).toBeVisible();

    const nameInput = page
      .locator('label', { hasText: 'Name' })
      .locator('xpath=following-sibling::input[1]');
    await nameInput.fill(updatedName);
    await page.getByRole('button', { name: 'Save Changes' }).click();
    await expect(page.getByRole('heading', { name: 'Edit Company' })).toBeHidden();
    await expect(page.getByRole('heading', { name: updatedName })).toBeVisible();

    // deleteTestCompany expects to start from the Companies list — navigate back to it first.
    await page.getByRole('link', { name: 'Back to Contacts' }).click();
    await deleteTestCompany(page, updatedName);
  });
});
