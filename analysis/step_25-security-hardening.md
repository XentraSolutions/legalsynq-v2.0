# Step 25 – Security Hardening

## Files Updated

| File | Changes |
|------|---------|
| `src/lib/auth.ts` | Cookie security upgrades; cross-tenant impersonation scope check; enhanced shape validation; audit log helper exports |
| `src/lib/logger.ts` | PII redaction layer (JWT/Bearer tokens + partial email masking); `buildEntry()` central constructor; sanitised fields in both emitDev and emitProd |
| `src/lib/api-client.ts` | Session pre-flight check (early redirect on missing cookie); `logWarn` import added |
| `src/app/actions/impersonation.ts` | `requirePlatformAdmin()` guard; automatic tenant context alignment; audit log on start/stop |
| `src/app/actions/tenant-context.ts` | `requirePlatformAdmin()` guard; audit log on enter/exit |
| `src/app/support/actions.ts` | `requirePlatformAdmin()` guard on all three actions |
| `src/app/settings/actions.ts` | `requirePlatformAdmin()` guard |
| `src/app/tenants/[id]/actions.ts` | `requirePlatformAdmin()` guard |

---

## Security Improvements

### 1. Session pre-flight check (`api-client.ts`)

Before making any network call, `apiFetch` now checks whether the
`platform_session` cookie is present. If it is absent entirely the request is
aborted immediately:

```
logWarn('security.session.missing_token', { requestId, method, endpoint })
redirect('/login?reason=session_expired')
```

This eliminates a round-trip: without a token the gateway would return 401
anyway, which `apiFetch` would redirect on at step 6. The pre-flight check
short-circuits that path one step earlier and emits a WARN log so operators
can detect clients with stale or cleared sessions.

The check is **presence only** — it does not validate the JWT signature or
expiry. Full validation happens on the Identity service when the token is
forwarded.

### 2. Cross-tenant impersonation scope check (`auth.ts → getImpersonation()`)

Every time `getImpersonation()` is called it now cross-validates the
impersonation session against the active tenant context:

```
if (tenantCtx && tenantCtx.tenantId !== session.tenantId) {
  logWarn('security.impersonation.tenant_mismatch', { ... })
  return null   // reject — prevents cross-tenant access
}
```

This prevents a platform admin from accidentally or deliberately impersonating
a user from tenant B while scoped to tenant A. The mismatch is logged at WARN
level so it appears in dashboards without raising false-positive alerts.

When no tenant context is set, the impersonation is accepted as-is (the admin
is in global view, which is a permitted operating mode).

### 3. Automatic tenant context alignment (`impersonation.ts → startImpersonationAction`)

`startImpersonationAction` now writes a matching `cc_tenant_context` cookie
whenever the current context is missing or points to a different tenant:

```ts
const currentTenantCtx = getTenantContext();
if (!currentTenantCtx || currentTenantCtx.tenantId !== user.tenantId) {
  setTenantContext({ tenantId, tenantName, tenantCode });
}
```

This guarantees that `cc_impersonation` and `cc_tenant_context` always agree
on `tenantId` at the moment the session begins, satisfying the scope check
on the very first `getImpersonation()` read.

### 4. Enhanced cookie shape validation (`auth.ts`)

Both `getTenantContext()` and `getImpersonation()` previously checked field
types only. They now also verify that all string values are **non-empty**
(`.trim().length > 0`) and log security warnings for any malformed input:

```
logWarn('security.tenant_context.invalid_shape', ...)
logWarn('security.tenant_context.parse_error', ...)
logWarn('security.impersonation.invalid_shape', ...)
logWarn('security.impersonation.parse_error', ...)
```

### 5. PII redaction in logger (`logger.ts`)

See dedicated "Logging Changes" section below.

### 6. Server-side guards on all Server Actions

Every Server Action now calls `requirePlatformAdmin()` before any mutation.
`requirePlatformAdmin()` calls `requirePlatformAdmin()` from `auth-guards.ts`
which calls `getServerSession()` from `session.ts`:

- No session cookie  → `redirect('/login?reason=unauthenticated')`
- Session invalid    → `redirect('/login?reason=unauthenticated')`
- Not PlatformAdmin  → `redirect('/login?reason=unauthorized')`

