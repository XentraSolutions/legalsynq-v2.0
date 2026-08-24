import { createMutationTest, expect } from '../../../support/mutation-test';
import { baseSelectTrigger, selectBaseSelectOption } from '../../../support/base-select';
import { clickMenuItem } from '../../../support/dropdown-menu';
import { getEnv } from '../../../config/environments';
import {
  KNOWN_ROLE_SEARCH,
  searchContactPersons,
  waitForContactPersonTypesResponse,
} from '../../../support/selling-companies';

/**
 * Picks `companySearch` from the Company Name field, then waits for the
 * resulting Role options to actually be present before picking `roleSearch`
 * from Role — retrying the whole sequence if the roles request is missed or
 * slow, rather than hanging on a single page.waitForResponse() for the rest
 * of the test's budget. Flaky in practice with just the single-shot wait:
 * the Role field's `useContactPersonTypes` re-fires per company type (see
 * contact-person-form-modal.tsx's effectiveCompanyTypeId), and a slow or
 * dropped response left the whole test hanging until timeout with no
 * recovery.
 *
 * Both Company Name and Role are `BaseSelect`-backed (Company Name via
 * `SellingEntitySelect`, wired to server-side search — see
 * contact-person-form-modal.tsx and selling-entity-select.tsx), not the
 * plain `<Combobox>` — so this drives them via base-select.ts's helpers,
 * whose options render `role="option"` rather than the implicit "button"
 * role `combobox.ts`'s helpers look for.
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

/**
 * Covers the "Add Contact Person" flow on the standalone Contacts directory
 * (/selling/contacts?view=contacts — contact-persons-directory-view.tsx),
 * added alongside the existing per-company "Add Contact Person" entry point
 * (companies list -> new/edit company -> ContactPersonFormModal). This is
 * the only place ContactPersonFormModal renders with `allowCompanySelect`
 * set, so Company Name is an editable Combobox here rather than a fixed,
 * disabled field — that's the behavior this spec exercises, not just
 * "a contact got created".
 *
 * Reuses the "lien" platform credentials/session: /selling lives under the
 * same rl-liens1 tenant portal and login as /lien, it isn't a separate
 * tenant or account.
 *
 * Mutation spec: creates and deletes a real contact person against
 * local/qa only.
 */

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`Selling contacts directory — add contact person [${env.name}]`, () => {
  test('creates a contact person via the directory\'s Add Contact Person modal, then deletes it', async ({
    page,
    credentials,
  }) => {
    const firstName = 'E2E';
    const lastName = `Contact ${Date.now()}`;
    const fullName = `${firstName} ${lastName}`;

    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts?view=contacts`);

    await page.getByRole('button', { name: 'Add Contact Person' }).click();
    await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeVisible();

    // Only this entry point renders Company Name as a real, editable
    // Combobox (allowCompanySelect) — everywhere else it's pre-filled and
    // disabled, so picking a company here is the behavior under test.
    //
    // Role options only load once a company is picked (useContactPersonTypes
    // is keyed off the selected company's type — see
    // contact-person-form-modal.tsx's effectiveCompanyTypeId). Opening the
    // Role combobox before that request resolves finds an empty option list
    // and falls through to its "Add New Role" action instead, opening a
    // second, unrelated modal on top — selectCompanyThenRole waits for and
    // retries around that instead of racing the two comboboxes once.
    await selectCompanyThenRole(page, 'fundcomp', KNOWN_ROLE_SEARCH);

    await page.getByPlaceholder('Enter first name').fill(firstName);
    await page.getByPlaceholder('Enter last name').fill(lastName);

    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText('Contact person created')).toBeVisible();

    // The tenant's contact list has plenty of unrelated rows (other tests'
    // leftovers included) — search down to this run's unique lastName rather
    // than assuming the new row lands on the default, unfiltered first page.
    await searchContactPersons(page, lastName);
    const row = page.locator('table tbody tr', { hasText: fullName });
    await expect(row).toBeVisible();
    await expect(row.getByText('FundComp')).toBeVisible();

    await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByText('Contact person deleted')).toBeVisible();
    await expect(row).toBeHidden();
  });
});
