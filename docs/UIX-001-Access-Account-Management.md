# UIX-001 — Access & Account Management
## Feature Design & Validation Report
**LegalSynq Control Center + Tenant Portal**
**Generated:** 2026-04-01
**Status:** Draft — Pending Implementation

---

## Report Header

| Field | Value |
|---|---|
| Feature ID | UIX-001 |
| Feature Name | Access & Account Management |
| Interfaces | Control Center (port 5004) + Tenant Portal (port 5000) |
| Authored | LegalSynq Platform Engineering |
| MVP Target | Q2 2026 |
| Phase 2 Target | Q3 2026 |

---

## Summary

This report defines the complete feature set for access and account management across the LegalSynq platform. It covers shared authentication flows (login, logout, password reset), tenant portal self-service features (profile, org/role display), and Control Center administrative capabilities (user management, activate/deactivate, admin-triggered resets). The report serves as the design authority for all subsequent implementation prompts tied to UIX-001.

---

## Scope Covered

- Login, logout, session restore, and invalid session handling
- Forgot password, reset password, change password with policy enforcement
- User profile display and editing
- Profile photo / avatar management
- Admin account management: activate, deactivate, resend invite, trigger reset
- Membership and role visibility
- Backend API surface definition (21 endpoints)
- Data model and storage considerations
- MVP and Phase 2 scoping
- Implementation sequence
- Validation checklist

---

## Out of Scope

- MFA implementation (defined, deferred to Phase 2)
- Session / device tracking UI (Phase 2)
- Email change with re-verification (Phase 2)
- Notification preferences (Phase 2)
- Product-level role assignment via UI (separate feature)
- Tenant creation and provisioning (separate admin flow)
- Billing and subscription management

---

## 1. Shared Features

Features that appear in both the Control Center and the Tenant Portal, potentially styled or scoped differently per interface.

| # | Feature | Purpose | Who Uses It | MVP |
|---|---|---|---|---|
| S01 | Login | Authenticate user, establish JWT session | All users | ✓ |
| S02 | Logout | Destroy session cookie and JWT, return to login | All users | ✓ |
| S03 | Session Restore | Re-hydrate session from cookie on app load via `/auth/me` | All users (automatic) | ✓ |
| S04 | Invalid Session Handling | On 401, redirect to login with reason message | All users (automatic) | ✓ |
| S05 | Forgot Password | Initiate reset by submitting email; triggers reset email | All users | ✓ |
| S06 | Reset Password | Set new password via valid single-use token from email | All users | ✓ |
| S07 | Change Password | Authenticated change — requires current password + new | All authenticated users | ✓ |
| S08 | Password Policy | Enforce min length, complexity on all password flows | All users (automatic) | ✓ |
| S09 | Password History | Reject reuse of recent passwords | All users | Phase 2 |
| S10 | Profile Display | Show name, email, role, avatar | All authenticated users | ✓ |
| S11 | Profile Edit | Edit name, phone; email change is Phase 2 | All authenticated users | ✓ |
| S12 | Email Change (verified) | Change email with re-verification email | All authenticated users | Phase 2 |
| S13 | Profile Photo | Upload, update, remove avatar photo | All authenticated users | Phase 2 |
| S14 | Session Management Basics | Last login time, current session indicator | All authenticated users | Phase 2 |
| S15 | MFA Recommendation | Banner prompting MFA enrolment (before enforcement) | All authenticated users | Phase 2 |
| S16 | MFA Enrolment | TOTP setup with backup codes | All authenticated users | Phase 2 |
| S17 | Audit / Security History | Recent login events (time, IP) sourced from audit service | All authenticated users | Phase 2 |

**Recommended Password Policy (MVP):**
- Minimum 10 characters
- At least one uppercase letter
- At least one numeric digit
- At least one special character
- History enforcement: Phase 2

---

## 2. Control Center Features

Features exclusive to the Control Center (port 5004). Accessed by PlatformAdmins and TenantAdmins based on scope.

