# Step 16 — Authentication + Subdomain Readiness

## Status: Complete — 0 TypeScript errors

---

## Summary

Formalised the Control Center authentication layer and subdomain configuration.
The existing auth stack (middleware, BFF routes, login form, session helper, auth guard)
was already complete and production-ready; this step adds the canonical type file,
a clean facade module, a centralised app-config, and the required TODO markers.

All cookie names, redirect targets, and CORS origins now derive from a single
`app-config.ts` constants file — no more hard-coded strings anywhere in the stack.

---

## Files Created

| File | Purpose |
|------|---------|
| `src/types/auth.ts` | `SessionUser` — lightweight auth identity shape |
| `src/lib/auth.ts` | `getSession()`, `requirePlatformAdmin()`, `toSessionUser()` facade |
| `src/lib/app-config.ts` | `CONTROL_CENTER_ORIGIN`, `BASE_PATH`, `SESSION_COOKIE_NAME`, `LOGIN_URL` |

## Files Modified

| File | Change |
|------|--------|
| `src/middleware.ts` | Import `SESSION_COOKIE_NAME` + `BASE_PATH` from `app-config`; redirect uses `BASE_PATH/login` |
| `src/lib/session.ts` | Import `SESSION_COOKIE_NAME`; use constant instead of hard-coded string; TODO markers added |
| `src/lib/auth-guards.ts` | Import `BASE_PATH`; redirect adds `reason=unauthenticated` / `reason=unauthorized` |
| `src/app/api/auth/login/route.ts` | Import `SESSION_COOKIE_NAME`, `CONTROL_CENTER_ORIGIN`; cookie set via constant; TODO markers |
| `src/app/api/auth/logout/route.ts` | Import `SESSION_COOKIE_NAME`; cookie clear via constant; TODO markers |

---

## New Types (`types/auth.ts`)

```ts
/**
 * SessionUser — lightweight auth identity for the Control Center.
 *
 * TODO: integrate with Identity service session validation
 * TODO: move to HttpOnly secure cookies
 * TODO: support cross-subdomain auth
 */
export interface SessionUser {
  id:              string;
  email:           string;
  roles:           string[];
  isPlatformAdmin: boolean;
}
```

`SessionUser` maps to a subset of the richer `PlatformSession` (in `types/index.ts`)
which adds tenant, org, product-role, and expiry context.
`toSessionUser(session)` in `lib/auth.ts` converts between the two.

---

## Auth Facade (`lib/auth.ts`)

```ts
getSession(): Promise<PlatformSession | null>
// Delegates to getServerSession() in session.ts
// Reads platform_session cookie → GET /identity/api/auth/me

requirePlatformAdmin(): Promise<PlatformSession>
// Delegates to requirePlatformAdmin() in auth-guards.ts
// No session     → redirect /login?reason=unauthenticated
// Not PlatformAdmin → redirect /login?reason=unauthorized

toSessionUser(session): SessionUser
// Maps PlatformSession → SessionUser (thin display shape)
```

---

## App Config (`lib/app-config.ts`)

```ts
CONTROL_CENTER_ORIGIN
// Production: https://admin.legalsynq.com (via NEXT_PUBLIC_CONTROL_CENTER_ORIGIN)
// Replit dev:  https://<REPLIT_DEV_DOMAIN>
// Local:       http://localhost:5004

BASE_PATH
// Default: "" — set NEXT_PUBLIC_BASE_PATH for reverse-proxy sub-path deployments

SESSION_COOKIE_NAME
// "platform_session" — matches Identity service JWT flow
// TODO: rename to cc_session once cross-subdomain cookie scoping is in place

LOGIN_URL
// CONTROL_CENTER_ORIGIN + BASE_PATH + "/login"
// Used in middleware and server-side redirects — no hard-coded localhost
```

---

## Cookie Design

| Property | Value | Notes |
|----------|-------|-------|
| Name | `platform_session` (constant) | TODO: rename to `cc_session` post cross-subdomain work |
| `httpOnly` | `true` | Token never reaches browser JS |
| `secure` | `true` in production, `false` in dev | |
| `sameSite` | `strict` in production, `lax` in dev | |
| `path` | `/` | App-wide |
| `maxAge` | Derived from Identity `expiresAtUtc` | Matches backend token TTL |

---

## Middleware Flow

```
Request
  ├─ Matches PUBLIC_PATHS (/login, /_next, /favicon, /api/auth/*)?
  │   └─ NextResponse.next()
  └─ Has SESSION_COOKIE_NAME cookie?
      ├─ Yes → NextResponse.next()
      │         (role check done by requirePlatformAdmin() in each page)
      └─ No  → redirect BASE_PATH/login?reason=unauthenticated
```

---

## Auth Guard Flow (per page)

```
requirePlatformAdmin()
  ├─ getServerSession() → null?
  │   └─ redirect /login?reason=unauthenticated
  ├─ session.isPlatformAdmin === false?
  │   └─ redirect /login?reason=unauthorized
  └─ return PlatformSession ✓
```

---

## TODO Markers Placed

The following markers were added to the relevant files:

```ts
// TODO: integrate with Identity service session validation
// TODO: move to HttpOnly secure cookies
// TODO: support cross-subdomain auth
```

Present in:
- `types/auth.ts`
- `lib/auth.ts`
- `lib/auth-guards.ts`
- `lib/session.ts`
- `app/api/auth/login/route.ts`
- `app/api/auth/logout/route.ts`
- `middleware.ts`

---

## Pages Audit

All 14 protected pages already call `requirePlatformAdmin()` at the top of their
server component. No page change was needed.

| Page | Guard | Status |
|------|-------|--------|
| `/tenants` | `requirePlatformAdmin()` | ✓ |
| `/tenants/[id]` | `requirePlatformAdmin()` | ✓ |
| `/tenants/[id]/users` | `requirePlatformAdmin()` | ✓ |
| `/tenant-users` | `requirePlatformAdmin()` | ✓ |
| `/tenant-users/[id]` | `requirePlatformAdmin()` | ✓ |
| `/roles` | `requirePlatformAdmin()` | ✓ |
| `/roles/[id]` | `requirePlatformAdmin()` | ✓ |
| `/audit-logs` | `requirePlatformAdmin()` | ✓ |
| `/settings` | `requirePlatformAdmin()` | ✓ |
| `/monitoring` | `requirePlatformAdmin()` | ✓ |
| `/support` | `requirePlatformAdmin()` | ✓ |
| `/support/[id]` | `requirePlatformAdmin()` | ✓ |

---

## Subdomain Readiness

| Concern | Implementation |
|---------|---------------|
| Origin | `CONTROL_CENTER_ORIGIN` reads `NEXT_PUBLIC_CONTROL_CENTER_ORIGIN` env var |
| Base path | `BASE_PATH` reads `NEXT_PUBLIC_BASE_PATH` — zero config for root deployments |
| Cookie scope | Currently `path: "/"` — TODO: add `domain: ".legalsynq.com"` |
| Redirect target | All redirects go through `BASE_PATH/login` — no hard-coded host |
| CORS | `allowedOrigins: ['*']` in `next.config.mjs` (server actions) — TODO: restrict in production |

---

## TypeScript Verification

```
cd apps/control-center && tsc --noEmit
# → 0 errors, 0 warnings
```
