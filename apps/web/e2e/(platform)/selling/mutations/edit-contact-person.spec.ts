import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import {
  KNOWN_COMPANY_NAME,
  KNOWN_COMPANY_SEARCH,
  KNOWN_ROLE_SEARCH,
  findCompanyRow,
  searchCompanies,
  waitForContactPersonTypesResponse,
} from '../../../support/selling-companies';
import { selectBaseSelectOption } from '../../../support/base-select';
import { clickMenuItem } from '../../../support/dropdown-menu';

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`Selling contacts — edit contact person [${env.name}]`, () => {
  test('edits a contact person from the company detail Contact Person tab', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    await searchCompanies(page, KNOWN_COMPANY_SEARCH);
    await findCompanyRow(page, KNOWN_COMPANY_NAME)
      .getByRole('link', { name: KNOWN_COMPANY_NAME, exact: true })
      .click();
    await expect(page).toHaveURL(/\/selling\/contacts\/[^/]+\/overview$/);
    await page.getByRole('link', { name: 'Contact Person', exact: true }).click();
    await expect(page).toHaveURL(/\/selling\/contacts\/[^/]+\/contacts$/);

    const originalLastName = `E2E Contact ${Date.now()}`;
    const rolesLoaded = waitForContactPersonTypesResponse(page);
    await page.getByRole('button', { name: 'Add Contact Person' }).click();
    await rolesLoaded;
    await selectBaseSelectOption(page, 'Role', KNOWN_ROLE_SEARCH);
    await page
      .locator('label', { hasText: 'First Name' })
      .locator('xpath=following-sibling::input[1]')
      .fill('E2E');
    await page
      .locator('label', { hasText: 'Last Name' })
      .locator('xpath=following-sibling::input[1]')
      .fill(originalLastName);
    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeHidden();
    await expect(page.getByText(originalLastName)).toBeVisible();

    const updatedLastName = `${originalLastName} Updated`;
    const row = page.getByRole('row', { name: originalLastName });
    await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Edit');
    await expect(page.getByRole('heading', { name: 'Edit Contact Person' })).toBeVisible();

    await page
      .locator('label', { hasText: 'Last Name' })
      .locator('xpath=following-sibling::input[1]')
      .fill(updatedLastName);
    await page.getByRole('button', { name: 'Save Changes' }).click();
    await expect(page.getByRole('heading', { name: 'Edit Contact Person' })).toBeHidden();

    const updatedRow = page.getByRole('row', { name: updatedLastName });
    await expect(updatedRow).toBeVisible();

    // Cleanup — delete the contact we created (this is the shared FundComp
    // company, not one we own, so only the contact gets removed).
    await clickMenuItem(page, updatedRow.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(updatedRow).toBeHidden();
  });
});
