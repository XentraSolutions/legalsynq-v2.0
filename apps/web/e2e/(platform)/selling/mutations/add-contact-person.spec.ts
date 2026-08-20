import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import {
  KNOWN_COMPANY_NAME,
  KNOWN_COMPANY_SEARCH,
  KNOWN_ROLE_SEARCH,
  deleteTestCompany,
  findCompanyRow,
  getCompanyTypeLabel,
  searchCompanies,
  waitForContactPersonTypesResponse,
} from '../../../support/selling-companies';

import { selectBaseSelectOption } from '../../../support/base-select';
import { clickMenuItem } from '../../../support/dropdown-menu';

const test = createMutationTest('lien');
const env = getEnv();

async function fillContactPersonNames(
  page: Parameters<typeof selectBaseSelectOption>[0],
  first: string,
  last: string,
) {
  await page
    .locator('label', { hasText: 'First Name' })
    .locator('xpath=following-sibling::input[1]')
    .fill(first);
  await page
    .locator('label', { hasText: 'Last Name' })
    .locator('xpath=following-sibling::input[1]')
    .fill(last);
}

test.describe(`Selling contacts — add contact person [${env.name}]`, () => {
  test('adds a contact person from a company detail page Contact Person tab', async ({
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

    const rolesLoaded = waitForContactPersonTypesResponse(page);
    await page.getByRole('button', { name: 'Add Contact Person' }).click();
    await rolesLoaded;
    await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeVisible();

    const lastName = `E2E Contact ${Date.now()}`;
    await selectBaseSelectOption(page, 'Role', KNOWN_ROLE_SEARCH);
    await fillContactPersonNames(page, 'E2E', lastName);
    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeHidden();

    const row = page.getByRole('row', { name: lastName });
    await expect(row).toBeVisible();

    await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(row).toBeHidden();
  });

  test('the "Add More" prompt after saving a contact person reopens the form for another one', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    // Reuse the known-good company's Company Type so the Role dropdown this
    // flow depends on is guaranteed to have options — not every Company
    // Type in this tenant has Contact Person Types configured for it.
    const fundCompType = await getCompanyTypeLabel(page, KNOWN_COMPANY_NAME);
    const companyName = `E2E Test Co ${Date.now()}`;

    await page.getByRole('button', { name: 'Add Company' }).click();
    await selectBaseSelectOption(page, 'Company Type', fundCompType);
    await page
      .locator('label', { hasText: 'Name' })
      .locator('xpath=following-sibling::input[1]')
      .fill(companyName);
    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByRole('heading', { name: 'New Company Added' })).toBeVisible();

    const rolesLoaded = waitForContactPersonTypesResponse(page);
    await page.getByRole('button', { name: 'Add Contact Person' }).click();
    await rolesLoaded;
    await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeVisible();

    const contactA = `E2E Loop ${Date.now()} A`;
    await selectBaseSelectOption(page, 'Role', KNOWN_ROLE_SEARCH);
    await fillContactPersonNames(page, 'E2E', contactA);
    await page.getByRole('button', { name: 'Create' }).click();

    await expect(page.getByRole('heading', { name: 'New Contact Person Added' })).toBeVisible();
    // No second waitForContactPersonTypesResponse here — React Query already cached this
    // Company Type's roles from the first fetch above, so reopening the form via "Add More"
    // reuses that cached data instantly instead of firing a second network request.
    await page.getByRole('button', { name: 'Add More' }).click();
    await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeVisible();

    const contactB = `${contactA} B`;
    await selectBaseSelectOption(page, 'Role', KNOWN_ROLE_SEARCH);
    await fillContactPersonNames(page, 'E2E', contactB);
    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByRole('heading', { name: 'New Contact Person Added' })).toBeVisible();
    await page.getByRole('button', { name: 'Done' }).click();
    await expect(page.getByRole('heading', { name: 'New Contact Person Added' })).toBeHidden();

    // Deleting the company cascades and removes both contacts created above.
    await deleteTestCompany(page, companyName);
  });
});
