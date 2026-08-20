import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';
import { baseSelectTrigger } from '../../../support/base-select';

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`Selling contacts — Contact Person directory filter modal [${env.name}]`, () => {
  test('opens with Company Type and Role fields, applies, and shows an active-filter badge', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts?view=contacts`);

    await page.getByRole('button', { name: 'Filter', exact: true }).click();
    const modal = page.getByRole('heading', { name: 'Filter Contact Persons' }).locator('xpath=ancestor::*[@role="dialog"]');
    await expect(modal).toBeVisible();
    await expect(modal.getByText('Company Type', { exact: true })).toBeVisible();
    await expect(modal.getByText('Role', { exact: true })).toBeVisible();

    // Role is scoped to whichever Company Type is picked, so it starts empty/disabled
    // until a Company Type is chosen — pick the first available one.
    const companyTypeTrigger = baseSelectTrigger(page, 'Company Type');
    await companyTypeTrigger.click();
    await page.locator('[data-radix-popper-content-wrapper]').getByRole('option').first().click();

    await page.getByRole('button', { name: 'Apply Filters' }).click();
    await expect(page.getByRole('heading', { name: 'Filter Contact Persons' })).toBeHidden();

    // activeFilterCount badge — a single pill showing "1" next to the Filter button.
    await expect(page.getByRole('button', { name: 'Filter' }).getByText('1', { exact: true })).toBeVisible();

    // Clearing resets the badge.
    await page.getByRole('button', { name: 'Filter' }).click();
    await page.getByRole('button', { name: 'Clear Filter' }).click();
    await page.getByRole('button', { name: 'Apply Filters' }).click();
    await expect(page.getByRole('button', { name: 'Filter' }).getByText('1', { exact: true })).toBeHidden();
  });
});
