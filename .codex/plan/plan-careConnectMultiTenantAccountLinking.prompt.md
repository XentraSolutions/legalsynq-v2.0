# Plan: CareConnect Multi-Tenant Account Linking

**TL;DR:** Introduce a `UserTenant` join table in the Identity service so one user account (one email) maps to multiple CareConnect tenants. Re-enrollment on a second tenant links the existing account; sign-in issues a JWT for the active tenant plus a `tenant_ids` claim for all accessible tenants.

---

## Phase 1 — Identity Schema (foundational, blocks all other phases)

1. **NEW** `Identity.Domain/UserTenant.cs` — `(Id, UserId, TenantId, IsActive, JoinedAtUtc)` entity, factory method `UserTenant.Create(userId, tenantId)`
2. **NEW** `Identity.Infrastructure/Data/Configurations/UserTenantConfiguration.cs` — table `idt_UserTenants`, unique index on `(UserId, TenantId)`, FK to `idt_Users` + `idt_Tenants`
3. **MODIFY** `IdentityDbContext.cs` — add `DbSet<UserTenant>`, register config
4. **NEW** EF migration `AddUserTenantsMultiTenant`:
   - Creates `idt_UserTenants` table
   - Backfills one row per existing user from `User.TenantId`: `INSERT INTO idt_UserTenants (Id, UserId, TenantId, IsActive, JoinedAtUtc) SELECT gen_random_uuid(), Id, TenantId, true, CreatedAtUtc FROM idt_Users`
   - **Drops** the `(TenantId, Email)` unique index on `idt_Users`
   - **Adds** a global `Email` unique index on `idt_Users` — one account per email across all tenants (enforced at DB level)
5. **MODIFY** `Identity.Domain/User.cs` — add `ICollection<UserTenant> TenantMemberships` nav collection

---

## Phase 2 — Repository Layer *(parallel with Phase 1 — depends on Step 1)*

6. **MODIFY** `IUserRepository.cs` + `UserRepository.cs`:
   - `GetByTenantAndEmailAsync`: extend query to also match users who have a `UserTenants` row for the requested tenant (not just `user.TenantId == tenantId`) — critical since `User.TenantId` can change after `AssignTenantAsync`
   - `GetPrimaryOrgMembershipAsync(userId, **tenantId**, ...)`: add tenant filter (`WHERE UserId = X AND Organization.TenantId = tenantId AND IsActive = true`) so the JWT `org_id` reflects the org in the login tenant. **Drop the `IsPrimary` condition** when `tenantId` is supplied — a user has exactly one active org per tenant in the CareConnect model, so the tenant scope alone is sufficient. Keep `IsPrimary` filter for existing callers that do not pass a `tenantId` (backward compat)
   - **NEW** `GetActiveTenantMembershipsAsync(userId)` — returns all active `UserTenant` rows for a user, ordered by `JoinedAtUtc`

---

## Phase 3 — Registration Flows *(depends on Phase 1 + 2)*

7. **MODIFY** `AdminEndpoints.SelfRegisterUser` (CC2-ENROLL direct sign-up):
   - Change idempotency check from `WHERE TenantId = org.TenantId AND Email = X` → **global** `WHERE Email = X`
   - **Same tenant** (UserTenants row already exists for this tenant): return existing — unchanged behavior
   - **Different tenant — user exists**: verify submitted password against existing hash; if wrong → `409 Conflict` with message prompting use of existing password; if correct → add `UserTenant(userId, org.TenantId)` + `UserOrganizationMembership` (no `IsPrimary` — tenant-scoped query is sufficient) → return `(UserId, IsNew: false)`
   - **New user**: create User + `UserTenant(org.TenantId)` + OrgMembership (adds the UserTenant row alongside the User row)

8. **MODIFY** `AdminEndpoints.ProvisionProviderUser` (invite flow):
   - Same global email check
   - **Different tenant — user IsActive=true** (already has password):
     1. Add `UserTenant` row (userId, org.TenantId)
     2. Add `UserOrganizationMembership` (userId, orgId) — no `IsPrimary` needed; tenant-scoped query resolves the org at login
     3. Send a **tenant-access-granted notification email** (NOT an invite — no set-password link) via `INotificationsEmailClient.SendTenantAccessGrantedEmailAsync(email, displayName, tenantName, portalUrl, tenantId, ct)` — best-effort, non-fatal if it fails; log warning on failure
     4. Return `ProvisionProviderUserResponse(existingUser.Id, InvitationId: null, IsNew: false, InvitationSent: false, NotificationSent: emailSent)`
     - Add `bool NotificationSent` field to `ProvisionProviderUserResponse` record (default `false` for backward compat)
     - Add `SendTenantAccessGrantedEmailAsync(...)` to `INotificationsEmailClient` interface + implementation
   - **Different tenant — user IsActive=false** (invite-pending): add `UserTenant` + OrgMembership; send new-tenant invite link; return `InvitationSent: true`
   - **New user**: unchanged (create user + UserTenant row)

