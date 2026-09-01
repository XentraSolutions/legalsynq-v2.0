import fs from 'fs';
import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';

const test = createMutationTest('lien');
const env = getEnv();

/**
 * Covers the case-scoped Bulk Upload flow on a case's Liens tab
 * (/selling/portfolio/cases/[id] — see case-liens-tab.tsx and
 * bulk-upload-form.tsx's `caseCode` prop): the template download has its
 * "Case Code*" column stripped (the case is already known from the page),
 * and uploading a CSV with no Case Code column still succeeds because the
 * form re-injects it client-side (csv-utils.ts's stripFirstCsvColumn /
 * prependCsvColumn) before submitting.
 *
 * Like bulk-upload-review.spec.ts, this doesn't hardcode the template's
 * column list — it downloads the real (case-scoped) template and asserts
 * against its own header row, so a template change on the backend changes
 * what this test expects instead of silently drifting out of sync.
 *
 * Stops at "Cancel" on the review modal: confirming creates a real Lien
 * with no delete endpoint, and this spec only needs to prove the case code
 * was interpolated correctly, not exercise the confirm step (already
 * covered, for the non-case-scoped path, by bulk-upload-review.spec.ts).
 */

function parseCsvLine(line: string): string[] {
  return line.split(',').map((field) => field.trim());
}

test.describe(`Selling case Liens tab — bulk upload case-code interception [${env.name}]`, () => {
  test('strips Case Code from the template download and re-injects it on upload', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/portfolio/cases`);

    await page.waitForFunction(
      () => {
        const rows = document.querySelectorAll('table tbody tr');
        return rows.length > 0 && !(rows[0].textContent ?? '').includes('Loading...');
      },
      { timeout: 15_000 },
    );

    const firstRow = page.locator('table tbody tr').first();
    const hasCase = !(await firstRow.locator('td[colspan]').isVisible().catch(() => false));
    test.skip(!hasCase, 'No cases exist in this environment yet.');

    await firstRow.locator('td').first().locator('a').click();
    await page.waitForURL(/\/selling\/portfolio\/cases\/[^/]+$/, { timeout: 20_000 });

    // The page header's subtitle renders caseDetail.caseNumber directly —
    // the same value passed to CaseLiensTab's `caseCode` prop — so read it
    // from there rather than the (possibly differently formatted) list cell.
    const caseCode = (
      await page.locator('p.text-sm.text-gray-400.mt-1.truncate').first().innerText()
    ).trim();
    expect(caseCode, 'expected the case detail page to show a case code').not.toBe('');

    await page.getByRole('button', { name: 'Liens', exact: true }).click();

    await page.getByRole('button', { name: 'Add New Lien' }).click();
    await page.getByRole('menuitem', { name: 'Bulk Upload' }).click();

    const uploadDialog = page.getByRole('dialog').filter({ hasText: 'Lien Bulk Upload' });
    await expect(uploadDialog).toBeVisible();

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      uploadDialog.getByRole('button', { name: 'Download Template' }).click(),
    ]);
    const templatePath = await download.path();
    expect(templatePath, 'expected the template download to succeed').toBeTruthy();
    const templateCsv = fs.readFileSync(templatePath as string, 'utf-8');

    const [headerLine, exampleLine] = templateCsv.trim().split(/\r?\n/);
    const headers = parseCsvLine(headerLine);

    // The case-scoped template must not carry a Case Code column — it's
    // already known from the page and gets re-added before upload.
    expect(
      headers.some((h) => h.startsWith('Case Code')),
      'expected the case-scoped template to have Case Code stripped',
    ).toBe(false);

    const uploadCsv = [headerLine, exampleLine].join('\n');

    await uploadDialog.locator('input[type="file"]').setInputFiles({
      name: 'case-bulk-upload-e2e.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(uploadCsv, 'utf-8'),
    });
    await uploadDialog.getByRole('button', { name: 'Continue' }).click();

    const reviewDialog = page
      .getByRole('dialog')
      .filter({ has: page.getByRole('heading', { name: 'Review Bulk Upload Details' }) });
    await expect(reviewDialog).toBeVisible({ timeout: 15_000 });

    // The uploaded file had no Case Code column, so the review table only
    // showing the correct value proves the form injected it client-side
    // rather than the row failing validation or coming back blank.
    await expect(reviewDialog.getByRole('columnheader', { name: 'Case Code' })).toBeVisible();
    await expect(reviewDialog.getByText(caseCode)).toBeVisible();

    await reviewDialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(reviewDialog).toBeHidden();
  });
});
