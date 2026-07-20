import { defineConfig, devices } from '@playwright/test';
import { execSync } from 'child_process';

// In the Replit/Nix environment, chromium is installed as a system package
// (declared in replit.nix) and preferred over Playwright's bundled browser.
//
// In CI, we always use the Playwright-managed Chromium installed via
// `playwright install chromium --with-deps` (or `pnpm playwright:install`).
// Setting executablePath to undefined lets Playwright resolve the browser from
// its own cache — by default ~/.cache/ms-playwright, or the directory set by
// the PLAYWRIGHT_BROWSERS_PATH environment variable when a custom cache
// location is needed (e.g. a shared CI cache volume).
//
// Keeping PLAYWRIGHT_BROWSERS_PATH unset in CI uses the default location,
// which matches the path cached in .github/workflows/e2e.yml.
function systemChromiumPath(): string | undefined {
  if (process.env.CI) {
    // Never use a system browser in CI: Ubuntu runners ship google-chrome but
    // its version can change between runner image updates, causing snapshot
    // drift. Always use the Playwright-managed Chromium in CI.
    return undefined;
  }
  try {
    return execSync('which chromium 2>/dev/null || which google-chrome 2>/dev/null', {
      encoding: 'utf8',
    }).trim() || undefined;
  } catch {
    return undefined;
  }
}

const chromiumExe = systemChromiumPath();

// Mocked component/rendering checks — NOT the e2e suite (that's
// playwright.config.ts / `pnpm test:e2e`, which mocks nothing at all — see
// its own doc comment). These never leave the local dev server: MSW
// (src/mocks/upstream-server.ts, started from src/instrumentation.ts when
// MOCK_UPSTREAM=1 below) intercepts outbound requests the BFF proxy routes
// make to GATEWAY_URL, so they stay hermetic and safe to run on every PR
// (see .github/workflows/e2e.yml). Kept under a separate config specifically
// so they can never accidentally run against a live environment, and so the
// real e2e suite can never accidentally run against this mocked webServer.
//
// Why MSW intercepts at the gateway boundary (GATEWAY_URL) rather than
// Playwright's own page.route() intercepting the browser-facing /api/*
// paths directly: the BFF proxy routes under src/app/api/*\/[...path]/
// route.ts aren't thin passthroughs — they read the session cookie, attach
// it as an Authorization: Bearer header, and do path/error handling of their
// own. page.route() intercepts in the browser, before the request ever
// reaches that code, so a spec built on it gets zero coverage of the BFF's
// own logic. Mocking one layer deeper (what the BFF calls out to) lets the
// real route handler run for real every time. GATEWAY_URL is pointed at an
// obviously-fake host below specifically so any request MSW doesn't have a
// handler for fails loudly instead of silently reaching a real service.
export default defineConfig({
  testDir: './e2e/mocked',
  timeout: 30_000,
  retries: process.env.CI ? 1 : 0,

  snapshotDir: './e2e/mocked/login-logo.spec.ts-snapshots',
  snapshotPathTemplate: '{snapshotDir}/{arg}{ext}',

  expect: {
    toHaveScreenshot: {
      maxDiffPixelRatio: 0.01,
    },
  },

  use: {
    baseURL: 'http://localhost:3001',
    trace: 'on-first-retry',
  },

  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: {
          // Replit/Nix: uses system chromium from replit.nix.
          // CI: undefined — Playwright uses its managed browser (installed via
          // `pnpm playwright:install` / `playwright install chromium --with-deps`).
          executablePath: chromiumExe,
          args: ['--no-sandbox', '--disable-dev-shm-usage'],
        },
      },
    },
  ],

  webServer: [
    {
      name:                'next-app',
      command:             'MOCK_UPSTREAM=1 GATEWAY_URL=http://mock-gateway.invalid CC_COMMON_PORTAL_HOSTNAME=test-careconnect.local npx next dev -p 3001',
      url:                 'http://localhost:3001',
      reuseExistingServer: !process.env.CI,
      timeout:             60_000,
    },
  ],
});
