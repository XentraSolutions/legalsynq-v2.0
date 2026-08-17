---
name: create-e2e-test
description: |
  Creates a new Playwright e2e test for the LegalSynq tenant portal (apps/web) following the
  repo's readonly/mutations split, the production type guardrail, and the environment-aware
  credentials setup. Use when: adding an e2e test, writing a Playwright test for apps/web,
  adding e2e coverage for a product (lien, careconnect, fund, insights, xenia), testing a
  login flow or dashboard end-to-end, or extending e2e coverage to a new tenant portal page.
---

# Create an e2e test (apps/web)

This repo's e2e suite (`apps/web/e2e/(platform)/`) runs against real, live environments
(local/qa/production via `E2E_ENV`) — no mocks. Every spec is built on one of two factories so
production automatically only ever runs the safe subset. Skipping the factories, or reaching for
a cast to work around a type error, defeats that guardrail — don't do either.

## 1. Decide: readonly or mutation?

Default to **readonly** unless the spec genuinely needs to create, update, or delete data.

- **readonly** — navigates and asserts only (login, view a dashboard, check a page renders,
  follow a link). Runs against every environment, including production.
- **mutation** — fills a form and submits it, clicks a button that creates/deletes something,
  anything that changes data. Never runs against production (refused structurally and at the
  fixture level — see `mutation-test.ts`). Only runs against local/qa.

If you're not sure, write it as readonly first — if `tsc` then rejects a call you need, that's
your answer: it's a mutation spec.

## 2. Where the file goes

```
e2e/(platform)/<product>/readonly/<name>.spec.ts     # e.g. lien/readonly/login.spec.ts
e2e/(platform)/<product>/mutations/<name>.spec.ts    # e.g. lien/mutations/create-case.spec.ts
```

`<product>` mirrors the route group under `src/app/(platform)/<product>` (`lien`, `careconnect`,
`fund`, `insights`, `xenia`, ...). Create the product directory if it doesn't exist yet — see
`e2e/(platform)/lien/readonly/login.spec.ts` for the reference example.

## 3. Credentials

Look for a `<product>` entry in `e2e/data/credentials.json`. If it doesn't exist yet:

1. Add it to **both** `e2e/data/credentials.json` (gitignored — ask the user for real
   credentials, don't invent them) and `e2e/data/credentials.example.json` (committed template,
   placeholder values only):
   ```json
   { "<product>": { "default": { "tenantCode": "...", "username": "...", "password": "..." } } }
   ```
2. `default` is used by both `local` and `qa` (they share a backend/tenant). Add a
   `"production"` key only once a separate prod account exists for this product.

Never hardcode credentials directly in a spec file.

## 4. Write the spec

### Readonly example

```ts
import { createReadOnlyTest, expectURL, expectVisible } from '../../../support/readonly-test';
import { getEnv } from '../../../config/environments';

const test = createReadOnlyTest('<product>');
const env = getEnv();

test.describe(`<Product> <flow> [${env.name}]`, () => {
  test('describes what this verifies', async ({ page }) => {
    // `page` arrives already logged in (see readonly-test.ts's autoLogin fixture) —
    // don't write your own login steps here.
    await expectURL(page, /\/dashboard$/);
    await expectVisible(page.getByText(/welcome back/i));

    // Only getByRole('link', ...) has .click() — that's a same-origin GET navigation,
    // not a mutation. getByRole('button', ...) etc. have no .click() on purpose.
    await page.getByRole('link', { name: /some product tile/i }).click();

    await expectURL(page, /\/<product>\/dashboard$/);
    await expectVisible(page.getByRole('heading', { name: 'Dashboard' }));
  });
});
```

Use `expectURL()` / `expectVisible()` from `readonly-test.ts` instead of Playwright's own
`expect(page).toHaveURL()` / `expect(locator).toBeVisible()` — the real `expect` rejects
`ReadOnlyPage`/`ReadOnlyLocator` by design; these wrappers are the sanctioned, audited way to
assert against them.

### Mutation example