---

## Phase 4 — UserMembershipService *(depends on Phase 1)*

9. **MODIFY** `UserMembershipService.AssignTenantAsync`:
   - **Keep** `ExecuteUpdateAsync(u => u.TenantId = cmd.TenantId)` — `User.TenantId` is updated to the newly assigned tenant (existing behavior preserved)
   - **Additionally** add a `UserTenant` row for `cmd.TenantId` (idempotent — check `UserTenants.AnyAsync` first so no duplicate row is inserted)
   - `alreadyInTenant` = `user.TenantId == cmd.TenantId || await db.UserTenants.AnyAsync(ut => ut.UserId == cmd.UserId && ut.TenantId == cmd.TenantId)`

---

## Phase 5 — Auth / JWT *(depends on Phases 2 + 4)*

10. **NEW** `Identity.Application/DTOs/TenantSummary.cs` — `record TenantSummary(Guid TenantId, string TenantCode)`
11. **MODIFY** `Identity.Application/DTOs/LoginResponse.cs` — add `IReadOnlyList<TenantSummary> Tenants`
12. **MODIFY** `IJwtTokenService` + `JwtTokenService.GenerateToken` — add `IEnumerable<Guid>? tenantIds` param; emit one `"tenant_ids"` claim per entry
13. **MODIFY** `AuthService.BuildLoginResponseAsync`:
    - Pass `tenant.Id` to `GetPrimaryOrgMembershipAsync` (tenant-scoped org for JWT)
    - Fix `UserResponse.TenantId` to use `tenant.Id` (active login tenant)
    - Load all tenant memberships via `GetActiveTenantMembershipsAsync`; pass to `GenerateToken`; build `TenantSummary` list from membership rows
    - Return `new LoginResponse(token, expiresAtUtc, userResponse, tenants)`
14. **MODIFY** `shared/building-blocks/BuildingBlocks/Context/CurrentRequestContext.cs` — parse `"tenant_ids"` claim into `IReadOnlyList<Guid> TenantIds`

---

## Phase 6 — Portal Access Role Guards *(parallel with Phase 5 — no schema dependency)*

Two symmetrical enforcement points: CC-only users blocked from the operator/tenant portal; non-CC-role users blocked from the CC common portal.

### Part A — CC Common Portal: require a CC product role

Block login to the CareConnect common portal for any user without `CARECONNECT_REFERRER` or `CARECONNECT_RECEIVER` in their effective product roles. The `AUTH-CC01` path (`ResolveByEmail=true`) is the only login path guarded — the normal tenant-code path is unchanged.

**Blocked roles (all "Tenant" system roles plus StandardUser/TenantUser without a CC product role):**

| Role | Reason blocked |
|---|---|
| `TenantAdmin` | Manages tenants via operator portal, not CC portal; already guarded as multi-tenant privilege risk |
| `TenantManager` | No CC product role auto-grant; product access via explicit grants only |
| `TenantStaff` | Same — no CC role auto-grant |
| `TenantViewer` | Same — read-only, no CC role auto-grant |
| `StandardUser` | No special logic; would land in CC portal without a CC role |
| `TenantUser` | Same |

These roles are only allowed through if they **also** hold `CARECONNECT_REFERRER` or `CARECONNECT_RECEIVER` as an explicit product role grant.

15. **MODIFY** `AuthService.BuildLoginResponseAsync` signature — add `bool requireCareConnectAccess = false` parameter (default `false` keeps all existing call-sites unchanged)

16. **MODIFY** `AUTH-CC01` call site (~line 126 in `AuthService.LoginAsync`) — pass `requireCareConnectAccess: true`:
    - `return await BuildLoginResponseAsync(ccUserWithRoles, globalTenant, request, sw, ipAddress, ct, requireCareConnectAccess: true);`

17. **MODIFY** `BuildLoginResponseAsync` — add the CC role guard **after** the CC-AUTH-01 auto-inject block (LAW_FIRM users without explicit provisioning have already had `CARECONNECT_REFERRER` injected by this point, so they pass cleanly):
    - If `requireCareConnectAccess` is `true` AND `productRolesFlat` has no entry starting with `"SYNQ_CARECONNECT:"`:
      - Call `EmitLoginFailed(... reason: "NoCareConnectRole" ...)`
      - `throw new UnauthorizedAccessException()`
    - No system-role bypass — `TenantAdmin`/`PlatformAdmin` are also blocked without a CC role on the common portal

