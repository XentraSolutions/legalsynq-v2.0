import type { Page } from '@playwright/test';
import type { PlatformCredentials } from './credentials';
import { getEnv } from '../config/environments';

/**
 * The interactive login form flow, used by global-setup.ts to log in once
 * per platform+env before the whole suite runs (see storage-state.ts) — the
 * resulting session is what readonly-test.ts / mutation-test.ts load into
 * every individual test instead of submitting this form themselves. This is
 * the one place in the whole e2e suite where `.fill()`/`.click()` appears
 * against a real, unrestricted `Page` against the login form.
 */
export async function login(page: Page, credentials: PlatformCredentials): Promise<void> {
  const env = getEnv();
  await page.goto(`${env.originFor(credentials.tenantCode)}/login`);

  // Local dev builds show a manual tenant-code field even on a recognized
  // subdomain host; deployed environments (qa/production) never render it —
  // tenant is resolved purely from the host.
  const tenantField = page.getByPlaceholder('e.g. HARTWELL');
  if (await tenantField.isVisible().catch(() => false)) {
    await tenantField.fill(credentials.tenantCode);
  }

  await page.getByPlaceholder('you@example.com').fill(credentials.username);
  await page.getByPlaceholder('••••••••').fill(credentials.password);
  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL(/\/dashboard$/, { timeout: 20_000 });
}