| # | Feature | Purpose | TenantAdmin Scope | PlatformAdmin Scope | MVP |
|---|---|---|---|---|---|
| CC01 | Admin Login | Platform admin authentication, unbranded | Own tenant | All tenants | ✓ |
| CC02 | Admin Forgot / Reset Password | Password recovery through CC UI | Own account | Any account | ✓ |
| CC03 | Admin Change Password | Authenticated password change via CC | Own account | Own account | ✓ |
| CC04 | Admin Profile Page | View + edit name/phone, see role and last login | Own profile | Own profile | ✓ |
| CC05 | Admin Profile Photo | Upload / update / remove avatar | Own | Own | Phase 2 |
| CC06 | User List | View all users in scope with status and last login | Tenant users only | All tenants | ✓ |
| CC07 | Activate User | Re-enable a deactivated user account | Tenant users | Any user | ✓ |
| CC08 | Deactivate User | Disable a user account without deleting it | Tenant users | Any user | ✓ |
| CC09 | Lock Account | Temporary lock (beyond deactivate) | Tenant users | Any user | Phase 2 |
| CC10 | Unlock Account | Remove a temporary lock | Tenant users | Any user | Phase 2 |
| CC11 | Resend Invite | Re-send onboarding / welcome email | Tenant users | Any user | ✓ |
| CC12 | Admin-Triggered Password Reset | Initiate reset email on behalf of a user | Tenant users | Any user | ✓ |
| CC13 | Force Logout | Invalidate all sessions for a user via session version increment | Tenant users | Any user | Phase 2 |
| CC14 | Membership & Role Visibility | Read-only display of user's org memberships and roles | Tenant scope | All scopes | ✓ |
| CC15 | Tenant Security Config | Session timeout, password policy overrides, MFA enforcement flag | Own tenant | All tenants | Phase 2 |
| CC16 | PlatformAdmin vs TenantAdmin Scope | CC UI adapts — PlatformAdmin sees tenant selector and cross-tenant data | N/A | N/A | ✓ |

---

## 3. Tenant Portal Features

Features in the main Tenant Portal (port 5000), used by tenant members including tenant admins operating within the portal.

| # | Feature | Purpose | Who Uses It | MVP |
|---|---|---|---|---|
| TP01 | Tenant-Branded Login | Login with tenant logo/colours from branding config | All tenant users | ✓ (exists) |
| TP02 | Logout | Sign out and clear session | All tenant users | ✓ |
| TP03 | Forgot Password | Branded forgot-password page at `/forgot-password` | All tenant users | ✓ |
| TP04 | Reset Password | Token-based reset at `/reset-password?token=...` (unauthenticated) | All tenant users | ✓ |
| TP05 | Change Password | Authenticated change from profile page | All authenticated users | ✓ |
| TP06 | My Profile Page | Self-service at `/profile` — edit name/phone; view org/role/products | All authenticated users | ✓ |
| TP07 | Org / Role / Product Display | Read-only view of current org, member role, and product entitlements | All authenticated users | ✓ |
| TP08 | Profile Photo | Upload / update / remove avatar from portal | All authenticated users | Phase 2 |
| TP09 | Notification Preferences | Opt in/out of system email categories | All authenticated users | Phase 2 |
| TP10 | Account Security Basics | Change password entry point; last login display | All authenticated users | Change pwd: ✓ / Last login: Phase 2 |
| TP11 | Self-Service Scope Boundary | Portal never exposes admin controls (deactivation, role assignment, etc.) | Enforced by omission | ✓ |

---

## 4. Feature Matrix

