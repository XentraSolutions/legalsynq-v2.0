# LegalSynq Control Center Admin Refresh

**Date:** 2026-03-30
**Scope:** Functional completion pass — nav reorganisation, status badges, backend alignment.
**Constraint:** No visual redesign; existing layout, shell, branding, and theme preserved.

---

## 1. Current UI Audit

| Route | Page Title | Status | Backend Endpoint |
|-------|-----------|--------|-----------------|
| `/` | Dashboard | **LIVE** | multiple (tenants, users, monitoring summary) |
| `/platform-readiness` | Platform Readiness | **LIVE** | `GET /identity/api/admin/platform-readiness` |
| `/legacy-coverage` | Legacy Migration Coverage | **LIVE** | `GET /identity/api/admin/legacy-coverage` |
| `/tenants` | Tenants | **LIVE** | `GET /identity/api/admin/tenants` |
| `/tenants/[id]` | Tenant Detail | **LIVE** | `GET /identity/api/admin/tenants/{id}` |
| `/tenants/[id]/users` | Tenant Users | **LIVE** | `GET /identity/api/admin/users?tenantId={id}` |
| `/tenant-users` | Users | **LIVE** | `GET /identity/api/admin/users` |
| `/tenant-users/[id]` | User Detail | **LIVE** | `GET /identity/api/admin/users/{id}` |
| `/roles` | Roles | **LIVE** | `GET /identity/api/admin/roles` |
| `/roles/[id]` | Role Detail | **LIVE** | `GET /identity/api/admin/roles/{id}` |
| `/org-types` | Organization Types | **LIVE** | `GET /identity/api/admin/organization-types` |
| `/relationship-types` | Relationship Types | **LIVE** | `GET /identity/api/admin/relationship-types` |
| `/org-relationships` | Organization Relationships | **LIVE** | `GET /identity/api/admin/organization-relationships` |
| `/product-rules` | Product Access Rules | **LIVE** | `GET /identity/api/admin/product-org-type-rules` + `GET /identity/api/admin/product-rel-type-rules` |
| `/careconnect-integrity` | CareConnect Integrity | **LIVE** | `GET /careconnect/api/admin/integrity` |
| `/audit-logs` | Audit Logs | **IN PROGRESS** | `GET /identity/api/admin/audit` (mock stub — real endpoint exists, not yet wired) |
| `/support` | Support Tools | **IN PROGRESS** | `GET /identity/api/admin/support` (mock stub) |
| `/support/[id]` | Support Case Detail | **IN PROGRESS** | `GET /identity/api/admin/support/{id}` (mock stub) |
| `/monitoring` | System Health | **IN PROGRESS** | `GET /platform/monitoring/summary` (mock stub) |
| `/settings` | Platform Settings | **IN PROGRESS** | `GET /identity/api/admin/settings` (mock stub) |
| `/scoped-roles` | Scoped Role Assignments | **MOCKUP** | No global list endpoint — per-user only via `/users/{id}/scoped-roles` |
| `/domains` | Tenant Domains | **MOCKUP** | No backend endpoint |
| `/products` | Products | **MOCKUP** | No standalone product catalog admin endpoint |

---

## 2. Navigation Changes

**File changed:** `apps/control-center/src/lib/nav.ts`

### Changes made

| Change | Before | After |
|--------|--------|-------|
| Moved **Tenants** out of IDENTITY section | IDENTITY: [Tenants, Users, Roles, Scoped Roles, Org Types] | IDENTITY: [Users, Roles, Scoped Roles, Org Types] |
| Added **Tenants** to TENANTS section | TENANTS: [Tenant Domains (MOCKUP)] | TENANTS: [Tenants, Tenant Domains (MOCKUP)] |
| Added `IN PROGRESS` badge to **Support Tools** | no badge | `badge: 'IN PROGRESS'` |
| Added `IN PROGRESS` badge to **Audit Logs** | no badge | `badge: 'IN PROGRESS'` |
| **Monitoring** already had `IN PROGRESS` badge | `badge: 'IN PROGRESS'` | unchanged |
| Added `IN PROGRESS` badge to **Platform Settings** | no badge | `badge: 'IN PROGRESS'` |

