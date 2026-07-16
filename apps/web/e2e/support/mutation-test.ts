import { test as base, expect } from '@playwright/test';
import { getCredentials, type PlatformCredentials } from './credentials';
import { getEnv } from '../config/environments';
import { login } from './login-flow';

/**
 * Full-access e2e test factory — the real, unrestricted Playwright `page`.
 * For specs that legitimately create, update, or delete data, on
 * environments where that's safe (local, qa).
 *
 * NEVER usable against production, enforced twice, independently:
 *   1. Structural: playwright.config.ts sets testMatch to only
 *      `**\/readonly/**` when E2E_ENV=production, so a spec under
 *      .../mutations/ is never even discovered when running against prod —
 *      it doesn't matter what fixtures it imports.
 *   2. Fixture-level: `credentials` below throws immediately if
 *      getEnv().name === 'production', regardless of how the spec was
 *      invoked. This covers the case where someone points Playwright
 *      directly at a mutation spec file — e.g.
 *      `playwright test "e2e/(platform)/lien/mutations/foo.spec.ts"` — which
 *      bypasses testMatch-based discovery entirely.
 *
 * Put specs built on this under e2e/(platform)/<product>/mutations/. For
 * anything that only needs to navigate and assert, prefer
 * createReadOnlyTest() from readonly-test.ts instead — it's the one that
 * also runs against production.
 */

interface MutationFixtures {
  credentials: PlatformCredentials;
  /** Autouse — not exposed to specs; only exists to run login before every test. */
  autoLogin: void;
}

export function createMutationTest(platform: string) {
  return base.extend<MutationFixtures>({
    credentials: async ({}, use) => {
      const env = getEnv();
      if (env.name === 'production') {
        throw new Error(
          `Refusing to run a mutation e2e test against production (E2E_ENV=${env.name}). ` +
          `Move this spec under a readonly/ directory and build it on createReadOnlyTest() ` +
          `instead, or run it with E2E_ENV=local or E2E_ENV=qa.`,
        );
      }
      await use(getCredentials(platform, env.name));
    },

    autoLogin: [async ({ page, credentials }, use) => {
      await login(page, credentials);
      await use();
    }, { auto: true }],
  });
}

export { expect };
