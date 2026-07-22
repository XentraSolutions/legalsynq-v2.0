import { identityHandlers } from "./handlers/identity";
import { lienHandlers } from "./handlers/lien";

/**
 * MSW handlers for the "upstream gateway" boundary — the URLs the BFF proxy
 * routes (src/app/api/*\/[...path]/route.ts) call out to via GATEWAY_URL,
 * not the browser-facing /api/* paths those proxies themselves expose.
 *
 * Consumed by src/instrumentation.ts when MOCK_UPSTREAM=1 (see
 * playwright.mocked.config.ts): the real BFF route handler still runs (cookie
 * reading, Bearer header attachment, request/response shaping) and only the
 * fetch() call it makes to the gateway is intercepted here.
 *
 * Grouped by domain under handlers/ (identity, lien, ...) — add a new file
 * and spread it in below when a spec needs to mock a different product's
 * gateway calls. `'*'` origin wildcard in each handler: matches regardless
 * of what GATEWAY_URL resolves to.
 */
export const upstreamHandlers = [...identityHandlers, ...lienHandlers];
