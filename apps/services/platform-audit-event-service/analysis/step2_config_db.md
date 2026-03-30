# Step 2 — Configuration & Database Bootstrap

**Date:** 2026-03-30  
**Phase:** Configuration hardening + MySQL wiring  
**Status:** Complete — build ✅ 0 errors, 0 warnings

---

## Settings Added

### `AuditService` section (updated)

| Setting | Type | Default | Purpose |
|---|---|---|---|
| `ServiceName` | string | "Platform Audit/Event Service" | Display name in health/swagger |
| `Version` | string | "1.0.0" | Version surfaced in Swagger |
| `EnvironmentTag` | string? | null | Optional env label in health responses |
| `ExposeSwagger` | bool | false | Force Swagger UI even outside Development |
| `AllowedCorsOrigins` | string[] | [] | Allowed CORS origins (empty = deny all cross-origin) |

Environment variable prefix: `AuditService__`

---

### `Database` section (new)

| Setting | Type | Default | Purpose |
|---|---|---|---|
| `Provider` | string | "InMemory" | "InMemory" \| "MySQL" |
| `ConnectionString` | string? | null | MySQL connection string (or use `ConnectionStrings:AuditEventDb`) |
| `ServerVersion` | string | "8.0.0-mysql" | Pomelo server version hint |
| `MaxPoolSize` | int | 100 | Connection pool max |
| `MinPoolSize` | int | 5 | Connection pool min |
| `ConnectionTimeoutSeconds` | int | 30 | TCP connect timeout |
| `CommandTimeoutSeconds` | int | 60 | EF query timeout |
| `MigrateOnStartup` | bool | false | Run `MigrateAsync()` at startup |
| `VerifyConnectionOnStartup` | bool | true | Run non-fatal probe at startup |
| `StartupProbeTimeoutSeconds` | int | 10 | Probe timeout |
| `EnableSensitiveDataLogging` | bool | false | Log SQL parameter values (dev only) |
| `EnableDetailedErrors` | bool | false | Full SQL in exceptions (dev only) |

Environment variable prefix: `Database__`

---

### `Integrity` section (new — replaces `AuditService:IntegrityHmacKeyBase64`)

| Setting | Type | Default | Purpose |
|---|---|---|---|
| `HmacKeyBase64` | string? | "" | Base64-encoded 32-byte HMAC secret |
| `Algorithm` | string | "HMAC-SHA256" | Hash algorithm (reserved for agility) |
| `VerifyOnRead` | bool | false | Verify hash on every read |
| `FlagTamperedRecords` | bool | true | Add tamper flag on mismatch (when VerifyOnRead=true) |

Environment variable: `Integrity__HmacKeyBase64`  
Generate key: `openssl rand -base64 32`

---

### `IngestAuth` section (new)

| Setting | Type | Default | Purpose |
|---|---|---|---|
| `Mode` | string | "None" | "None" \| "ApiKey" \| "Bearer" |
| `ApiKey` | string? | null | Shared secret for ApiKey mode |
| `ApiKeyHeader` | string | "X-Api-Key" | Header to read key from |
| `RequiredClaims` | string[] | [] | JWT claims required in Bearer mode |
| `RequiredRole` | string? | null | JWT role required in Bearer mode |
| `AllowedSources` | string[] | [] | Restrict ingestion to specific source values |

Environment variable: `IngestAuth__ApiKey`

---

### `QueryAuth` section (new)

| Setting | Type | Default | Purpose |
|---|---|---|---|
| `Mode` | string | "None" | "None" \| "ApiKey" \| "Bearer" |
| `PlatformAdminRoles` | string[] | ["platform-audit-admin"] | Cross-tenant read roles |
| `TenantAdminRoles` | string[] | ["tenant-admin","compliance-officer"] | Scoped read roles |
| `EnforceTenantScope` | bool | true | Restrict results to caller's tenantId claim |
| `MaxPageSize` | int | 500 | Hard cap on page size |
| `ExposeIntegrityHash` | bool | false | Include hash in query results |

---

### `Retention` section (new)

