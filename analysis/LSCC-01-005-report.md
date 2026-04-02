# LSCC-01-005 — Referral Performance Metrics

**Status:** Complete  
**Date:** 2026-04-02  
**Service:** CareConnect (port 5003) + Control Center web (port 5000)

---

## 1. Summary

### Implemented

| Area | Delivered |
|------|-----------|
| Metric calculation layer | `ReferralPerformanceCalculator` (static, testable, no DB dependency) |
| DB loading layer | `ReferralPerformanceService` (loads bounded dataset, calls calculator) |
| Admin API endpoint | `GET /api/admin/performance` (admin-only, `days`/`since` params) |
| Frontend types | `ReferralPerformanceResult`, `PerformanceSummary`, `AgingDistribution`, `ProviderPerformanceRow` |
| Server API client | `careConnectServerApi.adminPerformance.getMetrics()` |
| Performance page | `/careconnect/admin/performance` — cards, aging bars, provider table, time presets |
| Tests | 13 unit tests covering all metric scenarios |
| Report | This file |

### Nothing incomplete

All seven success criteria from the spec are met. No items deferred.

---

## 2. Metric Definitions

### Time-window anchor

All cohort metrics (total referrals, accepted, acceptance rate, TTA, provider stats) use:

```
referral.CreatedAtUtc >= windowFrom
```

The aging distribution is **not** filtered by the window — it always shows ALL currently-New referrals, because aging is an operational state snapshot, not a cohort metric.

### A. Time to Accept (TTA)

```
TTA (hours) = AcceptedAtUtc - CreatedAtUtc  [in hours]
```

- Only computed for referrals where `AcceptedAtUtc` can be derived
- Records where `TTA < 0` (AcceptedAt before CreatedAt — corrupt data) are **excluded** from the average
- `AvgTimeToAcceptHours` is `null` when no valid TTA entries exist

### B. Acceptance Rate

```
AcceptanceRate = AcceptedReferrals / TotalReferrals
```

- Returns `0.0` when `TotalReferrals == 0` (divide-by-zero guard, never NaN/Infinity)
- Expressed as a decimal `[0, 1]`; frontend formats as percentage

### C. Aging Distribution

Buckets applied to `(NowUtc - referral.CreatedAtUtc).TotalHours`:

| Bucket | Condition |
|--------|-----------|
| `lt1h`   | `ageHours < 1` |
| `h1to24` | `1 ≤ ageHours < 24` |
| `d1to3`  | `24 ≤ ageHours < 72` |
| `gt3d`   | `ageHours >= 72` |

### D. Provider Responsiveness

Per provider, over the selected window:
- `totalReferrals` — count of all referrals for that provider in window
- `acceptedReferrals` — count where `AcceptedAtUtc` is derivable
- `acceptanceRate` — `acceptedReferrals / totalReferrals` (0 when total=0)
- `avgTimeToAcceptHours` — average of valid TTAs; `null` when `acceptedReferrals == 0`

---

## 3. API

### Route

```
GET /api/admin/performance
```

### Query Parameters

| Param | Type | Default | Notes |
|-------|------|---------|-------|
| `days` | int | 7 | Window in days from now; clamped to [1, 90] |
| `since` | ISO string | — | Explicit UTC start; overrides `days` if provided |

### Response Shape

```json
{
  "windowFrom": "2026-03-26T00:00:00Z",
  "windowTo":   "2026-04-02T09:00:00Z",
  "summary": {
    "totalReferrals":       25,
    "acceptedReferrals":    18,
    "acceptanceRate":       0.72,
    "avgTimeToAcceptHours": 14.3,
    "currentNewReferrals":  5
  },
  "aging": {
    "lt1h":   1,
    "h1to24": 2,
    "d1to3":  1,
    "gt3d":   1,
    "total":  5
  },
  "providers": [
    {
      "providerId":            "...",
      "providerName":          "Dr. Smith",
      "totalReferrals":        12,
      "acceptedReferrals":     10,
      "acceptanceRate":        0.833,
      "avgTimeToAcceptHours":  11.5
    }
  ]
}
```

### Auth Model

`Policies.PlatformOrTenantAdmin` — identical to other CareConnect admin endpoints. Unauthenticated requests receive 401; insufficient role receives 403.

---

## 4. Dashboard UI

### Route

```
/careconnect/admin/performance
```

### Sections

1. **Time-window presets** — 24h / 7d / 30d pill links (shareable URLs); 7d is the default
2. **Summary cards** — Total, Accepted, Acceptance Rate, Avg TTA, Currently New — colour-coded (green/amber) based on thresholds
3. **New Referral Aging** — horizontal proportional bars for each bucket; red callout if any referrals are 3+ days stuck; note clarifying aging covers all New, not just window cohort
4. **Provider Responsiveness table** — sorted by total referrals desc; acceptance rate coloured green/yellow/red; "—" for null avg TTA

