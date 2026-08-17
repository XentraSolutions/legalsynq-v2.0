import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import {
  createTestCompany,
  deleteTestCompany,
  findCompanyRow,
  searchCompanies,
  searchContactPersons,
} from '../../../support/selling-companies';

const test = createMutationTest('lien');
const env = getEnv();

const NO_MATCH_SEARCH = 'zzz-e2e-no-such-record-999';

test.describe(`Selling contacts — empty states [${env.name}]`, () => {
  test('Companies list shows an empty state when a search matches nothing', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    await searchCompanies(page, NO_MATCH_SEARCH);

    await expect(page.getByText('No Company Yet')).toBeVisible();
    await expect(page.locator('tbody tr')).toHaveCount(0);
  });

  test('Contact Person directory shows an empty state when a search matches nothing', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts?view=contacts`);
    await searchContactPersons(page, NO_MATCH_SEARCH);

    await expect(page.getByText('No Contact Person Yet')).toBeVisible();
    await expect(page.getByText('No contact persons match your search or filters yet.')).toBeVisible();
  });

  test('a company detail page Contact Person tab shows an empty state before any contact is added', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    const companyName = await createTestCompany(page);

    await searchCompanies(page, companyName);
    await findCompanyRow(page, companyName)
      .getByRole('link', { name: companyName, exact: true })
      .click();
    await expect(page).toHaveURL(/\/selling\/contacts\/[^/]+\/overview$/);
    await page.getByRole('link', { name: 'Contact Person', exact: true }).click();
    await expect(page).toHaveURL(/\/selling\/contacts\/[^/]+\/contacts$/);

    await expect(page.getByText('Loading...')).toBeHidden();
    await expect(page.getByText('No Contact Person Yet')).toBeVisible();
    await expect(
      page.getByText('No contact persons have been added yet to this company. Add your first contact person.'),
    ).toBeVisible();

    // deleteTestCompany expects to start from the Companies list — navigate back to it first.
    await page.getByRole('link', { name: 'Back to Contacts' }).click();
    await deleteTestCompany(page, companyName);
  });
});
