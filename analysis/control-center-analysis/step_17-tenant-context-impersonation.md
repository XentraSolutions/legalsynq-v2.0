# Step 17 — Tenant Context Switching + Impersonation

## Status: Complete — 0 TypeScript errors

---

## Summary

Extended the Control Center type system and auth layer with the data shapes and
cookie helpers needed for Tenant Context Switching and Impersonation flows.

No pages were changed — this step delivers the foundational layer that
UI components and Server Actions will consume in subsequent steps.

---

## Files Changed

| File | Change |
|------|--------|
| `src/types/control-center.ts` | Added `TenantContext` and `ImpersonationSession` interfaces |
| `src/lib/app-config.ts` | Added `TENANT_CONTEXT_COOKIE_NAME = 'cc_tenant_context'` constant |
| `src/lib/auth.ts` | Added `getTenantContext()`, `setTenantContext()`, `clearTenantContext()` |

---

## New Types (`types/control-center.ts`)

### `TenantContext`

```ts
/**
 * Identifies which tenant the platform admin is currently scoped to
 * for context-switching and impersonation flows.
 *
 * Stored in the cc_tenant_context cookie.
 */
export interface TenantContext {
  tenantId:   string;
  tenantName: string;
  tenantCode: string;
}
```

### `ImpersonationSession`

```ts
/**
 * Records a live impersonation started by a platform admin acting as a tenant.
 *
 * TODO: persist impersonation session in backend and emit audit log entry
 */
export interface ImpersonationSession {
  originalAdminId:        string;   // userId of PlatformAdmin who started impersonation
  impersonatedTenantId:   string;   // id of the tenant whose context is active
  impersonatedTenantName: string;   // display name for UI banners
  startedAtUtc:           string;   // ISO 8601 UTC timestamp
}
```

---

## New Cookie Constant (`lib/app-config.ts`)

```ts
/**
 * TODO: persist tenant context in backend session
 */
export const TENANT_CONTEXT_COOKIE_NAME = 'cc_tenant_context' as const;
```

Follows the same pattern as `SESSION_COOKIE_NAME` — single source of truth,
never hard-coded in call sites.

---

## New Auth Helpers (`lib/auth.ts`)

### `getTenantContext(): TenantContext | null`

```ts
export function getTenantContext(): TenantContext | null
```

- Reads `cc_tenant_context` cookie via `cookies()` from `next/headers`
- Parses and validates JSON before returning — never throws on malformed data
- Returns `null` when no tenant is selected (global admin view) or cookie is absent/malformed
- Safe to call from Server Components, Server Actions, and Route Handlers

**Validation guard:**
```ts
if (
  parsed !== null &&
  typeof parsed === 'object' &&
  'tenantId'   in parsed && typeof parsed.tenantId   === 'string' &&
  'tenantName' in parsed && typeof parsed.tenantName === 'string' &&
  'tenantCode' in parsed && typeof parsed.tenantCode === 'string'
) { return parsed as TenantContext; }
return null;
```

### `setTenantContext(tenant: TenantContext): void`

```ts
export function setTenantContext(tenant: TenantContext): void
```

- Writes `cc_tenant_context` cookie as `JSON.stringify(tenant)`
- **Must only be called from a Server Action or Route Handler**
  (Next.js throws if `cookies().set()` is called inside a Server Component render)
- Cookie options:
  - `httpOnly: false` — not an auth credential; client JS may read for optimistic UI
  - `sameSite: 'lax'` — adequate for non-sensitive UI state
  - `secure: true` in production
  - `path: '/'` — available across all Control Center routes
  - No `maxAge` — session cookie; cleared on browser close + explicit logout

### `clearTenantContext(): void`

```ts
export function clearTenantContext(): void
```

- Deletes `cc_tenant_context` via `cookies().delete()`
- **Must only be called from a Server Action or Route Handler**
- Called on logout, "Exit tenant context" action, and global admin navigation

---

## Cookie Design

| Property | `platform_session` | `cc_tenant_context` |
|----------|-------------------|---------------------|
| Purpose | Auth credential | UI context state |
| `httpOnly` | `true` | `false` (client-readable) |
| `secure` | prod only | prod only |
| `sameSite` | `strict` prod / `lax` dev | `lax` |
| `maxAge` | Derived from JWT expiry | None (session cookie) |
| Cleared on logout | Yes (BFF route) | Yes (BFF route, future) |

---

## TODO Markers

```ts
// TODO: persist tenant context in backend session
```

Present in:
- `types/control-center.ts` (on `ImpersonationSession`)
- `lib/app-config.ts` (on `TENANT_CONTEXT_COOKIE_NAME`)
- `lib/auth.ts` (on `setTenantContext` and `clearTenantContext`)

---

## Usage Pattern (for implementing pages)

### Reading context in a Server Component

```ts
import { getTenantContext } from '@/lib/auth';

export default async function SomePage() {
  const ctx = getTenantContext();   // null → global admin view
  // ...
}
```

### Setting context in a Server Action

```ts
'use server';
import { setTenantContext } from '@/lib/auth';
import type { TenantContext } from '@/types/control-center';

export async function switchTenantContextAction(tenant: TenantContext) {
  setTenantContext(tenant);
  // redirect or revalidate
}
```

### Clearing context in a Server Action

```ts
'use server';
import { clearTenantContext } from '@/lib/auth';

export async function exitTenantContextAction() {
  clearTenantContext();
  // redirect to /tenants
}
```

---

## TypeScript Verification

```
cd apps/control-center && tsc --noEmit
# → 0 errors, 0 warnings
```
