# LSCC-011 — Activation Funnel Analytics

**Status:** Complete  
**Date:** 2026-03-31  
**Scope:** CareConnect analytics service + admin frontend page

---

## 1. Summary

### What Was Implemented

A thin analytics layer on top of LSCC-008, LSCC-009, and LSCC-010 data. Provides:

- A backend service (`ActivationFunnelAnalyticsService`) that reads directly from existing DB tables — no new analytics tables or event pipelines
- A REST endpoint: `GET /api/admin/analytics/funnel?days=7|30|90`
- An admin-only page at `/careconnect/admin/analytics/activation` with:
  - Summary metric cards (4-up grid)
  - Visual funnel breakdown with bar-width proportions
  - Supporting counts with drilldown links
  - URL-based date range filter (Last 7 / 30 / 90 days)
- A dedicated test suite (`ActivationFunnelAnalyticsTests.cs`) covering rate math and edge cases

### What Remains Incomplete

| Stage | Status | Reason |
|---|---|---|
| ReferralViewed | ❌ Not computed | Audit-log only — not persisted to DB |
| AutoProvisionFailed (direct) | ⚠️ Proxied | FallbackPending = requests still Pending in range |
| AutoProvisionSucceeded (direct) | ⚠️ Proxied | `ApprovedByUserId IS NULL` + Status=Approved |

These are documented as null/— in the UI and clearly explained in footnotes.

---

## 2. Metric Definitions

| Metric | Source | Filter |
|---|---|---|
| **Referrals Sent** | `Referrals.CreatedAtUtc` | Date range |
| **Referrals Accepted** | `Referrals.Status IN (Accepted, Scheduled, Completed)` + `CreatedAtUtc` | Date range |
| **Activation Started** | `ActivationRequests.CreatedAtUtc` | Date range |
| **Auto Provision Succeeded** | `ActivationRequests` where `Status=Approved` AND `ApprovedByUserId IS NULL` | Date range (by `CreatedAtUtc`) |
| **Admin Approved** | `ActivationRequests` where `Status=Approved` AND `ApprovedByUserId IS NOT NULL` | Date range (by `CreatedAtUtc`) |
| **Fallback Pending** | `ActivationRequests` where `Status=Pending` AND created in range | Date range |
| **Total Pending (snapshot)** | `ActivationRequests.Status = Pending` | None — current state |
| **Total Approved (snapshot)** | `ActivationRequests.Status = Approved` | None — current state |
| **Referral Viewed** | N/A — audit log only | N/A |

---

## 3. Aggregation Logic

All counts are single SQL `COUNT(*)` queries issued in parallel over `CareConnectDbContext`. No joins are required.

### Deduplication Strategy

`ActivationRequest` is already deduplicated by `(ReferralId, ProviderId)` at the domain level (LSCC-009). A single provider submitting the form twice produces one ActivationRequest — so `ActivationStarted` counts are naturally deduplicated.

`Referrals` are unique by identity. `Referrals Accepted` counts referrals whose status advanced to Accepted/Scheduled/Completed AND were created in the date range. There is no double-counting concern — each referral belongs to exactly one bucket.

### Known Approximation: Referrals Accepted

