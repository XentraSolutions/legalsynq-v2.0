# Step 26 – Deployment Config

## Files Created

| File | Purpose |
|------|---------|
| `src/lib/env.ts` | Centralised environment variable access — single source of truth for all `process.env` reads in the application |
| `.env.example` | Template listing every variable the app understands with descriptions and example values for each environment |

## Files Updated

| File | What changed |
|------|-------------|
| `src/lib/app-config.ts` | Replaced direct `process.env` reads with imports from `env.ts`; removed inline env resolution logic |
| `src/lib/api-client.ts` | Replaced inline `process.env.CONTROL_CENTER_API_BASE ?? ... ?? 'localhost'` with `CONTROL_CENTER_API_BASE` from `env.ts` |
| `src/lib/session.ts` | Replaced `process.env.GATEWAY_URL ?? 'localhost'` with `CONTROL_CENTER_API_BASE` from `env.ts` |
| `src/lib/server-api-client.ts` | Same as session.ts — replaced direct env read with `env.ts` import; added module-level docstring |
| `src/app/api/auth/login/route.ts` | Replaced `process.env.GATEWAY_URL` and `process.env.NODE_ENV` with imports from `env.ts` |
| `src/app/api/auth/logout/route.ts` | Same as login route |
| `next.config.mjs` | Added `headers()` function with 5 security headers (all envs) + HSTS (prod only); added inline documentation for each header |

---

## Environment Strategy

### Single env module (`env.ts`)

All `process.env` access in the Control Center is centralised in one file.
No other source file reads `process.env` directly — they import from `env.ts`.

The only permitted exceptions are:
- `next.config.mjs` — runs outside the TypeScript module system at build time
- `env.ts` itself

### Variable registry

| Variable | Type | Scope | Required (prod) | Resolution |
|----------|------|-------|-----------------|-----------|
| `NODE_ENV` | string | server | yes (set by Next.js) | `process.env.NODE_ENV` |
| `NEXT_PUBLIC_CONTROL_CENTER_ORIGIN` | string | both | no | env → Replit domain → `localhost:5004` |
| `CONTROL_CENTER_API_BASE` | string | server | yes | env → `GATEWAY_URL` → `localhost:5010` |
| `GATEWAY_URL` | string | server | no (legacy alias) | `CONTROL_CENTER_API_BASE` fallback |
| `NEXT_PUBLIC_BASE_PATH` | string | both | no | env → `''` |

### `getEnv(key, fallback?)` behaviour

```
                      ┌─ value set and non-empty? ─── return value
                      │
process.env[key] ─────┤
                      │
                      └─ absent or empty ──┬─ fallback provided? ─── return fallback
                                           │
                                           ├─ IS_PROD + no fallback ─── throw Error
                                           │
                                           └─ IS_DEV + no fallback ─── warn + return ''
```

Fail-fast in production ensures misconfigurations surface immediately at
process startup rather than manifesting as cryptic runtime errors during
a user session.

### Multi-environment values

| Variable | Dev (local) | Staging | Production |
|----------|-------------|---------|-----------|
| `CONTROL_CENTER_API_BASE` | `http://localhost:5010` | `https://api-staging.legalsynq.com` | `https://api.legalsynq.com` |
| `NEXT_PUBLIC_CONTROL_CENTER_ORIGIN` | _(auto: Replit domain or localhost)_ | `https://controlcenter-staging.legalsynq.com` | `https://controlcenter.legalsynq.com` |

---

## Security Headers

Configured via the `headers()` function in `next.config.mjs`. Applied to every
Control Center response via the `source: '/(.*)'` pattern.

### Headers added in all environments

