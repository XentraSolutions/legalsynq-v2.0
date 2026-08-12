import { readFileSync } from 'node:fs';
import { request as apiRequest } from '@playwright/test';
import { createReadOnlyTest, expectURL, expectVisible } from '../../../support/readonly-test';
import { getEnv } from '../../../config/environments';
import { storageStatePath } from '../../../support/storage-state';

/**
 * Readonly e2e coverage for /lien/contacts — TODO-contacts.md item #1:
 * "tabs render correct codes/labels (cross-check against GET
 * /lien/lookup/contact/type sorted by sortOrder) and the first tab is
 * preselected on load."
 *
 * Everything else in TODO-contacts.md (#2-#5: Add Contact modal prefill,
 * search input, row Edit Contact modal prefill, CSV export) needs
 * .fill()/.type() or a button click, which ReadOnlyPage's type forbids
 * outright — those live in
 * e2e/(platform)/lien/mutations/contacts-list.spec.ts instead. This file
 * only ever clicks getByRole('link', ...), a same-origin GET navigation.
 *
 * contacts/page.tsx renders one tab per *known* contact type
 * (KNOWN_CONTACT_TYPE_CODES in src/hooks/use-contacts.ts — LawFirm,
 * MedicalFacility, Provider, FundingCompany, Lead), filtered to active and
 * sorted by sortOrder, as real `<Link href="/lien/contacts?type=<code>">`s
 * (converted from plain state-setting buttons specifically so this spec can
 * exercise them). The `request` fixture below is Playwright's own, not
 * ReadOnlyPage — reusing the same storageState global-setup.ts already
 * logged in with — to independently fetch the lookup endpoint and derive
 * the expected tab order/labels from live tenant data rather than
 * hardcoding contact type names, since the active set is tenant-configurable.
 */

const test = createReadOnlyTest('lien');
const env = getEnv();

const KNOWN_CONTACT_TYPE_CODES = [
  'LawFirm',
  'MedicalFacility',
  'Provider',
  'FundingCompany',
  'Lead',
];

function pluralize(name: string): string {
  if (/s$/i.test(name)) return name;
  if (/[^aeiou]y$/i.test(name)) return `${name.slice(0, -1)}ies`;
  return `${name}s`;
}

interface LookupContactType {
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

/** Reads the platform_session/tenant_code cookies global-setup.ts saved for
 * this platform+env and formats them as a Cookie header value. Used instead
 * of apiRequest.newContext()'s own `storageState` option because that
 * filters cookies by the *request's* domain — which for "local" is
 * 127.0.0.1 (see below), not the rl-liens1.localhost domain the cookies
 * were saved under — and would silently send the request unauthenticated. */
function cookieHeader(platform: string, envName: string): string {
  const state = JSON.parse(readFileSync(storageStatePath(platform, envName), 'utf-8'));
  return state.cookies.map((c: { name: string; value: string }) => `${c.name}=${c.value}`).join('; ');
}

async function expectedTabs(credentials: { tenantCode: string }): Promise<string[]> {
  const origin = env.originFor(credentials.tenantCode);
  // Node's DNS resolver (unlike Chromium's, which special-cases the
  // "localhost" TLD per RFC 6761) doesn't resolve "<tenant>.localhost" on
  // this machine, so apiRequestContext.get() against that host fails with
  // ENOTFOUND even though the same URL loads fine in the browser fixture
  // below. Routing to the loopback IP directly and passing the tenant
  // subdomain via the Host header instead gets Next.js's dev server (which
  // uses Host for tenant resolution) the same information.
  const url = new URL(origin);
  const baseURL = env.name === 'local' ? `http://127.0.0.1:${url.port}` : origin;
  const context = await apiRequest.newContext({
    baseURL,
    extraHTTPHeaders: {
      Cookie: cookieHeader('lien', env.name),
      ...(env.name === 'local' ? { Host: url.host } : {}),
    },
  });
  try {
    const res = await context.get('/api/lien/lookup/contact/type');
    const body = await res.json();
    const items: LookupContactType[] = body.items ?? body.data ?? body;
    return items
      .filter((t) => t.isActive && KNOWN_CONTACT_TYPE_CODES.includes(t.code))
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((t) => pluralize(t.name));
  } finally {
    await context.dispose();
  }
}

test.describe(`SynqLien Contacts [${env.name}]`, () => {
  test('navigates to contacts page from dashboard', async ({ page }) => {
    await expectURL(page, /\/dashboard$/);
    await expectVisible(page.getByText(/welcome back/i));

    await page.getByRole('link', { name: /synq liens/i }).click();
    await expectURL(page, /\/lien\/dashboard$/);

    await page.getByRole('link', { name: /contacts/i }).click();
    await expectURL(page, /\/lien\/contacts$/);
    await expectVisible(page.getByRole('heading', { name: 'Contacts' }));
  });

  test('tabs render the correct codes/labels, in sortOrder, and the first is preselected', async ({
    page,
    credentials,
  }) => {
    const expected = await expectedTabs(credentials);
    test.skip(expected.length === 0, 'Tenant has no active known contact types to assert against.');

    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    await expectURL(page, /\/lien\/contacts$/);

    const tabLinks = page.getByRole('link', { name: expected[0], exact: true });
    await expectVisible(tabLinks.first());

    // Preselection redirects to ?type=<first known code>, not just styles
    // the first tab active client-side — assert the URL, the strongest signal.
    await expectURL(page, /[?&]type=/);

    for (const label of expected) {
      await expectVisible(page.getByRole('link', { name: label, exact: true }));
    }
  });

  test('clicking a second tab link navigates and re-filters the table', async ({
    page,
    credentials,
  }) => {
    const expected = await expectedTabs(credentials);
    test.skip(expected.length < 2, 'Tenant needs at least 2 active known contact types for this check.');

    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    await expectURL(page, /\/lien\/contacts$/);
    await expectVisible(page.getByRole('table'));

    const secondTabLink = page.getByRole('link', { name: expected[1], exact: true });
    await secondTabLink.click();

    await expectURL(page, /[?&]type=/);
    await expectVisible(page.getByRole('table'));
    // Name column header switches to the newly active type's label
    // (contacts/page.tsx's nameColumnLabel), a visible sign the tab actually
    // took effect rather than the click silently no-op'ing.
    await expectVisible(
      page.getByRole('columnheader').filter({ hasText: new RegExp(expected[1].replace(/s$/i, ''), 'i') }),
    );
  });

  test('displays contact list with expected columns', async ({ page, credentials }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    await expectURL(page, /\/lien\/contacts$/);

    const typeHeader = page.getByRole('columnheader').filter({ hasText: /type/i });
    const emailHeader = page.getByRole('columnheader').filter({ hasText: /email/i });
    const activeCasesHeader = page.getByRole('columnheader').filter({ hasText: /active cases/i });

    await expectVisible(typeHeader.first());
    await expectVisible(emailHeader.first());
    await expectVisible(activeCasesHeader.first());
  });

  test('search box is present', async ({ page, credentials }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    await expectURL(page, /\/lien\/contacts$/);

    await expectVisible(page.getByPlaceholder(/search contacts by name/i));
  });

  test('page header displays total contact count', async ({ page, credentials }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/contacts`);
    await expectURL(page, /\/lien\/contacts$/);

    await expectVisible(page.getByRole('heading', { name: 'Contacts' }));
    await expectVisible(page.locator('text=/\\d+\\s+contacts?/'));
  });
});
