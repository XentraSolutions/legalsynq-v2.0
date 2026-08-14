import { createReadOnlyTest, expectURL, expectVisible } from '../../../support/readonly-test';
import { getEnv } from '../../../config/environments';

const test = createReadOnlyTest('lien');
const env = getEnv();

test.describe(`Selling contacts — Companies/Contact Person tab switching [${env.name}]`, () => {
  test('defaults to Companies and switches to Contact Person via URL-driven tab', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    await expectURL(page, /\/selling\/contacts$/);
    await expectVisible(page.getByRole('columnheader', { name: 'Company Name' }));

    await page.getByRole('link', { name: 'Contact Person', exact: true }).click();
    await expectURL(page, /\/selling\/contacts\?view=contacts$/);
    await expectVisible(page.getByRole('columnheader', { name: 'Name' }));
    await expectVisible(page.getByRole('columnheader', { name: 'Company' }));
    await expectVisible(page.getByRole('columnheader', { name: 'Role' }));

    await page.getByRole('link', { name: 'Companies', exact: true }).click();
    await expectURL(page, /\/selling\/contacts$/);
    await expectVisible(page.getByRole('columnheader', { name: 'Company Name' }));
  });
});
