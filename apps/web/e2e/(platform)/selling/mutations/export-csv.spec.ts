import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import {
  KNOWN_COMPANY_NAME,
  KNOWN_COMPANY_SEARCH,
  findCompanyRow,
  searchCompanies,
} from '../../../support/selling-companies';

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`Selling contacts — CSV export [${env.name}]`, () => {
  test('exports the Companies list to a dated CSV', async ({ page, credentials }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Export' }).click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/^companies-\d{4}-\d{2}-\d{2}\.csv$/);
  });

  test('exports the Contact Person directory to a dated CSV', async ({ page, credentials }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts?view=contacts`);

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Export' }).click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/^contacts-\d{4}-\d{2}-\d{2}\.csv$/);
  });

  test('exports a company\'s Contact Person tab to a CSV named after the company', async ({
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

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Export' }).click(),
    ]);
    expect(download.suggestedFilename()).toMatch(
      new RegExp(`^${KNOWN_COMPANY_NAME}-contacts-\\d{4}-\\d{2}-\\d{2}\\.csv$`),
    );
  });
});
