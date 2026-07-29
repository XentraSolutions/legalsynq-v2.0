# Plan: Mobile Login Enhancement - Remember Tenant Code

## Source BRD

- Document: `Enhancement to Login Flow - Remember Tenant Code`
- Version: `1.0`
- Date: `2026-07-15`
- App target: `apps/mobile`

## Goal

Enhance the mobile login flow so a tenant code is required only on first login or when adding a new tenant. Tenant records are stored and retrieved locally in the app; there is no separate tenant create, retrieve, or validation API. A tenant code is considered confirmed only after the existing login API succeeds with that tenant code. Users can switch between locally remembered tenants or add another tenant code from the login flow.

## Current State

- Login UI lives at `apps/mobile/src/features/authentication/screens/LoginScreen/index.tsx`.
- Login form currently always renders `tenantCode`, `email`, and `password`.
- Login form currently has hardcoded development credentials in `defaultValues`.
- `useLogin` requires `{ email, password, tenantCode }`.
- `AuthenticationService.login(email, password, tenantCode)` posts to `/identity/api/auth/login`.
- Login response includes `user.tenantId` and optional `tenants: [{ tenantId, tenantCode }]`.
- `tenantSummarySchema` currently includes only `tenantId` and `tenantCode`, but the BRD requires tenant name and optional API endpoint.
- Secure storage exists via `SecureStorageService`, but `clearAll()` currently removes all secure values on logout.
- Non-secure async storage exists via `StorageService`.
- Auth navigation currently has only `Login` and `ForgotPassword`.

## Key Decisions

1. Store remembered tenant metadata in secure device storage because the BRD explicitly requires secure storage for tenant information.
2. Do not store passwords.
3. Use the active remembered tenant code for login requests when the tenant code field is hidden.
4. Do not add any separate tenant create/retrieve/validation API calls. Tenant selection reads and writes only local app storage.
5. Keep remembered tenants on logout and clear only authentication/session state.
6. Add a dedicated tenant-selection screen under the auth stack.
7. Remove hardcoded login defaults as part of this work.

## Tenant API Dependency

No new tenant API is required for this enhancement.

- The app will not call a tenant create endpoint.
- The app will not call a tenant retrieve/list endpoint.
- The app will not call a tenant validation/check-code endpoint.
- Tenant records are created, listed, selected, updated, and deleted only from local app storage.
- The existing login endpoint remains the only backend check for whether a tenant code can authenticate the user.

The local tenant model must support pending tenant records because a user can add a tenant code before authenticating with it:

```ts
type RememberedTenant = {
  id: string;
  tenantId?: string | null;
  tenantCode: string;
  tenantName: string;
  apiEndpoint?: string | null;
  isConfirmed: boolean;
  lastUsedAt: string;
};
```

For pending local tenants, use a generated local `id`, set `tenantName` to the entered tenant code, and set `isConfirmed` to `false`. After successful login, enrich the record with `user.tenantId` and any tenant details returned by `/identity/api/auth/login`, then set `isConfirmed` to `true`.

## Phase 1 - Types, Storage Keys, and Tenant Store Service

### Files

- `apps/mobile/src/shared/constants/storageKeys.ts`
- `apps/mobile/src/shared/services/TenantSelection/TenantSelectionService.ts`
- `apps/mobile/src/shared/services/TenantSelection/index.ts`
- `apps/mobile/src/shared/services/TenantSelection/TenantSelectionService.test.ts`
- `apps/mobile/src/shared/types/auth.ts` or new `apps/mobile/src/shared/types/tenant.ts`

### Steps

1. Add storage keys:
   - `REMEMBERED_TENANTS`
   - `ACTIVE_TENANT_ID`
2. Add `RememberedTenant` type:
   - `id`
   - `tenantId?: string | null`
   - `tenantCode`
   - `tenantName`
   - `apiEndpoint?: string | null`
   - `isConfirmed`
   - `lastUsedAt`
3. Create `TenantSelectionService` with:
   - `getRememberedTenants()`
   - `getActiveTenant()`
   - `setActiveTenant(id)`
   - `addLocalTenantCode(tenantCode)`
   - `upsertRememberedTenant(tenant)`
   - `removeRememberedTenant(id)` if needed later
   - `clearRememberedTenants()` for tests or future settings only, not logout
4. Prevent duplicates by normalized tenant code and, after successful login, tenant ID.
5. Update `lastUsedAt` whenever a tenant becomes active.
6. Handle malformed stored JSON by clearing only tenant-selection keys and returning safe defaults.

