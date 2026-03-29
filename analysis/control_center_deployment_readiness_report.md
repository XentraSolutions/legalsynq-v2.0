# Control Center Deployment Readiness Report

## 1. Current Coupling to Path-Prefix Deployment

The following assumptions were identified and exist **before** this hardening work:

### Hardcoded `/control-center/...` path strings

| File | Count | Detail |
|---|---|---|
| `lib/control-center-nav.ts` | 9 | All `href` values in every nav group item |
| `app/(control-center)/control-center/page.tsx` | 8 | All `href` values in the `MODULES` array |

### Same-origin auth redirect assumptions

| File | Assumption | Impact |
|---|---|---|
| `lib/auth-guards.ts` → `requirePlatformAdmin()` | Redirects to `/dashboard` on failed auth | `/dashboard` is an operator portal route — does not exist on a standalone CC host |
| `components/shell/control-center-shell.tsx` → `handleSignOut` | `window.location.href = '/login'` | `/login` is the operator portal login page — may not exist on a standalone CC host |

### Same-origin API/BFF assumptions

| File | Assumption | Impact in standalone mode |
|---|---|---|
| `app/api/identity/[...path]/route.ts` | Reads `platform_session` from request cookies | ✅ Safe — cookie is forwarded automatically if domain is correct |
| `app/api/identity/[...path]/route.ts` | `GATEWAY_URL` env var | ✅ Already configurable via env |
| `lib/control-center-api.ts` | Client-side `apiClient` calls relative `/api/identity/...` | ✅ Safe — relative paths resolve to current host; works if the standalone app includes the same BFF routes |

### Middleware scope

| Item | Assumption | Impact |
|---|---|---|
| `middleware.ts` | The `platform_session` cookie gate applies to all routes | ✅ Safe embedded — in standalone the entire host would need the same middleware with a different login redirect |
| `middleware.ts` | Cookie name `platform_session` is set on the operator portal's domain | ⚠️ **Gap** — in standalone mode the cookie must be set on `.legalsynq.com` (parent domain) or `controlcenter.legalsynq.com` specifically |

---

## 2. Changes Made

### A. `lib/control-center-config.ts` (new)

Centralizes all Control Center deployment configuration. Exports:
- `CC_STANDALONE` — boolean, driven by `NEXT_PUBLIC_CC_STANDALONE`
- `CC_BASE_PATH` — path prefix (empty in standalone, `/control-center` in embedded)
- `CC_ORIGIN` — full public origin for standalone (`NEXT_PUBLIC_CC_ORIGIN`)
- `CC_LOGIN_URL` — post-logout/no-session redirect (`NEXT_PUBLIC_CC_LOGIN_URL`, default `/login`)
- `CC_ACCESS_DENIED_URL` — redirect for authenticated but non-admin users (`/dashboard` embedded, `CC_LOGIN_URL` standalone)

### B. `lib/control-center-routes.ts` (new)

Centralizes all Control Center URL construction. Exports:
- `cc(path)` — internal function that prepends `CC_BASE_PATH` and handles the edge case of empty base + empty path → `/`
- `CCRoutes` — typed const object with all named routes: `dashboard`, `tenants`, `tenantUsers`, `roles`, `products`, `support`, `auditLogs`, `monitoring`, `settings`
- `CCRouteBuilders` — dynamic route builders for parameterized routes (e.g. `tenantDetail(id)`)

### C. `lib/control-center-nav.ts` (updated)

All 9 hardcoded `/control-center/...` href strings replaced with `CCRoutes.*` references.

### D. `app/(control-center)/control-center/page.tsx` (updated)

All 8 hardcoded `/control-center/...` href strings in the `MODULES` array replaced with `CCRoutes.*` references.  
Also updated to call `requireCCPlatformAdmin()` instead of `requirePlatformAdmin()`.

### E. `components/shell/control-center-shell.tsx` (updated)

- `handleSignOut` now redirects to `CC_LOGIN_URL` instead of hardcoded `/login`
- Logo/branding link in the top bar now uses `CCRoutes.dashboard` instead of no link
- CC_LOGIN_URL and CCRoutes imports added

### F. `lib/auth-guards.ts` (updated)

Added `requireCCPlatformAdmin()`:
- Reads session via `getServerSession()` (no redirect on missing session — this lets us control the target)
- No session → `redirect(CC_LOGIN_URL)` (config-driven, not hardcoded `/login`)
- Session but not PlatformAdmin → `redirect(CC_ACCESS_DENIED_URL)` (config-driven, not hardcoded `/dashboard`)
- All existing guards (`requirePlatformAdmin()`, etc.) are untouched

### G. `app/(control-center)/layout.tsx` (updated)

