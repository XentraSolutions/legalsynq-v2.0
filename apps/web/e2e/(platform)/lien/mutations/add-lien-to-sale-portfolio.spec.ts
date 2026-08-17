import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';

/**
 * Covers adding a single lien to a Lien Sales portfolio
 * (/lien/sales/[id] "Portfolio Builder").
 *
 * The backend rejects a lien with a zero balance (BALANCE_NOT_POSITIVE) or
 * one already assigned to another portfolio (LIEN_ALREADY_ASSIGNED) — and
 * the current UI silently swallows that rejection (the response's
 * addedCount/failedCount is never surfaced, see the flagged follow-up).
 * Rather than hardcode a lien number that happens to work today, this pulls
 * candidates with currentBalance > 0 straight from the same
 * GET /lien/api/liens/liens the /lien/liens page itself calls — via an
 * in-page fetch() (page.evaluate) rather than page.request, since
 * page.request resolves hosts through Node's own DNS and this repo's
 * *.localhost tenant subdomains only resolve inside the browser — then
 * tries them one at a time through the real UI until one is actually
 * accepted, robust to which specific liens are already spoken for in this
 * tenant.
 *
 * Cleanup removes the lien from the portfolio afterwards (the one mutation
 * this flow supports undoing) — there's no delete endpoint for the
 * portfolio itself (lien-sales.api.ts only exposes publish/withdraw), so the
 * DRAFT portfolio this test creates is left behind, same as other
 * non-deletable resources in this suite.
 */

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`SynqLien sales — add a single lien to a portfolio [${env.name}]`, () => {
  test('creates a portfolio, adds one lien, and it appears in Included Liens', async ({ page, credentials }) => {
    const origin = env.originFor(credentials.tenantCode);

    await page.goto(`${origin}/lien/liens`);
    const liensData = await page.evaluate(
      () => fetch('/api/lien/api/liens/liens?pageSize=100&page=1').then(res => res.json()),
    ) as { items: Array<{ lienNumber: string; currentBalance: number | null }> };
    const candidates = Array.from(new Set(
      liensData.items
        .filter(item => (item.currentBalance ?? 0) > 0)
        .map(item => item.lienNumber),
    )).slice(0, 8);
    expect(candidates.length, 'expected at least one lien with a positive balance in this tenant').toBeGreaterThan(0);

    const portfolioNumber = `E2E-SALE-${Date.now()}`;
    const portfolioName = `E2E Sale Portfolio ${Date.now()}`;

    await page.goto(`${origin}/lien/sales/new`);
    await expect(page.getByRole('heading', { name: 'New Sale Portfolio' })).toBeVisible();
    await page.getByLabel('Portfolio Number').fill(portfolioNumber);
    await page.getByLabel('Name').fill(portfolioName);
    await page.getByRole('button', { name: 'Create Portfolio' }).click();

    await expect(page).toHaveURL(/\/lien\/sales\/[^/]+$/);
    await expect(page.getByRole('heading', { name: portfolioName })).toBeVisible();

    const liensInput = page.getByPlaceholder('LIEN-001, LIEN-002, or one per line');
    const addButton = page.getByRole('button', { name: 'Add Liens' });

    let addedLienNumber: string | null = null;
    for (const candidate of candidates) {
      await liensInput.fill(candidate);
      await addButton.click();
      await expect(addButton).toBeEnabled({ timeout: 15_000 });

      if (await page.locator('tbody tr').filter({ hasText: candidate }).count() > 0) {
        addedLienNumber = candidate;
        break;
      }
    }
    expect(addedLienNumber, 'none of the candidate liens were accepted into the portfolio').not.toBeNull();

    const lienRow = page.locator('tbody tr').filter({ hasText: addedLienNumber! });
    await expect(lienRow).toBeVisible();
    await expect(page.getByText('No liens have been added.')).toBeHidden();

    await lienRow.getByRole('button', { name: 'Remove' }).click();
    await expect(lienRow).toBeHidden();
    await expect(page.getByText('No liens have been added.')).toBeVisible();
  });
});
