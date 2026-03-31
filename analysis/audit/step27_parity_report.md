# Step 27 — Parity Analysis: Legacy vs Canonical Audit Pipeline
**Date:** 2026-03-31  
**Author:** Step 27 Stabilization Pass  
**Status:** COMPLETE

---

## 1. Executive Summary

LegalSynq's platform-audit-event-service operates two ingestion paths:

| Path | Route | Storage | Chain | Auth |
|------|-------|---------|-------|------|
| **Legacy** | `POST /api/auditevents` | `AuditEvents` flat table | Per-source HMAC only | None (open) |
| **Canonical** | `POST /internal/audit/events` | `AuditEventRecords` (chain-linked) | HMAC + PreviousHash + idempotency | ServiceToken / None (dev) |

The canonical pipeline was introduced in Steps 17–20 and has been the sole target for new event producers since Step 22. The legacy controller is **frozen as of Step 27** pending removal in the next major release.

---

## 2. Feature-by-Feature Comparison

### 2.1 Ingestion

| Feature | Legacy (`/api/auditevents`) | Canonical (`/internal/audit/events`) |
|---------|-----------------------------|--------------------------------------|
| Hash-chain integrity | ✗ No `PreviousHash` | ✓ Per-(tenant, sourceSystem) chain |
| Idempotency guard | ✗ No dedup | ✓ `IdempotencyKey` + 409 Conflict |
| Batch ingest | ✗ Single only | ✓ `POST /internal/audit/events/batch` |
| HMAC payload signature | ✓ | ✓ |
| Event category/severity/scope | Partial (flat model) | ✓ Full canonical DTOs |
| `Before`/`After` snapshot | ✗ | ✓ |
| CorrelationId threading | ✗ | ✓ |
| Service token auth | ✗ (always open) | ✓ `IngestAuthMiddleware` |
| Deprecation headers | ✓ (`Deprecation: true`, `Sunset`, `Link`) | N/A |

### 2.2 Query

| Feature | Legacy (via `IAuditEventService`) | Canonical (`GET /audit/events`) |
|---------|-----------------------------------|---------------------------------|
| Tenant-scoped query | Partial (manual filter) | ✓ Full `IQueryAuthorizer` enforcement |
| PlatformAdmin cross-tenant | ✗ | ✓ |
| TenantAdmin isolation (403) | ✗ | ✓ |
| Pagination | ✓ | ✓ |
| CorrelationId filter | ✗ | ✓ |
| EventType filter | ✗ | ✓ |
| Audit-of-audit (`audit.log.accessed`) | ✗ | ✓ |

---

## 3. Canonical Producer Coverage (Step 27)

All 11 canonical event types are now emitted by live producers:

| Event Type | Producer Service | Endpoint |
|------------|-----------------|----------|
| `identity.user.login.succeeded` | Identity | `POST /api/auth/login` |
| `identity.user.login.failed` | Identity | `POST /api/auth/login` |
| `identity.user.logout` | Identity | `POST /api/auth/logout` |
| `identity.user.created` | Identity | `POST /api/users` |
| `identity.user.deactivated` *(NEW Step 27)* | Identity | `PATCH /api/admin/users/{id}/deactivate` |
| `identity.role.assigned` | Identity | `POST /api/admin/users/{id}/roles` |
| `identity.role.removed` | Identity | `DELETE /api/admin/users/{id}/roles/{role}` |
| `careconnect.referral.created` | CareConnect | `POST /api/referrals` |
| `careconnect.referral.updated` | CareConnect | `PUT /api/referrals/{id}` |
| `careconnect.appointment.scheduled` | CareConnect | `POST /api/appointments` |
| `careconnect.appointment.cancelled` | CareConnect | `DELETE /api/appointments/{id}` |

All events use `IdempotencyKey.For(...)` for deterministic deduplication and fire-and-observe dispatch (primary response never blocked by audit emission).

---

## 4. Legacy Freeze Status

**`AuditEventsController` (`POST /api/auditevents`) — FROZEN**

Changes applied in Step 27:
- `[Obsolete]` attribute added to class
- `[DeprecationHeaderFilter]` applied (RFC 8594 `Deprecation: true`, `Sunset: 2026-06-30`, `Link: </internal/audit/events>; rel="successor-version"`)
- All **writes** from `AdminEndpoints` migrated to canonical pipeline (already complete from Step 24)
- Legacy **reads** (`GET /api/admin/audit`) remain during freeze period for backward compatibility

**Removal target:** Next major release (2026 Q3)

---

## 5. Gap Analysis Summary

| Gap | Severity | Resolution |
|-----|----------|-----------|
| `identity.user.deactivated` missing producer | High | Fixed Step 27 Phase B — `PATCH /api/admin/users/{id}/deactivate` |
| Legacy controller open with no deprecation signal | Medium | Fixed Step 27 Phase H — `[DeprecationHeaderFilter]` + `[Obsolete]` |
| No cross-tenant isolation test coverage | High | Fixed Step 27 Phase C — 6 tests verifying 403 enforcement |
| No correlation ID roundtrip test | Medium | Fixed Step 27 Phase D — 4 tests |
| No concurrent chain integrity test | High | Fixed Step 27 Phase E — 4 tests |
| No load/stability test | Medium | Fixed Step 27 Phase F — 3 tests |
| No audit-of-audit test | High | Fixed Step 27 Phase G — 2 tests |

All gaps resolved. No open gaps remain.
