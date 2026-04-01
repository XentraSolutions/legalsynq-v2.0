# UIX-003-03 — Access Control Backend Completion
## Implementation Report

**Date**: 2026-04-01  
**Scope**: Full-stack — Identity microservice (C# .NET 8) + Control Center BFF (Next.js 14)

---

## Overview

UIX-003-03 delivers real admin security operations for the LegalSynq Control Center:
lock/unlock accounts, force-logout (session revocation), admin-triggered password reset,
and a live security panel on the user detail page.

---

## T001 — Backend Domain & Database

### Files changed
| File | Change |
|------|--------|
| `Identity.Domain/User.cs` | Added `IsLocked`, `LockedAtUtc?`, `LockedByAdminId?`, `LastLoginAtUtc?`, `SessionVersion` fields; `Lock()`, `Unlock()`, `RecordLogin()`, `IncrementSessionVersion()` methods |
| `Identity.Domain/PasswordResetToken.cs` | New domain class — SHA-256 hex hash stored; status enum `PENDING / USED / EXPIRED / REVOKED` |
| `Identity.Infrastructure/Persistence/UserConfiguration.cs` | Column mappings for new fields |
| `Identity.Infrastructure/Persistence/PasswordResetTokenConfiguration.cs` | New EF config for `PasswordResetTokens` table |
| `Identity.Infrastructure/Persistence/IdentityDbContext.cs` | Added `DbSet<PasswordResetToken>` |
| `Identity.Infrastructure/Persistence/Migrations/20260401200002_AddUserSecurityFields.cs` | Manual migration: adds columns + `PasswordResetTokens` table |

### Design decisions
- `SessionVersion` is an integer counter incremented on lock, password change, and explicit force-logout. JWTs embed the version; the auth service rejects tokens with an older version.
- Old JWTs without a `session_version` claim are **allowed through** for backward compatibility (zero-downtime deploys).
- Password reset tokens store a SHA-256 hex hash, never the raw token — consistent with `UserInvitation` pattern.
- `Lock()` automatically increments `SessionVersion` (one action locks + revokes all sessions).

---

## T002 — Auth Service Changes

### Files changed
| File | Change |
|------|--------|
| `Identity.Application/Services/JwtTokenService.cs` | Added `session_version` claim to every issued JWT |
| `Identity.Application/Services/AuthService.cs` | `LoginAsync`: checks `IsLocked` → emits `identity.user.login.blocked` event, throws `401`; calls `RecordLogin()` on success. `GetCurrentUserAsync`: validates `IsLocked` and `SessionVersion` on every authenticated request |

### Security flow
```
Login  →  IsLocked?  →  401 + audit event (identity.user.login.blocked)
       →  OK         →  RecordLogin() → JWT with session_version=N

Request →  token.session_version < user.SessionVersion  →  401
        →  user.IsLocked  →  401
        →  OK  →  normal flow
```

---

## T003 — Admin Endpoints

### Files changed
| File | Change |
|------|--------|
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Added `POST /lock`, `/unlock`, `/force-logout`, `/reset-password`, `GET /security`; fixed `GET /users/{id}` to return real `isLocked`, `lastLoginAtUtc`, `sessionVersion` |
| `Identity.Api/Endpoints/AuthEndpoints.cs` | Added `POST /api/auth/password-reset/confirm` |

### Endpoint summary
| Method | Path | Action |
|--------|------|--------|
| `POST` | `/identity/api/admin/users/{id}/lock` | Sets `IsLocked=true`, increments `SessionVersion`, audits |
| `POST` | `/identity/api/admin/users/{id}/unlock` | Sets `IsLocked=false`, audits |
| `POST` | `/identity/api/admin/users/{id}/force-logout` | Increments `SessionVersion` only — account stays active |
| `POST` | `/identity/api/admin/users/{id}/reset-password` | Creates `PasswordResetToken`, increments `SessionVersion`, emits audit |
| `GET`  | `/identity/api/admin/users/{id}/security` | Returns lock state, last login, session version, recent reset tokens |
| `POST` | `/identity/api/auth/password-reset/confirm` | Validates token hash, calls `SetPassword()`, marks token USED |

### Event categories
- Lock / unlock / force-logout → `EventCategory.Security`
- Password reset trigger → `EventCategory.Security`
- Login blocked → `EventCategory.Security`

---

## T004 — Control Center BFF Routes & API Client

### BFF routes created
| Route | Handler |
|-------|---------|
| `POST /api/identity/admin/users/[id]/lock` | Proxies to identity service, revalidates `cc:users` cache |
| `POST /api/identity/admin/users/[id]/unlock` | Proxies to identity service, revalidates `cc:users` cache |
| `POST /api/identity/admin/users/[id]/reset-password` | Proxies to identity service |
| `POST /api/identity/admin/users/[id]/force-logout` | Proxies to identity service |
| `GET  /api/identity/admin/users/[id]/security` | Proxies to identity service security summary |

### API client methods added (`control-center-api.ts`)
- `users.lock(id)`
- `users.unlock(id)`
- `users.resetPassword(id)`
- `users.forceLogout(id)`
- `users.getSecurity(id)` → `UserSecurity | null`

### Type additions (`types/control-center.ts`)
- `UserSecurity` interface — lock state, last login, session version, password reset list
- `PasswordResetSummary` interface — id, status, createdAt, expiresAt, usedAt

---

## T005 — Control Center UI Components

### Files changed
| File | Change |
|------|--------|
| `types/control-center.ts` | Added `UserSecurity`, `PasswordResetSummary`; extended `UserDetail` with `lockedAtUtc?`, `lastLoginAtUtc?`, `sessionVersion?` |
| `lib/api-mappers.ts` | `mapUserDetail` maps `lockedAtUtc`, `lastLoginAtUtc`, `sessionVersion` from snake_case and camelCase |
| `components/users/user-actions.tsx` | Full rewrite — lock, unlock, reset-password, force-logout wired to BFF; confirmation dialogs for destructive actions; inline spinner + feedback; `aria-*` attributes |
| `components/users/user-security-panel.tsx` | **New** — displays lock state (badge + timestamp), last login (relative + absolute), session version with revocation count, recent password resets with status badges |
| `app/tenant-users/[id]/page.tsx` | Fetches `getSecurity` in `Promise.allSettled` alongside existing parallel fetches; renders `<UserSecurityPanel security={security} />` |

### UserActions wiring
| Button | Action | Confirmation |
|--------|--------|-------------|
| Lock | `POST /lock` | Yes — danger dialog |
| Unlock | `POST /unlock` | No (non-destructive) |
| Reset Password | `POST /reset-password` | No |
| Force Logout | `POST /force-logout` | Yes — warning dialog |
| Activate | `POST /activate` | No |
| Deactivate | `POST /deactivate` | Yes — warning dialog |
| Resend Invite | `POST /resend-invite` | No |

### UserSecurityPanel sections
1. **Account Status** — locked/unlocked badge + timestamp when locked
2. **Last Sign In** — relative time ("2h ago") + absolute datetime
3. **Session Version** — `vN (sessions revoked N×)`
4. **Recent Password Resets** — table with `PENDING / USED / EXPIRED / REVOKED` status badges, creation time, expiry/used time

---

## Security & Compliance Notes

- All admin actions are **audit-logged** with `EventCategory.Security` and the acting admin's ID.
- Password reset tokens expire after a configurable window and are single-use.
- `SessionVersion` provides immediate, cryptographically-enforced session revocation without maintaining a server-side token denylist.
- Lock action is atomic: sets `IsLocked=true` AND increments `SessionVersion` in a single `SaveChangesAsync` call — no partial-lock window.
- HIPAA alignment: all user account state changes are traceable to a specific admin actor and timestamp.

---

## TypeScript Status

Pre-existing errors in `notifications/` pages are unrelated to this task and are excluded
per project convention. All new files and edited files compile cleanly.

---

## Testing Guidance

1. **Lock** a user → verify they cannot log in (403 response) → **Unlock** → login succeeds.
2. **Force Logout** → verify existing JWTs return 401 on next request (session version mismatch).
3. **Reset Password** → verify email dispatch (or audit log event) → confirm token via `POST /api/auth/password-reset/confirm`.
4. **Security Panel** → load user detail page → verify lock state, last login, session version, reset history are displayed correctly.
5. Locked user cannot log in even with a valid JWT (IsLocked check in `GetCurrentUserAsync`).
