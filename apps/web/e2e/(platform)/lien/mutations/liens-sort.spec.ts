import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';

/**
 * Covers the Liens list (/lien/liens) column-header sorting — follow-up to
 * TODO item 8 ("revisit liens filter and sorting - deferred, still waiting
 * for API"). Every column now has an accessor (src/app/(platform)/lien/liens
 * /page.tsx's plaintiffName column previously had none, which made it
 * unsortable to TanStack regardless of enableSorting) and the table is wired
 * for manualSorting, sending sortBy/sortDirection on GET
 * /lien/api/liens/liens the same way the Filter modal now sends
 * lawFirmIds/etc. (see liens-filter.spec.ts).
 *
 * The sortBy key sent per column (SORT_BY_MAP in page.tsx) is a guess at
 * what the backend recognizes — its source isn't available here (deployed
 * manually on QA, not pushed to a branch we can read). This spec is the
 * verification mechanism: it reads the actual API response for each column
 * and asserts the returned items are genuinely ordered by that field, not
 * just that the UI request looks right. A failing column here means that
 * column's sortBy key isn't (yet) honored server-side — worth filing back
 * to the API team rather than assuming the frontend wiring is wrong.
 */

const test = createMutationTest('lien');
const env = getEnv();

interface FieldSpec {
  /** Exact column header text as rendered in the table. */
  header: string;
  /** Raw LienResponse JSON field this column is expected to sort by. */
  dtoField: string;
  /** Converts a raw JSON value into something comparable, or null to exclude a row from the ordering check (nulls/blanks have no defined sort position). */
  parse: (value: unknown) => number | string | null;
}

// Lowercased so ordering checks use a case-insensitive "alphabetical" sense
// (matching how a DB's default collation and human expectations both treat
// "case" vs. "Case") instead of raw JS string comparison, which is a
// case-sensitive code-unit compare where every uppercase letter sorts before
// every lowercase one — that mismatch alone made otherwise-correctly-sorted
// server responses look broken.
function stringField(value: unknown): number | string | null {
  return value === null || value === undefined || value === '' ? null : String(value).toLowerCase();
}

function numberField(value: unknown): number | string | null {
  return value === null || value === undefined ? null : Number(value);
}

function dateField(value: unknown): number | string | null {
  if (value === null || value === undefined || value === '') return null;
  const t = new Date(String(value)).getTime();
  return Number.isNaN(t) ? null : t;
}

const FIELD_SPECS: FieldSpec[] = [
  { header: 'Lien ID', dtoField: 'lienNumber', parse: stringField },
  { header: 'Plaintiff Name', dtoField: 'plaintiff', parse: stringField },
  { header: 'Law Firm', dtoField: 'lawFirm', parse: stringField },
  { header: 'Medical Facility', dtoField: 'medicalFacility', parse: stringField },
  { header: 'Purchase Date', dtoField: 'purchaseDate', parse: dateField },
  { header: 'Purchase Amount', dtoField: 'totalPurchase', parse: numberField },
  { header: 'Billing Amount', dtoField: 'totalBilling', parse: numberField },
  { header: 'Lien Status', dtoField: 'status', parse: stringField },
  { header: 'Initial Service Date', dtoField: 'initialServiceDate', parse: dateField },
  { header: 'Case Manager', dtoField: 'caseManager', parse: stringField },
];

function isMonotonic(values: Array<number | string>, direction: 'asc' | 'desc'): boolean {
  for (let i = 1; i < values.length; i++) {
    const prev = values[i - 1];
    const curr = values[i];
    if (direction === 'asc' ? prev > curr : prev < curr) return false;
  }
  return true;
}

type Direction = 'asc' | 'desc';

/**
 * Clicks a sortable header repeatedly, collecting one API response per
 * direction it observes. TanStack's toggle handler cycles
 * unsorted → firstDir → otherDir → unsorted (removal), and which direction
 * comes first (asc vs. desc) depends on a runtime heuristic — the first
 * loaded row's value for that column, at the moment of the click
 * (`column.getAutoSortDir()`: a non-string value, e.g. null, picks
 * descending-first). That made some columns here cycle desc → unsorted in
 * just 2 clicks instead of the asc → desc most others show, which a
 * fixed "click twice, expect opposite direction" test can't tell apart from
 * a real bug (the unsorted click has no sortBy param, so a predicate
 * requiring one just hangs). Clicking until both directions are observed
 * sidesteps needing to predict that heuristic.
 */
async function collectDirectionResponses(
  page: import('@playwright/test').Page,
  header: ReturnType<import('@playwright/test').Page['getByRole']>,
  maxClicks = 6,
) {
  const seen: Partial<Record<Direction, import('@playwright/test').Response>> = {};
  for (let i = 0; i < maxClicks && (!seen.asc || !seen.desc); i++) {
    await expect(page.getByText(/^\d+ liens$/)).toBeVisible();
    const [response] = await Promise.all([
      page.waitForResponse((res) => res.url().includes('/lien/api/liens/liens'), { timeout: 15_000 }),
      header.click(),
    ]);
    const direction = new URL(response.url()).searchParams.get('sortDirection');
    if ((direction === 'asc' || direction === 'desc') && !seen[direction]) {
      seen[direction] = response;
    }
  }
  return seen;
}

test.describe(`SynqLien liens list — column sorting [${env.name}]`, () => {
  for (const spec of FIELD_SPECS) {
    test(`sorting by "${spec.header}" reorders results server-side`, async ({
      page,
      credentials,
    }) => {
      await page.goto(`${env.originFor(credentials.tenantCode)}/lien/liens`);
      await expect(page.getByText(/^\d+ liens$/)).toBeVisible({ timeout: 20_000 });

      // Not `exact: true` — the header's accessible name has a trailing
      // space (icon rendered after the text as a separate JSX child), same
      // whitespace quirk as the toolbar Filter button in liens-filter.spec.ts.
      const header = page.getByRole('columnheader', { name: spec.header });
      await expect(header).toBeVisible();

      const responses = await collectDirectionResponses(page, header);
      expect(responses.asc, `never observed an ascending request for "${spec.header}"`).toBeTruthy();
      expect(responses.desc, `never observed a descending request for "${spec.header}"`).toBeTruthy();

      const ascBody = await responses.asc!.json();
      const descBody = await responses.desc!.json();
      const ascIds = ascBody.items.map((item: Record<string, unknown>) => item.id);
      const descIds = descBody.items.map((item: Record<string, unknown>) => item.id);

      // The strongest, least data-dependent signal: asc and desc must return
      // rows in a genuinely different order. A per-value monotonic check
      // alone is too easy to pass by accident on a column that's mostly
      // null/duplicated in this page's data (e.g. an empty or single-value
      // array is trivially "monotonic" either direction) — this catches the
      // backend silently ignoring sortBy/sortDirection entirely, which a
      // weak per-value check on sparse data can miss.
      expect(
        ascIds,
        `"${spec.header}" (sortBy=${spec.dtoField}): ascending and descending requests returned identical row order — sortBy/sortDirection isn't being applied server-side for this field`,
      ).not.toEqual(descIds);

      for (const [direction, body] of [
        ['asc', ascBody],
        ['desc', descBody],
      ] as const) {
        const items = body.items as Array<Record<string, unknown>>;
        const values = items
          .map((item) => spec.parse(item[spec.dtoField]))
          .filter((v): v is number | string => v !== null);
        expect(
          isMonotonic(values, direction),
          `"${spec.header}" (${spec.dtoField}) not ordered ${direction} — got: ${JSON.stringify(values)}`,
        ).toBe(true);
      }
    });
  }
});