| Feature | Shared | Control Center | Tenant Portal | MVP | Phase 2 |
|---|---|---|---|---|---|
| Login | ✓ | ✓ | ✓ | ✓ | |
| Logout | ✓ | ✓ | ✓ | ✓ | |
| Session restore | ✓ | ✓ | ✓ | ✓ | |
| Invalid session handling | ✓ | ✓ | ✓ | ✓ | |
| Forgot password | ✓ | ✓ | ✓ | ✓ | |
| Reset password | ✓ | ✓ | ✓ | ✓ | |
| Change password | ✓ | ✓ | ✓ | ✓ | |
| Password policy enforcement | ✓ | ✓ | ✓ | ✓ | |
| Password history | ✓ | | | | ✓ |
| Profile display | ✓ | ✓ | ✓ | ✓ | |
| Profile edit (name, phone) | ✓ | ✓ | ✓ | ✓ | |
| Email change (verified) | ✓ | | | | ✓ |
| Profile photo upload/update/remove | ✓ | ✓ | ✓ | | ✓ |
| MFA recommendation banner | ✓ | ✓ | ✓ | | ✓ |
| MFA enrolment | ✓ | ✓ | ✓ | | ✓ |
| Session / device listing | ✓ | ✓ | ✓ | | ✓ |
| Security event history | ✓ | ✓ | ✓ | | ✓ |
| Tenant-branded login | | | ✓ | ✓ | |
| Org / role / product display | | | ✓ | ✓ | |
| Notification preferences | | | ✓ | | ✓ |
| Admin: list users | | ✓ | | ✓ | |
| Admin: activate / deactivate | | ✓ | | ✓ | |
| Admin: lock / unlock account | | ✓ | | | ✓ |
| Admin: resend invite | | ✓ | | ✓ | |
| Admin: trigger password reset | | ✓ | | ✓ | |
| Admin: force logout | | ✓ | | | ✓ |
| Admin: membership / role visibility | | ✓ | | ✓ | |
| Admin: tenant security config | | ✓ | | | ✓ |
| PlatformAdmin vs TenantAdmin scope | | ✓ | | ✓ | |

---

## 5. Required API / Backend Capabilities

No implementation yet — definition only.

| # | Capability | Method | Suggested Path | Notes |
|---|---|---|---|---|
| A01 | Login | POST | `/identity/auth/login` | Returns JWT + sets session cookie |
| A02 | Logout | POST | `/identity/auth/logout` | Clears cookie; optionally increments session version |
| A03 | Auth / Me | GET | `/identity/auth/me` | Returns user + tenant + org + roles |
| A04 | Forgot password | POST | `/identity/auth/forgot-password` | Accepts email; sends reset link via Notifications |
| A05 | Reset password | POST | `/identity/auth/reset-password` | Accepts token + new password |
| A06 | Change password | POST | `/identity/auth/change-password` | Authenticated; requires old + new password |
| A07 | Get my profile | GET | `/identity/users/me/profile` | Returns editable profile fields |
| A08 | Update my profile | PUT | `/identity/users/me/profile` | Name, phone, etc. |
| A09 | Upload profile photo | POST | `/identity/users/me/avatar` | Multipart; returns URL |
| A10 | Update profile photo | PUT | `/identity/users/me/avatar` | Replace existing |
| A11 | Remove profile photo | DELETE | `/identity/users/me/avatar` | Clears avatar reference |
| A12 | List my sessions | GET | `/identity/users/me/sessions` | Phase 2 |
| A13 | Revoke my session | DELETE | `/identity/users/me/sessions/{id}` | Phase 2 |
| A14 | Admin: list users | GET | `/identity/admin/users` | Scoped by tenantId for TenantAdmin |
| A15 | Admin: get user | GET | `/identity/admin/users/{id}` | |
| A16 | Admin: deactivate user | POST | `/identity/admin/users/{id}/deactivate` | |
| A17 | Admin: activate user | POST | `/identity/admin/users/{id}/activate` | |
| A18 | Admin: lock user | POST | `/identity/admin/users/{id}/lock` | Phase 2 |
| A19 | Admin: unlock user | POST | `/identity/admin/users/{id}/unlock` | Phase 2 |
| A20 | Admin: resend invite | POST | `/identity/admin/users/{id}/resend-invite` | |
| A21 | Admin: trigger reset | POST | `/identity/admin/users/{id}/reset-password` | Sends reset email to user |
| A22 | Admin: force logout | POST | `/identity/admin/users/{id}/force-logout` | Increments SessionVersion |