| Setting | Type | Default | Purpose |
|---|---|---|---|
| `DefaultRetentionDays` | int | 0 | 0 = indefinite |
| `CategoryOverrides` | dict | {} | Per-category override (e.g. `{"system":90}`) |
| `TenantOverrides` | dict | {} | Per-tenant override |
| `JobEnabled` | bool | false | Enable RetentionPolicyJob |
| `JobCronUtc` | string | "0 2 * * *" | Daily 02:00 UTC |
| `MaxDeletesPerRun` | int | 10000 | Batch cap per run |
| `ArchiveBeforeDelete` | bool | false | Archive to Export provider before delete |

---

### `Export` section (new)

| Setting | Type | Default | Purpose |
|---|---|---|---|
| `Provider` | string | "None" | "None" \| "Local" \| "S3" \| "AzureBlob" |
| `SupportedFormats` | string[] | ["Json","Csv","Ndjson"] | Export file formats |
| `MaxRecordsPerFile` | int | 100000 | File split threshold |
| `LocalOutputPath` | string? | null | Path for Local provider |
| `S3BucketName` | string? | null | S3 bucket |
| `S3KeyPrefix` | string? | null | S3 key prefix |
| `FileNamePrefix` | string | "audit-export" | Output file name prefix |

---

## DbContext Summary — `AuditEventDbContext`

**File:** `Data/AuditEventDbContext.cs`  
**Namespace:** `PlatformAuditEventService.Data`

### Entity configuration for `AuditEvent`

| Column | Type | Nullable | Constraints |
|---|---|---|---|
| `Id` | char(36) | No | PK, ValueGeneratedNever |
| `Source` | varchar(200) | No | Required |
| `EventType` | varchar(200) | No | Required |
| `Category` | varchar(100) | No | Required |
| `Severity` | varchar(20) | No | Default: INFO |
| `Description` | varchar(2000) | No | Required |
| `Outcome` | varchar(20) | No | Default: SUCCESS |
| `TenantId` | varchar(100) | Yes | |
| `ActorId` | varchar(200) | Yes | |
| `ActorLabel` | varchar(300) | Yes | |
| `TargetType` | varchar(200) | Yes | |
| `TargetId` | varchar(200) | Yes | |
| `IpAddress` | varchar(45) | Yes | IPv6 max = 45 chars |
| `UserAgent` | varchar(500) | Yes | |
| `CorrelationId` | varchar(200) | Yes | |
| `Metadata` | text | Yes | JSON string, no type constraint |
| `IntegrityHash` | varchar(64) | Yes | HMAC-SHA256 hex = 64 chars |
| `OccurredAtUtc` | datetime | No | |
| `IngestedAtUtc` | datetime | No | |

### Indexes

| Index Name | Columns | Purpose |
|---|---|---|
| `IX_AuditEvents_TenantId_OccurredAt` | (TenantId, OccurredAtUtc) | Primary query pattern |
| `IX_AuditEvents_Source_EventType` | (Source, EventType) | Event type feeds |
| `IX_AuditEvents_Category_Severity_Outcome` | (Category, Severity, Outcome) | Security dashboards |
| `IX_AuditEvents_ActorId` | ActorId | Actor audit trail |
| `IX_AuditEvents_TargetType_TargetId` | (TargetType, TargetId) | Resource lookup |
| `IX_AuditEvents_CorrelationId` | CorrelationId | Trace correlation |
| `IX_AuditEvents_IngestedAt` | IngestedAtUtc | Retention job, export |

---

## Startup Behavior

### DB provider switching (Program.cs)

```
Database:Provider = "InMemory"  →  UseInMemoryDatabase + InMemoryAuditEventRepository (Singleton)
Database:Provider = "MySQL"     →  UseMySql (Pomelo) + EfAuditEventRepository (Scoped)
                                   + IDbContextFactory<AuditEventDbContext>
```

### Startup DB connectivity probe

When `Database:Provider = "MySQL"` and `Database:VerifyConnectionOnStartup = true`:
1. Creates a scoped `AuditEventDbContext` from the factory
2. Calls `CanConnectAsync()` with a configurable timeout (`StartupProbeTimeoutSeconds`)
3. **Non-fatal**: timeout or connection failure logs a `Warning` but does NOT abort startup
4. Service remains up so Kubernetes / load-balancer health checks can report a degraded state rather than a crash loop

### Startup migration (opt-in)