### Final nav structure

```
OVERVIEW
  Dashboard                     (/)

PLATFORM
  Platform Readiness             (/platform-readiness)
  Legacy Coverage                (/legacy-coverage)

IDENTITY
  Users                          (/tenant-users)
  Roles                          (/roles)
  Scoped Roles        [MOCKUP]   (/scoped-roles)
  Org Types                      (/org-types)

RELATIONSHIPS
  Relationship Types             (/relationship-types)
  Organization Relationships     (/org-relationships)

PRODUCT RULES
  Access Rules                   (/product-rules)

CARECONNECT
  Integrity                      (/careconnect-integrity)

TENANTS
  Tenants                        (/tenants)
  Tenant Domains      [MOCKUP]   (/domains)

OPERATIONS
  Support Tools     [IN PROGRESS] (/support)
  Audit Logs        [IN PROGRESS] (/audit-logs)
  Monitoring        [IN PROGRESS] (/monitoring)

CATALOG
  Products          [MOCKUP]     (/products)

SYSTEM
  Platform Settings [IN PROGRESS] (/settings)
```

---

## 3. Live Pages Wired

All LIVE pages were already wired to their backend endpoints in previous steps. No new API wiring was required in this pass. The following pages now show a **LIVE** badge that was previously missing:

| Page | File | Badge Added |
|------|------|-------------|
| Legacy Migration Coverage | `apps/control-center/src/app/legacy-coverage/page.tsx` | ✓ |
| Organization Types | `apps/control-center/src/app/org-types/page.tsx` | ✓ |
| Relationship Types | `apps/control-center/src/app/relationship-types/page.tsx` | ✓ |
| Organization Relationships | `apps/control-center/src/app/org-relationships/page.tsx` | ✓ |
| Product Access Rules | `apps/control-center/src/app/product-rules/page.tsx` | ✓ |

Pages already carrying a LIVE badge (no change needed):

- `/platform-readiness` — had `LIVE` badge
- `/careconnect-integrity` — had `LIVE` badge

### Backend endpoints by LIVE page

| Page | Endpoint(s) |
|------|-------------|
| Platform Readiness | `GET /identity/api/admin/platform-readiness` |
| Legacy Coverage | `GET /identity/api/admin/legacy-coverage` |
| Tenants list | `GET /identity/api/admin/tenants` |
| Tenant detail | `GET /identity/api/admin/tenants/{id}` |
| Users list | `GET /identity/api/admin/users` |
| User detail | `GET /identity/api/admin/users/{id}` |
| Roles list | `GET /identity/api/admin/roles` |
| Role detail | `GET /identity/api/admin/roles/{id}` |
| Org Types | `GET /identity/api/admin/organization-types` |
| Relationship Types | `GET /identity/api/admin/relationship-types` |
| Org Relationships | `GET /identity/api/admin/organization-relationships` |
| Product Access Rules | `GET /identity/api/admin/product-org-type-rules` + `GET /identity/api/admin/product-rel-type-rules` |
| CareConnect Integrity | `GET /careconnect/api/admin/integrity` |

---

## 4. Mockup Pages Added

No new mockup pages were required in this pass — all needed mockups already existed from prior steps:

| Page | Status | Reason backend is not ready |
|------|--------|------------------------------|
| `/scoped-roles` | MOCKUP | No global list endpoint; scoped roles accessible per-user via `/tenant-users/[id]` only |
| `/domains` | MOCKUP | `GET /identity/api/admin/tenant-domains` not implemented |
| `/products` | MOCKUP | No standalone product catalog admin endpoint; per-tenant entitlements managed from tenant detail page |

Each mockup page carries a visible **MOCKUP** badge in the page header and displays illustrative placeholder data with all action buttons disabled.

---

## 5. Types / API / Mapper Changes

No type, API client, or mapper changes were required in this pass.

### Verification performed