| Header | Value | Why |
|--------|-------|-----|
| `X-Frame-Options` | `SAMEORIGIN` | Prevents clickjacking by blocking CC from being iframed on foreign origins |
| `X-Content-Type-Options` | `nosniff` | Prevents MIME-sniffing attacks; browser must honour declared Content-Type |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Prevents path parameters (tenant IDs, user IDs) from leaking in the Referer header to third-party services |
| `X-DNS-Prefetch-Control` | `off` | Prevents browser from leaking admin-visited external link domains to DNS resolvers before the admin clicks |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=()` | Explicitly disables browser APIs the CC does not use; reduces XSS blast radius |

### Production-only header

| Header | Value | Why prod-only |
|--------|-------|--------------|
| `Strict-Transport-Security` | `max-age=63072000; includeSubDomains; preload` | Only meaningful over HTTPS. Development runs over HTTP; HSTS applied there would be silently ignored. Keeping it prod-only is cleaner. |

### How the headers are configured

```js
// next.config.mjs
const SECURITY_HEADERS = [
  { key: 'X-Frame-Options',        value: 'SAMEORIGIN' },
  { key: 'X-Content-Type-Options', value: 'nosniff' },
  { key: 'Referrer-Policy',        value: 'strict-origin-when-cross-origin' },
  { key: 'X-DNS-Prefetch-Control', value: 'off' },
  { key: 'Permissions-Policy',     value: 'camera=(), microphone=(), geolocation=(), payment=()' },
  ...(IS_PROD ? [{ key: 'Strict-Transport-Security', value: 'max-age=63072000; ...' }] : []),
];

async headers() {
  return [{ source: '/(.*)', headers: SECURITY_HEADERS }];
}
```

---

## Subdomain Readiness

### No localhost assumptions remain

Every server-side reference to the gateway URL was a `process.env.GATEWAY_URL ?? 'http://localhost:5010'` fallback call:

| File | Before | After |
|------|--------|-------|
| `api-client.ts` | `process.env.CONTROL_CENTER_API_BASE ?? process.env.GATEWAY_URL ?? 'http://localhost:5010'` | `CONTROL_CENTER_API_BASE` from `env.ts` |
| `session.ts` | `process.env.GATEWAY_URL ?? 'http://localhost:5010'` | `CONTROL_CENTER_API_BASE` from `env.ts` |
| `server-api-client.ts` | `process.env.GATEWAY_URL ?? 'http://localhost:5010'` | `CONTROL_CENTER_API_BASE` from `env.ts` |
| `login/route.ts` | `process.env.GATEWAY_URL ?? 'http://localhost:5010'` | `CONTROL_CENTER_API_BASE` from `env.ts` |
| `logout/route.ts` | `process.env.GATEWAY_URL ?? 'http://localhost:5010'` | `CONTROL_CENTER_API_BASE` from `env.ts` |
| `app-config.ts` | `process.env.NEXT_PUBLIC_CONTROL_CENTER_ORIGIN ?? (REPLIT_DEV_DOMAIN ? ...)` | Re-exports `CONTROL_CENTER_ORIGIN` from `env.ts` |

### CONTROL_CENTER_ORIGIN in redirects

`app-config.ts` exports `LOGIN_URL = CONTROL_CENTER_ORIGIN + BASE_PATH + '/login'`.
This is used in `middleware.ts` for unauthenticated-redirect and in auth guards.
Setting `NEXT_PUBLIC_CONTROL_CENTER_ORIGIN` to the deployed subdomain ensures
redirects are always absolute and subdomain-correct.

### Tenant code from subdomain

The login BFF (`POST /api/auth/login`) already supports tenant code extraction
from subdomain:

```
hartwell.controlcenter.legalsynq.com  →  tenantCode = "HARTWELL"
```

No changes were needed — `extractTenantCodeFromHost()` was already in place.

---

## Deployment Notes

Full deployment instructions including Dockerfile requirements, CI/CD pipeline
guidance, DNS configuration, reverse proxy setup, and cookie scope notes are in:

```
/analysis/deployment-notes.md
```

---

## Package.json Scripts

`package.json` already contained the required scripts. No changes made.

| Script | Command | Purpose |
|--------|---------|---------|
| `dev` | `next dev -p 5004` | Start dev server on port 5004 |
| `build` | `next build` | Production build to `.next/` |
| `start` | `next start -p 5004` | Start production server on port 5004 |
| `type-check` | `tsc --noEmit` | TypeScript validation without emit |

---

## TODOs

```ts
// TODO: add Dockerfile
//   — Multi-stage build: node:22-alpine (build stage) → distroless (runtime).
//   — COPY .next/, public/, package.json into final image.
//   — EXPOSE 5004; CMD ["node", "server.js"].

