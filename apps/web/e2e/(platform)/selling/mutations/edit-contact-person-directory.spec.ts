import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import {
  KNOWN_ROLE_SEARCH,
  searchContactPersons,
  waitForContactPersonTypesResponse,
} from '../../../support/selling-companies';
import { baseSelectTrigger, selectBaseSelectOption } from '../../../support/base-select';
import { clickMenuItem } from '../../../support/dropdown-menu';

/**
 * Covers the "Edit" row action recently added to the standalone Contact
 * Person directory (/selling/contacts?view=contacts —
 * contact-persons-directory-view.tsx), which previously only offered "View
 * Company" and "Delete". Mirrors the existing per-company edit coverage
 * (mutations/edit-contact-person.spec.ts) but exercises the directory's own
 * row menu and modal wiring instead of the company detail Contact Person tab.
 *
 * Mutation spec: creates, edits, and deletes a real contact person against
 * local/qa only.
 */

async function selectCompanyThenRole(
  page: Parameters<typeof selectBaseSelectOption>[0],
  companySearch: string,
  roleSearch: string,
  attempts = 3,
): Promise<void> {
  let lastError: unknown;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      const rolesLoaded = waitForContactPersonTypesResponse(page).catch(() => undefined);
      await selectBaseSelectOption(page, 'Company Name', companySearch);
      await Promise.race([rolesLoaded, page.waitForTimeout(6_000)]);

      await baseSelectTrigger(page, 'Role').click();
      const firstOption = page.locator('[data-radix-popper-content-wrapper]').getByRole('option').first();
      const hasOptions = await firstOption.isVisible({ timeout: 4_000 }).catch(() => false);
      if (!hasOptions) {
        await page.keyboard.press('Escape');
        throw new Error(`Role combobox had no options after selecting company "${companySearch}"`);
      }
      await page.keyboard.type(roleSearch);
      await page.locator('[data-radix-popper-content-wrapper]').getByRole('option').first().click();
      return;
    } catch (err) {
      lastError = err;
    }
  }
  throw lastError;
}

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`Selling contacts directory — edit contact person [${env.name}]`, () => {
  test('edits a contact person from the directory\'s row action menu', async ({ page, credentials }) => {
    const firstName = 'E2E';
    const lastName = `Contact ${Date.now()}`;
    const fullName = `${firstName} ${lastName}`;

    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts?view=contacts`);

    await page.getByRole('button', { name: 'Add Contact Person' }).click();
    await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeVisible();
    await selectCompanyThenRole(page, 'fundcomp', KNOWN_ROLE_SEARCH);
    await page.getByPlaceholder('Enter first name').fill(firstName);
    await page.getByPlaceholder('Enter last name').fill(lastName);
    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText('Contact person created')).toBeVisible();

    await searchContactPersons(page, lastName);
    const row = page.locator('table tbody tr', { hasText: fullName });
    await expect(row).toBeVisible();

    await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Edit');
    await expect(page.getByRole('heading', { name: 'Edit Contact Person' })).toBeVisible();

    // The modal should open pre-filled with the existing contact's data.
    await expect(page.getByPlaceholder('Enter first name')).toHaveValue(firstName);
    await expect(page.getByPlaceholder('Enter last name')).toHaveValue(lastName);

    const updatedPhone = '(555) 123-4567';
    await page.getByPlaceholder('(000) 000-0000').fill(updatedPhone);
    await page.getByRole('button', { name: 'Save Changes' }).click();
    await expect(page.getByRole('heading', { name: 'Edit Contact Person' })).toBeHidden();

    await expect(row.getByText(updatedPhone)).toBeVisible();

    await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByText('Contact person deleted')).toBeVisible();
    await expect(row).toBeHidden();
  });
});
