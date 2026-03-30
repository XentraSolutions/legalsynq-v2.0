# Step 24 — UI Activation Notes

**Date:** 2026-03-30

## Control Center — Audit Logs Page

**Route:** `/audit-logs`  
**File:** `apps/control-center/src/app/audit-logs/page.tsx`  
**Access:** PlatformAdmin only (`requirePlatformAdmin()`)  
**Status:** ACTIVE (with AUDIT_READ_MODE=legacy default)

### Mode Behavior Summary

| AUDIT_READ_MODE | API Called | Table Component | Filter Set |
|---|---|---|---|
| `legacy` (default) | `GET /identity/api/admin/audit` | `<AuditLogTable>` | search, entityType, actor |
| `canonical` | `GET /audit-service/audit/events` | `<CanonicalAuditTable>` | search, eventType, category, severity, correlationId, dateFrom, dateTo |
| `hybrid` | canonical first, legacy fallback | whichever succeeded | canonical filters, legacy on fallback |

### To Enable Canonical Mode

```bash
# In Control Center deployment environment
AUDIT_READ_MODE=canonical
# or for safe validation:
AUDIT_READ_MODE=hybrid
```

### Active Gaps

| Gap | File | Effort |
|---|---|---|
| Export button (→ POST /audit/exports) | `audit-logs/page.tsx` | 1 day |
| Event detail drawer on row click | New component | 2 days |
| Organization-level filter | `audit-logs/page.tsx` | 2h |
| Source system multi-select filter | `audit-logs/page.tsx` | 2h |
| Integrity checkpoint status | New component | 3 days |
| Actor ID filter (separate from actorLabel) | `audit-logs/page.tsx` | 1h |

## Tenant Portal — Activity Page

**Route:** `/activity`  
**File:** `apps/web/src/app/(platform)/activity/page.tsx`  
**Access:** Authenticated org member (`requireOrg()`)  
**Status:** PHASE 1 PLACEHOLDER (BlankPage)

### Phase 2 Implementation Checklist

- [ ] Add `activityApi.list()` to `apps/web/src/lib/web-api.ts` (or equivalent)
- [ ] Call `GET /audit-service/audit/events?tenantId={session.tenantId}&...`
- [ ] Always inject `tenantId` from server session — NEVER from URL params
- [ ] Build `TenantActivityTable` component (use `CanonicalAuditTable` as reference, remove: ipAddress, source, integrityHash)
- [ ] Add filter bar: category, event type, date range, actor name
- [ ] Pagination
- [ ] Verify `QueryAuth:Mode` is non-None in audit service before enabling

### Tenant-Safe Columns

Show these columns to tenant users:

| Column | Notes |
|---|---|
| Time (UTC) | `occurredAtUtc` |
| Event Type | `eventType` (human-readable label recommended) |
| Category | `category` (badge) |
| Actor | `actorLabel` (not actorId) |
| Target | `targetType` + `targetId` |
| Outcome | `outcome` (badge) |

Hide these columns from tenant users:

| Column | Reason |
|---|---|
| Source | Platform-internal service name |
| Severity | Platform-internal severity assessment |
| IP Address | Privacy / HIPAA — show to admins only, not all users |
| Correlation ID | Platform-internal debugging field |
| Integrity Hash | Platform-internal tamper evidence |

## Canonical Audit Table Component

**File:** `apps/control-center/src/components/audit-logs/canonical-audit-table.tsx`

### Columns Rendered

| Column | Data Source | Formatting |
|---|---|---|
| Time (UTC) | `occurredAtUtc` | `YYYY-MM-DD HH:mm:ss` (monospace) |
| Source | `source` | Small badge (gray) |
| Event Type | `eventType` | Monospace, small |
| Category | `category` | Color-coded pill badge |
| Severity | `severity` | Color-coded pill badge |
| Actor | `actorLabel` + `actorId` | Label on top, ID below (smaller, monospace) |
| Target | `targetType` + `targetId` | Type prefix (uppercase), ID monospace |
| Outcome | `outcome` | Icon + text badge (green/red/neutral) |
| Correlation ID | `correlationId` | Truncated to 12 chars with title attribute |

### Category Badge Colors

| Category | Color |
|---|---|
| security | Red |
| access | Orange |
| business | Green |
| administrative | Gray |
| compliance | Purple |
| dataChange | Blue |

### Severity Badge Colors

| Severity | Color |
|---|---|
| info | Blue |
| warn | Amber |
| error | Red |
| critical | Dark red, bold |
