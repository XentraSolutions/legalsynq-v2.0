import fs from 'fs';
import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';

const test = createMutationTest('lien');
const env = getEnv();

/**
 * Covers the bulk-upload review step (/selling/portfolio's "Bulk Upload"
 * flow) through to the "Review Bulk Upload Details" table, then cancels
 * instead of confirming.
 *
 * The review table's columns are derived at render time from the row data
 * the backend returns (GET /bulk-imports/{id}/rows), not from a hardcoded
 * field list — see buildColumns() in review-bulk-upload-modal.tsx. To keep
 * this spec from going stale the same way the old hardcoded column list did,
 * it doesn't hardcode the template's fields either: it downloads the real
 * template via the "Download Template" link (same file the form's own
 * downloadTemplate() serves), re-uploads it with only the Case Code changed
 * to a unique value, and asserts the review table's headers against
 * *that downloaded header row* — so a template column change on the backend
 * changes what this test expects, instead of silently going out of sync.
 *
 * Stops at "Cancel" on the review modal: confirming creates a real Lien/Case
 * with no delete endpoint, and this spec only needs to exercise validation
 * and rendering, not the confirm step (already covered manually against a
 * successful import).
 */

/** Minimal CSV line parser — sufficient for the template's unquoted/simple fields. */
function parseCsvLine(line: string): string[] {
  return line.split(',').map((field) => field.trim());
}

test.describe(`Selling portfolio — bulk upload review [${env.name}]`, () => {
  test('uploads the downloaded template and reviews it under matching columns', async ({
    page,
    credentials,
  }) => {
    const caseCode = `E2E-BULK-${Date.now()}`;

    await page.goto(`${env.originFor(credentials.tenantCode)}/selling/portfolio`);

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
    const exampleValues = parseCsvLine(exampleLine);
    const caseCodeIndex = headers.findIndex((h) => h.startsWith('Case Code'));
    expect(caseCodeIndex, 'expected a Case Code column in the downloaded template').toBeGreaterThanOrEqual(0);
    exampleValues[caseCodeIndex] = caseCode;
    const uploadCsv = [headerLine, exampleValues.join(',')].join('\n');

    await uploadDialog.locator('input[type="file"]').setInputFiles({
      name: 'bulk-upload-e2e.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(uploadCsv, 'utf-8'),
    });
    await uploadDialog.getByRole('button', { name: 'Continue' }).click();

    const reviewDialog = page
      .getByRole('dialog')
      .filter({ has: page.getByRole('heading', { name: 'Review Bulk Upload Details' }) });
    await expect(reviewDialog).toBeVisible({ timeout: 15_000 });

    // The review table's headers are the template's own header row with the
    // trailing "*" stripped — assert every column the template declares.
    for (const header of headers) {
      await expect(
        reviewDialog.getByRole('columnheader', { name: header.replace(/\*$/, '') }),
      ).toBeVisible();
    }
    await expect(
      reviewDialog.getByRole('columnheader', { name: 'Listing Visibility' }),
    ).toHaveCount(0);

    await expect(reviewDialog.getByText(caseCode)).toBeVisible();

    await reviewDialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(reviewDialog).toBeHidden();
  });
});
