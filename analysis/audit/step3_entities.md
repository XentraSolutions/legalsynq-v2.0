# Step 3 — Core Data Model

**Date:** 2026-03-30  
**Phase:** Entity models and enums  
**Status:** Complete — build ✅ 0 errors, 0 warnings

---

## Entities Created

### 1. `AuditEventRecord`
**Path:** `Models/Entities/AuditEventRecord.cs`  
**Namespace:** `PlatformAuditEventService.Entities`

The canonical append-only persistence model. Represents one auditable event at full fidelity.

| Field | Type | Mutability | Notes |
|---|---|---|---|
| `Id` | `long` | init | Auto-increment surrogate PK for DB efficiency and ordered pagination |
| `AuditId` | `Guid` | init | Public stable identifier (UUIDv7 recommended) |
| `EventId` | `Guid?` | init | Source-assigned domain event ID; not guaranteed unique on retries |
| `EventType` | `string` | init | Dot-notation code e.g. `user.login.succeeded` |
| `EventCategory` | `EventCategory` | init | Typed enum: Security, Access, Business, Administrative, System, Compliance, DataChange, Integration, Performance |
| `SourceSystem` | `string` | init | Logical system name e.g. `identity-service` |
| `SourceService` | `string?` | init | Sub-component within SourceSystem |
| `SourceEnvironment` | `string?` | init | Deployment env tag from source (production/staging/dev) |
| `PlatformId` | `Guid?` | init | Platform partition; null for single-platform deploys |
| `TenantId` | `string?` | init | Top-level tenancy boundary |
| `OrganizationId` | `string?` | init | Organization within a tenant |
| `UserScopeId` | `string?` | init | User-level scope; may differ from ActorId in impersonation |
| `ScopeType` | `ScopeType` | init | Typed enum: Global, Platform, Tenant, Organization, User, Service |
| `ActorId` | `string?` | init | Principal identifier |
| `ActorType` | `ActorType` | init | Typed enum: User, ServiceAccount, System, Api, Scheduler, Anonymous, Support |
| `ActorName` | `string?` | init | Display name snapshot at time of event |
| `ActorIpAddress` | `string?` | init | IPv4 or IPv6; max 45 chars |
| `ActorUserAgent` | `string?` | init | User-Agent string |
| `EntityType` | `string?` | init | PascalCase resource name e.g. `User`, `Document` |
| `EntityId` | `string?` | init | Resource identifier |
| `Action` | `string` | init | PascalCase verb e.g. `Created`, `Approved` |
| `Description` | `string` | init | Human-readable summary; max 2000 chars |
| `BeforeJson` | `string?` | init | JSON state snapshot before mutation |
| `AfterJson` | `string?` | init | JSON state snapshot after mutation |
| `MetadataJson` | `string?` | init | Arbitrary JSON context object |
| `CorrelationId` | `string?` | init | W3C traceparent or X-Correlation-Id |
| `RequestId` | `string?` | init | HTTP request identifier |
| `SessionId` | `string?` | init | User session identifier |
| `VisibilityScope` | `VisibilityScope` | init | Typed enum: Platform, Tenant, Organization, User, Internal |
| `Severity` | `SeverityLevel` | init | Typed enum: Debug, Info, Notice, Warn, Error, Critical, Alert |
| `OccurredAtUtc` | `DateTimeOffset` | init | When the event happened in the source system |
| `RecordedAtUtc` | `DateTimeOffset` | init | When the ingest pipeline wrote this record |
| `Hash` | `string?` | init | HMAC-SHA256 over canonical fields |
| `PreviousHash` | `string?` | init | Hash of preceding record in same (TenantId, SourceSystem) chain |
| `IdempotencyKey` | `string?` | init | Source-provided dedup key; distinct from EventId |
| `IsReplay` | `bool` | init | True for re-submissions; original record is preserved |
| `TagsJson` | `string?` | init | JSON string array of ad-hoc tags |

**Total fields: 38**

---

### 2. `AuditExportJob`
**Path:** `Models/Entities/AuditExportJob.cs`  
**Namespace:** `PlatformAuditEventService.Entities`

Tracks an asynchronous request to export a filtered slice of audit records.

| Field | Type | Mutability | Notes |
|---|---|---|---|
| `Id` | `long` | init | Auto-increment surrogate PK |
| `ExportId` | `Guid` | init | Public stable identifier |
| `RequestedBy` | `string` | init | ActorId of the requesting principal |
| `ScopeType` | `ScopeType` | init | Typed enum — enforces what the worker may include |
| `ScopeId` | `string?` | init | Concrete scope value (tenantId, userId, etc.) |
| `FilterJson` | `string?` | init | Serialized query predicate for the export worker |
| `Format` | `string` | init | "Json" \| "Csv" \| "Ndjson" |
| `Status` | `ExportStatus` | set | Lifecycle state — updated by the worker |
| `FilePath` | `string?` | set | Set when Status = Completed |
| `ErrorMessage` | `string?` | set | Set when Status = Failed |
| `CreatedAtUtc` | `DateTimeOffset` | init | Submission timestamp |
| `CompletedAtUtc` | `DateTimeOffset?` | set | Set on terminal state |