Switched from `requirePlatformAdmin()` to `requireCCPlatformAdmin()`.

---

## 3. New Routing/Config Abstractions

### `lib/control-center-config.ts`

Single source of truth for CC deployment mode. Consumed by:
- `lib/control-center-routes.ts` (to build paths)
- `lib/auth-guards.ts` (for redirect targets)
- `components/shell/control-center-shell.tsx` (for logout redirect)

**Environment variables:**

| Variable | Default | Purpose |
|---|---|---|
| `NEXT_PUBLIC_CC_STANDALONE` | `false` | Switch to standalone/host-root mode |
| `NEXT_PUBLIC_CC_BASE_PATH` | `/control-center` | Override path prefix (embedded mode only) |
| `NEXT_PUBLIC_CC_ORIGIN` | `` | Full public origin in standalone mode |
| `NEXT_PUBLIC_CC_LOGIN_URL` | `/login` | Post-logout / no-session redirect |
| `NEXT_PUBLIC_CC_ACCESS_DENIED_URL` | `/dashboard` | Redirect for authenticated non-admins (embedded only) |

### `lib/control-center-routes.ts`

**Rule:** Every internal CC link (nav, dashboard, Server Components, Client Components) MUST use `CCRoutes.*` or `CCRouteBuilders.*`. Never write `/control-center/...` strings directly.

```ts
// Correct
import { CCRoutes } from '@/lib/control-center-routes';
<Link href={CCRoutes.tenants}>All Tenants</Link>

// Wrong — breaks standalone mode
<Link href="/control-center/tenants">All Tenants</Link>
```

Dynamic routes use builders:
```ts
import { CCRouteBuilders } from '@/lib/control-center-routes';
const href = CCRouteBuilders.tenantDetail(tenantId);
```

---

## 4. What Already Supports Separate Deployment

The following was already deployment-safe before this work, or is inherently safe by design:

| Item | Why safe |
|---|---|
| **BFF identity proxy** (`app/api/identity/[...path]/route.ts`) | Uses `GATEWAY_URL` env var — already fully configurable |
| **Client-side API client** relative paths `/api/identity/...` | Relative URLs resolve to current host — safe in both modes |
| **Server-side API client** (`lib/server-api-client.ts`) | Calls `GATEWAY_URL` directly — already configurable |
| **Auth cookie reading** (`platform_session`) | Read from request cookies — works on any host as long as cookie is present |
| **Tailwind / component library** | No domain-specific assumptions |
| **Route group `(control-center)`** | Next.js route groups are path-prefix-neutral by design |
| **TypeScript types** (`types/control-center.ts`) | Pure data types — no URL assumptions |
| **`requireCCPlatformAdmin()`** | Now uses config-driven redirect targets |
| **CC nav and dashboard** | Now use `CCRoutes.*` — path-prefix-neutral |

---

## 5. Remaining Gaps for `controlcenter.legalsynq.com`

These are **not solved** by this hardening step and require backend, infrastructure, or future frontend work:

### 5.1 Cookie domain — CRITICAL

**Problem:** `platform_session` is currently set as a `SameSite=Lax` cookie on `app.legalsynq.com`. Browsers will not send this cookie to `controlcenter.legalsynq.com`.

**Required fix:** When the Identity.Api issues the `platform_session` cookie on login, the `Domain` attribute must be set to `.legalsynq.com` (parent domain) to allow it to be shared across all subdomains.

**Risk:** Setting `Domain=.legalsynq.com` broadens the cookie scope — this is a deliberate security trade-off that must be evaluated. `Secure` and `HttpOnly` flags must remain set.

**Where to fix:** `Identity.Api` → login endpoint → `SetCookieOptions.Domain`.

### 5.2 Auth redirects on the standalone host

**Problem:** In standalone mode, when a user has no session, the middleware redirects to `/login`. That path must resolve to a usable login page on `controlcenter.legalsynq.com`. Options:
1. Build a thin login page inside the CC route group that redirects to the operator portal's full login flow with a `return_to` param
2. Set `NEXT_PUBLIC_CC_LOGIN_URL=https://app.legalsynq.com/login?return_to=...` and update middleware to redirect to the external URL

The current frontend code (via `CC_LOGIN_URL`) is now ready for option 2 in auth guards and shell. Middleware needs a separate update to support external login redirect.

### 5.3 Middleware scope

**Problem:** `middleware.ts` is currently scoped to the full Next.js app. In embedded mode, it protects `/control-center/*` as part of its broader cookie check. In standalone mode, the same middleware would cover all routes on the dedicated host — but the login redirect URL would need to be an external URL, not `/login` on the same host.

