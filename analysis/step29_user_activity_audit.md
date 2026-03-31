# Step 29 — Missing Audit Events + User Access Logs & Activity Reports

**Status:** COMPLETE  
**Date:** 2026-03-31

---

## Objective

1. Identify and wire up all uninstrumented admin mutations to the canonical audit pipeline.
2. Upgrade impersonation from console-log-only to canonical audit events.
3. Add a dedicated User Activity page to the Control Center (`/synqaudit/user-activity`).
4. Enhance the Tenant Portal `/activity` page with actor filtering and event category tabs.

---

## Audit Event Taxonomy (Full Inventory)

| Event Type | Source | Category | Severity | Status |
|---|---|---|---|---|
| `identity.user.login.succeeded` | AuthService | Security | Info | ✅ Already emitting |
| `identity.user.login.failed` | AuthService | Security | Warn | ✅ Already emitting |
| `identity.user.logout` | AuthEndpoints | Security | Info | ✅ Already emitting |
| `identity.user.created` | UserEndpoints | Administrative | Info | ✅ Already emitting |
| `identity.user.deactivated` | AdminEndpoints | Administrative | Warn | ✅ Already emitting |
| `identity.role.assigned` | AdminEndpoints | Administrative | Info | ✅ Already emitting |
| `identity.role.removed` | AdminEndpoints | Administrative | Warn | ✅ Already emitting |
| `careconnect.referral.created` | ReferralService | Business | Info | ✅ Already emitting |
| `careconnect.referral.updated` | ReferralService | Business | Info | ✅ Already emitting |
| `careconnect.appointment.scheduled` | AppointmentService | Business | Info | ✅ Already emitting |
| `careconnect.appointment.cancelled` | AppointmentService | Business | Warn | ✅ Already emitting |
| `platform.admin.tenant.entitlement.updated` | AdminEndpoints | Administrative | Warn | ✅ **NEW** |
| `platform.admin.org.relationship.created` | AdminEndpoints | Administrative | Info | ✅ **NEW** |
| `platform.admin.org.relationship.deactivated` | AdminEndpoints | Administrative | Warn | ✅ **NEW** |
| `platform.admin.impersonation.started` | CC impersonation server action | Security | Warn | ✅ **NEW** |
| `platform.admin.impersonation.stopped` | CC impersonation server action | Security | Info | ✅ **NEW** |

**Total: 16 canonical events across 4 source systems.**

---

## Changes Made

### 1. Backend — AdminEndpoints.cs (Identity.Api)

Three uninstrumented mutations wired to emit canonical events:

- **`UpdateEntitlement`** (`POST /api/admin/tenants/{id}/entitlement`)
  - Added `IAuditEventClient auditClient` parameter (DI-injected by Minimal API)
  - Emits `platform.admin.tenant.entitlement.updated` after `SaveChangesAsync`
  - Fire-and-observe pattern: `_ = auditClient.IngestAsync(...)` never gates the response

- **`CreateOrganizationRelationship`** (`POST /api/admin/organization-relationships`)
  - Added `IAuditEventClient auditClient` parameter
  - Emits `platform.admin.org.relationship.created` with `Before`/`After` JSON diff
  - Includes `relType.DisplayName` in description for human-readable context

- **`DeactivateOrganizationRelationship`** (`DELETE /api/admin/organization-relationships/{id}`)
  - Added `IAuditEventClient auditClient` parameter
  - Emits `platform.admin.org.relationship.deactivated` with `Before` JSON (isActive: true)

All three follow the established pattern: `VisibilityScope.Platform`, `ScopeType.Tenant`, fire-and-observe.

### 2. Control Center — Impersonation Canonical Audit

**File:** `apps/control-center/src/app/actions/impersonation.ts`

Upgraded from local log-only to dual-emit:
1. **Local log** (existing): `logImpersonationStart/Stop` → NDJSON stream
2. **Canonical audit** (NEW): `controlCenterServerApi.auditIngest.emit()` → Platform Audit Event Service

Pattern:
```typescript
void controlCenterServerApi.auditIngest.emit({...}).catch(() => {
  /* audit pipeline unavailable — operation still succeeds */
});
```
The `.catch()` swallows audit failures so impersonation is never gated by the audit pipeline.

Removed all `TODO: persist to AuditLog table` comments — those TODOs are now fulfilled.

### 3. Control Center API — `auditIngest.emit()`

**File:** `apps/control-center/src/lib/control-center-api.ts`

New `auditIngest` section added to `controlCenterServerApi`:
```typescript
auditIngest: {
  emit: async (payload: AuditIngestPayload): Promise<void> => {
    await apiClient.post<unknown>('/audit-service/audit/ingest', payload);
  },
},
```

**File:** `apps/control-center/src/types/control-center.ts`

New `AuditIngestPayload` interface added with all fields required by the ingest endpoint.

### 4. Control Center — User Activity Page

**File:** `apps/control-center/src/app/synqaudit/user-activity/page.tsx`

New `requirePlatformAdmin()`-guarded server component at `/synqaudit/user-activity`:
- **Category tabs**: All Events | Access (Security) | Admin Actions (Administrative) | Clinical (Business)
- **Actor filter**: filter by actor UUID to drill into a specific user's trail — clicking any actor ID in the table links back with the filter pre-set
- **Date range filter**: dateFrom / dateTo
- **Tenant context**: respects the active tenant context (narrows scope when a tenant is selected)
- **Trace link**: every row has a "Trace" link to `/synqaudit/investigation?search={auditId}`
- **Pagination**: consistent with other SynqAudit pages (max 20/page)
- **Empty state**: icon + message for filtered and unfiltered cases

**File:** `apps/control-center/src/lib/nav.ts`

Added "User Activity" entry (`ri-user-heart-line`, badge: LIVE) as the second item in the SYNQAUDIT section.

### 5. Tenant Portal — Activity Page Enhancements

**File:** `apps/web/src/app/(platform)/activity/page.tsx`

Added to the existing functional activity page:
- **Category tabs**: All | Access | Admin | Clinical — pre-filter the event stream by `category`
- **Actor filter field**: adds `actorId` to the query string, scoping the stream to a specific user
- **"My Activity" toggle**: header button that sets `actorId=me` (resolved to `session.userId`) — clicking it again returns to All Users view
- **Clickable actor IDs**: each actor ID in the table is now a link that re-filters by that actor
- **Unified `hrefFor()` helper**: all navigation (tabs, pagination, filters) preserves all other active filters

---

## Architecture Notes

- **Fire-and-observe is universal**: every emit uses `_ = auditClient.IngestAsync(...)` (C#) or `.catch(() => {})` (TypeScript) — the audit pipeline never gates user-facing operations.
- **No new migrations**: all changes are application-layer only. The `AuditEventRecord` schema is unchanged.
- **`EventCategory.Business`** is used for CareConnect events (not `Clinical` — that enum value does not exist in the shared lib).
- **`VisibilityScope.Platform`** is used for new admin events (entitlement, org-relationship, impersonation) since these are platform-admin operations, not tenant-scoped.
- The CC impersonation audit goes via the API gateway (`/audit-service/audit/ingest`) using the existing `apiClient.post()` — same pattern used by `auditExports.create()`.
