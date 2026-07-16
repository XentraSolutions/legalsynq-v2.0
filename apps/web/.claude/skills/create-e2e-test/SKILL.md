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
- Do not write `.fill()`/`.click()`-on-a-form/`.evaluate()` in a `readonly/` spec. If you need
  to, the spec belongs under `mutations/` instead.
- Do not cast around a `ReadOnlyPage`/`ReadOnlyLocator` type error (`as unknown as Page`) to make
  something compile in a `readonly/` spec. That error is the guardrail catching a mistake, not a
  false positive to silence.
- Do not add login steps inline in a spec — `login-flow.ts` already handles it via the factories'
  `autoLogin` fixture.
- Do not add this coverage to `e2e/mocked/` or `playwright.mocked.config.ts` — that suite is a
  separate, hermetic set of component/rendering checks used by CI, not part of this e2e suite.

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
