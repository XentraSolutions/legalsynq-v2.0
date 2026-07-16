import {
  test as base,
  expect as baseExpect,
  type Page,
  type Locator,
  type TestType,
  type PlaywrightTestArgs,
  type PlaywrightTestOptions,
  type PlaywrightWorkerArgs,
  type PlaywrightWorkerOptions,
} from '@playwright/test';
import type { ReadOnlyPage, ReadOnlyLocator } from './read-only-page';
import { getCredentials, type PlatformCredentials } from './credentials';
import { getEnv } from '../config/environments';
import { login } from './login-flow';

/**
 * Read-only e2e test factory — the one safe to run against every
 * environment, including production. `page` here is typed as ReadOnlyPage
 * (see read-only-page.ts), not Playwright's real Page — a spec file built on
 * `createReadOnlyTest()` cannot type-check a call to `.fill()`, `.click()` on
 * anything but a link, `.evaluate()`, or any other mutating/escape-hatch
 * method.
 *
 * Put specs built on this under e2e/(platform)/<product>/readonly/ — that
 * directory is the only one production's config discovers (see the testMatch
 * logic in playwright.config.ts). Specs that legitimately need to create,
 * update, or delete data belong under .../mutations/ instead, built on
 * createMutationTest() from mutation-test.ts, which is structurally and at
 * the fixture level refused for production.
 *
 * Authentication is the one interactive step every spec needs. It runs via
 * `autoLogin`, an autouse fixture using the real, unrestricted `page` —
 * deliberately never exposed under that name in InternalFixtures, so its
 * type stays the genuine Playwright `Page` (see the note on `PublicArgs`
 * below for why redeclaring `page`'s type on the SAME fixture name breaks
 * that). By the time a spec's test body runs, login has already happened;
 * the test only ever receives the read-only view.
 */

interface InternalFixtures {
  credentials: PlatformCredentials;
  /** Autouse — not exposed to specs; only exists to run login before every test. */
  autoLogin: void;
}

export function createReadOnlyTest(platform: string) {
  const internalTest = base.extend<InternalFixtures>({
    credentials: async ({}, use) => {
      await use(getCredentials(platform, getEnv().name));
    },

    autoLogin: [async ({ page, credentials }, use) => {
      await login(page, credentials);
      await use();
    }, { auto: true }],
  });

  // Type-only boundary — the ONE place this guardrail re-declares `page`'s
  // type, deliberately done as a single cast here rather than by redeclaring
  // `page` inside base.extend<>() itself: Playwright resolves a same-named
  // fixture override to its NEWLY declared type even inside that override's
  // own initializer, so an attempt to write `page: async ({ page: realPage
  // }) => ...)` above would have typed `realPage` as ReadOnlyPage too,
  // making it impossible to log in with the real API. Doing the cast only
  // here, after auto-login already ran against the genuine `Page`, avoids
  // that self-reference entirely.
  //
  // The runtime object handed to specs is unchanged — every method
  // ReadOnlyPage promises really exists on it. What changes is only what
  // `tsc` and the editor will let a spec file call on it.
  type PublicArgs =
    Omit<PlaywrightTestArgs, 'page'> &
    { page: ReadOnlyPage; credentials: PlatformCredentials };

  return internalTest as unknown as TestType<
    PublicArgs & PlaywrightTestOptions,
    PlaywrightWorkerArgs & PlaywrightWorkerOptions
  >;
}

// ── Sanctioned read-only assertion helpers ────────────────────────────────────
// Playwright's expect(page).toHaveURL() / expect(locator).toBeVisible() are
// typed against the real Page/Locator classes, so they reject ReadOnlyPage /
// ReadOnlyLocator directly. These wrappers are the one audited place that
// casts back — each only ever calls a read-only matcher (URL, visibility),
// never anything that types, selects, or clicks on the test's behalf.

export async function expectURL(
  page: ReadOnlyPage,
  pattern: RegExp,
  options?: { timeout?: number },
): Promise<void> {
  await baseExpect(page as unknown as Page).toHaveURL(pattern, options);
}

export async function expectVisible(
  locator: ReadOnlyLocator,
  options?: { timeout?: number },
): Promise<void> {
  await baseExpect(locator as unknown as Locator).toBeVisible(options);
}

export type { ReadOnlyPage, ReadOnlyLocator, ReadOnlyLinkLocator } from './read-only-page';
