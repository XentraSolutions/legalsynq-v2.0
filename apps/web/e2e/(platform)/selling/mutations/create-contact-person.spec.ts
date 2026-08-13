import { createMutationTest, expect } from '../../../support/mutation-test';
import { selectComboboxOption } from '../../../support/combobox';
import { clickMenuItem } from '../../../support/dropdown-menu';
import { getEnv } from '../../../config/environments';

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
    // second, unrelated modal on top — so wait for it explicitly rather than
    // racing the two comboboxes.
    const rolesLoaded = page.waitForResponse(
      (res) => res.url().includes('/lookups/contact-person-types') && res.ok(),
    );
    await selectComboboxOption(page, 'Company Name', 'fundcomp');
    await rolesLoaded;
    await selectComboboxOption(page, 'Role', 'underwriter');

    await page.getByPlaceholder('Enter first name').fill(firstName);
    await page.getByPlaceholder('Enter last name').fill(lastName);

    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText('Contact person created')).toBeVisible();

    const row = page.locator('table tbody tr', { hasText: fullName });
    await expect(row).toBeVisible();
    await expect(row.getByText('FundComp')).toBeVisible();

    await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByText('Contact person deleted')).toBeVisible();
    await expect(row).toBeHidden();
  });
});