**Total: 22 endpoints (14 MVP, 8 Phase 2)**

---

## 6. Data Model / Storage Considerations

### 6.1 Users Table — Required Additions

| Column | Type | Notes |
|---|---|---|
| `FirstName` | varchar | Display name component |
| `LastName` | varchar | Display name component |
| `PhoneNumber` | varchar nullable | Optional contact field |
| `AvatarUrl` | varchar nullable | Reference to stored image (not binary) |
| `IsActive` | bool | Soft deactivation flag; default true |
| `IsLocked` | bool | Temporary lock flag; Phase 2 |
| `LockedUntilUtc` | datetime nullable | Phase 2 |
| `FailedLoginCount` | int | Phase 2 lockout tracking |
| `LastLoginAtUtc` | datetime nullable | Updated on each successful auth |
| `SessionVersion` | int | Incremented on force-logout; checked in JWT middleware |
| `EmailVerifiedAtUtc` | datetime nullable | Null = unverified; Phase 2 enforcement |

### 6.2 New Table: PasswordResetTokens

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid | Primary key |
| `UserId` | uuid FK | References Users.Id |
| `TokenHash` | varchar | SHA-256 hash of the raw token sent in email |
| `ExpiresAtUtc` | datetime | 60-minute window recommended |
| `UsedAtUtc` | datetime nullable | Set on consumption; null = not yet used |
| `CreatedAtUtc` | datetime | |

Token is generated as a random 32-byte value, sent raw in the email URL, stored as SHA-256 hash. Single-use enforced by checking `UsedAtUtc IS NULL` and `ExpiresAtUtc > NOW()`.

### 6.3 New Table (Phase 2): UserSessions

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid | Primary key |
| `UserId` | uuid FK | |
| `DeviceFingerprint` | varchar nullable | |
| `IpAddress` | varchar | |
| `UserAgent` | varchar | |
| `CreatedAtUtc` | datetime | |
| `LastSeenAtUtc` | datetime | |
| `RevokedAtUtc` | datetime nullable | Null = active |

### 6.4 Avatar / Photo Storage

- Files stored in object storage (S3 or compatible).
- `AvatarUrl` on Users holds the public or pre-signed CDN URL — not raw binary.
- Upload goes through the Identity backend (not directly browser → S3) to enforce auth, size limits, and MIME type validation.
- Recommended max upload size: 5 MB. Accepted types: JPEG, PNG, WebP.

### 6.5 MFA (Phase 2)

| Column | Type | Notes |
|---|---|---|
| `MfaEnabled` | bool | Per-user flag |
| `MfaSecret` | varchar encrypted | TOTP seed; encrypted at rest |
| `MfaBackupCodes` | json | Array of hashed one-time backup codes |

### 6.6 Audit Events to Emit

All write operations in the Identity service must emit audit events with `actorId`, `tenantId`, `targetUserId`, `ipAddress`, `userAgent`.

| Event Type | Trigger |
|---|---|
| `auth.login.success` | Successful login |
| `auth.login.failed` | Failed login attempt |
| `auth.logout` | Explicit logout |
| `auth.password_changed` | Change password |
| `auth.password_reset` | Reset password via token |
| `auth.account_deactivated` | Admin deactivates user |
| `auth.account_activated` | Admin activates user |
| `auth.account_locked` | Admin or system lock |
| `auth.account_unlocked` | Admin unlock |
| `auth.invite_resent` | Admin resend invite |
| `auth.force_logout` | Admin force logout |

---

## 7. Recommended MVP Scope

### Control Center MVP
- Admin login / logout / session handling
- Admin profile page (read + name/phone edit)
- Change password
- Forgot / reset password flow
- User list (tenant-scoped for TenantAdmin; all tenants for PlatformAdmin)
- Activate / deactivate user
- Resend invite
- Admin-triggered password reset
- Membership and role visibility (read-only)
- PlatformAdmin vs TenantAdmin scope differentiation