**Total fields: 12**

---

### 3. `IntegrityCheckpoint`
**Path:** `Models/Entities/IntegrityCheckpoint.cs`  
**Namespace:** `PlatformAuditEventService.Entities`

Periodic snapshot of the aggregate hash over a time window for tamper detection.

| Field | Type | Mutability | Notes |
|---|---|---|---|
| `Id` | `long` | init | Auto-increment surrogate PK |
| `CheckpointType` | `string` | init | Open string: "hourly", "daily", "manual", custom label |
| `FromRecordedAtUtc` | `DateTimeOffset` | init | Window start (inclusive) |
| `ToRecordedAtUtc` | `DateTimeOffset` | init | Window end (exclusive) |
| `AggregateHash` | `string` | init | HMAC-SHA256 over sorted record hashes in window |
| `RecordCount` | `long` | init | Row count in window — drift signals deletion |
| `CreatedAtUtc` | `DateTimeOffset` | init | When this checkpoint was written |

**Total fields: 7**

---

### 4. `IngestSourceRegistration`
**Path:** `Models/Entities/IngestSourceRegistration.cs`  
**Namespace:** `PlatformAuditEventService.Entities`

Advisory registry of known source systems. Not a hard enforcement gate.

| Field | Type | Mutability | Notes |
|---|---|---|---|
| `Id` | `long` | init | Auto-increment surrogate PK |
| `SourceSystem` | `string` | init | Must match AuditEventRecord.SourceSystem |
| `SourceService` | `string?` | init | Null = covers entire system |
| `IsActive` | `bool` | set | Mutable — supports administrative pause |
| `Notes` | `string?` | set | Operator documentation; not exposed via API |
| `CreatedAtUtc` | `DateTimeOffset` | init | Registration creation timestamp |

**Total fields: 6**

---

## Enums Created

### 1. `EventCategory`
**Path:** `Models/Enums/EventCategory.cs`

| Value | Int | Purpose |
|---|---|---|
| Security | 1 | Auth, authorization, threats, intrusions |
| Access | 2 | Read/list/search operations |
| Business | 3 | Domain workflow events |
| Administrative | 4 | Settings, role, user management |
| System | 5 | Platform internals, startup, jobs |
| Compliance | 6 | HIPAA/SOC-2/regulatory events |
| DataChange | 7 | Explicit before/after mutations |
| Integration | 8 | Cross-service calls, webhooks, external APIs |
| Performance | 9 | Latency/throughput observations |

---

### 2. `SeverityLevel`
**Path:** `Models/Enums/SeverityLevel.cs`

| Value | Int | Maps to |
|---|---|---|
| Debug | 1 | Verbose trace |
| Info | 2 | Normal operational activity (default) |
| Notice | 3 | Significant but normal |
| Warn | 4 | Recoverable condition |
| Error | 5 | Failed operation |
| Critical | 6 | Service degradation |
| Alert | 7 | System-level failure / security breach |

Numeric values enable range comparisons (`Severity >= Error`). Maps loosely to syslog / OpenTelemetry conventions.

---

### 3. `VisibilityScope`
**Path:** `Models/Enums/VisibilityScope.cs`

| Value | Int | Who can read |
|---|---|---|
| Platform | 1 | Platform super-admins only |
| Tenant | 2 | Tenant admins + platform admins |
| Organization | 3 | Org-level roles + admins |
| User | 4 | The individual user + admins |
| Internal | 5 | No external access — integrity/system only |

Enforced by QueryAuth middleware against JWT claims.

---

### 4. `ScopeType`
**Path:** `Models/Enums/ScopeType.cs`

| Value | Int | Meaningful IDs |
|---|---|---|
| Global | 1 | None |
| Platform | 2 | PlatformId |
| Tenant | 3 | TenantId |
| Organization | 4 | TenantId + OrganizationId |
| User | 5 | TenantId + UserScopeId |
| Service | 6 | SourceSystem |

---

### 5. `ActorType`
**Path:** `Models/Enums/ActorType.cs`

| Value | Int | Description |
|---|---|---|
| User | 1 | Human authenticated via identity system |
| ServiceAccount | 2 | M2M client / managed identity |
| System | 3 | Platform internals, background jobs |
| Api | 4 | External API key caller |
| Scheduler | 5 | Cron job or scheduled task |
| Anonymous | 6 | Unauthenticated caller |
| Support | 7 | Internal support/operator impersonation |

---

### 6. `ExportStatus`
**Path:** `Models/Enums/ExportStatus.cs`

| Value | Int | Terminal? |
|---|---|---|
| Pending | 1 | No |
| Processing | 2 | No |
| Completed | 3 | Yes |
| Failed | 4 | Yes |
| Cancelled | 5 | Yes |
| Expired | 6 | Yes (purged output file) |

---

## Design Choices

### Explicit int backing values on all enums
All enum members have explicit integer backing values. This prevents value renumbering if members are reordered or inserted and makes database storage (int column) unambiguous.

