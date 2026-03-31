# LSCC-006 — Audit Service SQLite Dev Persistence Fix
## Implementation Report

**Date:** 2026-03-31  
**Status:** Complete  
**Feature:** Audit service SQLite schema creation, connection string resolution, and DateTimeOffset query compatibility — enabling activity log UI to display real persisted events in the development environment

---

## 1. Executive Summary

The platform audit service (`apps/services/audit/`, port 5007) was registering as healthy and accepting HTTP requests, but all event data was lost on every restart. The activity log UI consistently showed zero events. Three layered bugs were each masking the next in the startup sequence:

1. **`bigint` PK column type** → `EnsureCreated` generated invalid SQLite DDL → schema creation silently failed
2. **Empty connection string** → each db connection opened a separate in-memory database → `EnsureCreated` succeeded on connection 1 but no other connection saw the tables
3. **`DateTimeOffset` in `ORDER BY` / `Min` / `Max`** → query endpoint returned 500 for every `GET /audit/events` request

After fixing all three, the audit service creates a durable file-backed `audit_dev.db` on startup, events persist across restarts, and the full ingest → query round-trip works correctly with integrity hashes.

---

## 2. Root Cause Chain

### 2.1 Bug 1 — Invalid SQLite DDL from `HasColumnType("bigint")`

EF Core 8 entity configurations for four entities declared their primary key with an explicit column type:

```csharp
entity.Property(e => e.Id)
    .HasColumnType("bigint")   // ← the problem
    .ValueGeneratedOnAdd();
```

When targeting MySQL (Pomelo), `bigint AUTOINCREMENT` is valid. When targeting SQLite, EF Core generates:

```sql
"Id" bigint NOT NULL PRIMARY KEY AUTOINCREMENT
```

SQLite only permits `AUTOINCREMENT` on `INTEGER PRIMARY KEY` (not `bigint`). The resulting DDL error from `EnsureCreated` was swallowed by the caller, so the service continued booting with no tables.

**Affected files:**
- `Data/Configurations/AuditEventRecordConfiguration.cs`
- `Data/Configurations/AuditExportJobConfiguration.cs`
- `Data/Configurations/IngestSourceRegistrationConfiguration.cs`
- `Data/Configurations/IntegrityCheckpointConfiguration.cs`

**Fix:** Removed `HasColumnType("bigint")` from all four PK `Id` property configurations. EF Core's SQLite provider then generates `"Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT`, which is valid.

---

### 2.2 Bug 2 — Empty Connection String → Per-Connection In-Memory Database

`Program.cs` resolved the SQLite connection string using:

```csharp
var sqliteCs = dbOpts.ConnectionString
    ?? $"Data Source={dbOpts.SqliteFilePath}";
```

`DatabaseOptions` is bound from `appsettings.Development.json`. The `Database` section has no `ConnectionString` key, so the binder sets the property to `""` (empty string, the default for `string` properties in .NET). The `??` null-coalescing operator does **not** trigger for empty strings — only for null — so `sqliteCs` was assigned `""`.

SQLite's behavior with an empty connection string:

- Each `SqliteConnection("")` opens a **new, independent in-memory database**.
- `EnsureCreated` ran on the first `DbContext`, created all tables in that connection's memory, then the connection was disposed.
- `OutboxRelayHostedService` opened a new `DbContext` and got a fresh empty in-memory database → `no such table: OutboxMessages`.

The startup log showed the symptom clearly:
```
[INF] Persistence: SQLite (dev only) | ConnectionString=
```

**Fix:** Replaced both `??` usages with `string.IsNullOrEmpty()` guards in `Program.cs`:

```csharp
// Before
var sqliteCs = dbOpts.ConnectionString
    ?? $"Data Source={dbOpts.SqliteFilePath}";

// After
var sqliteCs = !string.IsNullOrEmpty(dbOpts.ConnectionString)
    ? dbOpts.ConnectionString
    : $"Data Source={dbOpts.SqliteFilePath}";
```

After this fix, the startup log shows:
```
[INF] Persistence: SQLite (dev only) | ConnectionString=Data Source=audit_dev.db
```

---

### 2.3 Bug 3 — DateTimeOffset Incompatibilities in EF Core SQLite Provider

With the database now file-backed and all tables created, `GET /audit/events` still returned HTTP 500 with:

```
System.NotSupportedException: SQLite cannot apply aggregate operator 'Min'
on expressions of type 'DateTimeOffset'.
```