18. **MODIFY** `apps/web/src/app/(platform)/careconnect/layout.tsx` — defense-in-depth after `requireProductAccess(FrontendProductCode.CareConnect)`:
    - Fetch session
    - If `session.isTenantAdmin || session.isPlatformAdmin` → pass (stale-JWT edge case only)
    - Else if `session.productRoles` includes neither `ProductRole.CareConnectReferrer` nor `ProductRole.CareConnectReceiver` → `redirect('/access-denied')`
    - Catches stale JWTs issued before the backend guard was deployed

### Part B — Operator/Tenant Portal: require a system role

Block login to the operator/tenant portal for any user whose only access is a CC product role (no active GLOBAL system role). These users belong exclusively in the CC common portal.

19. **MODIFY** `AuthService.BuildLoginResponseAsync` signature — add `bool requireTenantAccess = false` parameter alongside `requireCareConnectAccess`

20. **MODIFY** normal tenant-code call site (~line 236 in `AuthService.LoginAsync`):
    - `return await BuildLoginResponseAsync(userWithRoles, tenant, request, sw, ipAddress, ct, requireTenantAccess: true);`

21. **MODIFY** `BuildLoginResponseAsync` — add the inverse role guard **immediately after** `roleNames` is computed from `ScopedRoleAssignments` (before any product role logic):
    - If `requireTenantAccess` is `true` AND `roleNames` is empty (user has no active GLOBAL system roles):
      - Call `EmitLoginFailed(... reason: "CareConnectUserOnTenantPortal" ...)`
      - `throw new UnauthorizedAccessException()`
    - A user holding BOTH a system role AND a CC product role passes (intentional — e.g. a `TenantManager` who also has `CARECONNECT_RECEIVER`)

22. **MODIFY** `apps/web/src/app/(platform)/layout.tsx` — defense-in-depth after `requireOrg()`:
    - Fetch session
    - If `session.systemRoles` is empty AND `session.productRoles` includes `CareConnectReferrer` or `CareConnectReceiver` → `redirect('/careconnect')`
    - Catches stale JWTs; CC-only users are redirected to the CC sub-layout rather than blocked with an error

---

## Phase 7 — CareConnect Available Networks (follow-on) *(depends on Phase 5)*

23. Investigate which endpoint(s) serve the "Available Networks" tab — likely in `ProviderNetworkEndpoints` or similar
24. Extend those queries to filter by `CurrentRequestContext.TenantIds` instead of just `TenantId` — so all networks across the user's tenants appear in that tab

> **CareConnect enrollment/invite code (EnrollmentEndpoints / AutoProvisionService) requires NO changes.** Identity transparently handles the cross-tenant merge; CareConnect already manages a per-tenant Provider row correctly.

---

## Relevant Files

| File | Change |
|---|---|
| `Identity.Domain/UserTenant.cs` | NEW |
| `Identity.Domain/User.cs` | Add nav collection |
| `Identity.Infrastructure/Data/Configurations/UserTenantConfiguration.cs` | NEW |
| `Identity.Infrastructure/Data/IdentityDbContext.cs` | Add DbSet + register config |
| Migration `AddUserTenantsMultiTenant` | NEW (create table + backfill + index swap) |
| `Identity.Infrastructure/Repositories/UserRepository.cs` | Update 3 methods |
| `Identity.Infrastructure/Services/UserMembershipService.cs` | AssignTenantAsync — keep TenantId update + add UserTenant row |
| `Identity.Infrastructure/Services/JwtTokenService.cs` | Add tenant_ids claims |
| `Identity.Infrastructure/Services/NotificationsEmailClient.cs` | Add `SendTenantAccessGrantedEmailAsync` |
| `Identity.Application/IUserRepository.cs` | Update signatures |
| `Identity.Application/DTOs/LoginResponse.cs` | Add Tenants field |
| `Identity.Application/DTOs/TenantSummary.cs` | NEW |
| `Identity.Application/Interfaces/IJwtTokenService.cs` | Update GenerateToken signature |
| `Identity.Application/Interfaces/INotificationsEmailClient.cs` | Add `SendTenantAccessGrantedEmailAsync` |
| `Identity.Application/Services/AuthService.cs` | Tenant-scoped org + Tenants in response + `requireCareConnectAccess` + `requireTenantAccess` guards (Phase 6) |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | SelfRegisterUser + ProvisionProviderUser; add `NotificationSent` to response record |
| `shared/building-blocks/BuildingBlocks/Context/CurrentRequestContext.cs` | Parse tenant_ids |
| `apps/web/src/app/(platform)/careconnect/layout.tsx` | Add CC role defense-in-depth guard (Phase 6A step 18) |
| `apps/web/src/app/(platform)/layout.tsx` | Add CC-only redirect guard (Phase 6B step 22) |
| CareConnect network endpoint(s) | Phase 7 — pending investigation |

