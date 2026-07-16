import { createMutationTest, expect } from '../../../support/mutation-test';
import { selectComboboxOption } from '../../../support/combobox';
import { findContactRow } from '../../../support/contacts-list';
import { clickMenuItem } from '../../../support/dropdown-menu';
import { getEnv } from '../../../config/environments';

/**
 * Creates one top-level contact per contact type supported by the main
 * /lien/contacts "Add Contact" flow — the 5 codes in
 * KNOWN_CONTACT_TYPE_CODES (src/hooks/use-contacts.ts). None of these types
 * expose a subtype/role field from this entry point — AddContactModal only
 * renders a Contact Type select here (contacts/page.tsx passes
 * contactTypeOptions but no contactSubtype/roleOptions). Subtype coverage
 * (Law Firm "Legal Contacts" roles, Medical Facility staff) lives in the
 * sibling create-lawfirm-legal-contact.spec.ts and
 * create-medicalfacility-staff.spec.ts specs instead, since those only
 * appear once a parent Law Firm/Medical Facility contact already exists.
 *
 * Deletion is deliberately its own test at the end of the describe block,
 * not inline after each create — a real user is never a create-then-delete
 * click away, and back-to-back create/delete in the same test was masking
 * timing issues the UI needs to tolerate anyway. Ordering relies on this
 * repo's playwright.config.ts (workers: 1, fullyParallel: false), which
 * already runs every test in declaration order — no test.describe.serial()
 * needed for that. Plain (non-serial) describe is intentional here: if one
 * create test fails, the later ones and the final cleanup test still run,
 * so a single failure doesn't leave every other created contact stranded.
 *
 * Mutation spec: creates and deletes real contacts against local/qa only.
 */

const test = createMutationTest('lien');
const env = getEnv();

const CONTACT_TYPES = [
  { code: 'LawFirm', searchTerm: 'law' },
  // 'facilit' not 'facility': this tenant's lookup label is plural
  // ("Medical Facilities"), and "facilities" doesn't contain "facility"
  // as a substring — a stem both singular and plural forms share.
  { code: 'MedicalFacility', searchTerm: 'facilit' },
  { code: 'Provider', searchTerm: 'provider' },
  { code: 'FundingCompany', searchTerm: 'funding' },
  { code: 'Lead', searchTerm: 'lead' },
];

test.describe(`SynqLien contacts — create each supported type [${env.name}]`, () => {
  const createdNames: string[] = [];

  for (const { code, searchTerm } of CONTACT_TYPES) {
    test(`creates a ${code} contact`, async ({ page, credentials }) => {
      const name = `E2E ${code} ${Date.now()}`;

      await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
      await page.getByRole('button', { name: 'Add Contact' }).click();
      await expect(
        page.getByRole('heading', { name: 'Add New Contact' }),
      ).toBeVisible();

      await selectComboboxOption(page, 'Contact Type', searchTerm);
      await page.getByPlaceholder('Name', { exact: true }).fill(name);
      await page
        .getByPlaceholder('email@example.com')
        .fill(`${name.replace(/\s+/g, '.').toLowerCase()}@example.com`);
      await page.getByPlaceholder('(555) 555-0000').fill('5175551234');
      await page.getByPlaceholder('Address').fill('123 Test St');
      await page.getByPlaceholder('City').fill('Testville');
      await selectComboboxOption(page, 'State', 'california');
      await page.getByPlaceholder('Zip Code').fill('90210');

      await page.getByRole('button', { name: 'Save' }).click();
      await expect(page.getByText('Contact Created')).toBeVisible();

      await findContactRow(page, name);

      // Recorded for the cleanup test at the end of this file — not deleted here.
      createdNames.push(name);
    });
  }

  test('deletes every contact created above', async ({ page, credentials }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);

    for (const name of createdNames) {
      // findContactRow also waits out the "Refreshing..." indicator that
      // briefly overlaps the Actions column while the search re-query is in
      // flight — clicking through it too early can silently miss the button.
      const row = await findContactRow(page, name);

      await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Delete');
      await page.getByRole('button', { name: 'Delete', exact: true }).click();
      // .first(): deleting several contacts back-to-back can leave more than
      // one "Contact deleted" toast on screen at once (each auto-dismisses
      // after 4s — see lien-store.ts), so this is deliberately not exact.
      await expect(page.getByText('Contact deleted').first()).toBeVisible();
      await expect(row).toBeHidden();
    }
  });
});
