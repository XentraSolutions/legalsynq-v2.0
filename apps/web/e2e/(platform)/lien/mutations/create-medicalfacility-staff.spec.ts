import { createMutationTest, expect } from '../../../support/mutation-test';
import { selectComboboxOption } from '../../../support/combobox';
import { findContactRow } from '../../../support/contacts-list';
import { clickMenuItem } from '../../../support/dropdown-menu';
import { getEnv } from '../../../config/environments';

/**
 * Covers the MedicalFacility contact type's only subtype: staff attached to
 * a specific facility, always created as the fixed subtype
 * "FacilityContactPerson" (src/components/lien/medical-facility-staff-section.tsx).
 * Unlike LawFirm's Role, there's no select here — the subtype is a hardcoded
 * prop, not tenant-configured lookup data, so no dropdown appears on this
 * form at all (just First/Last name, email, phone).
 *
 * Deletion is its own test at the end, not inline right after creation —
 * see the comment in create-contact.spec.ts for why. Ordering relies on
 * this repo's playwright.config.ts (workers: 1, fullyParallel: false).
 *
 * Mutation spec: creates a Medical Facility contact plus one staff member
 * under it, then deletes both. Runs against local/qa only.
 */

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`SynqLien contacts — MedicalFacility staff subtype [${env.name}]`, () => {
  const facilityName = `E2E MedicalFacility ${Date.now()}`;
  const firstName = 'E2E';
  const lastName = `Staff ${Date.now()}`;

  test('creates a Medical Facility contact with a Facility Contact Person staff member', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    await page.getByRole('button', { name: 'Add Contact' }).click();
    await expect(
      page.getByRole('heading', { name: 'Add New Contact' }),
    ).toBeVisible();
    // 'facilit' not 'facility': this tenant's lookup label is plural
    // ("Medical Facilities"), and "facilities" doesn't contain "facility"
    // as a substring — a stem both singular and plural forms share.
    await selectComboboxOption(page, 'Contact Type', 'facilit');
    await page.getByPlaceholder('Name', { exact: true }).fill(facilityName);
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Created')).toBeVisible();

    const row = await findContactRow(page, facilityName, 'Medical Facilities');
    await row.getByRole('link', { name: facilityName, exact: true }).click();

    await page.getByRole('link', { name: 'Medical Facility Staff' }).click();
    await expect(
      page.getByRole('heading', { name: 'Medical Facility Staff' }),
    ).toBeVisible();

    await page.getByRole('button', { name: 'Add Staff' }).click();
    await expect(
      page.getByRole('heading', { name: 'Add New Medical Facility Staff' }),
    ).toBeVisible();
    await page.getByPlaceholder('First name').fill(firstName);
    await page.getByPlaceholder('Last name').fill(lastName);
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Created')).toBeVisible();

    // .first(): the toast's transient description text can briefly duplicate
    // this same string until it auto-dismisses (see lien-store.ts's 4s timeout).
    await expect(
      page.getByText(`${firstName} ${lastName}`).first(),
    ).toBeVisible();
  });

  test('deletes the staff member and the parent Medical Facility created above', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    const initialRow = await findContactRow(page, facilityName, 'Medical Facilities');
    await initialRow
      .getByRole('link', { name: facilityName, exact: true })
      .click();
    await page.getByRole('link', { name: 'Medical Facility Staff' }).click();

    const staffCard = page.getByText(`${firstName} ${lastName}`).first();
    await expect(staffCard).toBeVisible();
    await clickMenuItem(page, page.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByText('Staff Removed')).toBeVisible();
    await expect(staffCard).toBeHidden();

    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    // findContactRow also waits out the "Refreshing..." indicator that
    // briefly overlaps the Actions column while the search re-query is in
    // flight — clicking through it too early can silently miss the button.
    const facilityRow = await findContactRow(page, facilityName, 'Medical Facilities');
    await clickMenuItem(page, facilityRow.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByText('Contact deleted')).toBeVisible();
    await expect(facilityRow).toBeHidden();
  });
});
