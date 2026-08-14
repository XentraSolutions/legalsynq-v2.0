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

async function addContactPerson(
  page: Parameters<typeof selectBaseSelectOption>[0],
  lastName: string,
  // React Query caches the Role options per Company Type — only the first
  // "Add Contact Person" of a test run actually fires the
  // contact-person-types request; later opens reuse the cache instantly (see
  // add-contact-person.spec.ts's "Add More" test for the same reasoning).
  waitForRoles = true,
) {
  const rolesLoaded = waitForRoles ? waitForContactPersonTypesResponse(page) : undefined;
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
    .fill(lastName);
  await page.getByRole('button', { name: 'Create' }).click();
  await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeHidden();
  await expect(page.getByText(lastName)).toBeVisible();
}

test.describe(`Selling contacts — reassign contact person [${env.name}]`, () => {
  test('reassigns a contact person to another contact person via the row Actions menu', async ({
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

    const sourceLastName = `E2E Reassign Source ${Date.now()}`;
    const targetLastName = `E2E Reassign Target ${Date.now()}`;
    await addContactPerson(page, sourceLastName);
    await addContactPerson(page, targetLastName, false);

    const sourceRow = page.getByRole('row', { name: sourceLastName });
    await clickMenuItem(page, sourceRow.getByRole('button', { name: 'Actions menu' }), 'Reassign');
    await expect(page.getByRole('heading', { name: 'Reassign Contact Person' })).toBeVisible();
    // The subtitle paragraph ("Move everything associated with <source> to
    // another contact person.") — scoped to a <p> since the source name
    // could otherwise also match unrelated text on the page.
    await expect(page.locator('p').filter({ hasText: sourceLastName })).toBeVisible();

    await selectBaseSelectOption(page, 'Target Contact Person', targetLastName);
    await page.getByRole('button', { name: 'Reassign', exact: true }).click();
    await expect(page.getByText('Contact person reassigned')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Reassign Contact Person' })).toBeHidden();

    // Cleanup — delete both contacts we created (shared FundComp company, not
    // one we own, so only the contacts get removed).
    await clickMenuItem(page, sourceRow.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(sourceRow).toBeHidden();

    const targetRow = page.getByRole('row', { name: targetLastName });
    await clickMenuItem(page, targetRow.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(targetRow).toBeHidden();
  });
});