```ts
import { createMutationTest, expect } from '../../../support/mutation-test';

const test = createMutationTest('<product>');

test.describe('<Product> creates a <thing>', () => {
  test('creates a new <thing> and it appears in the list', async ({ page }) => {
    // `page` here is the real, unrestricted Playwright Page.
    await page.getByRole('button', { name: 'New <Thing>' }).click();
    await page.getByLabel('Name').fill('e2e test thing');
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByText('e2e test thing')).toBeVisible();
  });
});
```

Mutation specs should still clean up after themselves where practical (delete what they
created), since they run against real QA tenant data repeatedly.

## 5. Rules — do not do these

- Do not import `test`/`expect`/`page` from `@playwright/test` directly in a spec file. Always
  go through `createReadOnlyTest()` or `createMutationTest()` — a spec built on raw Playwright
  gets none of the guardrails and won't be excluded from a production run.
- Do not import `Page`/`Locator`/`Response` (or any other raw Playwright type) from
  `@playwright/test` in a spec file — neither as a top-level `import type`, nor inline as
  `import('@playwright/test').Page`. Both put the raw type in scope, which quietly reopens the
  `readonly/` guardrail: `tsc` accepts a plain `page as Page` cast on a `ReadOnlyPage`-typed value
  with **no error** (verified — the two types overlap enough that `as unknown as Page` isn't
  required). The guardrail's whole design depends on a bypass needing that loud, double
  `as unknown as Page` cast, which is easy to catch in review (see the doc comment atop
  `read-only-page.ts`); a bare `Page` sitting in scope defeats that with an inconspicuous
  single-word cast. Keep specs and their helpers built only on what `createMutationTest()` /
  `createReadOnlyTest()` hand you.
- When a local helper needs to accept `page` (or a `Locator`/`Response` derived from it), infer
  the type from the test itself via `PageOf<>` (`support/test-types.ts`) instead of naming it:
  ```ts
  import type { PageOf } from '../../../support/test-types';

  const test = createMutationTest('<product>'); // or createReadOnlyTest('<product>')

  type TestPage = PageOf<typeof test>;

  function helper(page: TestPage) {
    return page.getByRole('button', { name: 'Save' }); // Locator, inferred
  }
  ```
  This resolves to the real `Page` in a `mutations/` spec and to `ReadOnlyPage` in a `readonly/`
  spec automatically — the same helper signature is correct in both, and there's no
  `@playwright/test` import anywhere in the spec file to go stale or get copy-pasted into the
  wrong context. A `Locator`/`Response` needed on its own typically doesn't need naming at all —
  derive it with `ReturnType<TestPage['getByRole']>` or
  `Awaited<ReturnType<TestPage['waitForResponse']>>` rather than importing `Locator`/`Response`.
- Do not write `.fill()`/`.click()`-on-a-form/`.evaluate()` in a `readonly/` spec. If you need
  to, the spec belongs under `mutations/` instead.
- Do not cast around a `ReadOnlyPage`/`ReadOnlyLocator` type error (`as unknown as Page`) to make
  something compile in a `readonly/` spec. That error is the guardrail catching a mistake, not a
  false positive to silence.
- Do not add login steps inline in a spec — `login-flow.ts` already handles it via the factories'
  `autoLogin` fixture.