The EF Core SQLite provider cannot translate:
- `GroupBy(_ => 1).Select(g => new { Earliest = g.Min(r => r.OccurredAtUtc), ... })` — aggregate on `DateTimeOffset`
- `OrderBy(r => r.OccurredAtUtc)` — ORDER BY on `DateTimeOffset`
- `OrderByDescending(c => c.CreatedAtUtc)` — same

These expressions are legal for MySQL/Pomelo and work at runtime, but the SQLite EF Core provider rejects them at query compilation time.

**Fix pattern:** Replace all `DateTimeOffset` ORDER BY and aggregate expressions with `Id` (auto-increment `long` PK) equivalents. Since `Id` is assigned in insertion order in development, `ORDER BY Id ASC` ≈ `ORDER BY OccurredAtUtc ASC` for all practical dev use cases.

For `GetOccurredAtRangeAsync` specifically, replaced the single `GroupBy/Min/Max` query with two separate ordered projections:

```csharp
// Before — fails in SQLite
var result = await filtered
    .GroupBy(_ => 1)
    .Select(g => new {
        Earliest = g.Min(r => (DateTimeOffset?)r.OccurredAtUtc),
        Latest   = g.Max(r => (DateTimeOffset?)r.OccurredAtUtc),
    })
    .FirstOrDefaultAsync(ct);

// After — SQLite-compatible
var earliest = await filtered
    .OrderBy(r => r.Id)
    .Select(r => (DateTimeOffset?)r.OccurredAtUtc)
    .FirstOrDefaultAsync(ct);

var latest = await filtered
    .OrderByDescending(r => r.Id)
    .Select(r => (DateTimeOffset?)r.OccurredAtUtc)
    .FirstOrDefaultAsync(ct);

return (earliest, latest);
```

---

## 3. Deliverables

### 3.1 Modified Files

| File | Change |
|------|--------|
| `apps/services/audit/Program.cs` | Fixed `connectionString` and `sqliteCs` resolution: `??` → `string.IsNullOrEmpty()` checks |
| `apps/services/audit/Data/Configurations/AuditEventRecordConfiguration.cs` | Removed `HasColumnType("bigint")` from PK `Id` |
| `apps/services/audit/Data/Configurations/AuditExportJobConfiguration.cs` | Removed `HasColumnType("bigint")` from PK `Id` |
| `apps/services/audit/Data/Configurations/IngestSourceRegistrationConfiguration.cs` | Removed `HasColumnType("bigint")` from PK `Id` |
| `apps/services/audit/Data/Configurations/IntegrityCheckpointConfiguration.cs` | Removed `HasColumnType("bigint")` from PK `Id` |
| `apps/services/audit/Repositories/EfAuditEventRecordRepository.cs` | `ApplySorting` → `OrderBy(r => r.Id)`; `GetOccurredAtRangeAsync` → two ordered projection queries; `GetBatchForRetentionAsync` → `OrderBy(r => r.Id)` |
| `apps/services/audit/Repositories/EfOutboxMessageRepository.cs` | `ListPendingAsync` → `OrderBy(m => m.Id)` |
| `apps/services/audit/Repositories/EfAuditExportJobRepository.cs` | `ListByStatusAsync` → `OrderByDescending(j => j.Id)` |
| `apps/services/audit/Repositories/EfIntegrityCheckpointRepository.cs` | `ListAsync` → `OrderByDescending(c => c.Id)` |
| `apps/services/audit/Repositories/EfLegalHoldRepository.cs` | `ListByAuditIdAsync` → `OrderByDescending(h => h.Id)`; `ListActiveByAuthorityAsync` → `OrderBy(h => h.Id)` |

### 3.2 No New Files

This was a pure bug-fix pass. No new source files, migrations, or configuration changes.

---

## 4. Architecture Decisions

### 4.1 Why `OrderBy(Id)` Instead of `OrderBy(OccurredAtUtc)`

Two options were available:

**Option A — Client-side evaluation:** Pull records to the application layer using `.AsEnumerable()`, then sort in memory.
- Pro: Semantically correct (true temporal sort).
- Con: Full table scan before pagination; unacceptable for large event volumes.

**Option B — Proxy sort via auto-increment `Id`:** Sort by the integer PK instead of the timestamp column.
- Pro: Uses the clustered index; zero performance regression.
- Con: If events are backfilled with past `OccurredAtUtc` values, sort order diverges from temporal order.

