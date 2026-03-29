# Step 12 — Audit Logs UI

**Date**: 2026-03-29
**App**: `apps/control-center`
**Status**: Complete — 0 TypeScript errors

---

## Objective

Implement a read-only **Audit Logs** viewer in the Control Center.
Platform admins can browse, search, and filter all system-wide activity events —
user actions, tenant updates, entitlement changes, and role changes — paginated
15 entries per page.

---

## Changes Made

### 1. Types — `apps/control-center/src/types/control-center.ts`

Replaced the existing stub `AuditLogEntry` with the spec-aligned interface:

| Field | Type | Notes |
|-------|------|-------|
| `id` | `string` | Unique event identifier |
| `actorName` | `string` | Email for Admins; service name for System |
| `actorType` | `ActorType` | `'Admin' \| 'System'` (new union type) |
| `action` | `string` | Dot-namespaced action code e.g. `user.invite` |
| `entityType` | `string` | `'User'`, `'Tenant'`, `'Entitlement'`, `'Role'`, `'System'` |
| `entityId` | `string` | ID or code of the affected record |
| `metadata?` | `Record<string, unknown>` | Key/value context captured at event time |
| `createdAtUtc` | `string` | ISO 8601 UTC timestamp |

Added new type alias:
```ts
export type ActorType = 'Admin' | 'System';
```

### 2. API — `apps/control-center/src/lib/control-center-api.ts`

**Mock dataset** — 28 entries covering all four event categories:

| Category | Count | Sample actions |
|----------|-------|----------------|
| User actions | 8 | `user.invite`, `user.lock`, `user.unlock`, `user.deactivate`, `user.password_reset`, `user.session_expired` |
| Tenant updates | 6 | `tenant.create`, `tenant.update`, `tenant.activate`, `tenant.deactivate`, `tenant.suspend` |
| Entitlement changes | 6 | `entitlement.enable`, `entitlement.disable` |
| Role changes | 3 | `role.assign`, `role.revoke` |
| System events | 3 | `system.migration`, `system.health_check` |

Array is sorted newest-first at module load time via `.sort()`.

**`audit.list(params)`** added to `controlCenterServerApi`:

```ts
// TODO: replace with GET /identity/api/admin/audit
audit: {
  list(params?: {
    page?:       number;   // default 1
    pageSize?:   number;   // default 15
    search?:     string;   // matches action, entityId, actorName, entityType
    entityType?: string;   // exact match (case-insensitive)
    actor?:      string;   // partial match on actorName
  }): Promise<{ items: AuditLogEntry[]; totalCount: number }>
}
```

Filtering is additive — all active filters must match.

### 3. Component — `apps/control-center/src/components/audit-logs/audit-log-table.tsx` *(new)*

Pure Server Component. Five-column table:

| Column | Content |
|--------|---------|
| **When** | Date (short month) + time (HH:MM UTC) |
| **Actor** | `ActorTypeBadge` (Admin/System) + actor name |
| **Action** | Colour-coded `ActionBadge` (monospace font) |
| **Entity** | `EntityTypePill` (type) + `entityId` (monospace) |
| **Details** | First 3 key/value pairs from `metadata` |

Colour system:

- `ActorTypeBadge`: indigo = Admin, gray = System
- `ActionBadge`: green = create/enable/unlock/activate, red = lock/disable/suspend, orange = deactivate/revoke, blue = invite/update, yellow = password_reset, gray = system/session
- `EntityTypePill`: blue = User, purple = Tenant, emerald = Entitlement, indigo = Role, gray = System

Empty state renders a centered "No entries match your filters" placeholder.

### 4. Page — `apps/control-center/src/app/audit-logs/page.tsx` *(new)*

Server Component — reads `searchParams` from the URL, calls `audit.list()`, renders results.

**Filter bar** — plain `<form method="GET">` (no JavaScript required):
- Full-text search field (matches action, entity ID, actor, entity type)
- Entity Type `<select>`: All types / User / Tenant / Entitlement / Role / System
- Actor text field (partial match)
- Filter button + Clear link (visible only when filters are active)

**Active filter chips** — shown below the filter bar when any filter is active.

**Result counter** — "Showing 1–15 of 28 events" / "Page 2 of 2".

**Pagination** — smart page range (ellipsis for large page counts), built as `<a>` links that preserve active filter params in the query string. Disabled Prev/Next rendered as non-interactive `<span>`.

---

## Architecture

```
/audit-logs?search=…&entityType=…&actor=…&page=…   (GET)

AuditLogsPage (Server Component)
  └─ controlCenterServerApi.audit.list(params)       ← mock → real API
       └─ MOCK_AUDIT_LOGS (28 entries, sorted desc)
  └─ AuditLogTable (Server Component)
       └─ AuditRow × N
            ├─ ActorTypeBadge
            ├─ ActionBadge
            ├─ EntityTypePill
            └─ MetadataSummary
  └─ Pagination (Server Component, <a> links)
```

---

## Sidebar wiring

`/audit-logs` was already present in `apps/control-center/src/lib/nav.ts`
under the **Operations** group — no nav changes needed.

---

## Live-endpoint wiring (one-line change)

When `GET /identity/api/admin/audit` is ready, replace the body of `audit.list`
in `control-center-api.ts`:

```ts
// Replace mock body with:
const qs = toQs({ page, pageSize, search, entityType, actor });
return serverApi.get(`/identity/api/admin/audit${qs}`);
```

No changes needed in the component, table, or page.

---

## TypeScript verification

```
cd apps/control-center && tsc --noEmit
# → no output (0 errors)
```
