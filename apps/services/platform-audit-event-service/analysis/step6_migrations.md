# Step 6 — EF Core Migrations

## Service
`apps/services/platform-audit-event-service/`  
Provider: Pomelo.EntityFrameworkCore.MySql 8.0.0 + EF Core 8.0.0  
Target DB: MySQL 8.0+

---

## Migrations Created

| # | Migration | File | Generated |
|---|---|---|---|
| 1 | `InitialSchema` | `Data/Migrations/20260330140138_InitialSchema.cs` | 2026-03-30 |

**Supporting files:**
- `Data/Migrations/20260330140138_InitialSchema.Designer.cs` — EF design-time model snapshot at creation
- `Data/Migrations/AuditEventDbContextModelSnapshot.cs` — current full model snapshot (updated on each migration)
- `analysis/deploy_InitialSchema_idempotent.sql` — production-safe idempotent SQL script

---

## DesignTimeDbContextFactory Change

`ServerVersion.AutoDetect(connectionString)` was replaced with `new MySqlServerVersion(new Version(8, 0, 0))`.

**Why:** `AutoDetect` requires a live MySQL connection to query the server's version banner.  
Migration generation (`dotnet ef migrations add`) is a design-time operation — it must work without a running database.  
A fixed server version removes this constraint and makes the CLI usable in CI, offline dev, and ephemeral environments.

**Connection string resolution (priority order):**
1. `ConnectionStrings__AuditEventDb` environment variable
2. Localhost dev fallback: `Server=localhost;Port=3306;Database=audit_event_db;User=root;Password=;...`

---

## Schema Summary

### Tables created by `InitialSchema`

> **Note:** The legacy `AuditEvents` table is tracked in the EF model snapshot but is **not** created or dropped by this migration — it pre-exists in production and was not created by this service. For fresh databases, create it separately before applying this migration.

---

### `AuditEventRecords`
Canonical append-only audit event persistence model.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `bigint` | PK, AUTO_INCREMENT | Surrogate key; not exposed in API |
| `AuditId` | `char(36)` | NOT NULL, UNIQUE | Public identifier (UUIDv7 recommended) |
| `EventId` | `char(36)` | NULL | Source domain event ID |
| `EventType` | `varchar(200)` | NOT NULL | Dot-notation: `domain.resource.verb` |
| `EventCategory` | `tinyint` | NOT NULL | Enum: `HasConversion<int>()` |
| `SourceSystem` | `varchar(200)` | NOT NULL | Producing system name |
| `SourceService` | `varchar(200)` | NULL | Sub-component |
| `SourceEnvironment` | `varchar(100)` | NULL | e.g. `production`, `staging` |
| `PlatformId` | `char(36)` | NULL | Platform partition ID |
| `TenantId` | `varchar(100)` | NULL | Top-level tenant boundary |
| `OrganizationId` | `varchar(100)` | NULL | Organization within tenant |
| `UserScopeId` | `varchar(200)` | NULL | User-level scope ID |
| `ScopeType` | `tinyint` | NOT NULL | Enum: `HasConversion<int>()` |
| `ActorId` | `varchar(200)` | NULL | Principal identifier |
| `ActorType` | `tinyint` | NOT NULL | Enum: `HasConversion<int>()` |
| `ActorName` | `varchar(300)` | NULL | Display name snapshot |
| `ActorIpAddress` | `varchar(45)` | NULL | IPv4 (15) or IPv6 (45) |
| `ActorUserAgent` | `varchar(500)` | NULL | HTTP User-Agent |
| `EntityType` | `varchar(200)` | NULL | Resource type e.g. `User`, `Document` |
| `EntityId` | `varchar(200)` | NULL | Resource identifier |
| `Action` | `varchar(200)` | NOT NULL | PascalCase verb e.g. `Created` |
| `Description` | `varchar(2000)` | NOT NULL | Human-readable summary |
| `BeforeJson` | `mediumtext` | NULL | Resource state before action (≤16 MB) |
| `AfterJson` | `mediumtext` | NULL | Resource state after action (≤16 MB) |
| `MetadataJson` | `text` | NULL | Arbitrary additional context |
| `CorrelationId` | `varchar(200)` | NULL | Distributed trace ID |
| `RequestId` | `varchar(200)` | NULL | Originating HTTP request ID |
| `SessionId` | `varchar(200)` | NULL | User session ID |
| `VisibilityScope` | `tinyint` | NOT NULL | Enum: `HasConversion<int>()` |
| `Severity` | `tinyint` | NOT NULL | Enum: `HasConversion<int>()` |
| `OccurredAtUtc` | `datetime(6)` | NOT NULL | When the event occurred (source time) |
| `RecordedAtUtc` | `datetime(6)` | NOT NULL | When the record was written (server time) |
| `Hash` | `varchar(64)` | NULL | HMAC-SHA256 over canonical fields |
| `PreviousHash` | `varchar(64)` | NULL | Hash of preceding record in chain |
| `IdempotencyKey` | `varchar(300)` | NULL, UNIQUE | Dedup key; NULLs are distinct (MySQL 8) |
| `IsReplay` | `tinyint(1)` | NOT NULL, DEFAULT 0 | Marks re-submitted events |
| `TagsJson` | `text` | NULL | JSON array of string tags |

