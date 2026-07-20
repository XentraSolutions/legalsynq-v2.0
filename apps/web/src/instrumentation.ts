/**
 * Next.js instrumentation hook — runs once at server startup before any
 * requests are accepted.
 *
 * BLK-OPS-01: Used here to validate required server-side environment
 * variables so that misconfigured deployments fail immediately with a
 * clear error message rather than serving traffic with broken config.
 *
 * MOCK_UPSTREAM=1: starts the MSW upstream-gateway interceptor (see
 * src/mocks/upstream-server.ts) for the mocked Playwright suite
 * (playwright.mocked.config.ts, testDir e2e/mocked/) — never set outside
 * that config, so this is a no-op for every real deployment and for the
 * live e2e suite (playwright.config.ts), which never mocks anything.
 *
 * See: https://nextjs.org/docs/app/building-your-application/optimizing/instrumentation
 */
export async function register() {
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    const { validateServerEnv } = await import('./lib/env-validation');
    validateServerEnv();

    if (process.env.MOCK_UPSTREAM === '1') {
      const { upstreamServer } = await import('./mocks/upstream-server');
      upstreamServer.listen({ onUnhandledRequest: 'warn' });
    }
  }
}
