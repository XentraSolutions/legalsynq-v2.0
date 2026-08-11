# Biometric Login — Backend API Documentation

Backend implementation of the biometric login integration described in *Biometric
Login Integration for React Native Expo Mobile Application* (BRD v1.0,
2026-07-31), covering requirements BE-BIO-001 through BE-BIO-025. This document
covers the **new/changed backend surface only** — the mobile app's on-device
SecureStore/Face ID/Touch ID handling is out of scope here and is unchanged by
this document.

## Table of Contents

- [Overview](#overview)
- [Gateway Access](#gateway-access)
- [Security Model](#security-model)
- [Common Models](#common-models)
- [Error Codes](#error-codes)
- [Endpoints](#endpoints)
  - [Login (extended)](#post-identityapiauthlogin-extended)
  - [Refresh Session](#post-identityapiauthsessionrefresh)
  - [Logout](#post-identityapiauthlogout)
  - [Logout All Devices](#post-identityapiauthlogout-all)
  - [List Device Sessions](#get-identityapiauthdevice-sessions)
  - [Revoke Device Session](#delete-identityapiauthdevice-sessionsid)
  - [Enable Biometric Login](#post-identityapiauthdevice-sessionsidbiometricenable)
  - [Disable Biometric Login](#post-identityapiauthdevice-sessionsidbiometricdisable)
- [Rate Limits](#rate-limits)
- [Audit Events](#audit-events)
- [Configuration](#configuration)
- [Backward Compatibility](#backward-compatibility)

---

## Overview

The existing Identity service issued a single stateless, unversioned access
token at login (`POST /identity/api/auth/login` through the gateway) with no
refresh-token concept. This
feature adds an **additive, opt-in** subsystem:

- Device-specific, rotating, opaque refresh tokens (BE-BIO-001/002/006)
- A `DeviceSessions` record per device, tracking biometric-enabled status,
  expiry, and revocation (BE-BIO-010)
- Reuse detection with automatic family-wide revocation on confirmed token
  theft (BE-BIO-007)
- Device-session management (list/revoke/logout-all) for account security
  settings (BE-BIO-014/015/016)

Every new Identity service endpoint lives internally under `/api/auth/...`
and is exposed publicly through the gateway under `/identity/api/auth/...`.
**No existing endpoint's behavior changes** — see
[Backward Compatibility](#backward-compatibility).

## Gateway Access

Mobile and other external clients must call these APIs through the gateway.
The endpoint examples in this document therefore use gateway paths rather
than direct Identity service paths.

| Environment | Gateway base URL |
|---|---|
| Local development | `http://localhost:5010` |
| Deployed environments | The environment's configured LegalSynq gateway origin |

Gateway routing removes the `/identity` prefix before forwarding requests to
the Identity service:

```text
Public gateway route:   POST /identity/api/auth/session/refresh
Internal service route: POST /api/auth/session/refresh
```

Clients must not call the Identity service port directly.

## Security Model

- **The backend never trusts a client-asserted "biometric succeeded" claim.**
  The mobile app's biometric prompt and SecureStore gate are purely on-device;
  the backend's only signal is possession of a valid, unrevoked, correctly
  rotated refresh token (BRD §12.6, SEC-006).
- Refresh tokens are 256-bit cryptographically random values; only their
  SHA-256 hash is ever persisted (BE-BIO-002/003).
- Every successful refresh **rotates** the token — the old one is immediately
  invalid (BE-BIO-006, SEC-006).
- Resubmitting an already-rotated token outside a short grace window is
  treated as confirmed theft: the entire device session and every token
  generation in its family are revoked, and a `Critical`-severity audit event
  is recorded (BE-BIO-007). Externally, this looks identical to any other
  invalid token — the API never reveals that reuse was specifically detected
  (SEC-010).
- The `biometricEnabled` flag on a device session is **administrative
  metadata only**. It is never checked as an authorization condition (BRD
  §12.6, SEC-006).
- `logout-all` and revoking a device *other than the caller's own current
  session* require step-up: the calling session's `LastPrimaryAuthenticationAtUtc`
  must be within a configurable recency window, or the request is rejected
  with `SESSION_REAUTHENTICATION_REQUIRED` (SEC-014).

## Common Models

**DeviceInfo** — client-supplied device metadata, sent to opt into
device-session issuance. Never carries biometric data.

```json
{
  "platform": "ios",
  "appVersion": "1.4.2",
  "osVersion": "17.5",
  "deviceDisplayName": "Ralph's iPhone"
}
```

**RefreshTokenResponse** — returned by both initial issuance (via login) and
rotation (via refresh):

```json
{
  "accessToken": "<jwt>",
  "accessTokenExpiresAtUtc": "2026-08-07T12:15:00Z",
  "refreshToken": "<opaque base64 token>",
  "refreshTokenExpiresAtUtc": "2026-11-05T12:00:00Z",
  "deviceSessionId": "0198e2b1-...-...-...-..."
}
```

`refreshTokenExpiresAtUtc` is the earlier of the session's absolute and
inactivity expiry — both are enforced independently server-side regardless of
what this field communicates to the client.

**DeviceSessionSummary** — returned by the device-session list endpoint. Never
includes token material.

```json
{
  "id": "0198e2b1-...",
  "deviceDisplayName": "Ralph's iPhone",
  "platform": "ios",
  "lastUsedAtUtc": "2026-08-07T11:58:02Z",
  "createdAtUtc": "2026-07-30T09:12:00Z",
  "isCurrentDevice": true,
  "biometricEnabled": true
}
```

## Error Codes

All error responses use the standard `Results.Problem` shape with an
additional machine-readable `errorCode` extension:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "...",
  "status": 401,
  "detail": "The saved session has expired.",
  "errorCode": "REFRESH_TOKEN_EXPIRED"
}
```

| Code | HTTP Status | Meaning |
|---|---|---|
| `REFRESH_TOKEN_INVALID` | 401 | Token doesn't match the session's current token. Also returned (identically) for confirmed reuse — see [Security Model](#security-model). |
| `REFRESH_TOKEN_EXPIRED` | 401 | Session's absolute or inactivity expiry has passed. |
| `REFRESH_TOKEN_REVOKED` | 401 | Reserved for future use; currently surfaced as `DEVICE_SESSION_REVOKED`. |
| `DEVICE_SESSION_REVOKED` | 401 | Session was revoked (logout, biometric-disable, admin action, or reuse detection). |
| `DEVICE_SESSION_NOT_FOUND` | 404 | Session ID doesn't exist, or (for owner-scoped endpoints) doesn't belong to the caller — intentionally indistinguishable from "doesn't exist" to avoid confirming existence to a non-owner. |
| `ACCOUNT_DISABLED` | 403 | User account is inactive. |
| `ACCOUNT_LOCKED` | 403 | User account is administratively locked. |
| `SESSION_REAUTHENTICATION_REQUIRED` | 403 | Step-up required — the calling device session's last successful authentication is outside the configured recency window. |
| `RATE_LIMIT_EXCEEDED` | 429 | Rate limit hit (returned by the platform's standard rate-limiter rejection, not a custom body). |

Production error responses never reveal internal token-validation state
beyond the codes above (SEC-010).

## Endpoints

### `POST /identity/api/auth/login` (extended)

Unchanged path and unchanged response shape for every existing caller. Add an
optional `deviceInfo` object to the request body to additionally receive a
refresh token:

**Request**
```json
{
  "email": "user@example.com",
  "password": "...",
  "tenantCode": "acme",
  "deviceInfo": {
    "platform": "ios",
    "appVersion": "1.4.2",
    "osVersion": "17.5",
    "deviceDisplayName": "Ralph's iPhone"
  }
}
```

**Response** (existing fields unchanged; three new nullable fields added)
```json
{
  "accessToken": "<jwt>",
  "expiresAtUtc": "2026-08-07T12:00:00Z",
  "user": { "...": "..." },
  "tenants": [ "..." ],
  "refreshToken": "<opaque token>",
  "refreshTokenExpiresAtUtc": "2026-11-05T12:00:00Z",
  "deviceSessionId": "0198e2b1-..."
}
```

If `deviceInfo` is omitted, `refreshToken`/`refreshTokenExpiresAtUtc`/
`deviceSessionId` are `null` and the response is byte-for-byte identical to
the pre-existing shape (BE-BIO-024). Device-session creation is best-effort:
if it fails for any reason, primary login still succeeds — the client simply
receives no refresh token for that attempt.

Rate limit: `auth-login` (unchanged, 20/5min per IP).

---

### `POST /identity/api/auth/session/refresh`

**Auth:** anonymous (the access token may be legitimately expired — that's
the point of calling this).
**Rate limit:** `auth-refresh` (30/5min per IP).

**Request**
```json
{ "refreshToken": "<opaque token>", "deviceSessionId": "0198e2b1-..." }
```

**Response** `200 OK` — a [RefreshTokenResponse](#common-models). The
returned `refreshToken` **replaces** the submitted one; the submitted token
is immediately invalid.

**Errors:** `REFRESH_TOKEN_INVALID`, `REFRESH_TOKEN_EXPIRED`,
`DEVICE_SESSION_REVOKED`, `DEVICE_SESSION_NOT_FOUND`, `ACCOUNT_DISABLED`,
`ACCOUNT_LOCKED`, `RATE_LIMIT_EXCEEDED`.

---

### `POST /identity/api/auth/logout`

Revokes the given device session and its refresh token when refresh credentials
are supplied. Existing web callers may continue sending no request body; that
retains the legacy stateless, cookie-deletion-only behavior.

**Auth:** anonymous (access token may be expired).
**Rate limit:** `auth-logout` (20/5min per IP).
**Idempotent** — repeated calls return `204` without error.

**Request** — possession of the refresh token authenticates the logout even when the access token is expired.
```json
{ "refreshToken": "<opaque token>", "deviceSessionId": "0198e2b1-..." }
```

**Response:** `204 No Content`.

---

### `POST /identity/api/auth/logout-all`

Revokes every active device session for the caller, including the one making
the call. Always requires step-up.

**Auth:** authenticated (Bearer JWT) + step-up (`SESSION_REAUTHENTICATION_REQUIRED` if not satisfied).
**Rate limit:** `auth-logout-all` (5/15min per IP).

**Response** `200 OK`
```json
{ "revokedCount": 3 }
```

---

### `GET /identity/api/auth/device-sessions`

Lists the caller's active device sessions. Never includes token material.

**Auth:** authenticated.
**Rate limit:** `auth-device-session-list` (30/5min per IP).

**Response** `200 OK` — array of [DeviceSessionSummary](#common-models).

---

### `DELETE /identity/api/auth/device-sessions/{id}`

Revokes a specific device session. Revoking the caller's **own current**
session requires no step-up (they're already using it); revoking any
**other** device requires step-up.

**Auth:** authenticated. IDOR-checked — a session ID belonging to another
user returns `404 DEVICE_SESSION_NOT_FOUND`, not `403`.
**Rate limit:** `auth-device-session-revoke` (15/5min per IP).

**Response:** `204 No Content`.
**Errors:** `DEVICE_SESSION_NOT_FOUND`, `SESSION_REAUTHENTICATION_REQUIRED`.

---

### `POST /identity/api/auth/device-sessions/{id}/biometric/enable`

Marks a device session as biometric-enabled. **Administrative flag only** —
see [Security Model](#security-model).

**Auth:** authenticated. IDOR-checked.
**Rate limit:** `auth-biometric-toggle` (20/5min per IP).

**Response:** `204 No Content`.
**Errors:** `DEVICE_SESSION_NOT_FOUND`.

---

### `POST /identity/api/auth/device-sessions/{id}/biometric/disable`

Disables the biometric flag **and revokes the session's refresh token** in
the same operation (BE-BIO-012).

**Auth:** authenticated. IDOR-checked.
**Rate limit:** `auth-biometric-toggle` (20/5min per IP).
**Idempotent** — repeated calls return `204` without error.

**Response:** `204 No Content`.
**Errors:** `DEVICE_SESSION_NOT_FOUND`.

## Rate Limits

| Policy | Limit | Window | Partition |
|---|---|---|---|
| `auth-refresh` | 30 | 5 min | IP + account/device/client context |
| `auth-logout` | 20 | 5 min | IP + account/device/client context |
| `auth-logout-all` | 5 | 15 min | IP + account/device/client context |
| `auth-biometric-toggle` | 20 | 5 min | IP + account/device/client context |
| `auth-device-session-list` | 30 | 5 min | IP + account/device/client context |
| `auth-device-session-revoke` | 15 | 5 min | IP + account/device/client context |

Client IP is resolved from `X-Forwarded-For` (set by the gateway), matching
the existing `auth-login`/`auth-forgot-password` policies.

Refresh/session metrics are emitted from the `LegalSynq.Identity.DeviceSessions`
meter. Confirmed reuse also emits a critical security log and audit event for alerting.

## Database Migration

`20260810113000_AddBiometricDeviceSessions` creates the prefixed device-session
and refresh-token-ledger tables. The previous startup DDL guard has been removed.

## Audit Events

All events are emitted fire-and-observe (never gate the response) via the
platform audit client, `EventCategory=Security`, tagged for filtering.
Payloads never contain raw tokens, token hashes, passwords, or biometric
data — only opaque IDs and non-sensitive metadata.

| Event Type | Severity | Emitted On |
|---|---|---|
| `identity.session.device_created` | Info | Device session created at login |
| `identity.session.refresh_succeeded` | Info | Successful rotation |
| `identity.session.refresh_failed` | Warn | Any refresh failure (not-found/expired/revoked/account-disabled) |
| `identity.session.refresh_reused` | **Critical** | Confirmed token reuse — structured with `tokenFamilyId`/`deviceSessionId`/`userId` for future alerting |
| `identity.session.device_revoked` | Info/Warn | Any session revocation |
| `identity.user.biometric_enabled` | Info | Biometric flag enabled |
| `identity.user.biometric_disabled` | Info | Biometric flag disabled (+ session revoked) |
| `identity.user.logged_out` | Info | `POST /identity/api/auth/logout` |
| `identity.user.logged_out_all_sessions` | Warn | `POST /identity/api/auth/logout-all` |

## Configuration

`appsettings.json` → `RefreshTokenPolicy` section (tunable without a
redeploy; defaults below are the BRD's recommended starting values, pending
final Security/Product sign-off per BRD §24):

```json
"RefreshTokenPolicy": {
  "RefreshInactivityDays": 30,
  "RefreshAbsoluteDays": 90,
  "MaxActiveSessionsPerUser": 10,
  "StepUpWindowMinutes": 15,
  "ReuseGraceSeconds": 10
}
```

`Jwt:RefreshedAccessTokenExpiryMinutes` (default `15`) controls the lifetime
of access tokens minted by the refresh endpoint — deliberately separate from
`Jwt:ExpiryMinutes` (`60`), which remains unchanged for the existing
password-login path.

## Backward Compatibility

- `POST /identity/api/auth/login` and `GET /identity/api/auth/me` remain
  unchanged for existing callers.
- `POST /identity/api/auth/logout` remains a stateless no-op for callers with
  no request body, so the Next.js BFFs continue to work as before. Biometric
  clients can supply `refreshToken` and `deviceSessionId` to revoke their
  device session through that same unversioned endpoint.
- The existing anonymous gateway route for `/identity/api/auth/{**catch-all}`
  forwards login, refresh, and logout. Authenticated device-session management
  endpoints still enforce JWT authorization independently in Identity.
