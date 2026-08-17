/**
 * Extracts the `page` fixture's type off a test built by `createMutationTest()` /
 * `createReadOnlyTest()` — the real Playwright `Page` for the former, `ReadOnlyPage` for the
 * latter (see `read-only-page.ts`). Use this instead of importing `Page`/`Locator`/`Response`
 * from `@playwright/test` in a spec: that puts the raw, unrestricted types back in scope, which
 * lets a `readonly/` spec silently cast a `ReadOnlyPage` to `Page` with no `as unknown as` step —
 * see create-e2e-test/SKILL.md section 5 for why that matters.
 *
 * Matched against the `(title, details, body)` overload specifically, not `(title, body)` —
 * `TestType`'s call signature is overloaded and matching the 2-arg one would infer `details`
 * (a plain object type) as the fixtures argument instead.
 */
export type PageOf<TestFn> = TestFn extends (
  title: string,
  details: any,
  body: (args: infer Args, testInfo: any) => any,
) => void
  ? Args extends { page: infer Page }
    ? Page
    : never
  : never;
