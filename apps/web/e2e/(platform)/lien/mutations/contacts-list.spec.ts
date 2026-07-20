import { createMutationTest, expect } from '../../../support/mutation-test';
import { selectComboboxOption, comboboxTrigger } from '../../../support/combobox';
import { findContactRow } from '../../../support/contacts-list';
import { clickMenuItem } from '../../../support/dropdown-menu';
import { getEnv } from '../../../config/environments';

/**
 * Covers the /lien/contacts list-page behaviors from TODO-contacts.md that
 * create-contact.spec.ts / edit-delete-contact.spec.ts don't: the Add
 * Contact modal's Contact Type preselecting from whichever tab is active
 * (#2), the search box's debounced request params (#3), the row Edit
 * Contact modal's full-field prefill including the known Address/Zip gap
 * (#4), and CSV export (#5).
 *
 * Filed under mutations/ rather than readonly/ purely because typing into
 * the search box and clicking non-link buttons (Add Contact, the row
 * Actions menu, Export) requires the real Page — ReadOnlyPage's type has no
 * .fill()/.type() and only getByRole('link') has .click(). None of these
 * tests submit the Add/Edit form: #2 and #4 open the modal to assert
 * prefilled values and close it via Cancel, never Save.
 */

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`SynqLien contacts list — Add/Edit prefill, search params, export [${env.name}]`, () => {
  test('Add Contact modal preselects the Contact Type of whichever tab is active', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);

    // Switch off whatever tab preselected first, so this isn't trivially
    // true for the default tab — Medical Facilities is never the first
    // known tab (LawFirm sorts first in every tenant seen so far).
    await page.getByRole('link', { name: 'Medical Facilities', exact: true }).click();
    await expect(page).toHaveURL(/[?&]type=MedicalFacility/);

    await page.getByRole('button', { name: 'Add Contact' }).click();
    await expect(page.getByRole('heading', { name: 'Add New Contact' })).toBeVisible();

    // contacts/page.tsx always passes contactTypeOptions (so the Contact
    // Type combobox always renders here, editable since isEdit is false)
    // *and* passes typeFilter as the fixed `contactType` prop in create
    // mode — the combobox's initial value comes from that prop, so it
    // should already show "Medical Facility" without the user picking
    // anything, unlike a fresh/unselected combobox showing its placeholder.
    // "facilit" not "facility": this tenant's lookup label is plural
    // ("Medical Facilities"), matching the stem singular/plural share.
    const contactTypeField = comboboxTrigger(page, 'Contact Type');
    await expect(contactTypeField).toHaveText(/medical facilit/i);

    await page.getByRole('button', { name: 'Cancel' }).click();
    await expect(page.getByRole('heading', { name: 'Add New Contact' })).toBeHidden();
  });

  test('search input debounces and fires the request with search + active tab params', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    await page.getByRole('link', { name: 'Law Firms', exact: true }).click();
    await expect(page).toHaveURL(/[?&]type=LawFirm/);

    const requestPromise = page.waitForRequest((req) =>
      req.url().includes('/api/lien/api/liens/contacts') && req.url().includes('search=zzz-e2e-search'),
    );
    await page.getByPlaceholder('Search contacts by name, org, or email...').fill('zzz-e2e-search');
    const request = await requestPromise;

    const url = new URL(request.url());
    expect(url.searchParams.get('search')).toBe('zzz-e2e-search');
    expect(url.searchParams.get('ContactType')).toBe('LawFirm');
    // Explicit "" (not omitted) — main contacts only, not sub-contacts.
    expect(url.searchParams.get('ContactSubtype')).toBe('');
  });

  test('row Edit Contact modal prefills Name/Email/Phone/City/State/Type, leaves Address/Zip blank', async ({
    page,
    credentials,
  }) => {
    const name = `E2E EditPrefill ${Date.now()}`;

    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    await page.getByRole('button', { name: 'Add Contact' }).click();
    await selectComboboxOption(page, 'Contact Type', 'lead');
    await page.getByPlaceholder('Name', { exact: true }).fill(name);
    await page.getByPlaceholder('email@example.com').fill('e2e.editprefill@example.com');
    await page.getByPlaceholder('(555) 555-0000').fill('5175559876');
    await page.getByPlaceholder('Address').fill('456 Prefill Ave');
    await page.getByPlaceholder('City').fill('Prefillville');
    await selectComboboxOption(page, 'State', 'california');
    await page.getByPlaceholder('Zip Code').fill('94105');
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Created')).toBeVisible();

    try {
      const row = await findContactRow(page, name, 'Leads');
      await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Edit Contact');
      await expect(page.getByRole('heading', { name: 'Edit Contact' })).toBeVisible();

      // Row-level edit renders Contact Type, disabled while editing.
      const contactTypeField = comboboxTrigger(page, 'Contact Type');
      await expect(contactTypeField).toBeDisabled();
      await expect(contactTypeField).toHaveText(/lead/i);

      await expect(page.getByPlaceholder('Name', { exact: true })).toHaveValue(name);
      await expect(page.getByPlaceholder('email@example.com')).toHaveValue(
        'e2e.editprefill@example.com',
      );
      await expect(page.getByPlaceholder('(555) 555-0000')).toHaveValue('(517) 555-9876');
      await expect(page.getByPlaceholder('City')).toHaveValue('Prefillville');
      await expect(comboboxTrigger(page, 'State')).toHaveText(/california/i);

      // Known gap: ContactListItem (the row data this modal is opened from)
      // has no addressLine1/postalCode — see the EditableContact doc
      // comment in add-contact-modal.tsx — so these stay blank even though
      // the contact really has them (set above). Not something to "fix"
      // here; asserting the current, intentional behavior.
      await expect(page.getByPlaceholder('Address')).toHaveValue('');
      await expect(page.getByPlaceholder('Zip Code')).toHaveValue('');

      await page.getByRole('button', { name: 'Cancel' }).click();
      await expect(page.getByRole('heading', { name: 'Edit Contact' })).toBeHidden();
    } finally {
      const row = await findContactRow(page, name, 'Leads');
      await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Delete');
      await page.getByRole('button', { name: 'Delete', exact: true }).click();
      await expect(page.getByText('Contact deleted').first()).toBeVisible();
    }
  });

  test('Export produces a real, non-empty CSV file', async ({ page, credentials }) => {
    // Row content is the backend's responsibility (and export-csv is known,
    // 2026-07-21, to return extra rows the list/search endpoint can't reach
    // at all — see TODO-contacts.md #5) — this only checks the frontend's
    // half: clicking Export actually produces a downloadable, non-empty CSV.
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);

    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Export' }).click();
    const download = await downloadPromise;

    expect(download.suggestedFilename()).toMatch(/^contacts_.*\.csv$/);
    const csvPath = await download.path();
    const fs = await import('node:fs');
    const csv = fs.readFileSync(csvPath!, 'utf-8');
    expect(csv.length).toBeGreaterThan(0);
  });
});