// TODO: add CI/CD pipeline
//   — On merge to main: tsc --noEmit → next build → docker build → push ECR.
//   — Required CI secrets: CONTROL_CENTER_API_BASE, NEXT_PUBLIC_CONTROL_CENTER_ORIGIN.
//   — Deploy step: ECS task def update / Railway redeploy / Vercel deploy.

// TODO: add health check endpoint
//   — GET /api/health → { status: "ok", service: "control-center", uptime: N }
//   — Verify gateway reachability (HEAD CONTROL_CENTER_API_BASE/identity/api/auth/me).
//   — Used by ALB / container health checks.

// TODO: add Content-Security-Policy header
//   — Requires audit of inline scripts and third-party resource origins.
//   — Highest-impact XSS mitigation available via HTTP headers.

// TODO: add CSRF protection on BFF route handlers
//   — Double-submit cookie or signed nonce for /api/auth/login, /api/auth/logout.

// TODO: lock serverActions.allowedOrigins in next.config.mjs
//   — Replace '*' with [NEXT_PUBLIC_CONTROL_CENTER_ORIGIN] before production.
```

---

## Independence Validation

- `env.ts` has zero imports from any project code — only `process.env`
- `app-config.ts` imports only from `env.ts` and re-exports the same shapes
  → all existing consumers of `app-config.ts` (`SESSION_COOKIE_NAME`,
    `CONTROL_CENTER_ORIGIN`, `LOGIN_URL`, etc.) remain fully compatible
- `api-client.ts` public API unchanged: `apiFetch`, `ApiError`, `CACHE_TAGS`,
  `CacheTag`, `ApiFetchOptions` signatures all identical
- `session.ts` public API unchanged: `getServerSession`, `requireSession`
- `server-api-client.ts` public API unchanged: `serverApi`, `ServerApiError`
- Route handler logic (cookie values, response shapes, status codes) unchanged
- `next.config.mjs` additions are purely additive: new `headers()` function;
  existing `experimental.serverActions` and `rewrites()` untouched
- TypeScript check: **0 errors**

---

## Any Issues or Assumptions

1. **`next.config.mjs` still reads `process.env` directly** — the rewrites()
   function in `next.config.mjs` reads `CONTROL_CENTER_API_BASE ?? GATEWAY_URL ?? localhost`
   directly rather than importing from `env.ts`. This is intentional: `next.config.mjs`
   runs in a separate context (not part of the TypeScript module system) at
   build time. It cannot import TypeScript source files. The same resolution
   logic is duplicated there, but `env.ts` remains the authoritative source for
   all runtime code.

2. **`CONTROL_CENTER_ORIGIN` re-exported through two modules** — `env.ts` exports
   `CONTROL_CENTER_ORIGIN` and `app-config.ts` re-exports the same value. This
   creates two import paths for the same constant. All existing code already
   imports from `app-config.ts` and will continue to work. New code should
   prefer importing from `env.ts` directly unless it also needs `LOGIN_URL`,
   `SESSION_COOKIE_NAME`, or other app-config constants.

3. **`IS_PROD` and `NODE_ENV` are now exported from `env.ts`** — previously
   each file defined its own local `const IS_PROD = process.env.NODE_ENV === 'production'`.
   Login, logout, and auth.ts have been updated to use the centralised export.
   Logger and api-client still define their own local `IS_DEV` for readability
   since they are self-contained modules. A follow-up could consolidate these.

4. **`getEnv()` is a utility — not yet used by the exported constants** — The
   exported constants (`CONTROL_CENTER_ORIGIN`, `CONTROL_CENTER_API_BASE`,
   `BASE_PATH`) read `process.env` directly with fallback chains rather than
   calling `getEnv()`. This is intentional: those constants need specific
   multi-step fallback chains that `getEnv(key, fallback)` cannot express in
   a single call (e.g. `envA ?? envB ?? literal`). `getEnv()` is provided
   for ad-hoc use by future callers that need a single-variable lookup.

5. **HSTS `preload` flag** — Including `preload` in the HSTS header submits
   the domain to browser preload lists (HSTS Preload List). Once submitted,
   the domain is hard-coded in browsers to use HTTPS even before the first
   visit. This is a strong setting and requires the domain to always serve
   HTTPS. It can only be removed by submitting a removal request. This is
   appropriate for `controlcenter.legalsynq.com` but the team should be
   aware of the commitment before deploying to production.