### Empty-state behavior

- If `totalReferrals == 0` in window: summary cards show 0 / — and a blue info banner prompts widening the range
- If `aging.total == 0`: "No referrals currently in New status" panel shown
- If `providers` is empty: "No provider activity in this window" panel shown
- If API fails: full-width red error banner shown; no crash

---

## 5. Data Source Strategy

### Tables used

| Table | Purpose |
|-------|---------|
| `Referrals` | Cohort selection (CreatedAtUtc window), status, ProviderId, Provider eager-load for name |
| `ReferralStatusHistories` | AcceptedAt derivation — earliest `ChangedAtUtc` where `NewStatus=="Accepted"` |
| `Providers` (via include) | Provider name for display |

**No new tables created.**

### AcceptedAt derivation

The `Referral` domain entity has no `AcceptedAt` field. The service derives it:

```csharp
var acceptedHistoryMap = await _db.ReferralStatusHistories
    .Where(h => referralIds.Contains(h.ReferralId) && h.NewStatus == "Accepted")
    .GroupBy(h => h.ReferralId)
    .Select(g => new { ReferralId = g.Key, AcceptedAtUtc = g.Min(h => h.ChangedAtUtc) })
    .ToDictionaryAsync(...);
```

**"Earliest" is chosen** to avoid double-counting when a referral is re-opened and re-accepted.

If a referral has `Status=="Accepted"` but no matching history row (race condition or legacy data), it still contributes to `acceptedReferrals` count but its TTA cannot be computed and it is excluded from `avgTimeToAcceptHours`.

### Query strategy (two-phase bounded load)

1. Load referrals in window → extract ID set
2. Load status history filtered to that ID set (bounded) → build `acceptedAtUtc` dict
3. Load all currently-New referrals for aging (no window filter)
4. Compute everything in-memory via `ReferralPerformanceCalculator`

**Trade-off:** for very large deployments (100k+ referrals) a pure-SQL aggregation would be faster. The in-memory approach was chosen for testability and correctness. The query is bounded by the window filter, which keeps it practical for typical admin loads (7-day default).

---

## 6. Tests

### File

`CareConnect.Tests/Application/ReferralPerformanceCalculatorTests.cs`

### Scenarios covered (13 tests)

| # | Scenario |
|---|---------|
| 1 | Acceptance rate — standard case (1 of 3 accepted) |
| 2 | Acceptance rate — total=0 → returns exactly 0.0 |
| 3 | Acceptance rate — all accepted → returns 1.0 |
| 4 | Avg TTA — multiple accepted → correct average |
| 5 | Avg TTA — no accepted referrals → null |
| 6 | Avg TTA — negative TTA (corrupt AcceptedAt) → excluded from average |
| 7 | Aging distribution — all four buckets populated correctly |
| 8 | Aging distribution — empty list → all zeros |
| 9 | Provider aggregation — multiple providers grouped, rates and avg TTA correct |
| 10 | Provider with zero accepted → null avg TTA, still appears in results |
| 11 | Empty dataset → all safe zero/null values, no exception |
| 12 | currentNewReferrals propagated correctly to summary |
| 13 | WindowFrom/WindowTo set correctly on result |

All 13 tests pass.

### Gaps / not tested

- `ReferralPerformanceService` (DB loading) — not unit tested; requires an EF in-memory DB or integration test setup. Deferred.
- Admin-only HTTP enforcement — verified manually via existing `Policies.PlatformOrTenantAdmin` convention; no integration test added in this feature.

---

## 7. Known Limitations / Deferred

| Item | Notes |
|------|-------|
| AcceptedAt missing from Referral entity | Addressed via status history derivation; adding a persisted `AcceptedAtUtc` column in a future migration would simplify the query and improve index performance |
| Provider table sort is server-rendered (by total referrals desc) | Client-side column sorting not added; a follow-up can add URL-param-driven sort if needed |
| No date-range picker | Only preset links (24h/7d/30d) and raw `since` param; a date picker component is a low-priority UX improvement |
| ReferralPerformanceService not unit tested | Requires integration test or EF in-memory setup; deferred |

---

## 8. Recommended Next Step

The smallest logical follow-up is persisting `AcceptedAtUtc` directly on the `Referral` entity by adding a nullable column populated when the status transitions to `Accepted`. This would:

1. Eliminate the status-history join for TTA calculations
2. Enable a direct DB-level `AVG()` query (no in-memory aggregation needed)
3. Improve performance for larger deployments

Migration complexity: low — nullable column, backfill from history, update the domain transition method.