### Tests

- Empty storage returns no tenants and no active tenant.
- `addLocalTenantCode` stores a pending local tenant and makes it active when requested.
- `upsertRememberedTenant` enriches or stores a tenant after successful login.
- Duplicate tenant code updates existing record instead of adding a duplicate.
- `setActiveTenant` preserves other remembered tenants.
- Bad JSON does not crash the app.

## Phase 2 - Local Tenant Add and Selection Rules

### Files

- `apps/mobile/src/shared/services/TenantSelection/TenantSelectionService.ts`
- `apps/mobile/src/shared/services/TenantSelection/TenantSelectionService.test.ts`
- `apps/mobile/src/shared/validation/authSchemas.ts`
- `apps/mobile/src/shared/validation/authSchemas.test.ts`

### Steps

1. Add a tenant-code-only validation schema for the local add form.
2. Normalize tenant codes for comparison and storage.
3. When the user adds a tenant code, create or update a local pending tenant record.
4. Set the added tenant as active immediately so the login screen can hide the tenant code field and use the active local code.
5. Do not call any backend tenant endpoint during add, select, list, or retrieve behavior.
6. If login later succeeds, update the same local tenant record with backend login response metadata.
7. If login later fails, keep the local tenant record but leave it unconfirmed unless product decides failed tenant codes should be removed.

### Tests

- Adding a tenant code creates a local pending tenant.
- Adding the same code twice does not duplicate the list.
- Selecting a local pending tenant makes it active.
- No API client is invoked during local tenant add/list/select flows.

## Phase 3 - Authentication Service Changes

### Files

- `apps/mobile/src/shared/services/Authentication/AuthenticationService.ts`
- `apps/mobile/src/shared/services/Authentication/AuthenticationAdapter.ts`
- `apps/mobile/src/shared/services/Authentication/AuthenticationAdapter.test.ts`
- `apps/mobile/src/features/authentication/hooks/useLogin.ts`

### Steps

1. Change login input to accept:
   - `email`
   - `password`
   - optional `tenantCode`
   - optional `activeTenant`
2. Resolve the effective tenant code:
   - use submitted tenant code for first login
   - otherwise use active remembered tenant code
   - fail with a clear message if neither exists
3. After successful login, derive tenant metadata from the response:
   - use response tenant matching `user.tenantId` when present
   - fallback to submitted/active tenant code if response omits `tenants`
   - use tenant display name from login response once available
   - fallback tenant display name to tenant code when the login response does not provide a name
4. Persist or enrich the remembered tenant and set it active after successful login.
5. Update logout/session clearing so remembered tenants and active tenant are retained.
6. Replace broad `SecureStorageService.clearAll()` with explicit auth-key deletion if tenant data is stored in secure storage.

### Tests

- First login stores or confirms the authenticated tenant.
- Returning login uses the active local tenant without requiring tenant code.
- Logout clears auth/session but keeps remembered tenants.
- Login fails clearly when no tenant code or active tenant is available.

## Phase 4 - Dynamic Login Schema and Login UI

### Files

- `apps/mobile/src/shared/validation/authSchemas.ts`
- `apps/mobile/src/shared/validation/authSchemas.test.ts`
- `apps/mobile/src/features/authentication/screens/LoginScreen/index.tsx`
- `apps/mobile/src/features/authentication/screens/LoginScreen/index.test.tsx`

### Steps

1. Remove hardcoded default email, password, and tenant code.
2. Load active tenant on screen mount.
3. If no active tenant exists, render:
   - Tenant Code
   - Email Address
   - Password
   - Login Button
4. If active tenant exists, render:
   - Current Tenant section with tenant name
   - show pending/local-only tenant codes using the tenant code as display name
   - Switch Tenant action
   - Email Address
   - Password
   - Login Button
5. Use a dynamic schema or separate schemas:
   - first login requires tenant code
   - returning login does not require tenant code
6. Keep the screen styled with existing Figma token utilities and Plus Jakarta Sans typography.
7. Show loading state while active tenant is being read.
8. Keep forgot password behavior intact. If forgot-password requires tenant code, decide whether to pass active tenant code or prompt for tenant selection.

### Tests

- No active tenant renders tenant code field.
- Active tenant hides tenant code field and displays tenant name.
- Switch Tenant action navigates to tenant-selection screen.
- Form submission sends the correct effective tenant code.
- Validation errors match the current UI style.

## Phase 5 - Tenant Selection Screen

### Files

