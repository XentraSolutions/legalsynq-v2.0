# UIX-002-C: Product Role Eligibility & Assignment Guardrails

## Status: COMPLETE

## Summary

Implemented end-to-end product role eligibility and assignment guardrails across the Identity API backend and Control Center frontend. Product roles (e.g., CARECONNECT_REFERRER, SYNQLIEN_BUYER) are now automatically provisioned into the Roles table at startup, and the assignment workflow enforces org-type and product-enablement rules.

## Architecture

### Data Model (No Migration Required)

- **ProductRole** table: Defines product-specific roles (Code, Name, ProductId)
- **ProductOrganizationTypeRule**: Links ProductRoles to allowed OrganizationTypes
- **Role** table: System roles (PlatformAdmin, TenantAdmin, StandardUser) + seeded product roles
- **ScopedRoleAssignment**: FK → Role.Id — used for all role assignments
- **TenantProducts**: Tracks which products are enabled per tenant

### Seeder Flow (Program.cs)

At startup, for each active ProductRole with no matching Role entry:
1. Creates a new Role (IsSystemRole=false, Name=ProductRole.Code)
2. Sets description to `[Product] {Name} — {Description}`
3. Uses the earliest active tenant as the platform tenant
4. Idempotent — skips existing entries

**Seeder output (first run):**
- CARECONNECT_REFERRER, CARECONNECT_RECEIVER (SynqCareConnect)
- SYNQLIEN_SELLER, SYNQLIEN_BUYER, SYNQLIEN_HOLDER (SynqLiens)
- SYNQFUND_REFERRER, SYNQFUND_FUNDER, SYNQFUND_APPLICANT_PORTAL (SynqFund)

## Backend Changes

### 1. Startup Seeder (Program.cs)
- Syncs ProductRoles → Roles table at boot
- 8 product roles seeded on first run
- Idempotent on subsequent restarts

### 2. Hardened AssignRole (AdminEndpoints.cs)
- For non-system roles, checks if role maps to a ProductRole
- **Tenant product enablement**: Validates TenantProducts table
- **Org type eligibility**: Validates user's primary org type against ProductOrganizationTypeRules
- **Error codes**: PRODUCT_NOT_ENABLED_FOR_TENANT, INVALID_ORG_TYPE_FOR_ROLE, NO_ORGANIZATION_MEMBERSHIP

### 3. New Endpoint: GET /api/admin/users/{id}/assignable-roles
Returns all roles with eligibility metadata:
- `assignable` (boolean) — whether the role can be assigned
- `disabledReason` (string) — human-readable explanation
- `isProductRole`, `productCode`, `productName` — product association
- `allowedOrgTypes` — org types permitted for the role
- `isAssigned` — whether already assigned to the user
- `userOrgType` — the user's primary org type
- `tenantEnabledProducts` — count of enabled products

### 4. Extended ListRoles Endpoint
- Added `isProductRole`, `productCode`, `productName`, `allowedOrgTypes` to role list items
- System roles sorted first, then product roles

## Frontend Changes

### 1. Types (control-center.ts)
- Extended `RoleSummary` with optional product fields
- New `AssignableRole` interface
- New `AssignableRolesResponse` interface

### 2. API Mappers (api-mappers.ts)
- Extended `mapRoleSummary` with product metadata mapping
- New `mapAssignableRole` function

### 3. API Client (control-center-api.ts)
- New `users.getAssignableRoles(id)` method
- 10s cache, tagged cc:users

### 4. BFF Route
- `GET /api/identity/admin/users/[id]/assignable-roles/route.ts`

### 5. RoleAssignmentPanel (role-assignment-panel.tsx)
- Grouped dropdown: System Roles / Product Roles (Eligible)
- Product role info banner when selecting a product role
- Ineligible product roles section with reasons
- Color-coded badges: purple for product roles, indigo for system roles
- Product name shown alongside assigned product roles
- User org type badge in header
- Client-side refresh of assignable roles after assign/revoke

### 6. User Detail Page (tenant-users/[id]/page.tsx)
- Fetches assignable-roles in parallel with other data
- Passes assignable roles + userOrgType to RoleAssignmentPanel

### 7. Roles List Table (role-list-table.tsx)
- Split into sections: System Roles / Product Roles / Other Roles
- Product roles show product name badge and allowed org types
- PRODUCT badge on role names

### 8. Role Detail Card (role-detail-card.tsx)
- "Role Type" row: System-defined or Product Role (with product badge)
- Allowed Org Types row for product roles

## Validation Checklist

| Item | Status |
|------|--------|
| Startup seeder creates Role entries for ProductRoles | PASS |
| Seeder is idempotent | PASS |
| AssignRole validates product enablement | PASS |
| AssignRole validates org type eligibility | PASS |
| AssignRole returns structured error codes | PASS |
| Assignable-roles endpoint returns eligibility metadata | PASS |
| ListRoles includes product metadata | PASS |
| Frontend shows eligible/ineligible roles | PASS |
| Frontend shows product role reasons | PASS |
| Roles list grouped by type | PASS |
| Role detail shows product association | PASS |
| No database migration required | PASS |
| Identity API compiles without errors | PASS |
| Control Center compiles without errors | PASS |

## Files Modified

### Backend
- `apps/services/identity/Identity.Api/Program.cs` — startup seeder
- `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` — hardened AssignRole, new assignable-roles endpoint, extended ListRoles

### Frontend
- `apps/control-center/src/types/control-center.ts` — AssignableRole types
- `apps/control-center/src/lib/api-mappers.ts` — mapAssignableRole
- `apps/control-center/src/lib/control-center-api.ts` — getAssignableRoles
- `apps/control-center/src/app/api/identity/admin/users/[id]/assignable-roles/route.ts` — BFF route (new)
- `apps/control-center/src/components/users/role-assignment-panel.tsx` — eligibility guardrails
- `apps/control-center/src/app/tenant-users/[id]/page.tsx` — fetch assignable roles
- `apps/control-center/src/components/roles/role-list-table.tsx` — product role sections
- `apps/control-center/src/components/roles/role-detail-card.tsx` — product metadata
- `apps/control-center/src/app/roles/page.tsx` — updated info banner
