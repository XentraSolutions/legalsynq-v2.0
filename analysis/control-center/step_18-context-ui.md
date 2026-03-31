# Step 18 – Tenant Context UI

## Status: Complete — 0 TypeScript errors

---

## Files Created

| File | Purpose |
|------|---------|
| `src/app/actions/tenant-context.ts` | Server Actions: `switchTenantContextAction`, `exitTenantContextAction` |
| `src/components/layout/tenant-context-banner.tsx` | Amber banner shown when a tenant context is active |

## Files Updated

| File | Change |
|------|--------|
| `src/components/shell/cc-shell.tsx` | Calls `getTenantContext()`, conditionally renders `TenantContextBanner` |
| `src/app/tenants/[id]/page.tsx` | Adds "Switch to Tenant Context" button (bound Server Action) |
| `src/app/tenants/page.tsx` | Adds "Scoped to [Tenant Name]" indicator when context is active |

---

## Server Actions Added

### `switchTenantContextAction(tenant: TenantContext)`

File: `src/app/actions/tenant-context.ts`

```ts
'use server';
export async function switchTenantContextAction(tenant: TenantContext): Promise<never> {
  setTenantContext(tenant);    // writes cc_tenant_context cookie
  redirect('/');               // redirect to root
}
```

Triggered by a form with a bound action on the tenant detail page:
```tsx
const switchAction = switchTenantContextAction.bind(null, {
  tenantId:   tenant.id,
  tenantName: tenant.displayName,
  tenantCode: tenant.code,
});
<form action={switchAction}>
  <button type="submit">Switch to Tenant Context</button>
</form>
```

### `exitTenantContextAction()`

```ts
'use server';
export async function exitTenantContextAction(): Promise<never> {
  clearTenantContext();         // deletes cc_tenant_context cookie
  redirect('/tenants');         // return to global tenants list
}
```

Triggered by the "Exit Context" button in `TenantContextBanner`.

---

## UI Behavior

### Tenant Detail Page — Switch Button

Location: top-right header area of `/tenants/[id]`, alongside `TenantActions`.

Appearance: amber-tinted button (`bg-amber-50 border-amber-300 text-amber-700`) with ⇄ icon.

On click → `switchTenantContextAction` fires → `cc_tenant_context` cookie written →
redirect to `/` → banner appears on all subsequent pages.

### Tenant Context Banner

Location: full-width strip rendered by `CCShell` between the top bar and the body
(below the indigo header, above sidebar + main content).

Visible: only when `getTenantContext()` returns a non-null `TenantContext`.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  ● CONTEXT MODE    Viewing as: Hartwell Law                    [✕ Exit Context]│
│                    CODE: HARTWELL                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

Visual: amber background (`bg-amber-50`), amber border (`border-amber-200`),
amber pill badge. Clearly distinguishable from the white/indigo normal UI.

Contents:
- "Context Mode" pill with amber dot
- "Viewing as: [Tenant Name]" in amber-900
- Mono-font tenant code badge
- "✕ Exit Context" button (submits `exitTenantContextAction`)

### Tenants List — Dashboard Awareness (Optional, Step 7)

When context is active, the Tenants page heading gains an inline pill:

```
Tenants  ● Scoped to Hartwell Law
```

Implemented with a `getTenantContext()` call at the top of `TenantsPage` —
the same synchronous read pattern used in CCShell.

---

## Context Flow

```
Admin opens /tenants/[id]
  └─ Clicks "Switch to Tenant Context"
      └─ switchTenantContextAction({ tenantId, tenantName, tenantCode })
          ├─ setTenantContext() → cc_tenant_context cookie written
          └─ redirect('/')

Every subsequent page using CCShell:
  └─ CCShell calls getTenantContext()
      ├─ null  → banner hidden  (normal admin view)
      └─ TenantContext → TenantContextBanner rendered (amber strip)

Admin clicks "Exit Context" in banner:
  └─ exitTenantContextAction()
      ├─ clearTenantContext() → cc_tenant_context cookie deleted
      └─ redirect('/tenants')
```

---

## TODOs for Backend Integration

| Location | TODO |
|----------|------|
| `app/actions/tenant-context.ts` | `TODO: persist tenant context in backend session` |
| `app/actions/tenant-context.ts` | `TODO: integrate impersonation with Identity service` |
| `app/actions/tenant-context.ts` | `TODO: emit audit log entry for context switch` |

---

## Independence Validation

- Zero imports from `apps/web`
- All auth helpers come from `@/lib/auth` (local)
- All types come from `@/types/control-center` (local)
- All routes use `@/lib/routes` (local)
- Server Actions use `next/navigation` redirect (framework only)
- Cookie operations via `@/lib/auth` wrappers → `next/headers` (framework only)

---

## Assumptions

1. **`getTenantContext()` is synchronous** — reads cookie directly via `cookies().get()`.
   No `await` needed in `CCShell` or `TenantsPage`.

2. **CCShell is a Server Component** — no `'use client'` directive. Calling
   `getTenantContext()` inside a Server Component render (read-only) is safe.
   Only `setTenantContext` / `clearTenantContext` (write) are restricted to
   Server Actions, and they are correctly placed in `app/actions/tenant-context.ts`.

3. **Bound server action pattern** used on the tenant detail page instead of a
   client component, keeping the page fully server-rendered.

4. **Banner placement** — between top bar and sidebar/main body in CCShell,
   so it appears full-width on every authenticated page when context is active.

5. **`redirect('/')` for switch** — follows spec exactly. The root page (`/`) is
   public and provides a quick-nav. The banner appears on all subsequent CCShell
   pages once the cookie is set.