**Indexes (17 total):**

| Name | Columns | Type |
|---|---|---|
| `PK_AuditEventRecords` | `Id` | Primary |
| `UX_AuditEventRecords_AuditId` | `AuditId` | Unique |
| `UX_AuditEventRecords_IdempotencyKey` | `IdempotencyKey` | Unique (nulls distinct) |
| `IX_AuditEventRecords_TenantId` | `TenantId` | Index |
| `IX_AuditEventRecords_TenantId_OccurredAt` | `TenantId, OccurredAtUtc` | Composite |
| `IX_AuditEventRecords_TenantId_Category_OccurredAt` | `TenantId, EventCategory, OccurredAtUtc` | Composite |
| `IX_AuditEventRecords_OccurredAtUtc` | `OccurredAtUtc` | Index |
| `IX_AuditEventRecords_RecordedAtUtc` | `RecordedAtUtc` | Index |
| `IX_AuditEventRecords_EventType` | `EventType` | Index |
| `IX_AuditEventRecords_EventCategory` | `EventCategory` | Index |
| `IX_AuditEventRecords_ActorId` | `ActorId` | Index |
| `IX_AuditEventRecords_CorrelationId` | `CorrelationId` | Index |
| `IX_AuditEventRecords_RequestId` | `RequestId` | Index |
| `IX_AuditEventRecords_SessionId` | `SessionId` | Index |
| `IX_AuditEventRecords_OrganizationId` | `OrganizationId` | Index |
| `IX_AuditEventRecords_EntityType_EntityId` | `EntityType, EntityId` | Composite |
| `IX_AuditEventRecords_Severity_RecordedAt` | `Severity, RecordedAtUtc` | Composite |
| `IX_AuditEventRecords_VisibilityScope` | `VisibilityScope` | Index |

---

### `AuditExportJobs`
Async export job lifecycle tracking.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `bigint` | PK, AUTO_INCREMENT | Surrogate key |
| `ExportId` | `char(36)` | NOT NULL, UNIQUE | Public job identifier |
| `RequestedBy` | `varchar(200)` | NOT NULL | Actor ID of requester |
| `ScopeType` | `tinyint` | NOT NULL | Enum |
| `ScopeId` | `varchar(200)` | NULL | Concrete scope value |
| `FilterJson` | `text` | NULL | Serialized query predicate |
| `Format` | `varchar(20)` | NOT NULL | `Json` / `Csv` / `Ndjson` |
| `Status` | `tinyint` | NOT NULL | Enum: Pending→Processing→Completed/Failed |
| `FilePath` | `varchar(1000)` | NULL | Output file path when completed |
| `ErrorMessage` | `text` | NULL | Failure description |
| `CreatedAtUtc` | `datetime(6)` | NOT NULL | Job submission time |
| `CompletedAtUtc` | `datetime(6)` | NULL | Terminal state timestamp |

**Indexes (6 total):**

| Name | Columns | Type |
|---|---|---|
| `PK_AuditExportJobs` | `Id` | Primary |
| `UX_AuditExportJobs_ExportId` | `ExportId` | Unique |
| `IX_AuditExportJobs_Status` | `Status` | Index |
| `IX_AuditExportJobs_RequestedBy` | `RequestedBy` | Index |
| `IX_AuditExportJobs_RequestedBy_Status_CreatedAt` | `RequestedBy, Status, CreatedAtUtc` | Composite |
| `IX_AuditExportJobs_ScopeType_ScopeId` | `ScopeType, ScopeId` | Composite |
| `IX_AuditExportJobs_CreatedAtUtc` | `CreatedAtUtc` | Index |

---

