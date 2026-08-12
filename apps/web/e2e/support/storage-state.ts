import path from 'node:path';

/**
 * Where global-setup.ts saves a logged-in browser context for a given
 * platform + environment, and where mutation-test.ts / readonly-test.ts load
 * it back from. One real login per platform+env per `playwright test`
 * invocation, reused by every test in the run — instead of each test
 * submitting the login form itself, which is what was tripping the backend's
 * own login rate limit during a full-suite run.
 *
 * Keyed by env too, not just platform: "local" and "qa" are different
 * origins (see environments.ts), so a session captured for one doesn't
 * carry over to the other.
 */
export function storageStatePath(platform: string, envName: string): string {
  return path.join(__dirname, '..', '.auth', `${platform}-${envName}.json`);
}