### Tenant Portal MVP
- Tenant-branded login (already exists)
- Logout
- Forgot / reset / change password
- `/profile` page — name/phone edit; org, role, product display (read-only)
- Change password section on profile page
- Basic account security entry point

### Excluded from MVP (both portals)
Avatar/photo, MFA, session listing, force logout, account lock/unlock, notification preferences, email change, security event history beyond the existing audit log.

---

## 8. Implementation Sequence

| Step | Area | Task | Blocks |
|---|---|---|---|
| 1 | Backend | Users table migration — add profile + session version fields | All |
| 2 | Backend | `PasswordResetTokens` table | Forgot/reset flows |
| 3 | Backend | GET/PUT `/users/me/profile` | Profile pages |
| 4 | Backend | `LastLoginAtUtc` update on successful login | Profile display |
| 5 | Backend | Forgot password + reset password endpoints + email trigger | Password reset UI |
| 6 | Backend | Change password endpoint | Change pwd UI |
| 7 | Tenant Portal | `/profile` page — edit form + org/role/product display + change password | — |
| 8 | Tenant Portal | `/forgot-password` + `/reset-password` pages (unauthenticated) | — |
| 9 | CC | Admin profile page — name/phone edit + change password | — |
| 10 | CC | Admin forgot / reset / change password UI | — |
| 11 | CC | User management list + activate/deactivate | — |
| 12 | CC | Resend invite + admin-triggered reset actions | — |
| 13 | CC | Membership/role visibility panel on user detail | — |
| 14 | Backend | `SessionVersion` increment + JWT middleware check | Force logout |
| 15 | CC | Force logout action (Phase 2) | Requires step 14 |
| 16 | Backend + Both | Avatar upload/update/remove (Phase 2) | Requires object storage |
| 17 | Backend + Both | Account lock/unlock + failed login tracking (Phase 2) | — |
| 18 | Backend + Both | UserSessions table + session listing/revocation UI (Phase 2) | — |
| 19 | Backend + Both | MFA TOTP enrolment + enforcement (Phase 2) | — |

---

## 9. Open Questions / Risks

| # | Question / Risk | Severity | Notes |
|---|---|---|---|
| R01 | **Email delivery in dev** — SendGrid credentials not configured; forgot-password emails will silently fail locally | High | Need fallback log output for dev + real credentials for staging/prod |
| R02 | **Avatar storage** — No object storage (S3/R2) provisioned; Phase 2 avatar work is blocked | Medium | Provision before Phase 2 avatar sprint |
| R03 | **`IsActive` gap** — No confirmed `IsActive` field on Users table; migration must be validated against live data | High | Query schema before migration; default existing rows to active |
| R04 | **TenantAdmin CC access** — Unclear if TenantAdmins currently can reach CC or only PlatformAdmins | High | Must clarify before building CC user management |
| R05 | **Reset token portal-awareness** — Reset link must point to the correct portal (CC vs Tenant Portal); origin must be embedded in token or email template | Medium | Consider a `portal` field on `PasswordResetTokens` |
| R06 | **`SessionVersion` JWT latency** — Validating session version requires a DB read per request; adds latency | Medium | Cache with short TTL or accept the trade-off for force-logout accuracy |
| R07 | **Email change safety** — Changing email without re-verification is a security risk; must remain Phase 2 | High | Do not allow email changes in MVP profile edit |

---

## 10. Validation Checklist

Use this checklist when validating implementation of UIX-001 features.

### Authentication Flows
- [ ] Login works on both Control Center and Tenant Portal
- [ ] Logout clears session cookie and redirects to login
- [ ] Session restores from valid cookie on app load without re-login
- [ ] Expired/invalid session redirects with reason message

