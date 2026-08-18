import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';

/**
 * Covers the "sell a portfolio lien" wizard's buyer-selection step
 * (/selling/portfolio/lien/[id]/sell/step-1) through to landing on the
 * review step (step-2) — not the full "Authorize & Send", which requires
 * uploading real documents for every required slot
 * (REQUIRED_SALE_DOCUMENT_TYPES in selling-detail.mapper.ts) and would move
 * the lien into an in-sale state on real tenant data.
 *
 * Only a Pending lien can be sold — its detail page auto-opens the
 * Keep/Sell decision modal (autoOpenDecision in lien-row-actions-menu.tsx,
 * gated on sellerStatus === "Pending"). Internal liens don't offer this
 * path. Picks whichever lien in this tenant's selling portfolio has status
 * Pending (despite the LienListItem type also declaring a `sellerStatus`
 * field, the real GET /selling/api/liens/selling/liens response only ever
 * carries `status`) via the same list endpoint the /selling/portfolio page
 * itself calls, rather than hardcoding a lien number.
 *
 * Selecting a buyer persists immediately via saveCaseInformation (step 1's
 * Continue) — there's no "clear buyer selection" endpoint to undo that, so
 * this is left in place afterwards, same as the non-deletable resources
 * other specs in this suite leave behind (see
 * add-lien-to-sale-portfolio.spec.ts's docstring). "Save for Later" on step
 * 2 is a client-side-only navigation (no sellerStatus change), so the lien
 * doesn't move into an in-sale state.
 */

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`SynqLien selling — sell lien wizard [${env.name}]`, () => {
  test('selects a buyer, reaches the review step, and exits via Save for Later', async ({ page, credentials }) => {
    const origin = env.originFor(credentials.tenantCode);

    await page.goto(`${origin}/selling/portfolio`);
    const liensData = (await page.evaluate(() =>
      fetch('/api/selling/api/liens/selling/liens?pageSize=100&page=1').then((res) => res.json()),
    )) as { items: Array<{ lienId: string; lienNumber: string; status: string }> };

    const candidates = liensData.items.filter((item) => item.status === 'Pending');
    expect(
      candidates.length,
      "expected at least one Pending lien in this tenant's selling portfolio",
    ).toBeGreaterThan(0);
    const lien = candidates[0];

    // Heading is fundingCompany.name || lienNumber (portfolio-details.tsx) —
    // a lien that already has a buyer assigned shows the company name there
    // instead, so check the lien number subtitle underneath it instead.
    await page.goto(`${origin}/selling/portfolio/lien/${lien.lienId}`);
    await expect(page.getByText(lien.lienNumber)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Manage Lien' })).toBeVisible();

    const decisionModal = page
      .getByRole('dialog')
      .filter({ has: page.getByRole('heading', { name: 'What Would You Like to Do With This Lien?' }) });
    await expect(decisionModal).toBeVisible();
    await decisionModal.getByRole('button', { name: 'Sell' }).click();

    await expect(page).toHaveURL(/\/sell\/step-1$/);
    await expect(page.getByRole('heading', { name: 'Select a Funding Company' })).toBeVisible();

    const firstCompanyOption = page
      .locator('label')
      .filter({ has: page.locator('input[name="fundingCompany"]') })
      .first();
    await expect(
      firstCompanyOption,
      'expected at least one funding company available to select as buyer',
    ).toBeVisible();
    const companyName = (await firstCompanyOption.locator('span').textContent())?.trim();
    await firstCompanyOption.click();

    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page).toHaveURL(/\/sell\/step-2$/);
    await expect(page.getByRole('heading', { name: 'Prepare Your Lien for Sale' })).toBeVisible();
    if (companyName) {
      await expect(page.getByText(companyName).first()).toBeVisible();
    }

    await page.getByRole('button', { name: 'Save for Later' }).click();
    await expect(page).toHaveURL(new RegExp(`/selling/portfolio/lien/${lien.lienId}$`));
  });
});
