# LS-LIENS-UI-012: Role-Based UX & Permissions Hardening

## Feature ID
LS-LIENS-UI-012

## Objective
Harden SynqLiens into a role-aware enterprise product by aligning visible navigation, actions, and workflow affordances with real user role context, while preserving provider mode behavior.

## Backend Permission Model (Reference)

### Product Roles
| Role | Constant | Value |
|------|----------|-------|
| Seller | `SynqLienSeller` | `SYNQ_LIENS:SYNQLIEN_SELLER` |
| Buyer | `SynqLienBuyer` | `SYNQ_LIENS:SYNQLIEN_BUYER` |
| Holder | `SynqLienHolder` | `SYNQ_LIENS:SYNQLIEN_HOLDER` |

### Backend Permission Mapping
| Role | Permissions |
|------|------------|
| Seller | lien:create, lien:offer, lien:read:own |
| Buyer | lien:browse, lien:purchase, lien:read:held |
| Holder | lien:read:held, lien:service, lien:settle |

### Mode Interaction
- Sell mode (default): Full marketplace, offers, BOS surfaces visible per role
- Manage mode: No marketplace, offers, or BOS — mode always overrides role

## Implementation Summary

### 1. Centralized Role Access Service
**New files:**
- `apps/web/src/lib/role-access/role-access.types.ts` — `LienAction`, `LienModule`, `RoleAccessInfo` types
- `apps/web/src/lib/role-access/role-access.service.ts` — `buildRoleAccess()` pure function
- `apps/web/src/lib/role-access/index.ts` — barrel export
- `apps/web/src/hooks/use-role-access.ts` — `useRoleAccess()` React hook

**Design:**
- `buildRoleAccess(productRoles, isPlatformAdmin, isTenantAdmin, isSellMode)` returns `RoleAccessInfo`
- `RoleAccessInfo.can(action)` — granular action check (e.g., `case:create`, `lien:offer`, `bos:manage`)
- `RoleAccessInfo.canViewModule(module)` — module-level visibility check
- Role flags: `isSeller`, `isBuyer`, `isHolder`, `isAdmin`, `isTenantAdmin`, `hasAnyLienRole`
- Admin/TenantAdmin get full access (mode-gated for sell-only actions)
- Mode always overrides: sell-only actions return false in manage mode

### 2. Legacy canPerformAction Replacement
**Pages updated (15 files):**
- `dashboard/page.tsx` — `canPerformAction(role, 'create')` → `ra.can('case:create')`
- `cases/page.tsx` — create + edit → `case:create`, `case:edit`
- `cases/[id]/page.tsx` — edit → `case:edit`
- `liens/page.tsx` — create → `lien:create`
- `liens/[id]/page.tsx` — edit → `lien:edit`
- `servicing/page.tsx` — create + edit → `servicing:assign`, `servicing:edit`
- `servicing/[id]/page.tsx` — edit → `servicing:edit`
- `task-manager/page.tsx` — create + edit → `servicing:create`, `servicing:edit`
- `bill-of-sales/page.tsx` — create + edit → `bos:manage`
- `bill-of-sales/[id]/page.tsx` — edit → `bos:manage`
- `contacts/page.tsx` — create → `contact:create`
- `contacts/[id]/page.tsx` — edit → `contact:edit`
- `document-handling/page.tsx` — create + edit → `document:upload`, `document:edit`
- `document-handling/[id]/page.tsx` — edit → `document:edit`
- `user-management/page.tsx` — admin check → `ra.isAdmin || ra.isTenantAdmin`
- `user-management/[id]/page.tsx` — admin check → `ra.isAdmin || ra.isTenantAdmin`

**Pattern:** Every page now imports `useRoleAccess()` instead of `useLienStore currentRole` + `canPerformAction()`.

### 3. Navigation Hardening
- `filterNavByAccess()` added to `nav.ts` — combines role filtering + mode filtering in one call
- Sidebar updated to use `filterNavByAccess()` instead of separate `filterNavByRoles` + manual sellModeOnly filtering
- Existing `requiredRoles` on nav items preserved (Marketplace → Buyer, My Liens → Seller, Portfolio → Buyer|Holder)
- `sellModeOnly` flags preserved (Bill of Sales item, Marketplace section)

### 4. Action-Level Permission Matrix

| Action | Seller | Buyer | Holder | Admin | Mode Gate |
|--------|--------|-------|--------|-------|-----------|
| case:create | ✓ | | ✓ | ✓ | |
| case:edit | ✓ | | ✓ | ✓ | |
| case:view | ✓ | ✓ | ✓ | ✓ | |
| lien:create | ✓ | | | ✓ | |
| lien:edit | ✓ | | | ✓ | |
| lien:view | ✓ | ✓ | ✓ | ✓ | |
| lien:offer | ✓ | | | ✓ | sell only |
| lien:purchase | | ✓ | | ✓ | sell only |
| offer:create | ✓ | | | ✓ | sell only |
| offer:accept | | ✓ | | ✓ | sell only |
| bos:view | ✓ | ✓ | ✓ | ✓ | sell only |
| bos:manage | ✓ | ✓ | | ✓ | sell only |
| servicing:create | ✓ | | ✓ | ✓ | |
| servicing:edit | ✓ | | ✓ | ✓ | |
| servicing:assign | ✓ | | ✓ | ✓ | |
| servicing:view | ✓ | ✓ | ✓ | ✓ | |
| contact:create | ✓ | | ✓ | ✓ | |
| contact:edit | ✓ | | ✓ | ✓ | |
| document:upload | ✓ | | ✓ | ✓ | |
| document:edit | ✓ | | ✓ | ✓ | |
| user:manage | | | | ✓* | |
| financial:view | ✓ | ✓ | ✓ | ✓ | |

*User management requires Admin or TenantAdmin system role.

## Build Verification
- TypeScript: `npx tsc --noEmit` — PASS (0 errors)
- .NET: `dotnet build LegalSynq.sln` — PASS (0 warnings, 0 errors)
- No remaining `canPerformAction` imports in any lien page
- No remaining `useLienStore currentRole` references in any lien page
- Legacy `AppRole`/`canPerformAction` retained only as internal store mock-data guards

## Post-Review Fixes
1. **Dashboard quick actions role-gated** — Each tile now checks `ra.can()` (e.g., `case:create`, `lien:create`, `bos:view`) + mode filter. Batch Import gated to Seller/Admin.
2. **Batch Entry nav item** — Added `requiredRoles: [SynqLienSeller]` to the nav definition.
3. **BOS detail deep-link mode guard** — `bill-of-sales/[id]/page.tsx` now imports `useProviderMode` and redirects to dashboard in manage mode, matching the list page behavior.

## Status
COMPLETE
