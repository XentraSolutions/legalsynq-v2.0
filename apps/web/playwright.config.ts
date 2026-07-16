import { defineConfig, devices } from '@playwright/test';
import { execSync } from 'child_process';
import { getEnv } from './e2e/config/environments';

// Same system-Chromium preference as playwright.mocked.config.ts — see that
// file for the reasoning (local Nix/Replit chromium vs. CI-managed Chromium).
function systemChromiumPath(): string | undefined {
  if (process.env.CI) return undefined;
  try {
    return execSync('which chromium 2>/dev/null || which google-chrome 2>/dev/null', {
      encoding: 'utf8',
    }).trim() || undefined;
  } catch {
    return undefined;
  }
}

/**
 * The e2e suite. No mocks — every test here exercises a live environment,
 * selected via E2E_ENV (see e2e/config/environments.ts for the registry —
 * "local", "qa", "production").
 *
 * Tests live under e2e/(platform)/<product>/, mirroring the src/app/(platform)/
 * route groups, and split into two kinds per product:
 *   .../<product>/readonly/    built on createReadOnlyTest() (readonly-test.ts) —
 *                               navigate + assert only, type-enforced (see
 *                               read-only-page.ts). Runs on every environment.
 *   .../<product>/mutations/   built on createMutationTest() (mutation-test.ts) —
 *                               real, unrestricted `page`, for specs that
 *                               legitimately create/update/delete data.
 *
 * Production only ever runs the readonly/ subset — testMatch below is scoped
 * to `**\/readonly/**` whenever E2E_ENV=production, so a mutations/ spec is
 * never discovered at all when targeting prod, regardless of what fixtures it
 * imports. (mutation-test.ts also refuses at the fixture level, as a second,
 * independent guard for the case where a mutation spec is pointed at
 * directly, bypassing this file's testMatch-based discovery.)
 *
 * All specs build their target URL from getEnv().originFor(tenantCode) rather
 * than a fixed baseURL (each environment/tenant has a different origin), and
 * read credentials from e2e/data/credentials.json (gitignored — see
 * e2e/data/credentials.example.json for the schema).
 *
 * Only "local" needs a dev server; reuseExistingServer is unconditionally
 * true so an already-running `pnpm dev` session is reused instead of a
 * second instance failing to start (Next.js allows only one dev server per
 * project directory). "qa" and "production" hit already-deployed frontends
 * directly — no webServer entry at all.
 *
 * This is the only e2e suite meant by the name "e2e" — the pre-existing
 * mocked component/rendering checks (login page, logos) live under
 * playwright.mocked.config.ts / `pnpm test:e2e:mocked` and are not e2e in
 * this sense: they never leave the local dev server.
 *
 * Run with:
 *   pnpm --dir apps/web test:e2e         (E2E_ENV=local, default — readonly + mutations)
 *   pnpm --dir apps/web test:e2e:qa      (readonly + mutations)
 *   pnpm --dir apps/web test:e2e:prod    (readonly only, structurally)
 */
const env = getEnv();

export default defineConfig({
  testDir: './e2e/(platform)',
  testMatch: env.name === 'production' ? '**/readonly/**' : undefined,
  // Logs in once per platform up front and saves the session for every test
  // to reuse (see global-setup.ts) — avoids each test submitting the login
  // form itself, which was tripping the backend's own login rate limit
  // during a full-suite run.
  globalSetup: require.resolve('./e2e/support/global-setup'),
  timeout: 30_000,
  retries: 0,
  fullyParallel: false,
  workers: 1,

  use: {
    trace: 'on-first-retry',
  },

  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: {
          executablePath: systemChromiumPath(),
          args: ['--no-sandbox', '--disable-dev-shm-usage'],
        },
      },
    },
  ],

  webServer: env.needsLocalServer
    ? {
        command:             'next dev -p 3000',
        url:                  'http://localhost:3000',
        reuseExistingServer:  true,
        timeout:              60_000,
      }
    : undefined,
});
