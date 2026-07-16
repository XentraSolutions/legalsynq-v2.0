/**
 * e2e environment registry.
 *
 * Selected via the E2E_ENV variable (defaults to "local"):
 *   E2E_ENV=local       pnpm --dir apps/web test:e2e         (local frontend, QA backend)
 *   E2E_ENV=qa          pnpm --dir apps/web test:e2e:qa      (deployed QA frontend)
 *   E2E_ENV=production  pnpm --dir apps/web test:e2e:prod    (not wired up yet — see below)
 *
 * "local" and "qa" both exercise the same backend (GATEWAY_URL=https://core-qa.legalsynq.net
 * in .env.local) and the same tenant data — "local" just serves the frontend from a
 * local dev server instead of the deployed QA one. That's why they share the
 * "default" credentials entry in e2e/data/credentials.json.
 *
 * Production is a live system with real customer/tenant data. Specs that run
 * against it must stick to read-only assertions (login + view, no form
 * submissions or state-changing actions) — enforced at the type level by
 * e2e/support/prod-safe-test.ts and e2e/support/read-only-page.ts, not just
 * documented here.
 */

export type E2EEnvName = 'local' | 'qa' | 'production';

export interface E2EEnvironment {
  name: E2EEnvName;
  /** Builds the tenant portal origin (scheme + host, no trailing slash) for a given tenant subdomain. */
  originFor(tenantCode: string): string;
  /** True for any environment backed by real, live data — gates read-only-only behavior in specs. */
  isLive: boolean;
  /** True when Playwright must start/reuse a local `next dev` server to serve the frontend. */
  needsLocalServer: boolean;
}

export const ENVIRONMENTS: Record<E2EEnvName, E2EEnvironment> = {
  local: {
    name:             'local',
    originFor:        (tenantCode) => `http://${tenantCode}.localhost:3000`,
    isLive:           true,
    needsLocalServer: true,
  },
  qa: {
    name:             'qa',
    originFor:        (tenantCode) => `https://${tenantCode}.legalsynq.net`,
    isLive:           true,
    needsLocalServer: false,
  },
  production: {
    name:             'production',
    originFor() {
      // TODO: fill in once the production frontend URL pattern is known,
      // e.g. `https://${tenantCode}.legalsynq.com`.
      throw new Error(
        'E2E_ENV=production is scaffolded but not configured yet — ' +
        'set the real origin pattern in e2e/config/environments.ts before running prod e2e tests.',
      );
    },
    isLive:           true,
    needsLocalServer: false,
  },
};

export function getEnv(): E2EEnvironment {
  const name = (process.env.E2E_ENV ?? 'local') as E2EEnvName;
  const env = ENVIRONMENTS[name];
  if (!env) {
    throw new Error(`Unknown E2E_ENV "${name}". Valid values: ${Object.keys(ENVIRONMENTS).join(', ')}`);
  }
  return env;
}
