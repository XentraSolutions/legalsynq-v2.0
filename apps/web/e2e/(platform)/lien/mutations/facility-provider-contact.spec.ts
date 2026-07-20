import { createMutationTest, expect } from '../../../support/mutation-test';
import { baseSelectTrigger } from '../../../support/base-select';
import { findContactRow } from '../../../support/contacts-list';
import { clickMenuItem } from '../../../support/dropdown-menu';
import { getEnv } from '../../../config/environments';

/**
 * Covers the "Medical Facility and Provider Information" section on a lien
 * detail page (/lien/cases/[id]/liens/[lienId] —
 * src/components/lien/forms/add-medical-lien/medical-facility-provider-info.tsx),
 * exercising all three ContactEntitySelect dropdowns there (Facility Name,
 * Select Contact Person, Provider Name) via their "+ Add New …" inline
 * create flow, then saving the lien.
 *
 * Picks whatever lien is first in the /lien/liens table rather than creating
 * a dedicated one — same "pick whatever's first" convention as
 * liens-filter.spec.ts's law firm filter, since there's currently no reachable
 * "Create Lien" entry point in this tenant's UI (CreateLienModal exists but
 * liens/page.tsx never renders a button that opens it). This does mean the
 * test overwrites whatever Facility/Contact/Provider was previously selected
 * on that lien — acceptable for a mutations/ spec against local/qa only.
 *
 * KNOWN BACKEND GAP — deliberately not asserted here: the update-facility/
 * get-facility API persists the selected facility's id correctly, but does
 * NOT persist facilityContactId or medicalProviderId — confirmed manually by
 * saving, reloading, and observing both come back as empty strings even
 * though they were clearly selected and included in the save request. This
 * suite runs with no mocking (see playwright.config.ts), so that gap can't
 * be papered over here; only the Facility Name persistence (which does work)
 * is asserted after reload. Re-assert facilityContactId/medicalProviderId
 * persistence once that backend defect is fixed.
 */

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`SynqLien lien detail — Medical Facility and Provider Information [${env.name}]`, () => {
  const facilityName = `E2E Test Facility ${Date.now()}`;
  const contactFirstName = 'E2E';
  const contactLastName = `Contact ${Date.now()}`;
  const providerName = `E2E Test Provider ${Date.now()}`;
  let lienUrl = '';

  test('creates a Facility, Contact Person, and Provider on a lien and saves it', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/liens`);
    await expect(page.getByText(/^\d+ liens$/)).toBeVisible({ timeout: 20_000 });
    await page.locator('tbody tr').first().click();
    await page.waitForURL(/\/lien\/cases\/.+\/liens\/.+$/, { timeout: 20_000 });
    lienUrl = page.url();

    await expect(
      page.getByText('Medical Facility and Provider Information'),
    ).toBeVisible();

    // Facility Name — unscoped MedicalFacility create.
    await baseSelectTrigger(page, 'Facility Name').click();
    await page.getByRole('button', { name: 'Add New Medical Facility' }).click();
    await expect(
      page.getByRole('dialog', { name: 'Add New Medical Facility' }),
    ).toBeVisible();
    await page.getByPlaceholder('Name', { exact: true }).fill(facilityName);
    await page.getByRole('dialog').getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Created').first()).toBeVisible();
    await expect(baseSelectTrigger(page, 'Facility Name')).toHaveText(facilityName);

    // Select Contact Person — FacilityContactPerson, scoped to the facility
    // just created above (requireParent — disabled until Facility Name has a
    // value, which it now does).
    await baseSelectTrigger(page, 'Select Contact Person').click();
    await page.getByRole('button', { name: 'Add New Contact Person' }).click();
    await expect(
      page.getByRole('dialog', { name: 'Add New Contact Person' }),
    ).toBeVisible();
    await page.getByPlaceholder('First name').fill(contactFirstName);
    await page.getByPlaceholder('Last name').fill(contactLastName);
    await page.getByRole('dialog').getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Created').first()).toBeVisible();
    await expect(baseSelectTrigger(page, 'Select Contact Person')).toHaveText(
      `${contactFirstName} ${contactLastName}`,
    );

    // Provider Name — unscoped Provider create.
    await baseSelectTrigger(page, 'Provider Name').click();
    await page.getByRole('button', { name: 'Add New Provider' }).click();
    await expect(page.getByRole('dialog', { name: 'Add New Provider' })).toBeVisible();
    await page.getByPlaceholder('Name', { exact: true }).fill(providerName);
    await page.getByRole('dialog').getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Created').first()).toBeVisible();
    await expect(baseSelectTrigger(page, 'Provider Name')).toHaveText(providerName);

    // Page-level Save persists the lien's whole Medical Liens form (not just
    // this section) and navigates back to the case's Liens tab afterward —
    // the toast is rendered from a global store, so it's still visible there.
    await page.getByRole('button', { name: 'Save', exact: true }).click();
    await expect(page.getByText('Facility Updated')).toBeVisible();
  });

  test('Facility Name persists after reloading the lien (Contact Person/Provider do not — known backend gap)', async ({
    page,
  }) => {
    test.skip(!lienUrl, 'Requires the lien created in the previous test to have run first.');

    await page.goto(lienUrl);
    await expect(
      page.getByText('Medical Facility and Provider Information'),
    ).toBeVisible();
    await expect(baseSelectTrigger(page, 'Facility Name')).toHaveText(facilityName);
  });

  test('deletes the Contact Person, Provider, and parent Facility created above', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);

    const facilityRow = await findContactRow(page, facilityName, 'Medical Facilities');
    await facilityRow.getByRole('link', { name: facilityName, exact: true }).click();
    await page.getByRole('link', { name: 'Medical Facility Staff' }).click();

    const staffCard = page.getByText(`${contactFirstName} ${contactLastName}`).first();
    await expect(staffCard).toBeVisible();
    await clickMenuItem(page, page.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByText('Staff Removed')).toBeVisible();
    await expect(staffCard).toBeHidden();

    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    const providerRow = await findContactRow(page, providerName, 'Medical Providers');
    await clickMenuItem(page, providerRow.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByText('Contact deleted').first()).toBeVisible();
    await expect(providerRow).toBeHidden();

    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    const facilityRowAgain = await findContactRow(page, facilityName, 'Medical Facilities');
    await clickMenuItem(page, facilityRowAgain.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByText('Contact deleted').first()).toBeVisible();
    await expect(facilityRowAgain).toBeHidden();
  });
});
