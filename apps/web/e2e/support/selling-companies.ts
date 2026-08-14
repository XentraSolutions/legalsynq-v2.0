import { expect, type Locator, type Page } from '@playwright/test';
import { selectBaseSelectOption } from './base-select';
import { clickMenuItem } from './dropdown-menu';

/**
 * Waits out the "Refreshing..." indicator (contacts/page.tsx,
 * contact-persons-directory-view.tsx) that's absolutely positioned over the
 * top-right of the table while a debounced search/filter re-query is in
 * flight — clicking anything in that corner before it clears can silently
 * land on the overlay instead of the control underneath.
 */
export async function waitForRefresh(page: Page): Promise<void> {
  await expect(page.getByText('Refreshing...')).toBeHidden();
}

/** Types into the Companies list search box and waits for the debounced re-query to settle. */
export async function searchCompanies(page: Page, term: string): Promise<void> {
  await page.getByPlaceholder('Search companies by name, email...').fill(term);
  await waitForRefresh(page);
}

/** Types into the Contact Person directory search box and waits for the debounced re-query to settle. */
export async function searchContactPersons(page: Page, term: string): Promise<void> {
  await page.getByPlaceholder('Search contact persons by name, email...').fill(term);
  await waitForRefresh(page);
}

/** Finds a Companies list row by (unique) name, waiting for any in-flight search first. */
export function findCompanyRow(page: Page, name: string): Locator {
  return page.getByRole('row', { name });
}

/**
 * A real, pre-existing company in the rl-liens1 tenant known to have at least
 * one Contact Person Type ("Underwriter") configured for its Company Type —
 * see e2e/(platform)/selling/mutations/create-contact-person.spec.ts, the
 * first spec written against this flow. Not every Company Type a fresh
 * "Add Company" picks at random has roles configured, so contact-person
 * specs that need a working Role dropdown reuse this company instead of
 * creating a new one with an arbitrary type.
 */
export const KNOWN_COMPANY_NAME = 'FundComp';
export const KNOWN_COMPANY_SEARCH = 'fundcomp';
export const KNOWN_ROLE_SEARCH = 'underwriter';

/** Reads the Companies list "Type" column text for `companyName`, e.g. to reuse its Company Type elsewhere. */
export async function getCompanyTypeLabel(page: Page, companyName: string): Promise<string> {
  await searchCompanies(page, companyName);
  const typeCell = findCompanyRow(page, companyName).getByRole('cell').nth(2);
  return ((await typeCell.textContent()) ?? '').trim();
}

/**
 * Creates a company via the "Add Company" modal on /selling/contacts,
 * picking whatever the first available Company Type option is (deterministic
 * across tenants isn't guaranteed, so this only asserts the flow works, not
 * a specific type). Declines the "Add contact person?" follow-up prompt.
 * Returns the generated unique name so the caller can find/clean it up.
 */
export async function createTestCompany(
  page: Page,
  namePrefix = 'E2E Test Co',
  typeSearchTerm = '',
): Promise<string> {
  const name = `${namePrefix} ${Date.now()}`;
  await page.getByRole('button', { name: 'Add Company' }).click();
  await expect(page.getByRole('heading', { name: 'Add Company' })).toBeVisible();

  await selectBaseSelectOption(page, 'Company Type', typeSearchTerm);
  await page
    .locator('label', { hasText: 'Name' })
    .locator('xpath=following-sibling::input[1]')
    .fill(name);

  await page.getByRole('button', { name: 'Create' }).click();
  await expect(page.getByRole('heading', { name: 'New Company Added' })).toBeVisible();
  await page.getByRole('button', { name: 'Maybe Later' }).click();
  await expect(page.getByRole('heading', { name: 'New Company Added' })).toBeHidden();

  return name;
}

/**
 * A promise for the Role combobox's contact-person-types lookup resolving.
 * Role options only load once a company (and thus its Company Type) is
 * known — opening the Role combobox before that request settles finds an
 * empty option list and, if a `createAction` is configured, silently falls
 * through to "Add New Role" instead. Start this *before* triggering
 * whatever picks the company (see create-contact-person.spec.ts, where this
 * was first worked out), then await it before opening Role.
 */
export function waitForContactPersonTypesResponse(page: Page) {
  return page.waitForResponse((res) => res.url().includes('/lookups/contact-person-types') && res.ok());
}

/** Deletes a company by name from the Companies list row actions menu, confirming the warning dialog. */
export async function deleteTestCompany(page: Page, name: string): Promise<void> {
  await searchCompanies(page, name);
  const row = findCompanyRow(page, name);
  await expect(row).toBeVisible();
  await clickMenuItem(page, row.getByRole('button', { name: 'Actions menu' }), 'Delete');
  await expect(page.getByRole('heading', { name: 'Delete Company' })).toBeVisible();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Delete Company' })).toBeHidden();
  await searchCompanies(page, name);
  await expect(row).toBeHidden();
}
