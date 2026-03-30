# Step 24 — Event Type Matrix

**Date:** 2026-03-30

## Identity / Auth Events

| Event Type | Category | Severity | Status | Source | Notes |
|---|---|---|---|---|---|
| `user.login.succeeded` | Security | Info | **EMITTING** | `identity-service / auth-api` | IdempotencyKey includes timestamp |
| `user.login.failed` | Security | Warn | **MISSING** | `identity-service / auth-api` | AuthService throws before audit call |
| `user.logout` | Security | Info | **MISSING** | `identity-service / auth-api` | Logout is BFF-side cookie clear; no identity endpoint |
| `user.session.expired` | Security | Info | **MISSING** | `identity-service / auth-api` | JWT expiry not surfaced as event |
| `user.role.assigned` | Administrative | Info | **EMITTING** | `identity-service / admin-api` | IdempotencyKey is stable (no timestamp) |
| `user.role.revoked` | Administrative | Warn | **EMITTING** | `identity-service / admin-api` | IdempotencyKey is stable |
| `user.invited` | Administrative | Info | **MISSING** | `identity-service / admin-api` | User invitation endpoint not assessed |
| `user.deactivated` | Administrative | Warn | **MISSING** | `identity-service / admin-api` | User deactivation endpoint not assessed |
| `user.created` | Administrative | Info | **MISSING** | `identity-service / admin-api` | CreateUser endpoint writes legacy AuditLog only |
| `tenant.created` | Administrative | Info | **MISSING** | `identity-service / admin-api` | Tenant creation endpoint not assessed |
| `product.access.changed` | Administrative | Info | **MISSING** | `identity-service / admin-api` | Entitlement update writes legacy only |

## CareConnect Events

| Event Type | Category | Severity | Status | Notes |
|---|---|---|---|---|
| `careconnect.referral.created` | Business | Info | **MISSING** | DI wired, IAuditEventClient not injected into ReferralService |
| `careconnect.referral.updated` | Business | Info | **MISSING** | Same |
| `careconnect.referral.status.changed` | Business | Info | **MISSING** | ReferralStatusHistory table exists but doesn't emit canonical event |
| `careconnect.appointment.scheduled` | Business | Info | **MISSING** | DI wired, not injected into AppointmentService |
| `careconnect.appointment.updated` | Business | Info | **MISSING** | Same |
| `careconnect.appointment.cancelled` | Business | Warn | **MISSING** | Same |
| `careconnect.provider.linked` | Administrative | Info | **MISSING** | Provider → Organization linkage changes |
| `careconnect.facility.linked` | Administrative | Info | **MISSING** | Facility → Organization linkage changes |

## Fund Events (not assessed)

| Event Type | Category | Severity | Status | Notes |
|---|---|---|---|---|
| `fund.lien.created` | Business | Info | **NOT ASSESSED** | Fund service not reviewed |
| `fund.disbursement.approved` | Business | Info | **NOT ASSESSED** | — |
| `fund.document.uploaded` | Business | Info | **NOT ASSESSED** | — |

## Platform Events

| Event Type | Category | Severity | Status | Notes |
|---|---|---|---|---|
| `platform.admin.login` | Security | Info | Captured via `user.login.succeeded` | PlatformAdmin flag in Actor scope |
| `platform.settings.changed` | Administrative | Warn | **MISSING** | Settings are stub (no DB yet) |
| `platform.maintenance.toggled` | Administrative | Warn | **MISSING** | — |

## Event Idempotency Strategy

| Event Type | Key Strategy | Rationale |
|---|---|---|
| `user.login.succeeded` | `ForWithTimestamp(now, service, eventType, userId)` | Same user can log in multiple times; timestamp makes each event unique |
| `user.role.assigned` | `For(service, eventType, userId, roleId)` | Assignment is a state change; same userId+roleId should not produce duplicate records |
| `user.role.revoked` | `For(service, eventType, userId, roleId)` | Same as above |
| Future: `referral.created` | `For(service, eventType, referralId)` | Referral ID is globally unique |
| Future: `user.login.failed` | `ForWithTimestamp(...)` | Multiple failures can occur; timestamp needed |
