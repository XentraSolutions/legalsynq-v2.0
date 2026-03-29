# Step 19 – Tenant Context Data Scoping

## Status: Complete — 0 TypeScript errors

---

## Files Updated

| File | What changed |
|------|-------------|
| `src/lib/control-center-api.ts` | Extended `tenants.list()`, `support.list()`, `audit.list()` with `tenantId?` param; added TODO markers to all four namespaces |
| `src/app/tenants/page.tsx` | Passes `tenantCtx?.tenantId` to `tenants.list()` |
| `src/app/tenants/[id]/page.tsx` | Context-aware switch button (Active Context / Switch Context / Switch to Tenant Context) |
| `src/app/tenant-users/page.tsx` | Adds `getTenantContext()`, scoped API call, amber header badge, `showTenantColumn={!tenantCtx}` |
| `src/app/support/page.tsx` | Adds `getTenantContext()`, scoped API call, amber header badge, dynamic subtitle |
| `src/app/support/[id]/page.tsx` | Adds mismatch warning when case tenant ≠ active context tenant |
| `src/app/audit-logs/page.tsx` | Adds `getTenantContext()`, scoped API call, amber header badge, dynamic subtitle |

---

## API Changes

### `tenants.list(params)` — NEW `tenantId?` param

```ts
list(params: {
  page?:     number;
  pageSize?: number;
  search?:   string;
  tenantId?: string;   // ← NEW
})
```

Behavior when `tenantId` provided: filters `MOCK_TENANTS` to the single matching tenant.
Behavior when absent: returns all tenants (global view, unchanged).

### `users.list(params)` — already had `tenantId?`

No functional change. Added TODO markers:
```ts
// TODO: enforce tenant scoping server-side
// TODO: validate tenant context against session
```

### `support.list(params)` — NEW `tenantId?` param

```ts
list(params: {
  ...existing params...
  tenantId?: string;   // ← NEW
})
```

Behavior: `filtered = filtered.filter(c => c.tenantId === tenantId)` applied before
text/status/priority filters.

### `audit.list(params)` — NEW `tenantId?` param

```ts
list(params: {
  ...existing params...
  tenantId?: string;   // ← NEW
})
```

Behavior: cross-references `MOCK_TENANTS` to get the tenant code, then filters:
```ts
meta?.tenantCode === code || (e.entityType === 'Tenant' && e.entityId === code)
```

Matches events that reference the tenant in `metadata.tenantCode` (user/entitlement events)
or as a direct entity (`entityType: 'Tenant'`).

---

## UI Behavior Changes

### All scoped pages (Tenants, Users, Support, Audit Logs)

When `tenantCtx` is non-null every page heading gains an inline amber pill:

```
[Page Title]  ● Scoped to Hartwell & Associates
```

Subtitle also updates:
- Users: "Users within Hartwell & Associates" vs "All users across all tenants"
- Support: "Cases for Hartwell & Associates — track, investigate, and resolve." vs global
- Audit: "Events for Hartwell & Associates (HARTWELL)" vs "System-wide activity log…"

### Tenant Users — tenant column hidden when scoped

```tsx
showTenantColumn={!tenantCtx}
```

The tenant column is redundant when every row is the same tenant. Hides automatically in
scoped mode; shows in global view.

### Tenant Detail — three-state context button

| State | Display | Action |
|-------|---------|--------|
| No active context | "⇄ Switch to Tenant Context" amber button | Calls `switchTenantContextAction` |
| Context is THIS tenant | "● Active Context" amber badge (non-interactive) | None |
| Context is ANOTHER tenant | "⇄ Switch Context" amber button | Calls `switchTenantContextAction` to replace context |

### Support Detail — context mismatch warning

When admin is scoped to tenant A but navigates to a case for tenant B:

```
⚠  Tenant context mismatch
   This case belongs to Meridian Care Partners, but you are currently
   viewing the context for Hartwell & Associates. Data shown is for
   the case tenant, not your active context.
```

Rendered as an amber banner above the case header.
Does NOT redirect — admin may intentionally view cross-tenant cases.

---

## Context Enforcement Rules

| Page | Scoped behavior | Unscoped behavior |
|------|----------------|-------------------|
| `/tenants` | Shows only the scoped tenant | Shows all tenants |
| `/tenant-users` | Shows only users of scoped tenant; hides tenant column | Shows all users |
| `/support` | Shows only cases for scoped tenant | Shows all cases |
| `/audit-logs` | Shows only events referencing scoped tenant | Shows all events |
| `/tenants/[id]` | Context button shows state (Active/Switch) | Switch button offered |
| `/support/[id]` | Mismatch warning if case ≠ context tenant | No warning |

---

## TODOs for Backend Integration

Added to every affected API namespace:

```ts
// TODO: enforce tenant scoping server-side
// TODO: validate tenant context against session
```

Present in:
- `controlCenterServerApi.tenants` (new)
- `controlCenterServerApi.users` (new)
- `controlCenterServerApi.support` (new)
- `controlCenterServerApi.audit` (new)

---

## Independence Validation

- Zero imports from `apps/web`
- All helpers: `getTenantContext()` from `@/lib/auth` (local)
- All types: `@/types/control-center` (local)
- No new dependencies added

---

## Assumptions

1. **Audit log tenant filtering** uses `metadata.tenantCode` (present in all user/entitlement
   events) and `entityType=Tenant + entityId=code` (for tenant-direct events). This covers
   all 28 mock rows. Once the real backend is wired, the server-side query will use `tenantId`
   directly.

2. **Tenants list with context** shows only the one scoped tenant — provides a focused view
   when working within a tenant's context. The global view is restored on exit.

3. **Mismatch is non-blocking** — admins can view cross-tenant support cases (e.g., when a
   case was created without switching context first). The warning is informational, not a gate.

4. **`getTenantContext()` is synchronous** — called without `await` in all pages.