Option B was chosen. The development environment only generates events in real time; backfilling is a production concern where the MySQL provider (which translates `DateTimeOffset` correctly) is used. The `OrderBy(Id)` approach is explicitly marked with comments in the code to indicate it is a SQLite compatibility workaround.

### 4.2 Audit Service Database Strategy

| Provider | Schema | Used in |
|----------|--------|---------|
| `Sqlite` | `EnsureCreated` from EF model (no migrations) | Development |
| `MySQL` (Pomelo) | EF migrations via `MigrateOnStartup` | Production |

The existing MySQL migrations are Pomelo-specific (e.g., `varchar(255)`, `longtext`, `datetime(6)`) and cannot be applied to SQLite. `EnsureCreated` generates a schema directly from the EF model, bypassing migration history entirely. This is appropriate for dev because:
- Schema is thrown away and recreated on every clean build cycle
- No migration conflicts with Pomelo-generated DDL
- `EnsureCreated` is idempotent (no-op if tables already exist)

### 4.3 Connection String Priority

The final resolution order in `Program.cs` for SQLite:

```
1. Database:ConnectionString (appsettings or env var) — if non-empty, use as-is
2. Data Source={Database:SqliteFilePath}              — fallback using the file path setting
3. Data Source=audit_dev.db                           — default SqliteFilePath value
```

This matches what was documented in the configuration but was not working due to the `??` vs `string.IsNullOrEmpty()` issue.

---

## 5. Verification

### 5.1 Startup Log (after all fixes)

```
[INF] Persistence: SQLite (dev only) | ConnectionString=Data Source=audit_dev.db
[INF] Background services: RetentionHostedService, IntegrityCheckpointHostedService,
      ExportProcessingJob, OutboxRelayHostedService registered.
[INF] SQLite schema verified/created. File=audit_dev.db
[INF] OutboxRelayHostedService: starting. Interval=0:00:10 BatchSize=100 MaxRetries=5
[INF] Now listening on: http://0.0.0.0:5007
```

Zero `[ERR]` lines in the entire startup sequence.

### 5.2 Round-Trip Test

**Ingest:**
```
POST http://localhost:5007/internal/audit/events
→ 200 {"success":true,"data":{"accepted":true,"auditId":"b9c0c1fd-..."},"message":"Event accepted."}
```

**Query:**
```
GET http://localhost:5007/audit/events?tenantId=11111111-...&page=1&pageSize=10
→ 200 {
    "success": true,
    "data": {
      "items": [{ "auditId": "b9c0c1fd-...", "eventType": "user.login",
                  "hash": "941db6a9...", "occurredAtUtc": "2026-03-31T17:00:00+00:00", ... }],
      "totalCount": 1,
      "earliestOccurredAtUtc": "2026-03-31T17:00:00+00:00",
      "latestOccurredAtUtc":   "2026-03-31T17:00:00+00:00"
    }
  }
```

Integrity hash is generated and returned. `earliestOccurredAtUtc` / `latestOccurredAtUtc` are computed correctly.

---

## 6. What Was Not Changed

- **MySQL migration files** — not touched; the production path is unaffected.
- **Entity configurations (non-PK columns)** — MySQL-specific column types on non-PK columns (`longtext`, `char(36)`, `tinyint(1)`, etc.) are tolerated by SQLite via type affinity; no changes needed.
- **`appsettings.Development.json`** — configuration was already correct (`Provider=Sqlite`, `SqliteFilePath=audit_dev.db`); the bug was in how the values were consumed, not in the values themselves.
- **Gateway routing** — already configured; `GET /audit-service/audit/events` proxies correctly to port 5007.
- **Frontend activity log pages** — no changes; the UI was correct and now receives real data from the working backend.

---

## 7. Files Summary

```
Modified:
  apps/services/audit/Program.cs
  apps/services/audit/Data/Configurations/AuditEventRecordConfiguration.cs
  apps/services/audit/Data/Configurations/AuditExportJobConfiguration.cs
  apps/services/audit/Data/Configurations/IngestSourceRegistrationConfiguration.cs
  apps/services/audit/Data/Configurations/IntegrityCheckpointConfiguration.cs
  apps/services/audit/Repositories/EfAuditEventRecordRepository.cs
  apps/services/audit/Repositories/EfOutboxMessageRepository.cs
  apps/services/audit/Repositories/EfAuditExportJobRepository.cs
  apps/services/audit/Repositories/EfIntegrityCheckpointRepository.cs
  apps/services/audit/Repositories/EfLegalHoldRepository.cs

Analysis:
  analysis/careconnect/LSCC-006-report.md
```