| File | Finding |
|------|---------|
| `apps/control-center/src/types/control-center.ts` | All types current — `RoleAssignmentsCoverage` uses Phase G shape (no retired dual-write fields); `ScopedRoleAssignment` correctly reflects Phase G model |
| `apps/control-center/src/lib/api-mappers.ts` | `mapLegacyCoverageReport` correctly reads Phase G `userRolesRetired`/`usersWithScopedRoles`/`totalActiveScopedAssignments`; legacy fields (`usersWithGapCount`, `dualWriteCoveragePct`) not read from backend or emitted to frontend |
| `apps/control-center/src/lib/control-center-api.ts` | All API methods present and use correct gateway paths; cache TTLs appropriate |
| `apps/control-center/src/lib/nav.ts` | Updated — see Navigation Changes section |

---

## 6. Route and Component Changes

### Files changed

| File | Change |
|------|--------|
| `apps/control-center/src/lib/nav.ts` | Moved Tenants from IDENTITY → TENANTS; added IN PROGRESS badges to Support, Audit Logs, Monitoring, Settings |
| `apps/control-center/src/app/legacy-coverage/page.tsx` | Added LIVE badge to page header |
| `apps/control-center/src/app/org-types/page.tsx` | Added LIVE badge to page header |
| `apps/control-center/src/app/relationship-types/page.tsx` | Added LIVE badge to page header |
| `apps/control-center/src/app/org-relationships/page.tsx` | Added LIVE badge to page header |
| `apps/control-center/src/app/product-rules/page.tsx` | Added LIVE badge to page header |
| `apps/control-center/src/app/audit-logs/page.tsx` | Added IN PROGRESS badge to page header |
| `apps/control-center/src/app/support/page.tsx` | Added IN PROGRESS badge to page header |
| `apps/control-center/src/app/monitoring/page.tsx` | Added IN PROGRESS badge to page header |
| `apps/control-center/src/app/settings/page.tsx` | Added IN PROGRESS badge to page header |

### Files confirmed correct (no changes needed)

- All LIVE page components (tables, cards, panels) — verified against current backend response shapes
- `apps/control-center/src/components/platform/legacy-coverage-card.tsx` — uses Phase G fields correctly
- `apps/control-center/src/components/platform/platform-readiness-card.tsx` — aligned with Phase I shape
- All mockup pages (scoped-roles, domains, products) — already have MOCKUP badges and illustrative data

---

## 7. Build and Verification Status

| Check | Result |
|-------|--------|
| `tsc --noEmit` (control-center) | ✅ 0 errors |
| Nav structure verified manually | ✅ matches requirements |
| All LIVE pages have LIVE badge | ✅ |
| All IN PROGRESS pages have IN PROGRESS badge | ✅ |
| All MOCKUP pages have MOCKUP badge | ✅ |
| No visual redesign | ✅ — only badges added, no layout changes |
| No fake CRUD on mock pages | ✅ — all action buttons disabled |

---

## 8. Remaining UI Gaps

The following capabilities have backend support but are read-only in the UI. Full CRUD can be added as a follow-on once risk tolerance is established:

| Feature | Current state | Next step |
|---------|--------------|-----------|
| Create org relationship | Button present, disabled | Wire `POST /identity/api/admin/organization-relationships` |
| Add org type / relationship type | Button present, disabled | Wire `POST` endpoints when added to AdminEndpoints.cs |
| Audit logs live endpoint | Mock stub in API | Update `controlCenterServerApi.audit.list()` to remove stub once `GET /identity/api/admin/audit` is confirmed live |
| Support cases live endpoint | Mock stub in API | Update `controlCenterServerApi.support.*` once support endpoints are confirmed live |
| Platform monitoring live endpoint | Mock stub in API | Update `controlCenterServerApi.monitoring.getSummary()` once `GET /platform/monitoring/summary` is live |
| Platform settings live endpoint | Mock stub in API | Update `controlCenterServerApi.settings.*` once `GET /identity/api/admin/settings` is live |
| Scoped roles global list | MOCKUP | Add `GET /identity/api/admin/scoped-role-assignments` endpoint and wire `/scoped-roles` page |
| Tenant domain management | MOCKUP | Implement `GET/POST/DELETE /identity/api/admin/tenant-domains` and wire `/domains` page |
| Product catalog admin | MOCKUP | Implement product catalog admin endpoints and wire `/products` page |