Previously `stopImpersonationAction` and all three data-mutation actions
(`updateCaseStatus`, `addCaseNote`, `createSupportCase`, `updateSetting`,
`updateProductEntitlement`) had NO session guard at all. Any caller could
invoke them without a session cookie.

### 7. Audit logging for privileged events (`auth.ts` + actions)

Three new audit log helpers exported from `auth.ts`:

| Helper | Event label | When |
|--------|-------------|------|
| `logImpersonationStart(adminId, impersonatedUserId, tenantId)` | `audit.impersonation.start` | After writing `cc_impersonation` cookie |
| `logImpersonationStop(adminId, impersonatedUserId, tenantId)` | `audit.impersonation.stop` | After clearing `cc_impersonation` cookie |
| `logTenantContextSwitch(adminId, tenantId, action)` | `audit.tenant_context.switch` | After writing/clearing `cc_tenant_context` |

All three use `logInfo` so audit events appear in both dev (coloured console
lines) and prod (NDJSON to stdout) log streams. Fields included:

```json
{ "level": "INFO", "message": "audit.impersonation.start",
  "adminId": "uuid", "impersonatedUserId": "uuid", "tenantId": "uuid",
  "service": "control-center", "timestamp": "ISO-8601" }
```

---

## Cookie Strategy

| Cookie | httpOnly | secure | sameSite (prod) | sameSite (dev) | Notes |
|--------|----------|--------|-----------------|----------------|-------|
| `platform_session` | `true` | `true` | `strict` | `lax` | JWT credential — set by BFF login/logout route. Was already `httpOnly:true`. |
| `cc_impersonation` | `true` ← **upgraded** | `true` | `strict` ← **upgraded** | `lax` | Impersonation session. Was `httpOnly:false` — now httpOnly to prevent XSS from exfiltrating the impersonated identity. sameSite upgraded from `lax` to `strict` in prod. |
| `cc_tenant_context` | `false` | `true` | `strict` ← **upgraded** | `lax` | Non-auth UI state. Kept `httpOnly:false` so client JS can read it for potential banner usage without a server round-trip. sameSite upgraded from `lax` to `strict` in prod. |

### Why `cc_impersonation` is now `httpOnly: true`

The impersonation cookie contains `adminId + impersonatedUserId + impersonatedUserEmail`.
Making it client-readable (`httpOnly: false`) was a security defect:

1. **XSS exfiltration** — a script injection on any CC page could read the
   cookie and extract the impersonated user's identity, including email.
2. **Client-side forgery** — client JS could write a new impersonation cookie
   directly, bypassing `startImpersonationAction` and its `requirePlatformAdmin()` guard.

Making it `httpOnly: true` fixes both. All reads of this cookie happen
server-side via `getImpersonation()`, so no client-side functionality is broken.

### Why `cc_tenant_context` remains `httpOnly: false`

The tenant context cookie contains only non-secret display values
(`tenantId`, `tenantName`, `tenantCode`). These do not constitute auth
credentials — they are opaque identifiers that the server validates anyway
on every request. Keeping `httpOnly: false` preserves the option for future
client-side banner rendering without a server round-trip.

The `sameSite: 'strict'` upgrade in production reduces CSRF exposure even
without httpOnly.

---

## Access Control Rules

### Who can call Server Actions

| Action file | Required role | Guard mechanism |
|-------------|---------------|-----------------|
| `app/actions/impersonation.ts` | PlatformAdmin | `requirePlatformAdmin()` (start + stop) |
| `app/actions/tenant-context.ts` | PlatformAdmin | `requirePlatformAdmin()` (switch + exit) |
| `app/support/actions.ts` | PlatformAdmin | `requirePlatformAdmin()` (all 3 actions) |
| `app/settings/actions.ts` | PlatformAdmin | `requirePlatformAdmin()` (1 action) |
| `app/tenants/[id]/actions.ts` | PlatformAdmin | `requirePlatformAdmin()` (1 action) |

### Guard call chain

```
requirePlatformAdmin()           ← auth.ts facade
  └── _requirePlatformAdmin()   ← auth-guards.ts
        └── getServerSession()  ← session.ts
              └── fetch('/identity/api/auth/me', { Bearer token })
```

The session is validated against the Identity service on every guard call.
There is no in-memory cache that a stale session could exploit.

### Impersonation scope

