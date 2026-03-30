# Step 5 — EF Core Mappings

**Date:** 2026-03-30  
**Phase:** Entity Framework Core Fluent API configuration  
**Status:** Complete — build ✅ 0 errors, 0 warnings

---

## Architecture

All entity type configurations are in separate `IEntityTypeConfiguration<T>` classes
under `Data/Configurations/`. The DbContext discovers them automatically via
`ApplyConfigurationsFromAssembly(typeof(AuditEventDbContext).Assembly)` — no manual
registration required when new configurations are added.

```
Data/
├── AuditEventDbContext.cs            (rebuilt — uses ApplyConfigurationsFromAssembly)
└── Configurations/
    ├── AuditEventRecordConfiguration.cs
    ├── AuditExportJobConfiguration.cs
    ├── IntegrityCheckpointConfiguration.cs
    ├── IngestSourceRegistrationConfiguration.cs
    └── LegacyAuditEventConfiguration.cs    (extracted from inline OnModelCreating)
```

---

## Table Mappings

### `AuditEventRecords`
**Entity:** `PlatformAuditEventService.Entities.AuditEventRecord`  
**Configuration:** `AuditEventRecordConfiguration`

| Column | MySQL Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `bigint` | No | PK, AUTO_INCREMENT |
| `AuditId` | `char(36)` | No | Public identifier, UX (unique) |
| `EventId` | `char(36)` | Yes | Source domain event ID |
| `EventType` | `varchar(200)` | No | Dot-notation event code |
| `EventCategory` | `tinyint` | No | Enum → int: Security=1 … Performance=9 |
| `SourceSystem` | `varchar(200)` | No | |
| `SourceService` | `varchar(200)` | Yes | |
| `SourceEnvironment` | `varchar(100)` | Yes | |
| `PlatformId` | `char(36)` | Yes | Platform partition GUID |
| `TenantId` | `varchar(100)` | Yes | |
| `OrganizationId` | `varchar(100)` | Yes | |
| `UserScopeId` | `varchar(200)` | Yes | May differ from ActorId (impersonation) |
| `ScopeType` | `tinyint` | No | Enum → int: Global=1 … Service=6 |
| `ActorId` | `varchar(200)` | Yes | |
| `ActorType` | `tinyint` | No | Enum → int: User=1 … Support=7 |
| `ActorName` | `varchar(300)` | Yes | Snapshot at event time |
| `ActorIpAddress` | `varchar(45)` | Yes | IPv4 (15) or IPv6 (max 45) |
| `ActorUserAgent` | `varchar(500)` | Yes | |
| `EntityType` | `varchar(200)` | Yes | |
| `EntityId` | `varchar(200)` | Yes | |
| `Action` | `varchar(200)` | No | PascalCase verb |
| `Description` | `varchar(2000)` | No | Human-readable summary |
| `BeforeJson` | `mediumtext` | Yes | Pre-mutation snapshot (up to 16 MB) |
| `AfterJson` | `mediumtext` | Yes | Post-mutation snapshot (up to 16 MB) |
| `MetadataJson` | `text` | Yes | Supplementary JSON context (up to 64 KB) |
| `CorrelationId` | `varchar(200)` | Yes | W3C traceparent / X-Correlation-Id |
| `RequestId` | `varchar(200)` | Yes | |
| `SessionId` | `varchar(200)` | Yes | |
| `VisibilityScope` | `tinyint` | No | Enum → int: Platform=1 … Internal=5 |
| `Severity` | `tinyint` | No | Enum → int: Debug=1 … Alert=7 |
| `OccurredAtUtc` | `datetime(6)` | No | UTC; microsecond precision |
| `RecordedAtUtc` | `datetime(6)` | No | UTC; microsecond precision |
| `Hash` | `varchar(64)` | Yes | HMAC-SHA256 hex (exactly 64 chars) |
| `PreviousHash` | `varchar(64)` | Yes | Chain link to preceding record hash |
| `IdempotencyKey` | `varchar(300)` | Yes | UX (unique, NULLs allowed) |
| `IsReplay` | `tinyint(1)` | No | Bool, default 0 |
| `TagsJson` | `text` | Yes | JSON string array |

---

### `AuditExportJobs`
**Entity:** `PlatformAuditEventService.Entities.AuditExportJob`  
**Configuration:** `AuditExportJobConfiguration`

