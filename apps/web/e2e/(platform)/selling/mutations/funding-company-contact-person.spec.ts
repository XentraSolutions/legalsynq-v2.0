import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import type { PageOf } from '../../../support/test-types';
import {
  KNOWN_COMPANY_NAME,
  KNOWN_ROLE_SEARCH,
  createTestCompany,
  deleteTestCompany,
  getCompanyTypeLabel,
  waitForContactPersonTypesResponse,
} from '../../../support/selling-companies';
import { baseSelectTrigger, selectBaseSelectOption } from '../../../support/base-select';

const test = createMutationTest('lien');
const env = getEnv();

type TestPage = PageOf<typeof test>;

async function fillContactPersonNames(page: TestPage, first: string, last: string) {
  await page
    .locator('label', { hasText: 'First Name' })
    .locator('xpath=following-sibling::input[1]')
    .fill(first);
  await page
    .locator('label', { hasText: 'Last Name' })
    .locator('xpath=following-sibling::input[1]')
    .fill(last);
}

/**
 * Regression coverage for LSV3-1294: the lien wizard's "Provider & Funding
 * Details" step (funding-company-info.tsx -> provider-funding-fields.tsx)
 * has its own Contact Person picker, separate from the Companies-directory
 * "Add Contact Person" flow already covered by add-contact-person.spec.ts.
 * That picker's `FundingCompanyContactSelect` called `SellingEntitySelect`
 * with `isContactPerson` but no `entityType` — the create-modal render guard
 * (`showCreate && isContactPerson && companyId && companyType`) silently
 * never passed because `companyType` only ever resolves from `entityType`,
 * so clicking "Add Contact Person" flipped internal state but no modal
 * appeared. See provider-funding-fields.tsx's `FundingCompanyContactSelect`.
 */
test.describe(`Selling lien wizard — funding company contact person [${env.name}]`, () => {
  test('"Add Contact Person" on the Provider & Funding Details step opens the create modal', async ({
    page,
    credentials,
  }) => {
    const origin = env.originFor(credentials.tenantCode);

    // A dedicated, disposable funding company so this test doesn't depend on
    // (or pollute) KNOWN_COMPANY_NAME's existing contacts — reuses its
    // Company Type, which is known to have Contact Person Types configured.
    await page.goto(`${origin}/selling/contacts`);
    const fundingCompanyType = await getCompanyTypeLabel(page, KNOWN_COMPANY_NAME);
    const companyName = await createTestCompany(page, 'E2E Funding Co', fundingCompanyType);

    const liensData = (await page.evaluate(() =>
      fetch('/api/selling/api/liens/selling/liens?pageSize=1&page=1').then((res) => res.json()),
    )) as { items: Array<{ lienId: string }> };
    expect(liensData.items.length, 'expected at least one lien in this tenant\'s selling portfolio').toBeGreaterThan(0);
    const lienId = liensData.items[0].lienId;

    // Navigated to directly rather than via the wizard's own flow — this
    // step only reads/writes the lien's provider & funding association, and
    // nothing here is saved (no "Continue" click), so the lien itself is
    // left untouched.
    await page.goto(`${origin}/selling/portfolio/lien/${lienId}/edit/step-2`);
    await expect(page.getByText('Provider & Funding Details')).toBeVisible();

    await selectBaseSelectOption(page, 'Funding Company', companyName);
    await expect(page.getByText('Select a funding company first')).toBeHidden();

    await baseSelectTrigger(page, 'Contact Person').click();
    await expect(page.getByText('No Available Contact Person')).toBeVisible();

    const rolesLoaded = waitForContactPersonTypesResponse(page);
    await page.getByRole('button', { name: 'Add Contact Person' }).click();
    await rolesLoaded;
    // This is the assertion that fails without the fix: the modal never
    // rendered because `companyType` silently resolved to undefined.
    await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeVisible();

    const lastName = `E2E Funding Contact ${Date.now()}`;
    await selectBaseSelectOption(page, 'Role', KNOWN_ROLE_SEARCH);
    await fillContactPersonNames(page, 'E2E', lastName);
    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByRole('heading', { name: 'Add Contact Person' })).toBeHidden();

    await expect(baseSelectTrigger(page, 'Contact Person')).toContainText(lastName);

    // Deleting the company cascades and removes the contact created above.
    await page.goto(`${origin}/selling/contacts`);
    await deleteTestCompany(page, companyName);
  });
});