When `Database:MigrateOnStartup = true`:
1. Calls `db.Database.MigrateAsync()` before the HTTP server starts
2. **Fatal**: migration failure throws and aborts startup (by design — running with wrong schema is dangerous)
3. Default is `false` — use explicit `dotnet ef database update` for production deploys

---

## New Files

| File | Purpose |
|---|---|
| `Configuration/DatabaseOptions.cs` | DB provider, connection, pool, timeouts, startup behavior |
| `Configuration/IntegrityOptions.cs` | HMAC key, algorithm, verify-on-read settings |
| `Configuration/IngestAuthOptions.cs` | Auth mode, API key, source allowlist for ingestion |
| `Configuration/QueryAuthOptions.cs` | Auth mode, roles, tenant scope enforcement for reads |
| `Configuration/RetentionOptions.cs` | Retention windows, job schedule, archive-before-delete |
| `Configuration/ExportOptions.cs` | Export provider, formats, S3/Azure/local settings |
| `Data/DesignTimeDbContextFactory.cs` | EF CLI migrations factory (reads `ConnectionStrings__AuditEventDb`) |
| `Repositories/EfAuditEventRepository.cs` | EF Core MySQL-backed append-only repository |

## Modified Files

| File | Change |
|---|---|
| `Configuration/AuditServiceOptions.cs` | Refactored to service-level only (HMAC key moved to IntegrityOptions) |
| `Data/AuditEventDbContext.cs` | Full column/index config, UserAgent + Metadata added |
| `Services/AuditEventService.cs` | Uses `IOptions<IntegrityOptions>` + `VerifyOnRead` logic |
| `Program.cs` | Full DI wiring, provider switch, startup probe, migration support |
| `appsettings.json` | All 7 config sections with documented defaults |
| `appsettings.Development.json` | Dev overrides: InMemory, Swagger on, auth off, detailed errors |

---

## Environment Variable Guidance

### Minimal production set (MySQL)

```bash
# Database
Database__Provider=MySQL
Database__ConnectionString="Server=<host>;Port=3306;Database=audit_event_db;User=<user>;Password=<pass>;SslMode=Required;"
Database__MigrateOnStartup=false
Database__VerifyConnectionOnStartup=true

# Integrity (REQUIRED — generate with: openssl rand -base64 32)
Integrity__HmacKeyBase64=<your-32-byte-base64-secret>
Integrity__VerifyOnRead=true

# Service
AuditService__AllowedCorsOrigins__0=https://portal.yourapp.com

# Auth (when enabling)
IngestAuth__Mode=ApiKey
IngestAuth__ApiKey=<your-secure-api-key>
QueryAuth__Mode=Bearer
QueryAuth__EnforceTenantScope=true
```

### Connection string alternative (ASP.NET Core standard)

```bash
ConnectionStrings__AuditEventDb="Server=<host>;Port=3306;Database=audit_event_db;User=<user>;Password=<pass>;SslMode=Required;"
```

Connection string resolution order:
1. `Database__ConnectionString` (explicit)
2. `ConnectionStrings__AuditEventDb` (standard convention)

---

## Migration Commands

```bash
cd apps/services/platform-audit-event-service

# Create first migration
ConnectionStrings__AuditEventDb="<conn>" \
  dotnet ef migrations add InitialAuditSchema --output-dir Data/Migrations

# Apply to database
ConnectionStrings__AuditEventDb="<conn>" \
  dotnet ef database update

# List applied migrations
ConnectionStrings__AuditEventDb="<conn>" \
  dotnet ef migrations list
```

---

## Next Steps (Step 3+)

1. **Run initial migration** — create `Data/Migrations/` with `InitialAuditSchema`
2. **IngestAuth enforcement** — middleware reads `IngestAuthOptions.Mode` and validates `X-Api-Key` / JWT
3. **QueryAuth enforcement** — middleware reads `QueryAuthOptions.Mode` and applies tenant scope filtering
4. **RetentionPolicyJob** — implement `IHostedService` with configurable cron via `RetentionOptions`
5. **Export endpoints** — `GET /api/auditevents/export` using `ExportOptions.Provider`
6. **OpenTelemetry** — add distributed tracing for correlation with upstream services