```
getImpersonation()
  1. Read cc_impersonation cookie
  2. Validate shape (5 required non-empty string fields)
  3. getTenantContext()
     - if tenantCtx.tenantId ≠ impersonation.tenantId → logWarn + return null
  4. Return UserImpersonationSession
```

---

## Logging Changes

### PII redaction in `logger.ts`

A `redactSensitive` pipeline was added between log entry construction and
output emission. The pipeline runs inside `buildEntry()` (called by all three
public log functions) and applies:

#### Token redaction (ALL environments — dev and prod)

| Pattern | Replacement | Rationale |
|---------|-------------|-----------|
| `eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}` | `[REDACTED_JWT]` | Catches JWT access tokens that accidentally end up in error messages |
| `Bearer\s+[A-Za-z0-9_\-.+/=]{8,}` | `Bearer [REDACTED_TOKEN]` | Catches Authorization header values in error messages |

Token redaction runs in **both environments** because:
- A JWT in a log line has zero debugging value (it's opaque base64)
- Its presence indicates a code path that accidentally serialised a credential

#### Email redaction (production only)

```
margaret@hartwell.law  →  m*******@hartwell.law
admin@legalsynq.com    →  a****@legalsynq.com
```

Only the first character of the local part is preserved. The domain is kept
so operators can identify tenant-specific issues.

In development, full email addresses are preserved for easier tracing.

#### Fields sanitised

| Field | Always | Prod only |
|-------|--------|-----------|
| `impersonatedUserEmail` | token redaction | + email masking |
| `endpoint` | token redaction | — |
| `errorMessage` | token redaction | + email masking |
| All other string fields (index signature) | token redaction | + email masking (prod) |

#### Impact on existing log output

In development, the change is invisible — no email masking applies and no JWTs
appear in existing log entries. The only observable change would be if a future
bug accidentally included a JWT in an error message; that would now be redacted
to `[REDACTED_JWT]` even in dev.

In production, `impersonatedUserEmail` (when present) will appear as
`m*******@hartwell.law` instead of the full address in NDJSON output.

### New log events

| Event | Level | When | Fields |
|-------|-------|------|--------|
| `security.session.missing_token` | WARN | `apiFetch` called with no `platform_session` cookie | requestId, method, endpoint |
| `security.tenant_context.invalid_shape` | WARN | `cc_tenant_context` cookie present but fails validation | endpoint=cookie name |
| `security.tenant_context.parse_error` | WARN | `cc_tenant_context` JSON parse failure | endpoint=cookie name |
| `security.impersonation.invalid_shape` | WARN | `cc_impersonation` cookie present but fails validation | endpoint=cookie name |
| `security.impersonation.parse_error` | WARN | `cc_impersonation` JSON parse failure | endpoint=cookie name |
| `security.impersonation.tenant_mismatch` | WARN | `cc_impersonation.tenantId ≠ cc_tenant_context.tenantId` | impersonatedUserId, tenantId |
| `audit.impersonation.start` | INFO | Admin starts impersonating a user | adminId, impersonatedUserId, tenantId |
| `audit.impersonation.stop` | INFO | Admin stops impersonation | adminId, impersonatedUserId, tenantId |
| `audit.tenant_context.switch` | INFO | Admin enters or exits a tenant context | adminId, tenantId, auditAction |

---

## TODOs

```ts
// TODO: add CSRF protection
//   — Add a CSRF token to all Server Action forms using the
//     double-submit cookie pattern or a signed nonce.
//   — Next.js 14 App Router provides some built-in CSRF protection via
//     the same-origin enforcement on Server Actions, but an explicit
//     token adds defence-in-depth.

// TODO: add rate limiting
//   — Limit impersonation start/stop and tenant context switch actions
//     to prevent brute-force abuse. A Redis-backed counter keyed on
//     adminId + action type is the recommended approach.

// TODO: add RBAC enforcement middleware
//   — Move the requirePlatformAdmin() call into a shared middleware
//     (next.config.js matcher or a custom middleware.ts) so that any
//     new Server Action added to the codebase is protected by default,
//     rather than requiring every developer to remember to add the guard.

// TODO: add security headers (CSP, HSTS)
//   — Set Content-Security-Policy, Strict-Transport-Security,
//     X-Frame-Options, X-Content-Type-Options, and Referrer-Policy
//     headers on all Control Center responses via next.config.js
//     headers() configuration.
//   — CSP is especially important for the Control Center because it
//     handles sensitive tenant and impersonation data; a strict CSP
//     limits the damage of any XSS vulnerability.

// TODO: validate tenantId ownership against backend session
//   — getTenantContext() currently validates the cookie shape only.
//     A full ownership check would call GET /identity/api/admin/tenants/{id}
//     and verify the admin has access to that tenant before trusting the
//     cookie value.

// TODO: integrate audit events with AuditLog table
//   — logImpersonationStart / logImpersonationStop / logTenantContextSwitch
//     currently emit structured log lines only. Persist these to the
//     AuditLog entity (already seeded in the Identity service) via a
//     POST /identity/api/admin/audit endpoint.
```

---

## Independence Validation

- No new npm packages installed — all changes use existing imports
- Public API signatures unchanged:
  - `apiFetch<T>(path, options?)` → same signature
  - `logInfo / logWarn / logError` → same signatures
  - `getTenantContext / getImpersonation / setImpersonation / clearImpersonation / setTenantContext / clearTenantContext / getSession / requirePlatformAdmin / toSessionUser` → same signatures
  - All Server Action function signatures (argument shapes + return types) unchanged
- New exports from `auth.ts`: `logImpersonationStart`, `logImpersonationStop`, `logTenantContextSwitch` — additive only
- No UI components modified
- No route handlers modified
- `platform_session` handling in `login/route.ts` and `logout/route.ts` unchanged (was already `httpOnly: true`)
- TypeScript check: **0 errors**

---

## Any Issues or Assumptions

1. **`cc_impersonation` httpOnly upgrade** — making this cookie httpOnly means
   any client-side code that previously read `document.cookie` to check
   impersonation state will now receive an empty value. No such client-side
   reads exist in the current codebase (all reads go through `getImpersonation()`
   server-side), but future developers should be aware of this constraint.

2. **Automatic tenant context alignment in `startImpersonationAction`** — the
   `tenantCode` field in the auto-written TenantContext is derived from
   `tenantName` (first 10 chars, uppercased, whitespace stripped). This is a
   best-effort derivation. The correct fix is for the call site to always pass
   the tenantCode explicitly. The current approach is safe because `tenantCode`
   is display-only and is never used for auth decisions.

3. **`stopImpersonationAction` audit log when no impersonation is active** — if
   `stopImpersonationAction` is called when no impersonation cookie exists (e.g.
   the browser was closed between start and stop), the audit log records
   `impersonatedUserId: "none"`. This is intentional — the event is logged at
   INFO level so operators can see that a stop was attempted even if the session
   had already expired.

4. **Session pre-flight check creates a redirect-instead-of-error contract** —
   `apiFetch` can now call `redirect()` before emitting `api.request.start`.
   Callers that wrap `apiFetch` in a try/catch expecting only `ApiError` should
   be aware that `redirect()` throws a special Next.js sentinel error that
   propagates through try/catch. This is the same behaviour as the existing
   401 redirect path and is handled identically by Next.js.

5. **Cross-tenant scope check rejects valid global-view impersonation** — if a
   platform admin sets a tenant context to tenant A, then navigates to a user
   list that loads users from tenant B (possible with future cross-tenant list
   features) and starts impersonation, `startImpersonationAction` auto-aligns
   the tenant context to B. The old A context is overwritten. This is the
   intended behaviour (impersonation always takes the user into the impersonated
   user's tenant) but could surprise admins who had explicitly chosen a different
   context.

6. **PII redaction regex false-positive risk** — the JWT pattern
   `eyJ[A-Za-z0-9_-]{8,}\...` could theoretically match a non-JWT base64url
   string that begins with `eyJ`. This is extremely rare in practice because
   `eyJ` decodes to `{"` (the start of a JSON object header), so the pattern
   is effectively specific to JWT headers. A false positive would replace
   a non-sensitive string with `[REDACTED_JWT]`, which is a minor dev
   experience issue, not a security or correctness issue.

7. **Email masking is production-only** — developers working with real tenant
   data in a local development environment will see full email addresses in
   the console. This is a deliberate trade-off to keep the dev experience
   ergonomic. If the dev environment ever processes real production data, the
   masking should be enabled unconditionally.
