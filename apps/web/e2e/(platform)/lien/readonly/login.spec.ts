import { createReadOnlyTest, expectURL, expectVisible } from '../../../support/readonly-test';
import { getEnv } from '../../../config/environments';

/**
 * e2e smoke test — SynqLien tenant login → dashboard. Read-only: runs
 * against every environment, including production.
 *
 * No mocks: runs against a live environment selected via E2E_ENV (see
 * e2e/config/environments.ts) — "local" (local frontend + QA backend, the
 * default), "qa" (deployed QA frontend), or "production" (scaffolded, not
 * wired up yet).
 *
 * Built on readonly-test.ts: the `page` fixture arrives already logged in
 * and on /dashboard, typed as ReadOnlyPage — see read-only-page.ts for what
 * that does and does not allow. This spec only navigates and asserts; it
 * never fills a form or clicks anything but a link, which is what makes it
 * safe to run against every environment, production included.
 *
 * A spec that needs to create/update/delete data belongs next to this one
 * under .../mutations/ instead, built on createMutationTest() from
 * mutation-test.ts — that factory refuses to run against production.
 *
 * Needs e2e/data/credentials.json populated for the target environment
 * (gitignored — copy credentials.example.json and fill in real values).
 */

const test = createReadOnlyTest('lien');
const env = getEnv();

test.describe(`SynqLien login → dashboard [${env.name}]`, () => {

  test('lands on the dashboard already authenticated, then loads the SynqLien dashboard', async ({ page }) => {
    await expectURL(page, /\/dashboard$/, { timeout: 20_000 });
    await expectVisible(page.getByText(/welcome back/i));

    await page.getByRole('link', { name: /synq liens/i }).click();

    await expectURL(page, /\/lien\/dashboard$/, { timeout: 20_000 });
    await expectVisible(page.getByRole('heading', { name: 'Dashboard' }));
    await expectVisible(page.getByText('Reporting Period'));
  });

});