### Password Flows
- [ ] Forgot password accepts email and sends reset link via Notifications service
- [ ] Reset password validates token (single-use, not expired), applies new password
- [ ] Change password validates old password before accepting new one
- [ ] Password policy (length, complexity) is enforced on all password create/change flows
- [ ] Reset token is hashed before storage; raw token only in email

### Profile
- [ ] Profile display shows name, email, role, last login
- [ ] Profile edit saves name and phone successfully
- [ ] Email field is read-only in MVP (no unverified email change)
- [ ] Org, member role, and product entitlements display correctly on Tenant Portal profile

### Profile Photo
- [ ] Upload endpoint accepts JPEG/PNG/WebP, rejects other types (Phase 2)
- [ ] Update replaces previous photo (Phase 2)
- [ ] Remove clears `AvatarUrl` and reverts to initials/default (Phase 2)

### Admin Account Controls (Control Center)
- [ ] User list is tenant-scoped for TenantAdmin, cross-tenant for PlatformAdmin
- [ ] Activate re-enables an inactive user successfully
- [ ] Deactivate prevents the user from logging in
- [ ] Resend invite sends onboarding email to the user
- [ ] Admin-triggered reset sends a reset email to the user without exposing the password
- [ ] Membership/role panel shows correct org and role for the user

### Tenant Self-Service Controls (Tenant Portal)
- [ ] No admin controls (deactivate, role assign, etc.) are accessible in Tenant Portal
- [ ] Profile page is scoped to current user only
- [ ] Change password works from the profile page

### API Coverage
- [ ] All 22 defined endpoints are implemented (14 MVP, 8 Phase 2)
- [ ] MVP endpoints (A01–A11, A14–A17, A20–A21) are all live before Phase 2 work begins
- [ ] Admin endpoints enforce role-based scope (TenantAdmin cannot access other tenants)

### MVP Clarity
- [ ] All MVP features are implemented before Phase 2 begins
- [ ] Phase 2 features have no partial UI fragments in MVP release
- [ ] Avatar storage is provisioned before Phase 2 avatar work starts

---

## 11. Recommended Implementation Prompts

Use these as sequential follow-up prompts for implementation.

**Prompt 1 — Backend: User profile fields + password flows**
Implement forgot password, reset password, and change password endpoints in the Identity service. Create the `PasswordResetTokens` table, generate and hash tokens, send reset emails via the Notifications service, enforce password policy. Add `FirstName`, `LastName`, `PhoneNumber`, `LastLoginAtUtc`, `SessionVersion`, `IsActive` to the Users table. Add GET/PUT `/users/me/profile` endpoints.

**Prompt 2 — Control Center: Admin account features**
Build the admin profile page in the Control Center at `/account/profile` with name/phone edit and change password form. Build forgot/reset password pages in the CC. Build the user management list at `/users` with activate/deactivate, resend invite, and admin-triggered password reset actions.

**Prompt 3 — Tenant Portal: Self-service account features**
Build the `/profile` page in the Tenant Portal with name/phone edit, read-only org/role/product display, and a change password section. Build the `/forgot-password` and `/reset-password` pages as unauthenticated routes with tenant branding applied.

**Prompt 4 — Control Center: User detail + membership panel**
Build the user detail view in the CC with a read-only membership/role panel, status history, and last login. Wire activate/deactivate/resend invite/trigger reset as action buttons.

**Prompt 5 — Phase 2: Profile photo / avatar support**
Add avatar upload, update, and remove to both portals. Wire to POST/PUT/DELETE `/users/me/avatar`. Store in provisioned object storage. Display avatar in TopBar and profile pages.

**Prompt 6 — Phase 2: Force logout + session management**
Add `SessionVersion` increment to force-logout endpoint. Validate `SessionVersion` in JWT middleware. Build session listing UI at `/account/security` in both portals.

**Prompt 7 — Phase 2: MFA enrolment and enforcement**
Design and implement TOTP MFA enrolment with QR code and backup codes. Add MFA enforcement flag to tenant configuration. Build MFA verification step in the login flow.

---

*End of UIX-001 Report*