`Referrals.CreatedAtUtc` is used as the anchor for date filtering, not `AcceptedAtUtc` (which doesn't exist as a separate column). This means "Referrals Accepted in range" actually means "Referrals created in range that have since been accepted". This is correct for funnel analysis (following cohort behavior from creation date).

### Known Approximation: FallbackPending

Requests still Pending after creation = "auto-provision fell back to queue and is awaiting admin". This under-counts fallbacks if an admin quickly processes them; it over-counts if some requests are slow but eventually auto-provisioned via retry (unlikely). Documented clearly in the UI.

---

## 4. Conversion Rate Calculations

```
ActivationRate          = ActivationStarted / ReferralsSent
AutoProvisionSuccessRate = AutoProvisionSucceeded / ActivationStarted
FallbackRate            = FallbackPending / ActivationStarted
OverallApprovalRate     = (AutoProvisionSucceeded + AdminApproved) / ActivationStarted
ReferralAcceptanceRate  = ReferralsAccepted / ReferralsSent
ViewRate                = null (not computable)
```

**Zero-denominator rule:** `SafeRate(n, 0) → 0.0` always. Never NaN, never Infinity. Verified by tests.

**Rounding:** Rates are stored as `double` in [0, 1] with 6 decimal precision. The UI formats as percentages with `Math.round(rate * 100)%`.

---

## 5. Admin UI

**Route:** `/careconnect/admin/analytics/activation`

**Auth:** `requireAdmin()` — TenantAdmin or PlatformAdmin

**Layout:**
1. Header + DateFilter (client component) + "View Queue →" link
2. Summary cards (4-up grid): Referrals Sent, Activation Started, Auto-Provisioned, Referrals Accepted
3. Funnel breakdown (horizontal bar chart): 7 stages, bar width proportional to Referrals Sent
4. Supporting counts (3-up grid): Pending in Queue (→ queue), Total Approved, Overall Approval Rate
5. Data source footnote explaining limitations

**Components:**
- `page.tsx` — server component, fetches data at request time
- `date-filter.tsx` — client component, updates URL params on click (no page reload)

---

## 6. Date Filtering

**Presets:**
- Last 7 days (`?days=7`)
- Last 30 days (`?days=30`, default)
- Last 90 days (`?days=90`)

**Implementation:** URL-based — `?days=N` triggers a server re-fetch when the URL changes. `DateFilter` is a client component that calls `router.push()` with the new param. No custom range implemented in this iteration.

**API contract:**
```
GET /api/admin/analytics/funnel?days=30
GET /api/admin/analytics/funnel?startDate=2026-01-01&endDate=2026-03-31
```

`startDate/endDate` custom range is supported on the backend (for future use). The UI only exposes presets.

---

## 7. Data Sources

| Table | Used For |
|---|---|
| `Referrals` | ReferralsSent, ReferralsAccepted |
| `ActivationRequests` | ActivationStarted, AutoProvisionSucceeded, AdminApproved, FallbackPending, snapshots |

No reads from audit log tables, no new tables created.

---

## 8. Tests

**File:** `CareConnect.Tests/Application/ActivationFunnelAnalyticsTests.cs`

| Test | Coverage |
|---|---|
| `SafeRate_NonZero_*` | Correct proportion for multiple inputs |
| `SafeRate_ZeroDenominator_*` | Returns 0.0, not NaN/Infinity |
| `SafeRate_BothZero_*` | Handles double-zero case |
| `ComputeRates_FullData_*` | All 5 rates computed correctly |
| `ComputeRates_ZeroActivationStarted_*` | Zero-denom safety for 3 rates |
| `ComputeRates_ZeroReferralsSent_*` | Zero-denom safety for acceptance rate |
| `ComputeRates_AllZero_*` | All rates zero, no NaN |
| `ComputeRates_AllAutoProvisioned_*` | 100% success rate |
| `ComputeRates_MixedAutoAndAdmin_*` | OverallApprovalRate combines both |
| `ComputeRates_ViewRate_AlwaysNull` | ViewRate is always null |
| `FunnelCounts_ReferralViewed_AlwaysNull` | Stage not queryable — null |
| `IsEmpty_*` (3 cases) | Empty flag logic correct |
| `ComputeRates_Deterministic_*` | Same input → same output |

**Total: 16 tests**

**Gaps:**
- DB query layer (`ComputeCountsAsync`) is not unit-tested — would require EF InMemory provider
- No frontend rendering tests

---

## 9. Known Limitations

| Limitation | Impact | Mitigation |
|---|---|---|
| ReferralViewed not in DB | View rate is always — | Document clearly; low impact |
| FallbackPending is a proxy | Under/over-counts edge cases | Documented in UI footnote |
| AutoProvisionSucceeded uses `ApprovedByUserId IS NULL` proxy | Small error if future code sets approver | Accept for now; fix when real events stored |
| Accepted uses CreatedAtUtc anchor | Cohort-style, not calendar-style accepted count | Documented; consistent with standard funnel analysis |
| No custom date range in UI | 7/30/90 only | Accepted scope; backend already supports it |
| No export | Raw data not downloadable | Out of scope for LSCC-011 |

---

## 10. Recommended Next Steps

1. **Persist funnel events to DB** — Add a `ReferralFunnelEvent` table populated by the existing `TrackFunnelEventAsync` handler. This would enable accurate ReferralViewed counts and true AutoProvisionSucceeded/Failed metrics (currently audit-log-only).

2. **Real AutoProvision tracking** — Store `ProvisionedAt` and `ProvisionMethod` on ActivationRequest to distinguish auto vs admin approval definitively.

3. **Drilldown by provider** — Link funnel rows to provider detail pages for per-provider activation performance.

4. **Alert on high fallback rate** — If FallbackRate > threshold (e.g., 30%), emit a notification to admins so they can investigate Identity service connectivity.

5. **Custom date range** — The backend already supports `startDate`/`endDate`. Adding a calendar picker to the frontend is a thin frontend-only addition.

---

## Files Created / Modified

### New
| File | Description |
|---|---|
| `CareConnect.Application/DTOs/ActivationFunnelDto.cs` | FunnelCounts + FunnelRates + ActivationFunnelMetrics DTOs |
| `CareConnect.Application/Interfaces/IActivationFunnelAnalyticsService.cs` | Service interface |
| `CareConnect.Infrastructure/Services/ActivationFunnelAnalyticsService.cs` | Implementation (DB queries + rate math) |
| `CareConnect.Api/Endpoints/AnalyticsEndpoints.cs` | `GET /api/admin/analytics/funnel` |
| `apps/web/src/app/(platform)/careconnect/admin/analytics/activation/page.tsx` | Admin analytics page (server component) |
| `apps/web/src/app/(platform)/careconnect/admin/analytics/activation/date-filter.tsx` | Date preset selector (client component) |
| `CareConnect.Tests/Application/ActivationFunnelAnalyticsTests.cs` | 16 tests |
| `analysis/LSCC-011-report.md` | This report |

### Modified
| File | Change |
|---|---|
| `CareConnect.Infrastructure/DependencyInjection.cs` | Register `IActivationFunnelAnalyticsService` |
| `CareConnect.Api/Program.cs` | `MapAnalyticsEndpoints()` |
| `apps/web/src/types/careconnect.ts` | Add `FunnelCounts`, `FunnelRates`, `ActivationFunnelMetrics` interfaces |
| `apps/web/src/lib/careconnect-server-api.ts` | Add `analytics.getFunnel()` helper |
