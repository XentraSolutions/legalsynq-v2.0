# LSCC-01-006: Automatic Tenant Provisioning and Subdomain-Based Login

**Status:** Complete  
**Date:** 2026-04-07  
**Scope:** Identity service (domain, application, infrastructure, API), Control Center UI, Web login

---

## 1. Summary

This feature adds automatic DNS provisioning when a tenant is created and enables subdomain-based login routing. Platform admins can optionally specify a preferred subdomain during tenant creation; if omitted, one is derived from the tenant name. The system provisions a CNAME record in AWS Route53, tracks provisioning status with retry support, and exposes the full lifecycle through the Control Center UI. In production, the login page resolves the tenant from the request hostname, eliminating the need for a manual tenant code.

---

## 2. Deliverables

### 2.1 Domain Model (T001)

| Item | File | Detail |
|------|------|--------|
| `ProvisioningStatus` enum | `Identity.Domain/Tenant.cs` | `Pending`, `InProgress`, `Active`, `Failed` |
| Tenant provisioning fields | `Identity.Domain/Tenant.cs` | `Subdomain`, `ProvisioningStatus`, `LastProvisioningAttemptUtc`, `ProvisioningFailureReason` |
| `SlugGenerator` utility | `Identity.Domain/Tenant.cs` (static class) | `GenerateFromName()`, `Normalize()`, `Validate()` |
| Reserved slug list | `SlugGenerator` | `www`, `api`, `app`, `admin`, `mail`, `ftp`, `login`, `status` |
| Slug rules | `SlugGenerator` | 3-63 chars, lowercase alphanumeric + hyphens, no leading/trailing hyphens, regex-validated |
| `Tenant.Create()` overload | `Identity.Domain/Tenant.cs` | Accepts optional `preferredSubdomain`, normalizes or auto-generates |
| `SetProvisioningStatus()` | `Identity.Domain/Tenant.cs` | State-machine method for status transitions |
| `SetSubdomain()` | `Identity.Domain/Tenant.cs` | Idempotent subdomain assignment |
| Unique slug resolution | `TenantProvisioningService` | Appends numeric suffix (`-2`, `-3`, ...) on collision |

### 2.2 Provisioning Service (T002)

| Item | File | Detail |
|------|------|--------|
| `ITenantProvisioningService` | `Identity.Application/Interfaces/ITenantProvisioningService.cs` | `ProvisionAsync()`, `RetryProvisioningAsync()` |
| `ProvisioningResult` record | Same file | `Success`, `Hostname`, `ErrorMessage` |
| `TenantProvisioningService` | `Identity.Infrastructure/Services/TenantProvisioningService.cs` | Orchestrates: slug resolution → DNS creation → `TenantDomain` upsert → status updates |
| Idempotent design | Implementation | Checks existing `TenantDomain` before DNS call; re-entrant for retry |
| `IDnsService` integration | Existing interface | `CreateCnameRecordAsync()` called with generated hostname |
| DI registration | `DependencyInjection.cs` | `TenantProvisioningService` as Scoped |

### 2.3 API Endpoints (T003)

| Endpoint | Method | Change |
|----------|--------|--------|
| `POST /api/admin/tenants` | CreateTenant | Accepts optional `PreferredSubdomain`; validates via `SlugGenerator`; invokes provisioning; emits per-step audit events |
| `POST /api/admin/tenants/{id}/provisioning/retry` | RetryProvisioning | New endpoint; retries failed provisioning; emits audit event |
| `GET /api/admin/tenants` | ListTenants | Response includes `subdomain`, `provisioningStatus` |
| `GET /api/admin/tenants/{id}` | GetTenant | Response includes all provisioning fields |

**Audit events emitted:**
- `tenant.provisioning.started` — slug resolved, DNS call initiated
- `tenant.provisioning.completed` — DNS record created, domain saved
- `tenant.provisioning.failed` — error recorded with reason
- `tenant.provisioning.retry` — manual retry initiated

### 2.4 EF Migration

| Item | File |
|------|------|
| Migration | `20260407000001_AddTenantProvisioningFields.cs` |

Adds columns: `Subdomain` (varchar 63, nullable, unique filtered index), `ProvisioningStatus` (int, default 0), `LastProvisioningAttemptUtc` (datetime, nullable), `ProvisioningFailureReason` (varchar 500, nullable).

Seed SQL sets `ProvisioningStatus = 2` (Active) for the existing LEGALSYNQ tenant.

### 2.5 Control Center UI (T004)

| Component | File | Detail |
|-----------|------|--------|
| Create Tenant Modal | `create-tenant-modal.tsx` | New optional "Subdomain" text input with helper text |
| Tenant List | `tenant-detail-card.tsx` | DNS status column/badge showing provisioning state |
| Tenant Detail Card | `tenant-detail-card.tsx` | Provisioning status panel with subdomain, status badge, failure reason, last attempt |
| Retry Button | `retry-provisioning-button.tsx` | Client component with loading state; calls `retryProvisioning` server action |
| Server Actions | `actions.ts` | `retryProvisioning(tenantId)` action |
| API Client | `control-center-api.ts` | `retryTenantProvisioning()` method |

### 2.6 Login Page (T005)

| Item | File | Detail |
|------|------|--------|
| Host-based resolution | `apps/web/src/app/api/auth/login/route.ts` | `extractTenantCodeFromHost()` extracts subdomain from `Host` header |
| Dev fallback | `login-form.tsx` | Tenant Code field shown only when `NEXT_PUBLIC_ENV === 'development'` |
| Error handling | `login-form.tsx` | Clear error message when tenant cannot be resolved from subdomain |