### `long` surrogate PKs, `Guid` public identifiers
All entities use `long Id` as the database PK (auto-increment; efficient for MySQL B-tree indexes and clustered scans) paired with a `Guid` as the stable public identifier exposed through the API. This separates DB internals from API contracts.

### `DateTimeOffset` throughout
`DateTimeOffset` rather than `DateTime` preserves UTC offset information and avoids the ambiguity of `DateTimeKind.Unspecified`. All timestamps are documented as UTC with an intent to store them as `datetime` columns (MySQL does not store offset; convention is enforced at the application layer).

### `init`-only setters on `AuditEventRecord`
Every field is `init`-only. Records are written once by the ingest pipeline and never mutated. This makes the append-only contract explicit at the compiler level — a setter cannot accidentally be called on a post-ingest record.

### Mixed mutability on `AuditExportJob`
Identity and scope fields are `init`-only; lifecycle fields (`Status`, `FilePath`, `ErrorMessage`, `CompletedAtUtc`) use regular setters so the export worker can update them via tracked EF entities without reconstructing the object.

### `string?` for all JSON columns
`BeforeJson`, `AfterJson`, `MetadataJson`, `TagsJson`, `FilterJson` are stored as raw text strings. No deserialization is done at the model layer. This keeps the entities schema-agnostic: the JSON shape is owned by the source, not by the audit service. EF maps these to `text` columns with no JSON type constraint.

### `PreviousHash` scoped chain vs. global chain
`PreviousHash` links to the preceding record in the same `(TenantId, SourceSystem)` chain rather than a global sequence. A global chain creates write serialization contention at scale. Scoped chains can be verified independently per tenant/source and are more practical for multi-tenant HIPAA deployments.

### `IntegrityCheckpoint.CheckpointType` as open string
Using a free string rather than an enum allows compliance teams to define custom checkpoint cadences (e.g. `"pre-audit-2026-Q2"`) without a code or schema change. Known values are documented by convention.

### `IngestSourceRegistration` is advisory
The source registry does not gate ingestion. It provides extensibility hooks for future per-source configuration (rate limits, event type allowlists, schema versions, per-source retention) without requiring a significant model change. `IsActive` is intentionally mutable to allow administrative pausing.

### Namespace isolation from existing constants
Existing `EventCategory`, `EventSeverity`, `EventOutcome` static string-constant classes live in `PlatformAuditEventService.Models` and are preserved for backward compatibility with the existing `AuditEvent` model and service layer.

New typed enums were initially placed in `PlatformAuditEventService.Models.Enums` but this caused a build error: because C# resolves names from parent namespaces before `using`-imported namespaces, entity classes declared in `PlatformAuditEventService.Models.Entities` resolved `EventCategory` to the static class rather than the enum. This cannot be disambiguated by a `using` directive alone.

Resolution: enums are in `PlatformAuditEventService.Enums` and entities in `PlatformAuditEventService.Entities` — both top-level namespaces, entirely disjoint from `PlatformAuditEventService.Models`. The file paths (`Models/Enums/`, `Models/Entities/`) follow the project's folder convention; the namespaces do not need to mirror the folder hierarchy.

### No navigation properties yet
Entity relationships (e.g. `AuditExportJob` → `IngestSourceRegistration`) are not modelled as EF navigation properties at this stage. EF configuration and relationships will be added to `AuditEventDbContext` in the next step (DB wiring) to avoid coupling the pure domain models to EF concerns.

---

## File Layout

```
Models/
├── Entities/
│   ├── AuditEventRecord.cs          (38 fields, all init-only, append-only)
│   ├── AuditExportJob.cs            (12 fields, mixed init/mutable)
│   ├── IntegrityCheckpoint.cs       (7 fields, all init-only)
│   └── IngestSourceRegistration.cs  (6 fields, mixed init/mutable)
└── Enums/
    ├── EventCategory.cs             (9 values)
    ├── SeverityLevel.cs             (7 values)
    ├── VisibilityScope.cs           (5 values)
    ├── ScopeType.cs                 (6 values)
    ├── ActorType.cs                 (7 values)
    └── ExportStatus.cs              (6 values)
```

---

## Next Steps (Step 4+)

1. **EF Core configuration** — add `DbSet` properties for all 4 entities to `AuditEventDbContext`; write `IEntityTypeConfiguration<T>` classes with column types, indexes, and constraints
2. **Migration** — `dotnet ef migrations add Step3Entities` to produce the initial MySQL schema
3. **Repository interfaces** — `IAuditEventRecordRepository`, `IAuditExportJobRepository`, `IIntegrityCheckpointRepository`, `IIngestSourceRegistrationRepository`
4. **EF implementations** — concrete repositories using `IDbContextFactory<AuditEventDbContext>` + async query patterns
5. **InMemory implementations** — thread-safe in-memory backends for development/test parity
6. **Ingest DTO mapping** — map `IngestAuditEventRequest` → `AuditEventRecord` with default assignment (AuditId generation, RecordedAtUtc, hash computation, IsReplay=false)
