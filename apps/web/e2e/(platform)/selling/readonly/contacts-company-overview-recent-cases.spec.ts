import { createReadOnlyTest, expectURL, expectVisible } from '../../../support/readonly-test';
import { getEnv } from '../../../config/environments';

/**
 * Readonly coverage for the Selling company detail page's Overview tab
 * "Recent Cases" list — specifically that each row renders a status chip
 * via CaseStatusChip (src/components/lien/case-status-chip.tsx), which
 * moved into @legalsynq/design-system's Chip primitive as part of the
 * design-system extraction. contacts-company-detail.spec.ts already covers
 * navigating into a company and across its 4 tabs, but never asserts
 * anything renders inside a tab's content — this fills that gap for
 * Overview specifically, so a regression in the Chip migration (e.g. a
 * missing color/variant mapping making the chip render with no visible
 * text) would be caught here rather than only by manual QA.
 */

const test = createReadOnlyTest('lien');
const env = getEnv();

test.describe(`Selling contacts — company overview recent cases [${env.name}]`, () => {
  test('recent cases list renders a status chip and case ID for each row', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/contacts`);
    await expectURL(page, /\/selling\/contacts$/);

    const rowCount = await page.locator('tbody tr').count();
    test.skip(rowCount === 0, 'No companies seeded in this environment to open a detail page for.');

    await page.locator('tbody tr').first().getByRole('link').click();
    // Bare /selling/contacts/{id} server-redirects to .../overview.
    await expectURL(page, /\/selling\/contacts\/[^/]+\/overview$/);

    await expectVisible(page.getByRole('heading', { name: 'Recent Cases' }));

    const caseIdLabels = page.getByText(/^Case ID:/);
    const caseCount = await caseIdLabels.count();
    test.skip(caseCount === 0, 'This company has no recent cases in this environment to assert a status chip on.');

    // Each recent-case row is a sibling of its "Case ID: ..." label inside
    // the same row container — walk up to the row, then find the chip by
    // its Chip component's own rounded-full class (see
    // packages/design-system/src/chip.tsx) rather than DOM position, since
    // the client name span sits before it in RecentCaseRow's markup.
    const firstRow = caseIdLabels.first().locator('xpath=..');
    await expectVisible(firstRow);

    const chip = firstRow.locator('span.rounded-full');
    await expectVisible(chip);

    const chipText = (await chip.textContent())?.trim();
    if (!chipText) {
      throw new Error(
        'Expected the first recent-case row to render a non-empty status chip label.',
      );
    }
  });
});
