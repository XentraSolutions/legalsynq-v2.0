import { chromium } from '@playwright/test';
import { getEnv } from '../config/environments';
import { getCredentials } from './credentials';
import { login } from './login-flow';
import { storageStatePath } from './storage-state';

/**
 * Runs once per `playwright test` invocation (wired up via `globalSetup` in
 * playwright.config.ts), before any spec runs. Logs in once per platform
 * here and saves the resulting session (cookies/localStorage) to disk, so
 * mutation-test.ts / readonly-test.ts can load it back into every test's
 * browser context instead of each test submitting the login form itself.
 * That per-test login is what was tripping the backend's own "too many
 * requests" / "login is temporarily unavailable" rate limit during a full
 * local run — this cuts a full suite down to one real login.
 *
 * Add a platform here as soon as it has an e2e/data/credentials.json entry
 * and its own readonly/mutations specs.
 */
const PLATFORMS = ['lien'];

export default async function globalSetup(): Promise<void> {
  const env = getEnv();
  // production's origin isn't wired up yet (see environments.ts) — nothing
  // to log into, and env.originFor() would throw before a page even loads.
  if (env.name === 'production') return;

  const browser = await chromium.launch();
  try {
    for (const platform of PLATFORMS) {
      const credentials = getCredentials(platform, env.name);
      const page = await browser.newPage();
      await login(page, credentials);
      await page.context().storageState({
        path: storageStatePath(platform, env.name),
      });
      await page.close();
    }
  } finally {
    await browser.close();
  }
}
