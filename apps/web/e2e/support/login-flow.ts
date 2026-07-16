import type { Page } from '@playwright/test';
import type { PlatformCredentials } from './credentials';
import { getEnv } from '../config/environments';

/**
 * Shared login steps, used by both readonly-test.ts and mutation-test.ts.
 * This is the one place in the whole e2e suite where `.fill()`/`.click()`
 * appears against a real, unrestricted `Page` — every other file only ever
 * sees the already-logged-in result, either as a `ReadOnlyPage`
 * (readonly-test.ts) or the real `Page` for environments where mutating is
 * allowed (mutation-test.ts).
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
