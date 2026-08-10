import { createMutationTest, expect } from '../../../support/mutation-test';
import { selectComboboxOption, comboboxTrigger } from '../../../support/combobox';
import { findContactRow } from '../../../support/contacts-list';
import { clickMenuItem } from '../../../support/dropdown-menu';
import { getEnv } from '../../../config/environments';

/**
 * Covers editing and deleting a contact through both places the UI exposes
 * those actions — the row-level Action Menu on /lien/contacts (the
 * ActionMenu component, aria-label "Actions menu") and the contact detail
 * page's own "Actions" dropdown (contact-detail-shell.tsx) — which are two
 * separately-wired paths to the same AddContactModal / delete flow, with
 * real differences worth asserting on:
 *   - Row-level edit (contacts/page.tsx) passes contactTypeOptions, so a
 *     (disabled, since isEdit) Contact Type field renders alongside the
 *     prefilled fields.
 *   - Detail-page edit (contact-detail-shell.tsx) omits contactTypeOptions
 *     entirely, so no Contact Type field renders at all — easy to miss
 *     since both ultimately open the same AddContactModal.
 *   - Detail-page delete shows no ConfirmDialog warningItems (unlike the
 *     row-level delete's "This will also remove..." list), redirects to
 *     /lien/contacts afterward, and its toast reads "Contact Deleted"
 *     (title-case) vs. the row path's lowercase "Contact deleted".
 *
 * Row-level delete itself is already covered by create-contact.spec.ts's
 * cleanup test, so this file focuses on the row-edit / detail-edit /
 * detail-delete combinations that aren't covered anywhere else.
 *
 * Deletion is its own test at the end, not inline right after — see the
 * comment in create-contact.spec.ts for why. Ordering relies on this repo's
 * playwright.config.ts (workers: 1, fullyParallel: false).
 *
 * Mutation spec: creates, edits, and deletes a real contact against
 * local/qa only.
 */

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`SynqLien contacts — edit & delete via row and detail-page actions [${env.name}]`, () => {
  const originalName = `E2E EditDelete ${Date.now()}`;
  const rowEditedName = `${originalName} Row-Edited`;
  const detailEditedName = `${originalName} Detail-Edited`;

  test('creates a Lead contact', async ({ page, credentials }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    await page.getByRole('button', { name: 'Add Contact' }).click();
    await expect(
      page.getByRole('heading', { name: 'Add New Contact' }),
    ).toBeVisible();

    await selectComboboxOption(page, 'Contact Type', 'lead');
    await page.getByPlaceholder('Name', { exact: true }).fill(originalName);
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Created')).toBeVisible();

    await findContactRow(page, originalName, 'Leads');
  });

  test('edits the contact via the row-level Action Menu', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    // findContactRow also waits out the "Refreshing..." indicator that
    // briefly overlaps the Actions column while the search re-query is in
    // flight — clicking through it too early can silently miss the button.
    const row = await findContactRow(page, originalName, 'Leads');

    await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Edit Contact');
    await expect(page.getByRole('heading', { name: 'Edit Contact' })).toBeVisible();

    // Row-level edit renders a Contact Type field, locked while editing.
    const contactTypeField = comboboxTrigger(page, 'Contact Type');
    await expect(contactTypeField).toBeVisible();
    await expect(contactTypeField).toBeDisabled();
    await expect(contactTypeField).toHaveText(/lead/i);
    await expect(page.getByPlaceholder('Name', { exact: true })).toHaveValue(
      originalName,
    );

    await page.getByPlaceholder('Name', { exact: true }).fill(rowEditedName);
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Updated')).toBeVisible();

    await findContactRow(page, rowEditedName, 'Leads');
  });

  test('edits the contact via the details-page Actions menu', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    const row = await findContactRow(page, rowEditedName, 'Leads');
    await row.getByRole('link', { name: rowEditedName, exact: true }).click();
    // Wait for the detail page (and its background data fetches) to settle
    // before opening the dropdown — clicking "Actions" too early raced with
    // a re-render that closed the menu right as the item click landed.
    await expect(page.getByRole('heading', { name: rowEditedName })).toBeVisible();

    await clickMenuItem(page, page.getByRole('button', { name: 'Actions' }), 'Edit Contact');
    await expect(page.getByRole('heading', { name: 'Edit Contact' })).toBeVisible();

    // Detail-page edit omits contactTypeOptions, so no Contact Type field
    // renders at all here (unlike the row-level edit modal above).
    await expect(
      page.locator('label').filter({ hasText: 'Contact Type' }),
    ).toHaveCount(0);
    await expect(page.getByPlaceholder('Name', { exact: true })).toHaveValue(
      rowEditedName,
    );

    await page.getByPlaceholder('Name', { exact: true }).fill(detailEditedName);
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Updated')).toBeVisible();
    await expect(
      page.getByRole('heading', { name: detailEditedName }),
    ).toBeVisible();
  });

  test('deletes the contact via the details-page Actions menu', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    const row = await findContactRow(page, detailEditedName, 'Leads');
    await row
      .getByRole('link', { name: detailEditedName, exact: true })
      .click();
    await expect(
      page.getByRole('heading', { name: detailEditedName }),
    ).toBeVisible();

    await clickMenuItem(page, page.getByRole('button', { name: 'Actions' }), 'Delete Contact');
    await expect(page.getByRole('heading', { name: 'Delete Contact' })).toBeVisible();
    await page.getByRole('button', { name: 'Delete', exact: true }).click();

    // Title-case "Contact Deleted" — distinct from the row-level delete
    // path's lowercase "Contact deleted" toast (create-contact.spec.ts).
    await expect(page.getByText('Contact Deleted')).toBeVisible();
    await expect(page).toHaveURL(/\/lien\/contacts$/);

    await page
      .getByPlaceholder('Search contacts by name, org, or email...')
      .fill(detailEditedName);
    await expect(page.getByRole('row', { name: detailEditedName })).toBeHidden();
  });
});
