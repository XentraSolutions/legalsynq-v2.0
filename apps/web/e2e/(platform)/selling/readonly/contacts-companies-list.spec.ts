import { createReadOnlyTest, expectURL, expectVisible } from '../../../support/readonly-test';
import { getEnv } from '../../../config/environments';

const test = createReadOnlyTest('lien');
const env = getEnv();

test.describe(`Selling contacts — Companies list [${env.name}]`, () => {
  test('renders the Companies list with tabs, table columns, and toolbar', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    await expectURL(page, /\/selling\/contacts$/);

    await expectVisible(page.getByRole('heading', { name: 'Contacts' }));
    await expectVisible(page.getByRole('link', { name: 'Companies', exact: true }));
    await expectVisible(page.getByRole('link', { name: 'Contact Person', exact: true }));

    await expectVisible(
      page.getByPlaceholder('Search companies by name, email...'),
    );
    await expectVisible(page.getByRole('columnheader', { name: 'Company Name' }));
    await expectVisible(page.getByRole('columnheader', { name: 'Email' }));
    await expectVisible(page.getByRole('columnheader', { name: 'Type' }));
    await expectVisible(page.getByRole('columnheader', { name: 'Active Cases' }));
  });
});