### `IntegrityCheckpoints`
Periodic aggregate hash snapshots for tamper detection.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `bigint` | PK, AUTO_INCREMENT | Surrogate key |
| `CheckpointType` | `varchar(100)` | NOT NULL | Open string: `hourly`, `daily`, `manual`, custom |
| `FromRecordedAtUtc` | `datetime(6)` | NOT NULL | Window start (inclusive) |
| `ToRecordedAtUtc` | `datetime(6)` | NOT NULL | Window end (exclusive) |
| `AggregateHash` | `varchar(64)` | NOT NULL | HMAC-SHA256 over ordered record hashes |
| `RecordCount` | `bigint` | NOT NULL | Records covered in hash computation |
| `CreatedAtUtc` | `datetime(6)` | NOT NULL | Checkpoint creation time |

**Indexes (4 total):**

| Name | Columns | Type |
|---|---|---|
| `PK_IntegrityCheckpoints` | `Id` | Primary |
| `IX_IntegrityCheckpoints_CheckpointType` | `CheckpointType` | Index |
| `IX_IntegrityCheckpoints_CheckpointType_FromAt` | `CheckpointType, FromRecordedAtUtc` | Composite |
| `IX_IntegrityCheckpoints_Window` | `FromRecordedAtUtc, ToRecordedAtUtc` | Composite |
| `IX_IntegrityCheckpoints_CreatedAtUtc` | `CreatedAtUtc` | Index |

---

### `IngestSourceRegistrations`
Advisory registry of known ingest sources.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `bigint` | PK, AUTO_INCREMENT | Surrogate key |
| `SourceSystem` | `varchar(200)` | NOT NULL | Must match `AuditEventRecord.SourceSystem` |
| `SourceService` | `varchar(200)` | NULL | NULL = covers all services within system |
| `IsActive` | `tinyint(1)` | NOT NULL, DEFAULT 1 | Admin toggle |
| `Notes` | `text` | NULL | Operator documentation |
| `CreatedAtUtc` | `datetime(6)` | NOT NULL | Registration creation time |

**Indexes (2 total):**

| Name | Columns | Type |
|---|---|---|
| `PK_IngestSourceRegistrations` | `Id` | Primary |
| `UX_IngestSourceRegistrations_SourceSystem_SourceService` | `SourceSystem, SourceService` | Unique (NULLs distinct) |
| `IX_IngestSourceRegistrations_IsActive` | `IsActive` | Index |

---

## Apply / Update Commands

All commands are run from the **service root**:
```
cd apps/services/platform-audit-event-service
```

### Generate a new migration
```bash
dotnet ef migrations add <MigrationName> --output-dir Data/Migrations
```
No database connection required — the `DesignTimeDbContextFactory` uses a fixed server version.

### Apply migrations to a database

**Using the EF CLI (development / staging):**
```bash
export ConnectionStrings__AuditEventDb="Server=<host>;Port=3306;Database=audit_event_db;User=<user>;Password=<pass>;SslMode=Required;"
dotnet ef database update
```

**Using the idempotent SQL script (production / CI):**
```bash
# 1. Generate the script (no DB connection needed)
dotnet ef migrations script --idempotent --output analysis/deploy_InitialSchema_idempotent.sql

# 2. Apply via MySQL CLI
mysql -h <host> -u <user> -p audit_event_db < analysis/deploy_InitialSchema_idempotent.sql
```
The idempotent script wraps each migration in a stored procedure that checks `__EFMigrationsHistory` before executing — safe to run multiple times.

### List applied migrations
```bash
export ConnectionStrings__AuditEventDb="..."
dotnet ef migrations list
```

### Roll back a migration (development only)
```bash
export ConnectionStrings__AuditEventDb="..."
dotnet ef database update <PreviousMigrationName>
# Then remove the migration file:
dotnet ef migrations remove
```

### Generate a plain (non-idempotent) SQL script
```bash
dotnet ef migrations script
```
Useful for manual review or DBA sign-off before applying in production.

---

## Environment Variable Reference

| Variable | Description |
|---|---|
| `ConnectionStrings__AuditEventDb` | Full MySQL connection string for CLI and runtime |
| `Database:Provider` | `MySQL` (durable) or `InMemory` (dev/test) — controls runtime provider selection |
| `Database:MigrateOnStartup` | `true` to auto-apply migrations on service boot (MySQL mode only) |
| `Database:VerifyConnectionOnStartup` | `true` to probe DB connectivity on startup |

---

## Build Status

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Migration compiles cleanly. The `InitialSchema` migration is tracked in `__EFMigrationsHistory` and will not be re-applied on subsequent `dotnet ef database update` runs.