| Column | MySQL Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `bigint` | No | PK, AUTO_INCREMENT |
| `ExportId` | `char(36)` | No | Public identifier, UX (unique) |
| `RequestedBy` | `varchar(200)` | No | ActorId of requester |
| `ScopeType` | `tinyint` | No | Enum → int |
| `ScopeId` | `varchar(200)` | Yes | Concrete scope value |
| `FilterJson` | `text` | Yes | Serialized query predicate (up to 64 KB) |
| `Format` | `varchar(20)` | No | "Json" \| "Csv" \| "Ndjson" |
| `Status` | `tinyint` | No | Enum → int: Pending=1 … Expired=6 |
| `FilePath` | `varchar(1000)` | Yes | Base path or key; URL generated at read time |
| `ErrorMessage` | `text` | Yes | Error detail when Status=Failed |
| `CreatedAtUtc` | `datetime(6)` | No | UTC |
| `CompletedAtUtc` | `datetime(6)` | Yes | Set on terminal state |

---

### `IntegrityCheckpoints`
**Entity:** `PlatformAuditEventService.Entities.IntegrityCheckpoint`  
**Configuration:** `IntegrityCheckpointConfiguration`

| Column | MySQL Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `bigint` | No | PK, AUTO_INCREMENT |
| `CheckpointType` | `varchar(100)` | No | Open string: "hourly", "daily", "manual", custom |
| `FromRecordedAtUtc` | `datetime(6)` | No | Window start (inclusive), UTC |
| `ToRecordedAtUtc` | `datetime(6)` | No | Window end (exclusive), UTC |
| `AggregateHash` | `varchar(64)` | No | HMAC-SHA256 hex over ordered record hashes |
| `RecordCount` | `bigint` | No | Count of records in hash window |
| `CreatedAtUtc` | `datetime(6)` | No | UTC |

---

### `IngestSourceRegistrations`
**Entity:** `PlatformAuditEventService.Entities.IngestSourceRegistration`  
**Configuration:** `IngestSourceRegistrationConfiguration`

| Column | MySQL Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `bigint` | No | PK, AUTO_INCREMENT |
| `SourceSystem` | `varchar(200)` | No | |
| `SourceService` | `varchar(200)` | Yes | NULL = covers entire system |
| `IsActive` | `tinyint(1)` | No | Default 1 (true) |
| `Notes` | `text` | Yes | Operator documentation |
| `CreatedAtUtc` | `datetime(6)` | No | UTC |

---

### `AuditEvents` (legacy — preserved)
**Entity:** `PlatformAuditEventService.Models.AuditEvent`  
**Configuration:** `LegacyAuditEventConfiguration`

Unchanged from previous steps. Config extracted from inline `OnModelCreating` block
into `LegacyAuditEventConfiguration` for consistency. Schema is identical.

---

## Index Summary

### `AuditEventRecords` — 16 indexes

| Index Name | Columns | Type | Purpose |
|---|---|---|---|
| `UX_AuditEventRecords_AuditId` | AuditId | UNIQUE | Public ID lookup (GET by AuditId) |
| `IX_AuditEventRecords_TenantId` | TenantId | Standard | Tenant-scoped COUNT / admin queries |
| `IX_AuditEventRecords_OrganizationId` | OrganizationId | Standard | Org-scoped queries |
| `IX_AuditEventRecords_ActorId` | ActorId | Standard | Actor audit trail |
| `IX_AuditEventRecords_EntityType_EntityId` | (EntityType, EntityId) | Composite | Resource history lookup |
| `IX_AuditEventRecords_EventType` | EventType | Standard | Per-event-type feeds, alerts |
| `IX_AuditEventRecords_EventCategory` | EventCategory | Standard | Category dashboards, retention |
| `IX_AuditEventRecords_CorrelationId` | CorrelationId | Standard | Distributed trace reconstruction |
| `IX_AuditEventRecords_RequestId` | RequestId | Standard | Request-scoped lookup |
| `IX_AuditEventRecords_SessionId` | SessionId | Standard | Session-scoped lookup |
| `IX_AuditEventRecords_OccurredAtUtc` | OccurredAtUtc | Standard | Global time-range queries |
| `IX_AuditEventRecords_RecordedAtUtc` | RecordedAtUtc | Standard | Checkpoint window computation |
| `IX_AuditEventRecords_VisibilityScope` | VisibilityScope | Standard | Pre-filter before role check |
| `UX_AuditEventRecords_IdempotencyKey` | IdempotencyKey | UNIQUE | Dedup on ingest (NULLs allowed) |
| `IX_AuditEventRecords_TenantId_OccurredAt` | (TenantId, OccurredAtUtc) | Composite | Primary tenant time-range query |
| `IX_AuditEventRecords_TenantId_Category_OccurredAt` | (TenantId, EventCategory, OccurredAtUtc) | Composite | Category-filtered tenant dashboard |
| `IX_AuditEventRecords_Severity_RecordedAt` | (Severity, RecordedAtUtc) | Composite | Security alert feeds |

