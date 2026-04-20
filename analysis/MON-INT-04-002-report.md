# MON-INT-04-002 — Uptime Aggregation Engine

> Status: **COMPLETE** — all sections below reflect the final delivered state.

---

## 1. Task Summary

Build a durable, idempotent uptime aggregation engine for the Monitoring Service that:

1. Derives hourly uptime rollup metrics from the canonical `check_results` table.
2. Persists rollups to a new `uptime_hourly_rollups` table (EF migration).
3. Exposes two read-only anonymous API endpoints.
4. Runs on a configurable cadence (default 5 min) with configurable lookback (default 91 days).
5. Is isolated from alert state — check history is the single source of truth for uptime %.
6. Applies pending DB migrations at startup automatically.

---

## 2. Existing Raw Data Analysis

- `check_results` table stores one row per check execution: `monitored_entity_id`, `checked_at_utc`, `outcome` (varchar enum), `elapsed_ms`, `entity_name`.
- `CheckOutcome` enum values: `Success=1`, `NonSuccessStatusCode=2`, `Timeout=3`, `InvalidTarget=4`, `NetworkFailure=5`, `Skipped=6`, `UnexpectedFailure=99`.
- No prior uptime aggregation table existed.
- No `Database.Migrate()` call existed at startup — added via `MonitoringMigrationsHostedService`.

---

## 3. Uptime Aggregation Model

### State Classification

| CheckOutcome            | Uptime State | In Denominator |
|-------------------------|--------------|----------------|
| `Success`               | Up           | Yes            |
| `NonSuccessStatusCode`  | Degraded     | Yes            |
| `Timeout`               | Down         | Yes            |
| `NetworkFailure`        | Down         | Yes            |
| `InvalidTarget`         | Down         | Yes            |
| `UnexpectedFailure`     | Down         | Yes            |
| `Skipped`               | Unknown      | **No**         |

### Uptime Formulas

- **Strict uptime**: `up / (up + degraded + down)` — excludes Unknown from denominator.
- **Weighted availability**: `(up + degraded × 0.5) / (up + degraded + down)`.
- **InsufficientData = true** when the denominator is zero.

---

## 4. Persistence Design

### Table: `uptime_hourly_rollups`

| Column               | Type         | Notes                          |
|----------------------|--------------|--------------------------------|
| `id`                 | char(36) PK  | GUID, ValueGeneratedNever      |
| `monitored_entity_id`| char(36) FK  | ON DELETE CASCADE              |
| `entity_name`        | varchar(200) | Snapshotted from check_results |
| `bucket_hour_utc`    | datetime(6)  | UTC start of 1-hour window     |
| `up_count`           | int          |                                |
| `degraded_count`     | int          |                                |
| `down_count`         | int          |                                |
| `unknown_count`      | int          |                                |
| `total_count`        | int          | up + deg + down + unknown      |
| `sum_elapsed_ms`     | bigint       | For avg latency computation    |
| `max_elapsed_ms`     | bigint       |                                |
| `uptime_ratio`       | double NULL  | Null if insufficient_data      |
| `weighted_availability` | double NULL | Null if insufficient_data    |
| `insufficient_data`  | tinyint(1)   |                                |
| `computed_at_utc`    | datetime(6)  | Updated on every recompute     |
| `created_at_utc`     | datetime(6)  | Set once at row creation       |

**Unique index**: `(monitored_entity_id, bucket_hour_utc)` → `ix_uptime_hourly_entity_hour`  
**Index**: `(bucket_hour_utc)` → `ix_uptime_hourly_bucket_hour`  
**FK**: → `monitored_entities.id` ON DELETE CASCADE

---

## 5. Aggregation Engine Implementation

### Strategy: Full-Window Recompute per Cycle

On each cycle:
1. Query all `check_results` where `checked_at_utc >= now - LookbackDays`.
2. Project only: `(MonitoredEntityId, EntityName, CheckedAtUtc, Outcome, ElapsedMs)`.
3. Group by `(MonitoredEntityId, EntityName, TruncateToHour(CheckedAtUtc))`.
4. Load existing rollups for the same window into a dictionary keyed by `(entityId, hourBucket)`.
5. For each (entity, hour) group: either update the existing row or add a new one.
6. `SaveChangesAsync` — single round-trip for all inserts/updates.

**Idempotency**: Running the engine twice over the same `check_results` data produces identical rollup values.

### Startup Behaviour

1. `MonitoringMigrationsHostedService` runs first (registered before `MonitoringEntityBootstrap`), calling `db.Database.MigrateAsync()`.
2. `UptimeAggregationHostedService.ExecuteAsync` immediately runs one cycle before entering the periodic loop.
3. On first run: all historical buckets are inserted (51 buckets on live dev). On subsequent runs: existing buckets are updated.

### Configuration

```json
{
  "Monitoring": {
    "UptimeAggregation": {
      "Enabled": true,
      "IntervalSeconds": 300,
      "LookbackDays": 91
    }
  }
}
```

---

## 6. Read API Design & Implementation

### `GET /monitoring/uptime/rollups?window=24h|7d|30d|90d`

Anonymous. Reads from `uptime_hourly_rollups`, groups by entity, sums counts, returns per-component + overall stats.