---

## 3. Architecture

```
┌─────────────────────┐
│  Control Center UI  │
│  (Next.js :5004)    │
│  create / retry     │
└────────┬────────────┘
         │ POST /api/admin/tenants
         │ POST /api/admin/tenants/{id}/provisioning/retry
         ▼
┌─────────────────────┐
│  Identity API       │
│  AdminEndpoints     │──── Audit Events ──▶ PlatformAuditEventService
│  (:5001)            │
└────────┬────────────┘
         │ ITenantProvisioningService
         ▼
┌─────────────────────┐      ┌──────────────────┐
│ TenantProvisioning  │─────▶│ IDnsService      │
│ Service (Scoped)    │      │ (Route53, Singlet│
│                     │      │  on)             │
│ - slug resolution   │      └──────────────────┘
│ - status tracking   │             │
│ - TenantDomain save │             ▼
└────────┬────────────┘      AWS Route53 CNAME
         │
         ▼
┌─────────────────────┐
│ IdentityDbContext    │
│ Tenants table        │
│ TenantDomains table  │
└─────────────────────┘

┌─────────────────────┐
│  Web Login (:5000)  │
│  BFF /api/auth/login│──── extractTenantCodeFromHost()
│  login-form.tsx     │     subdomain → tenant code
└─────────────────────┘
```

---

## 4. Checklist Verification

| # | Requirement | Status |
|---|-------------|--------|
| 1 | Subdomain generated from tenant name or preferred input | Done |
| 2 | Slug validation (length, charset, reserved names) | Done |
| 3 | Unique slug collision resolution with suffix | Done |
| 4 | DNS provisioning via AWS Route53 (CNAME) | Done |
| 5 | Provisioning status tracking (Pending → InProgress → Active/Failed) | Done |
| 6 | Retry endpoint for failed provisioning | Done |
| 7 | Audit events for provisioning lifecycle | Done |
| 8 | Idempotent provisioning (safe to re-run) | Done |
| 9 | EF Core migration with seed data | Done |
| 10 | Control Center: subdomain input in create modal | Done |
| 11 | Control Center: provisioning status display | Done |
| 12 | Control Center: retry button for failed provisioning | Done |
| 13 | Login: host-based tenant resolution in production | Done |
| 14 | Login: dev fallback with manual tenant code | Done |
| 15 | Build succeeds with 0 errors, 0 warnings | Done |

---

## 5. Configuration

| Secret / Env Var | Purpose |
|------------------|---------|
| `Route53__HostedZoneId` | AWS Route53 hosted zone for DNS records |
| `Route53__BaseDomain` | Base domain (e.g., `legalsynq.com`) |
| `Route53__RecordValue` | CNAME target value (e.g., load balancer) |
| `NEXT_PUBLIC_ENV` | Controls dev vs prod behavior on login page |

---

## 6. Files Modified

| File | Lines | Change Type |
|------|-------|-------------|
| `Identity.Domain/Tenant.cs` | 189 | Modified — enum, fields, slug generator, domain methods |
| `Identity.Application/Interfaces/ITenantProvisioningService.cs` | 14 | New — service contract |
| `Identity.Infrastructure/Services/TenantProvisioningService.cs` | 122 | New — orchestration service |
| `Identity.Infrastructure/DependencyInjection.cs` | — | Modified — DI registration |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | 4132 | Modified — CreateTenant, RetryProvisioning, response DTOs |
| `Identity.Infrastructure/Persistence/Migrations/20260407000001_*.cs` | 69 | New — schema migration |
| `apps/control-center/.../create-tenant-modal.tsx` | 402 | Modified — subdomain input |
| `apps/control-center/.../tenant-detail-card.tsx` | 149 | Modified — provisioning panel |
| `apps/control-center/.../retry-provisioning-button.tsx` | 57 | New — client component |
| `apps/control-center/.../actions.ts` | 81 | Modified — retry action |
| `apps/control-center/.../control-center-api.ts` | 1643 | Modified — retry API method |
| `apps/web/.../login-form.tsx` | 188 | Modified — dev/prod tenant resolution |
| `apps/web/.../login/route.ts` | 127 | Modified — host extraction |

---

## 7. Code Review Fixes

Three issues identified during code review were resolved in the same session:

| Issue | Severity | Fix |
|-------|----------|-----|
| Subdomain saved before collision resolution, causing unique-index failures on slug conflicts | Critical | `Tenant.Create()` now sets `Subdomain = null` and stores preferred input in a `[NotMapped] PreferredSubdomain` property; provisioning service resolves uniqueness before first DB write of subdomain |
| Login BFF accepted explicit `tenantCode` in production, bypassing host-based tenant binding | Serious | Server-side route now ignores `explicitTenantCode` when `NEXT_PUBLIC_ENV !== 'development'`, enforcing host-derived tenant in prod |
| Retry endpoint returned inconsistent DTO shape for already-Active tenants | Medium | Already-Active branch now returns `{ success, provisioningStatus, hostname, error }` matching the standard response contract |

---

## 8. Known Limitations

1. **DNS propagation is not verified** — The system marks provisioning as Active once the Route53 API call succeeds; actual DNS propagation may take minutes.
2. **No automated DNS deletion** — Deactivating a tenant does not remove the CNAME record.
3. **Route53 credentials required** — DNS provisioning will fail gracefully (status = Failed) if AWS secrets are not configured, with retry available.
4. **Browser hydration warning** — A pre-existing SSR hydration mismatch in the web app produces a console warning; this is unrelated to this feature.
