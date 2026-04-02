# LSCC-01-004 — Admin Queue & Operational Visibility

**Status:** Complete  
**Date:** 2026-04-02  
**Service:** CareConnect (port 5003) + Control Center web (port 5000)

---

## Objective

Surface operational visibility for administrators by:

1. Recording every failed provider access-readiness event in a durable log
2. Exposing three new admin API endpoints (dashboard metrics, blocked-provider queue, referral monitor)
3. Providing three new admin frontend pages linked off a central dashboard

---

## Changes Delivered

### Domain Layer (`CareConnect.Domain`)

| File | Change |
|------|--------|
| `BlockedProviderAccessLog.cs` | New domain entity — factory `Create()`, all fields private-set, email normalised to lowercase |

### Infrastructure Layer (`CareConnect.Infrastructure`)

| File | Change |
|------|--------|
| `Configurations/BlockedProviderAccessLogConfiguration.cs` | EF Core config — `char(36)` GUIDs, `varchar` string lengths, two indexes |
| `Repositories/BlockedAccessLogRepository.cs` | `AddAsync` + `SaveChangesAsync` — concrete implementation |
| `Repositories/IBlockedAccessLogRepository.cs` | Repository interface |
| `Data/CareConnectDbContext.cs` | `BlockedProviderAccessLogs` DbSet added |
| `DependencyInjection.cs` | `IBlockedAccessLogRepository → BlockedAccessLogRepository` (Scoped), `IBlockedAccessLogService → BlockedAccessLogService` (Scoped) |
| `Migrations/20260402010000_LSCC01004_BlockedProviderAccessLog.cs` | Migration — creates `BlockedProviderAccessLogs` table with two indexes |
| `Migrations/CareConnectDbContextModelSnapshot.cs` | Snapshot updated with new entity block |

### Application Layer (`CareConnect.Application`)

| File | Change |
|------|--------|
| `Interfaces/IBlockedAccessLogService.cs` | Interface — `LogAsync(...)` contract; best-effort guarantee documented |
| `Services/BlockedAccessLogService.cs` | Implementation — calls `BlockedProviderAccessLog.Create()` → `AddAsync`; swallows all exceptions with `LogWarning` |

### API Layer (`CareConnect.Api`)

| File | Change |
|------|--------|
| `Endpoints/ReferralEndpoints.cs` | `/api/referrals/access-readiness` — `IBlockedAccessLogService` injected; `_ = blockedLogSvc.LogAsync(...)` fired when `IsProvisioned=false` (fire-and-observe, `CancellationToken.None`) |
| `Endpoints/AdminDashboardEndpoints.cs` | **New file** — three read-only admin endpoints (see below) |
| `Program.cs` | `app.MapAdminDashboardEndpoints()` registered |

#### New Admin Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/admin/dashboard` | Aggregate 24h/7d metrics: referrals created, open referrals, blocked-access counts, distinct blocked users |
| `GET` | `/api/admin/providers/blocked` | Paged blocked-access log grouped by `(UserId, FailureReason)` — latest entry per group, attempt count, remediation link |
| `GET` | `/api/admin/referrals` | Paged cross-tenant referral monitor — provider join, status/tenant/since filters |

All three require `PlatformOrTenantAdmin`.

**Query parameters:**
- `/providers/blocked`: `page`, `pageSize` (max 100), `since` (ISO datetime, default last 7 days)
- `/referrals`: `page`, `pageSize` (max 100), `status`, `tenantId`, `since`

### Frontend (`apps/web`)

| File | Change |
|------|--------|
| `src/types/careconnect.ts` | Added: `DashboardMetrics`, `BlockedProviderLogItem`, `BlockedProviderLogPage`, `AdminReferralItem`, `AdminReferralPage` |
| `src/lib/careconnect-server-api.ts` | Added `adminDashboard` namespace: `getMetrics()`, `getBlockedProviders(params)`, `getReferrals(params)` |
| `src/app/(platform)/careconnect/admin/dashboard/page.tsx` | **New** — metric stat cards (referrals + blocked access), quick-links row |
| `src/app/(platform)/careconnect/admin/providers/blocked/page.tsx` | **New** — amber-accented queue table; Remediate link → provisioning page pre-filled with `?userId=` |
| `src/app/(platform)/careconnect/admin/referrals/page.tsx` | **New** — status-filter pills, table with provider+referrer join, link to existing referral detail page |

### Tests (`CareConnect.Tests`)

| File | Tests |
|------|-------|
| `Domain/BlockedProviderAccessLogTests.cs` | 8 tests covering: field mapping, Id uniqueness, UTC timestamp window, email normalisation, all-null context, null email stored as null, FailureReason verbatim, two-call ID distinctness |
| `Application/BlockedAccessLogServiceTests.cs` | 5 tests covering: happy-path AddAsync called, repository exception swallowed, warning logged on failure, null context still calls AddAsync, CancellationToken forwarded |

**Total new tests: 13 — all passing.**  
Pre-existing `ProviderAvailabilityServiceTests` failures are unchanged.

---

## Database Schema

```sql
CREATE TABLE BlockedProviderAccessLogs (
    Id              char(36)     NOT NULL PRIMARY KEY,
    TenantId        char(36)         NULL,
    UserId          char(36)         NULL,
    UserEmail       varchar(256)     NULL,
    OrganizationId  char(36)         NULL,
    ProviderId      char(36)         NULL,
    ReferralId      char(36)         NULL,
    FailureReason   varchar(128) NOT NULL,
    AttemptedAtUtc  datetime(6)  NOT NULL,

    INDEX IX_BlockedProviderAccessLogs_UserId_AttemptedAtUtc (UserId, AttemptedAtUtc),
    INDEX IX_BlockedProviderAccessLogs_AttemptedAtUtc        (AttemptedAtUtc)
);
```

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| Fire-and-observe (`_ = logSvc.LogAsync(...)`) in access-readiness endpoint | Log failure must never block the provider-facing response |
| `CancellationToken.None` passed to `LogAsync` when used as fire-and-forget | Prevents early cancellation of the log write if the HTTP request completes first |
| Grouped view in `/providers/blocked` (by UserId + FailureReason) | Deduplicates repeated attempts from same user — admin sees one row per failure mode, not one row per attempt |
| `since` defaults to 7 days for blocked-provider queue | Balances data volume vs. recency; admins can widen with `?since=` if needed |
| Remediation path returned as relative URL | Frontend can construct full URL without knowing the deployment hostname |
| Direct `CareConnectDbContext` queries in admin endpoints | Admin read paths have no domain behaviour — a service layer adds no value here; consistent with existing admin endpoints (`ProviderAdminEndpoints`, `ActivationAdminEndpoints`) |
| Auto-migrate at startup (Development mode) | Existing pattern in `Program.cs` — no manual `dotnet ef database update` step needed in dev |

---

## Rollback Notes

- Drop table: `DROP TABLE BlockedProviderAccessLogs;`
- Remove migration by reverting the migration file and snapshot changes
- The access-readiness endpoint still returns the same response shape with or without the logging call