**Partial fix available now:** `CC_LOGIN_URL` env var can be set to `https://app.legalsynq.com/login` and the middleware can be updated to use it. Not done in this step.

### 5.4 CORS at the gateway

**Problem:** If `controlcenter.legalsynq.com` makes any browser-side fetch to the gateway at `gateway.legalsynq.com` (or similar), CORS headers must permit the new origin.

**Status:** All browser fetches currently go through the BFF proxy (Next.js API routes), so browser → gateway CORS is not an issue today. Server-side calls (`GATEWAY_URL`) are server-to-server and not subject to CORS. This stays safe as long as the BFF pattern is preserved.

### 5.5 `/api/auth/logout` route

**Problem:** The logout POST (`/api/auth/logout`) clears the `platform_session` cookie. In standalone mode this route must exist on `controlcenter.legalsynq.com` and must clear the cookie with the correct `Domain=.legalsynq.com` attribute (matching the domain it was set on), otherwise the cookie will not be cleared.

**Status:** The route exists and works in embedded mode. In standalone mode it must also be deployed (it would be — same Next.js codebase) but the `Domain` attribute on the `Set-Cookie: platform_session=; Max-Age=0` response must match the login-time domain.

### 5.6 Environment configuration for the standalone deployment

A standalone deployment needs these env vars set at build/runtime:

```env
NEXT_PUBLIC_CC_STANDALONE=true
NEXT_PUBLIC_CC_ORIGIN=https://controlcenter.legalsynq.com
NEXT_PUBLIC_CC_LOGIN_URL=https://app.legalsynq.com/login
GATEWAY_URL=https://gateway.legalsynq.com   # or internal URL
```

These env vars are now supported by the config — they just need to be provided at deployment time.

---

## 6. Recommended Deployment Model

### Recommendation: Keep as one Next.js app, deploy twice

**Do not** create a separate repo or separate Next.js app at this time. The cleanest model for this stack and timeline is:

> **One codebase → two Next.js deployments from the same build output**

- **Deployment A** — `app.legalsynq.com` — operator portal only  
  `NEXT_PUBLIC_CC_STANDALONE=false` (default)  
  Includes the CC route group at `/control-center` — accessible to platform admins

- **Deployment B** — `controlcenter.legalsynq.com` — Control Center standalone  
  `NEXT_PUBLIC_CC_STANDALONE=true`  
  `NEXT_PUBLIC_CC_ORIGIN=https://controlcenter.legalsynq.com`  
  `NEXT_PUBLIC_CC_LOGIN_URL=https://app.legalsynq.com/login`  
  The operator portal routes still exist in the build but are never linked to or surfaced

### Why not a separate Next.js app in the monorepo?

A separate `apps/control-center` Next.js app would:
- Require duplicating or packaging shared auth, session, BFF, and component code
- Add a new build target, workflow, and deployment pipeline
- Create version drift risk between shared types and implementations
- Complicate the work before the CC feature set is even built

**When to reconsider:** If the Control Center grows to 20+ pages with its own design system, data fetching layer, and team, a separate app becomes worthwhile. Not yet.

### The middle path

A route-group-scoped deployment is already supported by the current architecture:
- Route group `(control-center)` is cleanly separated from the operator portal
- All routes, nav, guards, and BFF proxies are already isolated
- The monorepo can add a `deploy-control-center.sh` script that builds with `NEXT_PUBLIC_CC_STANDALONE=true` and deploys to the CC subdomain from the same codebase

---

## 7. Actionable Guidance for the Next Step

After this hardening work, the Control Center scaffold is deployment-safe in structure. The next recommended step is:

### Phase 2 — Build the Identity Admin backend endpoints

The Control Center pages cannot be built with real data until Identity.Api exposes admin endpoints:

| Endpoint | Purpose |
|---|---|
| `GET /identity/api/admin/tenants` | All tenants, paged |
| `GET /identity/api/admin/tenants/{id}` | Single tenant detail |
| `GET /identity/api/admin/users` | All users across tenants, paged |
| `GET /identity/api/admin/users/{id}` | Single user detail |
| `GET /identity/api/admin/roles` | All roles and their capabilities |
| `PUT /identity/api/admin/tenants/{id}/entitlements` | Set product entitlements |

These should be gated behind a `[Authorize(Roles = "PlatformAdmin")]` policy in Identity.Api.

Once these exist, Phase 3 (Tenants list page), Phase 4 (Tenant detail), and Phase 5 (Users) can be built — all of which are blocked on this backend work.

**Parallel work that can start now without the backend:**
- Monitoring page (calls existing `/health` endpoints on each service)
- Platform Settings page skeleton (frontend only, settings stored locally or in a config table)
- Control Center "link" in the operator portal top bar for `isPlatformAdmin` users