- Do not add coverage to `e2e/mocked/` / `playwright.mocked.config.ts` just because it's more
  convenient than dealing with real data — that suite is for hermetic component/rendering checks
  and for the narrow case in section 7 below (a spec that genuinely needs a backend response the
  real local/qa environment can't reliably produce), not a general escape hatch from this suite's
  "no mocks" rule.
- Do not use Playwright's `page.route()` to mock a response from a BFF-proxied endpoint
  (`src/app/api/*/[...path]/route.ts`) in `e2e/mocked/`, even though that suite does allow
  mocking. `page.route()` intercepts in the browser, before the request reaches the BFF route
  handler — so its own logic (cookie reading, `Authorization: Bearer` attachment, error shaping)
  never runs, and the test loses coverage of exactly the code most worth testing there. See
  section 7.

## 7. When a spec genuinely needs a mocked backend response

This only applies to `e2e/mocked/` — never to `e2e/(platform)/...`, which stays mock-free
structurally (see `playwright.config.ts`'s doc comment). Reach for this when a flow depends on a
backend response real local/qa data can't reliably guarantee (an edge case, a not-yet-implemented
endpoint, a specific error condition).

The mocked suite intercepts at the gateway boundary via MSW (`msw/node`), not at the browser via
`page.route()` — see `playwright.mocked.config.ts`'s doc comment for the full reasoning. In short:
`src/instrumentation.ts` starts `src/mocks/upstream-server.ts` when `MOCK_UPSTREAM=1` (set by
`playwright.mocked.config.ts`'s `next-app` webServer command), which intercepts the `fetch()`
calls the real BFF route handlers make to `GATEWAY_URL`. Handlers live under
`src/mocks/handlers/<domain>.ts` (one file per product/domain — `identity.ts`, `lien.ts`, ...),
spread together in `src/mocks/upstream-handlers.ts`; add a new domain file rather than growing an
existing one indefinitely. Pattern-match against the gateway-side path (e.g.
`*/liens/api/liens/cases/liens/get-facility/:lienId`), not the browser-facing `/api/lien/...`
path — the BFF route itself still runs for real.

Because `MOCK_UPSTREAM=1` starts the MSW server once, in-process, when the dev server itself
boots (not per-test), handlers can't be swapped per-test the way `server.use()` works in a
single-process Vitest run — every handler a spec needs must already be registered in
`src/mocks/handlers/`. For an authenticated platform page this can mean a real handler per
endpoint the page's full render tree touches (session, tenant settings, sidebar data, the page's
own API calls) — `src/mocks/handlers/lien.ts` plus `e2e/mocked/lien-facility-provider-contact.spec.ts`
is a complete worked example: an authenticated lien case detail page with a session cookie set
directly via `context.addCookies()` (no real login flow needed — MSW's `/identity/api/auth/me`
handler accepts any token), and a `get-facility` handler returning fields the real backend
currently doesn't persist, proving the frontend displays them correctly once it does. Two
response-shape pitfalls that cost real iteration time building that example: (1) not every
endpoint uses the `{isSuccess, message, data}` envelope — some (`lookupApi.getDocumentType()`,
`casesApi.getCaseUpdates()`) return the payload flat; check the `apiClient.get<T>()` type
argument at the call site rather than assuming; (2) `onUnhandledRequest: 'warn'` (the default
here) still lets the request fall through to a real `fetch()` against `GATEWAY_URL`, which is
pointed at a deliberately non-resolving host — the resulting `ENOTFOUND` errors are themselves how
you discover which endpoints still need handlers; run the spec, read the webServer's stdout for
MSW's warnings, add handlers for whichever leave the assertion still failing.

For a Vitest component test that needs to mock a fetch response instead (e.g. testing a hook or
component's data-fetching logic in isolation), use `msw/node`'s `setupServer()` directly in the
test file rather than `vi.mock()`-ing the service module away — see
`src/components/lien/contact-entity-select.test.tsx` for the pattern. This exercises the real
service/API-client code (query building, response mapping) and only fakes the network response,
which matters most for exactly the kind of bug that lives in that plumbing (see the comment atop
that test file). `vi.mock()` is still the right tool when a test doesn't care about that layer at
all and just needs a service call to resolve to a canned value.

## 6. Verify

```bash
cd apps/web
npx tsc --noEmit                                    # confirm no type errors (or confirm the
                                                     # expected one, if testing the guardrail)
npx playwright test <path-to-your-spec>             # E2E_ENV=local by default
E2E_ENV=qa npx playwright test <path-to-your-spec>
```

For a `readonly/` spec, also sanity-check it's excluded correctly from nothing and included in
production:

```bash
E2E_ENV=production npx playwright test --list
```

Your new spec should appear in that list if and only if it's under `readonly/`.
