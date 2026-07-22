import { setupServer } from "msw/node";
import { upstreamHandlers } from "./upstream-handlers";

/**
 * Node-side request interceptor for the mocked Playwright suite
 * (e2e/mocked/, playwright.mocked.config.ts) — see upstream-handlers.ts for
 * what this intercepts and why. Started from src/instrumentation.ts, gated
 * behind MOCK_UPSTREAM=1 so it never runs against a real deployment.
 */
export const upstreamServer = setupServer(...upstreamHandlers);
