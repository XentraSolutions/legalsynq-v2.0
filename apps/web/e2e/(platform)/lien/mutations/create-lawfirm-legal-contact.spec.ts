import { createMutationTest, expect } from '../../../support/mutation-test';
import { selectComboboxOption } from '../../../support/combobox';
import { findContactRow } from '../../../support/contacts-list';
import { clickMenuItem } from '../../../support/dropdown-menu';
import { getEnv } from '../../../config/environments';

/**
 * Covers the LawFirm contact type's only subtype: a Role, assigned to a
 * "Legal Contact" sub-contact attached to a specific Law Firm. Unlike the
 * top-level create flow (create-contact.spec.ts), this Role select
 * (src/components/lien/law-firm-contact-section.tsx) is fed by the live
 * lookup `GET /contact/lawfirm/role` — tenant-configured, not hardcoded — so
 * this only asserts against "Case Manager", the one role name guaranteed
 * resolvable by convention (see resolveCaseManagerRoleCode in
 * contacts.service.ts, matched case-insensitively by code or name).
 *
 * Deletion is its own test at the end, not inline right after creation —
 * see the comment in create-contact.spec.ts for why. Ordering relies on
 * this repo's playwright.config.ts (workers: 1, fullyParallel: false).
 *
 * Mutation spec: creates a Law Firm contact plus one Legal Contact under it,
 * then deletes both. Runs against local/qa only.
 */

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`SynqLien contacts — LawFirm Legal Contact subtype [${env.name}]`, () => {
  const lawFirmName = `E2E LawFirm ${Date.now()}`;
  const firstName = 'E2E';
  const lastName = `CaseManager ${Date.now()}`;

  test('creates a Law Firm contact with a Legal Contact (Case Manager role)', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    await page.getByRole('button', { name: 'Add Contact' }).click();
    await expect(
      page.getByRole('heading', { name: 'Add New Contact' }),
    ).toBeVisible();
    await selectComboboxOption(page, 'Contact Type', 'law');
    await page.getByPlaceholder('Name', { exact: true }).fill(lawFirmName);
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Created')).toBeVisible();

    const row = await findContactRow(page, lawFirmName);
    await row.getByRole('link', { name: lawFirmName, exact: true }).click();

    await page.getByRole('link', { name: 'Legal Contacts' }).click();
    await expect(
      page.getByRole('heading', { name: 'Legal Contacts' }),
    ).toBeVisible();

    await page.getByRole('button', { name: 'Add Contact' }).click();
    await expect(
      page.getByRole('heading', { name: 'Add Legal Contact' }),
    ).toBeVisible();
    await page.getByPlaceholder('First name').fill(firstName);
    await page.getByPlaceholder('Last name').fill(lastName);
    await selectComboboxOption(page, 'Role', 'case manager');
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Contact Created')).toBeVisible();

    // .first(): the toast's transient description text can briefly duplicate
    // this same string until it auto-dismisses (see lien-store.ts's 4s timeout).
    await expect(
      page.getByText(`${firstName} ${lastName}`).first(),
    ).toBeVisible();
    await expect(page.getByText('Case Manager').first()).toBeVisible();
  });

  test('deletes the Legal Contact and the parent Law Firm created above', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    const initialRow = await findContactRow(page, lawFirmName);
    await initialRow
      .getByRole('link', { name: lawFirmName, exact: true })
      .click();
    await page.getByRole('link', { name: 'Legal Contacts' }).click();

    const subContactCard = page.getByText(`${firstName} ${lastName}`).first();
    await expect(subContactCard).toBeVisible();
    await clickMenuItem(page, page.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByText('Contact Removed')).toBeVisible();
    await expect(subContactCard).toBeHidden();

    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    // findContactRow also waits out the "Refreshing..." indicator that
    // briefly overlaps the Actions column while the search re-query is in
    // flight — clicking through it too early can silently miss the button.
    const lawFirmRow = await findContactRow(page, lawFirmName);
    await clickMenuItem(page, lawFirmRow.getByRole('button', { name: 'Actions menu' }), 'Delete');
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(page.getByText('Contact deleted')).toBeVisible();
    await expect(lawFirmRow).toBeHidden();
  });
});