### `AuditExportJobs` — 6 indexes

| Index Name | Columns | Type | Purpose |
|---|---|---|---|
| `UX_AuditExportJobs_ExportId` | ExportId | UNIQUE | Public ID lookup |
| `IX_AuditExportJobs_Status` | Status | Standard | Worker queue polling |
| `IX_AuditExportJobs_RequestedBy` | RequestedBy | Standard | Requester history |
| `IX_AuditExportJobs_ScopeType_ScopeId` | (ScopeType, ScopeId) | Composite | Tenant export listing |
| `IX_AuditExportJobs_CreatedAtUtc` | CreatedAtUtc | Standard | Time-ordered listing |
| `IX_AuditExportJobs_RequestedBy_Status_CreatedAt` | (RequestedBy, Status, CreatedAtUtc) | Composite | "My pending exports" |

### `IntegrityCheckpoints` — 4 indexes

| Index Name | Columns | Type | Purpose |
|---|---|---|---|
| `IX_IntegrityCheckpoints_Window` | (FromRecordedAtUtc, ToRecordedAtUtc) | Composite | Window lookup |
| `IX_IntegrityCheckpoints_CheckpointType` | CheckpointType | Standard | List by cadence |
| `IX_IntegrityCheckpoints_CreatedAtUtc` | CreatedAtUtc | Standard | Chronological listing |
| `IX_IntegrityCheckpoints_CheckpointType_FromAt` | (CheckpointType, FromRecordedAtUtc) | Composite | Find checkpoint for type + period |

### `IngestSourceRegistrations` — 2 indexes

| Index Name | Columns | Type | Purpose |
|---|---|---|---|
| `UX_IngestSourceRegistrations_SourceSystem_SourceService` | (SourceSystem, SourceService) | UNIQUE | Dedup + primary lookup |
| `IX_IngestSourceRegistrations_IsActive` | IsActive | Standard | Active/paused source filter |

---

## Constraints

| Table | Constraint | Detail |
|---|---|---|
| `AuditEventRecords` | PK | `Id` bigint |
| `AuditEventRecords` | UNIQUE | `AuditId` char(36) |
| `AuditEventRecords` | UNIQUE | `IdempotencyKey` varchar(300) — NULLs distinct |
| `AuditEventRecords` | NOT NULL | EventType, EventCategory, SourceSystem, ScopeType, ActorType, Action, Description, VisibilityScope, Severity, OccurredAtUtc, RecordedAtUtc, IsReplay |
| `AuditExportJobs` | PK | `Id` bigint |
| `AuditExportJobs` | UNIQUE | `ExportId` char(36) |
| `AuditExportJobs` | NOT NULL | ExportId, RequestedBy, ScopeType, Format, Status, CreatedAtUtc |
| `IntegrityCheckpoints` | PK | `Id` bigint |
| `IntegrityCheckpoints` | NOT NULL | All columns |
| `IngestSourceRegistrations` | PK | `Id` bigint |
| `IngestSourceRegistrations` | UNIQUE | (SourceSystem, SourceService) — NULLs distinct |
| `IngestSourceRegistrations` | NOT NULL | SourceSystem, IsActive, CreatedAtUtc |

---

## Design Notes

### Surrogate bigint PK + Guid public identifier
All new entities use `long Id` (bigint AUTO_INCREMENT) as the database PK for:
- Efficient clustered index scans (sequential insertion order in InnoDB)
- Cheap join columns (8 bytes vs. 36 bytes for char(36))
- Fast pagination via keyset (`WHERE Id > :lastId`)

The `Guid` field (`AuditId`, `ExportId`) is the stable public identifier exposed through
the API. It is stored as `char(36)` with a UNIQUE constraint rather than as the PK to
decouple internal DB identity from the API contract.

