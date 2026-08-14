import { createReadOnlyTest, expectURL, expectVisible } from '../../../support/readonly-test';
import { getEnv } from '../../../config/environments';

const test = createReadOnlyTest('lien');
const env = getEnv();

test.describe(`Selling contacts — company detail page and tabs [${env.name}]`, () => {
  test('opens a company from the list and navigates all 4 detail tabs', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    await expectURL(page, /\/selling\/contacts$/);

    const firstRowLink = page.locator('tbody tr').first().getByRole('link');
    if ((await page.locator('tbody tr').count()) === 0) {
      test.skip(true, 'No companies seeded in this environment to open a detail page for.');
    }

    await firstRowLink.click();
    // Bare /selling/contacts/{id} server-redirects to .../overview.
    await expectURL(page, /\/selling\/contacts\/[^/]+\/overview$/);

    await expectVisible(page.getByRole('link', { name: 'Back to Contacts' }));
    await expectVisible(page.getByText(/Contact ID:/));
    await expectVisible(page.getByRole('button', { name: 'Manage Company' }));

    const tabs: Array<[name: string, path: string]> = [
      ['Overview', 'overview'],
      ['Cases', 'cases'],
      ['Activities', 'activities'],
      ['Contact Person', 'contacts'],
    ];

    for (const [label, path] of tabs) {
      await page.getByRole('link', { name: label, exact: true }).click();
      await expectURL(page, new RegExp(`/selling/contacts/[^/]+/${path}$`));
    }
  });
});