- `apps/mobile/src/features/authentication/screens/TenantSelectionScreen/index.tsx`
- `apps/mobile/src/features/authentication/screens/TenantSelectionScreen/index.test.tsx`
- `apps/mobile/src/features/authentication/index.ts`
- `apps/mobile/src/navigation/types/navigation.ts`
- `apps/mobile/src/navigation/AuthStack/AuthStack.tsx`

### Steps

1. Add `TenantSelection` route to `AuthStackParamList`.
2. Add a screen that displays:
   - title: `Select Tenant`
   - remembered tenant list
   - active tenant indicator
   - `Add New Tenant` area
   - tenant code input
   - `Continue` action
3. Selecting an existing tenant:
   - clears any current auth session if present
   - sets selected tenant active
   - returns to login
4. Adding a new tenant:
   - validates only the local form input format
   - stores the tenant code locally as a pending tenant record
   - sets it active
   - returns to login
5. Prevent duplicate displayed tenant rows.
6. Preserve BRD behavior: switching tenants does not authenticate the user.

### Tests

- Renders remembered tenants.
- Selecting a tenant sets it active and navigates back to login.
- Adding a tenant code stores it locally and makes it active.
- Invalid local input format shows an error and stores nothing.
- Duplicate add updates existing tenant and does not duplicate the list.

## Phase 6 - Session and API Header Safety

### Files

- `apps/mobile/src/shared/api/client/interceptors.ts`
- `apps/mobile/src/shared/services/Authentication/AuthenticationService.ts`
- related tests if present or new focused tests

### Steps

1. Confirm API calls use the current access token only after login.
2. Confirm login requests do not send stale bearer tokens.
3. Confirm switching tenant clears auth/session state so the next screen requires login.
4. If the API requires tenant headers, add active tenant headers only where safe:
   - `X-Tenant-Code`
   - `X-Tenant-Id`
5. Do not add tenant validation API behavior to interceptors or services.

### Tests

- Switching tenants removes auth state.
- Logout keeps active tenant metadata.
- Authenticated API calls still include bearer token after login.

## Phase 7 - Optional Settings Maintenance

This is not required by the BRD but is useful operationally.

### Files

- `apps/mobile/src/features/profile/screens/SettingsScreen/index.tsx`
- matching test file

### Steps

1. Add a remembered-tenant management section only if product wants user-facing cleanup.
2. Allow clearing remembered tenants behind a confirmation modal.
3. If all tenants are cleared, next login falls back to first-time login.

## Phase 8 - Documentation and BRD Update

### Files

- `Enterprise_Application_Blueprint_v7.0_Expanded.md`
- `apps/mobile/README.md`
- this plan file, if execution results need to be tracked

### Steps

1. Document remembered-tenant behavior.
2. Document secure storage keys and why tenants are retained on logout.
3. Document that remembered tenants are app-local and are not created, retrieved, or validated through tenant APIs.
4. Document that passwords are never stored.

## Phase 9 - Verification

Run targeted validation:

```bash
pnpm --dir apps/mobile typecheck
pnpm --dir apps/mobile lint
pnpm --dir apps/mobile test -- LoginScreen TenantSelectionScreen AuthenticationAdapter TenantSelectionService authSchemas --runInBand
```

No backend tests are required for this enhancement unless the existing login contract is changed.

## Acceptance Criteria Mapping

- AC-001: Phase 4
- AC-002: Phases 1 and 3
- AC-003: Phases 3 and 4
- AC-004: Phase 4
- AC-005: Phase 5
- AC-006: Phases 2 and 5, implemented as local tenant-code registration with login-time confirmation
- AC-007: Phases 1 and 5
- AC-008: Phases 3 and 6
- AC-009: Phases 3 and 6
- AC-010: Phases 5 and 6

## Implementation Notes

- Keep feature module structure aligned with the BRD and current scaffold rule: each screen is `index.tsx` with a counterpart `index.test.tsx`.
- Do not introduce a new storage library unless SecureStore proves unsuitable for the stored tenant payload size.
- If SecureStore cannot safely store the tenant list as one JSON value on all target platforms, store non-sensitive tenant display metadata in `StorageService` and keep active tenant/code in `SecureStorageService`; document the reason.
- Avoid clearing all secure storage during logout once remembered tenants are stored there.
- Use normalized tenant codes for duplicate detection.
- Display the login-response tenant name when available; otherwise display the stored tenant code.
- Do not scaffold `apps/mobile/src/shared/api/endpoints/Tenants` for this requirement.
