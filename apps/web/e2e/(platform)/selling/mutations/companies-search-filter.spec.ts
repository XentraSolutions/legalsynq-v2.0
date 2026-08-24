import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import { createTestCompany, deleteTestCompany, findCompanyRow, searchCompanies } from '../../../support/selling-companies';

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`Selling contacts — companies search and type filter [${env.name}]`, () => {
  test('debounced search narrows the list to a matching company by name', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    const name = await createTestCompany(page);

    await searchCompanies(page, name);
    await expect(findCompanyRow(page, name)).toBeVisible();
    await expect(page.locator('tbody tr')).toHaveCount(1);

    await deleteTestCompany(page, name);
  });

  test('the type Filter dropdown lists the tenant\'s company types', async ({ page, credentials }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);

    await page.getByRole('button', { name: 'Filter', exact: true }).click();
    await expect(page.getByRole('option').first()).toBeVisible();
    const optionsCount = await page.getByRole('option').count();
    expect(optionsCount).toBeGreaterThan(0);
  });
});