---

## Verification

1. Enroll Provider on TenantA → confirm `idt_UserTenants` row created with `TenantA`
2. Same email, correct password, enroll on TenantB → same `UserId` returned, `UserTenants` has both rows, no new `idt_Users` row
3. Same email, **wrong** password on TenantB → `409 Conflict`, no rows added
4. Sign in on TenantB portal → JWT `tenant_id = TenantB`, `tenant_ids = [TenantA, TenantB]`, `org_id = TenantB's org`
5. Common portal sign-in (ResolveByEmail) → JWT `tenant_id = home tenant`, `LoginResponse.Tenants` lists both
6. Invite existing **active** user to TenantB → `UserTenants` row added, tenant-access-granted notification email sent (`NotificationSent: true`), no invite email (`InvitationSent: false`)
7. Invite existing **inactive** (invite-pending) user to TenantB → `UserTenants` row added, new invite email sent for TenantB portal (`InvitationSent: true`)
8. `AssignTenantAsync` → `User.TenantId` updated to new tenant AND `UserTenants` row added for that tenant
9. Global `Email` unique index prevents duplicate User rows; old `(TenantId, Email)` index is gone
10. Build passes, existing test suite green
11. CC common portal login as `TenantManager`/`TenantStaff`/`TenantViewer`/`StandardUser`/`TenantUser` with no CC role → `401 Unauthorized`, audit event `NoCareConnectRole`
12. CC common portal login as LAW_FIRM user with no explicit provisioning (CC-AUTH-01 auto-inject) → success, `CARECONNECT_REFERRER` in JWT
13. CC common portal login as explicitly provisioned `CARECONNECT_RECEIVER` user → success
14. **Tenant-code** portal login as `TenantManager` (not `ResolveByEmail`) → guard does NOT fire, login succeeds
15. Stale JWT (no CC role) accessing `/careconnect/*` → CC layout redirects to `/access-denied`
16. Operator portal login as a user with only `CARECONNECT_REFERRER` (no system roles) → `401 Unauthorized`, audit event `CareConnectUserOnTenantPortal`
17. Operator portal login as a user with BOTH `TenantManager` AND `CARECONNECT_RECEIVER` → success (dual-role user passes both guards)
18. Stale JWT (CC-only role) accessing `/dashboard` or any operator route → platform layout redirects to `/careconnect`

---

## Decisions & Boundaries

- **User.TenantId** is updated by `AssignTenantAsync` (existing behavior kept); migration replaces the old `(TenantId, Email)` unique index with a global `Email` unique index
- **Global email uniqueness** enforced at DB level (global `Email` unique index on `idt_Users`) + in code (global lookup before create)
- **Existing active users invited to a new tenant** receive a tenant-access-granted notification email (no set-password link); `InvitationSent: false`, `NotificationSent: true`
- **Existing inactive users invited to a new tenant** receive a new invite email (set-password link for the new tenant's portal); `InvitationSent: true`
- **Password required for cross-tenant self-enrollment** — proves identity before linking
- **JWT `tenant_id`** = the tenant being logged into; `tenant_ids` = all accessible tenants
- **CC portal role gate (Phase 6A)** applies to all Tenant-tagged roles (`TenantAdmin`, `TenantManager`, `TenantStaff`, `TenantViewer`, `StandardUser`, `TenantUser`) — blocked from the CC common portal unless they also hold an explicit `CARECONNECT_REFERRER` or `CARECONNECT_RECEIVER` product role
- **Operator portal role gate (Phase 6B)** blocks CC-only users (no GLOBAL system role) from the tenant-code login path — they belong exclusively in the CC common portal
- **Dual-role users** (e.g. `TenantManager` + `CARECONNECT_RECEIVER`) pass both guards — no restriction
- **`IsPrimary` not required for cross-tenant org membership** — cross-tenant linking creates `UserOrganizationMembership` rows without `SetPrimary()`. The tenant-scoped `GetPrimaryOrgMembershipAsync(userId, tenantId)` query drops the `IsPrimary` filter when a `tenantId` is provided, relying on the single active org per user per tenant (CareConnect invariant). Existing single-tenant callers that omit `tenantId` continue to use `IsPrimary` unchanged
- **Phase 6 is independent** — can be shipped before Phases 1–5; no schema changes required
- **Common portal UI** — no changes; multiple tenants surfaced in "Available Networks" tab only
- **Phase 7** (cross-tenant Available Networks) is explicitly out of scope until Phase 5 is shipped and the correct endpoint is confirmed
