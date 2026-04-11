# LS-COR-AUT-010 — Permission Governance + Enforcement Migration Report

## Summary

Evolved the platform from permission-enabled (read-only catalog) to fully governed PBAC with CRUD management, audit events, enforcement migration, and admin UI.

## Changes

### T001: Capability Entity Governance Evolution
- **Files**: `Identity.Domain/Capability.cs`, `CapabilityConfiguration.cs`
- Added governance columns: `Category` (max 100), `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`
- Added domain methods: `Update()`, `Deactivate()`, `Activate()`
- Added naming convention validation: `^[a-z][a-z0-9]*(?:\:[a-z][a-z0-9]*)*$`
- Static `IsValidCode()` and `Create()` enforce the convention
- Updated seed data with Category values (CareConnect→Referral/Provider/Appointment; SynqLien→Lien; SynqFund→Application/Party)

### T002–T003: Permission Catalog CRUD API + Audit Events
- **Files**: `AdminEndpoints.cs`
- `POST /api/admin/permissions` — Create with naming convention validation, duplicate-code check
- `PATCH /api/admin/permissions/{id}` — Update name, description, category
- `DELETE /api/admin/permissions/{id}` — Soft delete (deactivate)
- Existing list endpoints updated to include `category`, `productCode`, `updatedAtUtc`
- Audit events emitted inline: `permission.created` (Info), `permission.updated` (Info), `permission.deactivated` (Warning)

### T004: Enforcement Migration (Fund Service)
- **Files**: `Fund.Api/Endpoints/ApplicationEndpoints.cs`
- Migrated from `RequireProductRole` → `RequirePermission` (PBAC primary)
- Permission mapping:
  - `application:create` → POST create, PUT update, POST submit (formerly REFERRER)
  - `application:evaluate` → POST begin-review (formerly FUNDER)
  - `application:approve` → POST approve (formerly FUNDER)
  - `application:decline` → POST deny (formerly FUNDER)
- Admin bypass preserved (TenantAdmin/PlatformAdmin)

### T005: Admin UI — Permission Catalog CRUD
- **Files**: `permissions/page.tsx`, `permissions/actions.ts`, `permission-create-dialog.tsx`, `permission-row-actions.tsx`, `permission-catalog-table.tsx`
- Replaced read-only notice with full CRUD capability
- Create dialog: product selector, code validation (client-side regex), name, description, category
- Table: added Category and Actions columns
- Row actions: inline edit dialog (name, description, category), deactivate with confirmation
- Server actions with `requirePlatformAdmin()` guard
- API client: `create()`, `update()`, `deactivate()` methods added

### T006: Admin UI — Role Detail Permission Management
- Already implemented in UIX-005 (RolePermissionPanel with assign/revoke)
- No changes needed

### T007: Tests (39 new, 107 total)
- **Files**: `PermissionGovernanceTests.cs`
- Naming convention validation: 7 valid codes, 12 invalid codes, null handling
- Capability.Create: field mapping, normalization, invalid code throws, empty name throws, whitespace trimming
- Capability.Update: field changes, empty name throws
- Capability.Deactivate/Activate: state transitions, updater tracking
- HasPermission claim: matching, non-matching, empty, case-insensitivity, multiple permissions
- Cross-product blocking: Fund permission doesn't grant CareConnect/Lien access
- Admin bypass: PlatformAdmin, TenantAdmin, regular user

### T008: Build Verification
- Identity.Api: builds clean (Release)
- Fund.Api: builds clean (Release)
- Control Center: Next.js build passes
- All 107 tests pass (0 failures)

## Type & Mapper Updates
- `PermissionCatalogItem`: added `category`, `productCode`, `updatedAtUtc` fields
- `mapPermissionCatalogItem`: maps `category`, `product_code`/`productCode`, `updated_at_utc`/`updatedAtUtc`
- API client: `permissions.create()`, `permissions.update()`, `permissions.deactivate()` with cache revalidation

## Architecture Notes
- Permission format: `{PRODUCT_CODE}.{capability_code}` (e.g. `SYNQ_FUND.application:create`)
- JWT claim: `permissions` (multi-value, case-insensitive comparison)
- RequirePermissionFilter: checks `permissions` claims, admin bypass, structured logging
- Server actions pattern: `requirePlatformAdmin()` → API client → `revalidateTag`
