import { createReadOnlyTest, expectURL, expectVisible } from '../../../support/readonly-test';
import { getEnv } from '../../../config/environments';

const test = createReadOnlyTest('lien');
const env = getEnv();

test.describe(`Selling contacts — Contact Person directory view [${env.name}]`, () => {
  test('renders the directory table and links back to the owning company', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts?view=contacts`);
    await expectURL(page, /\/selling\/contacts\?view=contacts$/);

    await expectVisible(
      page.getByPlaceholder('Search contact persons by name, email...'),
    );
    await expectVisible(page.getByRole('button', { name: /Filter/ }));
    await expectVisible(page.getByRole('columnheader', { name: 'Name' }));
    await expectVisible(page.getByRole('columnheader', { name: 'Company' }));
    await expectVisible(page.getByRole('columnheader', { name: 'Role' }));
    await expectVisible(page.getByRole('columnheader', { name: 'Email' }));
    await expectVisible(page.getByRole('columnheader', { name: 'Phone' }));

    const rowCount = await page.locator('tbody tr').count();
    if (rowCount === 0) {
      test.skip(true, 'No contact persons seeded in this environment to follow a company link for.');
    }

    const companyLink = page.locator('tbody tr').first().getByRole('link');
    await companyLink.click();
    // Bare /selling/contacts/{id} server-redirects to .../overview.
    await expectURL(page, /\/selling\/contacts\/[^/]+\/overview$/);
  });
});
