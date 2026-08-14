import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import { deleteTestCompany, findCompanyRow, searchCompanies } from '../../../support/selling-companies';
import { selectBaseSelectOption } from '../../../support/base-select';

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`Selling contacts — create company [${env.name}]`, () => {
  test('creates a company, declines the contact-person prompt, and it appears in the list', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    const name = `E2E Test Co ${Date.now()}`;

    await page.getByRole('button', { name: 'Add Company' }).click();
    await expect(page.getByRole('heading', { name: 'Add Company' })).toBeVisible();

    await selectBaseSelectOption(page, 'Company Type', '');
    await page
      .locator('label', { hasText: 'Name' })
      .locator('xpath=following-sibling::input[1]')
      .fill(name);

    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByRole('heading', { name: 'New Company Added' })).toBeVisible();
    await page.getByRole('button', { name: 'Maybe Later' }).click();
    await expect(page.getByRole('heading', { name: 'New Company Added' })).toBeHidden();

    await searchCompanies(page, name);
    await expect(findCompanyRow(page, name)).toBeVisible();

    await deleteTestCompany(page, name);
  });

  test("accepting the post-create prompt opens the Add Contact Person modal", async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    const name = `E2E Test Co ${Date.now()}`;

    await page.getByRole('button', { name: 'Add Company' }).click();
    await selectBaseSelectOption(page, 'Company Type', '');
    await page
      .locator('label', { hasText: 'Name' })
      .locator('xpath=following-sibling::input[1]')
      .fill(name);
    await page.getByRole('button', { name: 'Create' }).click();

    await expect(page.getByRole('heading', { name: 'New Company Added' })).toBeVisible();
    await page.getByRole('button', { name: 'Add Contact Person' }).click();
    const dialog = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'Add Contact Person' }) });
    await expect(dialog.getByRole('heading', { name: 'Add Contact Person' })).toBeVisible();
    // The subtitle paragraph ("...to add a contact to <name>.") — scoped to a <p>
    // since `name` also appears inside the (disabled) Company Name field's button.
    await expect(dialog.locator('p').filter({ hasText: name })).toBeVisible();

    await page.getByRole('button', { name: 'Cancel' }).click();
    await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeHidden();

    await deleteTestCompany(page, name);
  });
});