### Enum storage as tinyint
All enum properties use `HasConversion<int>().HasColumnType("tinyint")`. Advantages:
- Compact (1 byte per value vs. 4 for int, vs. varchar for string)
- Range-comparable (`Severity >= 5` for Error+Critical+Alert queries)
- Stable: enum integer backing values are explicit in the enum definitions and must not be reordered

MySQL signed tinyint range: -128 to 127. All enum values fit within this range (max value is 9 for EventCategory).

### DateTimeOffset → datetime(6) UTC
Pomelo converts `DateTimeOffset` to `datetime(6)` by stripping the UTC offset and
storing the UTC value. The offset is always reconstructed as `+00:00` on read.
This is safe because all service code generates timestamps with `DateTimeOffset.UtcNow`.
`datetime(6)` provides microsecond precision, which is sufficient for sub-millisecond
audit event deduplication.

### BeforeJson / AfterJson as mediumtext
Full domain-object JSON snapshots can exceed 64 KB for complex entities with embedded
collections (e.g. a Fund record with many participants). `mediumtext` (up to 16 MB)
ensures no snapshot is truncated. `text` (64 KB) is used for `MetadataJson`, `TagsJson`,
`FilterJson`, and `Notes` where payloads are bounded in practice.

### IdempotencyKey UNIQUE index — NULLs allowed
MySQL 8's UNIQUE index treats NULL as a non-equal value: two rows with NULL in a UNIQUE
column are not considered duplicates. This means:
- Rows submitted without an idempotency key (NULL) can coexist freely.
- Rows submitted with a key enforce uniqueness against other keyed rows.

This is the correct behavior for an optional dedup key without requiring a partial/filtered
index workaround. The application ingest layer enforces uniqueness rejection by catching the
MySQL duplicate key error (error 1062) on INSERT.

### No HasDefaultValueSql on required fields
Required fields (`EventType`, `SourceSystem`, etc.) have no database-level defaults.
Values must be provided by the ingest pipeline. Silent defaults mask data quality issues
and are inappropriate for an audit trail.

`IsReplay` and `IsActive` are the only fields with `HasDefaultValue` (false and true
respectively) because they have well-defined behavioral defaults that are safe to apply
at the database level for any consumer of the schema (e.g. direct SQL inserts during
migrations or bulk loads).

### Append-only design alignment
EF Core configuration does not add UPDATE or DELETE triggers; enforcement is in the
repository and service layers. The configuration avoids:
- `HasDefaultValueSql` on timestamps (no `NOW()` or `CURRENT_TIMESTAMP` defaults)
- Auto-computed columns that could be silently updated
- Cascading deletes (no FK relationships defined)

### LegacyAuditEventConfiguration — preserved compatibility
The original `AuditEvent` inline configuration is moved verbatim into a separate class.
`AuditEvents` table schema is unchanged. The migration path is:
1. New code paths write `AuditEventRecords`
2. Existing code paths continue reading/writing `AuditEvents`
3. When the service layer is re-wired (Step 6+), `AuditEvents` table becomes unused
4. A future cleanup migration drops the `AuditEvents` table

### TenantId index redundancy note
`IX_AuditEventRecords_TenantId` (single-column) and
`IX_AuditEventRecords_TenantId_OccurredAt` (composite) are both present.
In MySQL InnoDB, the composite index does serve TenantId-only lookups (leftmost prefix rule),
making the single-column index technically redundant for equality lookups.
Both are kept because:
- COUNT(*) by TenantId (no time filter) is a common admin query
- MySQL query planner may prefer the narrow single-column index for this pattern
- The space overhead is minimal (TenantId is at most 100 bytes per row)

---

## Next Steps (Step 6+)

1. **EF Core migration** — `dotnet ef migrations add Step5EfMappings` to generate the initial MySQL DDL for all 4 new tables
2. **Repository interfaces** — `IAuditEventRecordRepository`, `IAuditExportJobRepository`, `IIntegrityCheckpointRepository`, `IIngestSourceRegistrationRepository`
3. **EF Core repository implementations** — `EfAuditEventRecordRepository` backed by `IDbContextFactory<AuditEventDbContext>`
4. **InMemory repository implementations** — for provider=InMemory parity
5. **JsonStringEnumConverter registration** — in `Program.cs` for API enum string serialization
6. **Mapper** — `IngestAuditEventRequest` → `AuditEventRecord`