```json
{
  "window": "24h",
  "windowStartUtc": "2026-04-19T14:30:22Z",
  "windowEndUtc": "2026-04-20T14:30:22Z",
  "overallUptimePercent": 89.0931,
  "componentCount": 11,
  "insufficientData": false,
  "components": [
    {
      "entityId": "53906a5b-f170-4572-b8ee-d5f9d4cbb8cd",
      "entityName": "Audit",
      "uptimePercent": 97.7551,
      "weightedAvailabilityPercent": 97.7551,
      "upCount": 479, "degradedCount": 0, "downCount": 11, "unknownCount": 0,
      "totalCountable": 490,
      "avgLatencyMs": 5.31, "maxLatencyMs": 420,
      "insufficientData": false
    }
  ]
}
```

### `GET /monitoring/uptime/history?entityId={guid}&window=24h|7d|30d|90d`

Anonymous. Returns hourly bucket breakdown for one entity.
- `400` if `entityId` is missing or not a valid GUID.
- `404` if entity not found in DB.

```json
{
  "entityId": "53906a5b-f170-4572-b8ee-d5f9d4cbb8cd",
  "entityName": "Audit",
  "window": "24h",
  "windowStartUtc": "...", "windowEndUtc": "...",
  "buckets": [
    {
      "bucketStartUtc": "2026-04-20T06:00:00",
      "uptimePercent": 97.87,
      "dominantStatus": "Healthy",
      "upCount": 92, "degradedCount": 0, "downCount": 2, "unknownCount": 0,
      "avgLatencyMs": 4.82, "maxLatencyMs": 112,
      "insufficientData": false
    }
  ]
}
```

---

## 7. Validation

| Check | Result |
|-------|--------|
| `dotnet build Monitoring.Api` | ✅ 0 errors, 0 warnings |
| `MonitoringMigrations: migrations applied successfully.` (log) | ✅ |
| `UptimeAggregation: engine started. IntervalSeconds=300, LookbackDays=91.` (log) | ✅ |
| `UptimeAggregation: cycle complete. Inserted=51, Updated=0.` (log) | ✅ |
| `GET /monitoring/uptime/rollups?window=24h` → 200, 11 components, real data | ✅ |
| `GET /monitoring/uptime/rollups?window=7d` → 200 | ✅ |
| `GET /monitoring/uptime/history?entityId=<valid>&window=24h` → 200, hourly buckets | ✅ |
| `GET /monitoring/uptime/history?entityId=bad-guid` → 400 with error | ✅ |
| `GET /monitoring/uptime/history?window=7d` (no entityId) → 400 with error | ✅ |

---

## 8. Files Changed

### Created

| File | Description |
|------|-------------|
| `Monitoring.Domain/Monitoring/UptimeHourlyRollup.cs` | Domain entity |
| `Monitoring.Infrastructure/Persistence/Configurations/UptimeHourlyRollupConfiguration.cs` | EF config |
| `Monitoring.Infrastructure/Persistence/Migrations/20260420120000_AddUptimeRollups.cs` | Migration |
| `Monitoring.Infrastructure/Persistence/Migrations/20260420120000_AddUptimeRollups.Designer.cs` | Migration designer |
| `Monitoring.Infrastructure/Persistence/MonitoringMigrationsHostedService.cs` | Startup migration runner |
| `Monitoring.Infrastructure/UptimeAggregation/UptimeAggregationOptions.cs` | Options |
| `Monitoring.Infrastructure/UptimeAggregation/UptimeAggregationHostedService.cs` | Aggregation engine |
| `Monitoring.Application/Queries/IUptimeReadService.cs` | Read service interface |
| `Monitoring.Application/Queries/UptimeReadResults.cs` | Result record types |
| `Monitoring.Infrastructure/Queries/EfCoreUptimeReadService.cs` | EF Core read service |
| `Monitoring.Api/Contracts/UptimeResponses.cs` | API response contracts |
| `Monitoring.Api/Endpoints/UptimeReadEndpoints.cs` | Minimal API endpoints |

### Modified

| File | Change |
|------|--------|
| `MonitoringDbContext.cs` | Added `DbSet<UptimeHourlyRollup>` |
| `DependencyInjection.cs` | Registered migrations, aggregation, read services |
| `Program.cs` | Added `app.MapUptimeReadEndpoints()` |
| `MonitoringDbContextModelSnapshot.cs` | Added UptimeHourlyRollup entity block |

---

## 9. Known Gaps / Risks

- **Full-window scan per cycle**: The aggregation engine re-scans all check_results in the 91-day window every 5 minutes. At low check volumes (10 entities × ~240 checks/hour) this is negligible. If entity counts or check frequency grows significantly, a watermark-based incremental approach would reduce DB load.
- **No frontend integration yet**: The endpoints are ready; the Control Center dashboard/CC monitoring UI needs to be wired up in a future ticket.

---

## 10. Recommended Next Feature

- **MON-INT-05-001**: Control Center uptime dashboard tab wiring (consume `/monitoring/uptime/rollups` and `/monitoring/uptime/history` in the CC frontend).
- Or proceed with the next queued LegalSynq ticket.
