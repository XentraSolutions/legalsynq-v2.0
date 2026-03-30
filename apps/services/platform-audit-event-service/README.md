# Platform Audit/Event Service

Platform Audit/Event Service is a standalone, independently deployable, and portable platform service that ingests business, security, access, administrative, and system activity from distributed systems, normalizes it into a canonical event model, and persists immutable, tamper-evident audit records. It exposes secure, role-aware retrieval interfaces for administrative systems, tenant applications, individual users, reporting tools, compliance workflows, and future downstream consumers.

---

## Purpose

- Receive activity feeds from distributed microservices
- Normalize them into a canonical audit/event model
- Persist immutable, tamper-evident records with HMAC-SHA256 integrity hashing
- Support secure retrieval for platform admin, tenant, user, reporting, and compliance interfaces
- Provide an event-ready foundation for future integrations and downstream consumers

---

## Architecture

```
platform-audit-event-service/
├── Controllers/         HealthController, AuditEventsController
├── Services/            IAuditEventService, AuditEventService
├── Repositories/        IAuditEventRepository, InMemoryAuditEventRepository, EfAuditEventRepository
├── Models/              AuditEvent (sealed record), EventCategory, EventSeverity, EventOutcome
├── DTOs/                IngestAuditEventRequest, AuditEventResponse, ApiResponse<T>, PagedResult<T>
├── Validators/          IngestAuditEventRequestValidator (FluentValidation)
├── Middleware/          ExceptionMiddleware, CorrelationIdMiddleware
├── Jobs/                RetentionPolicyJob (placeholder)
├── Utilities/           IntegrityHasher, AuditEventMapper, TraceIdAccessor
├── Data/                AuditEventDbContext, DesignTimeDbContextFactory
├── Configuration/       AuditServiceOptions, DatabaseOptions, IntegrityOptions,
│                        IngestAuthOptions, QueryAuthOptions, RetentionOptions, ExportOptions
├── Docs/                architecture_overview.md
├── Examples/            Sample ingestion payloads
├── analysis/            Step-by-step implementation reports
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```

---

## API Endpoints

| Method | Path                    | Description                                   |
|--------|-------------------------|-----------------------------------------------|
| GET    | `/HealthCheck`          | Service health, version, and event count      |
| GET    | `/health`               | ASP.NET Core built-in health endpoint         |
| POST   | `/api/auditevents`      | Ingest a single audit event                   |
| GET    | `/api/auditevents/{id}` | Retrieve one event by ID                      |
| GET    | `/api/auditevents`      | Query events with filters + pagination        |
| GET    | `/swagger`              | Swagger UI (Development or when ExposeSwagger=true) |

---

## Canonical Event Model

| Field           | Required | Description                                                      |
|-----------------|----------|------------------------------------------------------------------|
| `source`        | Yes      | Originating service (e.g. `identity-service`)                    |
| `eventType`     | Yes      | Dot-notation code (e.g. `user.login`, `document.uploaded`)       |
| `category`      | Yes      | `security` \| `access` \| `business` \| `admin` \| `system`     |
| `severity`      | Yes      | `DEBUG` \| `INFO` \| `WARN` \| `ERROR` \| `CRITICAL`            |
| `description`   | Yes      | Human-readable description                                       |
| `outcome`       | Yes      | `SUCCESS` \| `FAILURE` \| `PARTIAL` \| `UNKNOWN`                |
| `tenantId`      | No       | Scoping tenant (null for platform-level events)                  |
| `actorId`       | No       | Acting user or service principal                                 |
| `targetType`    | No       | Resource type (e.g. `User`, `Document`)                          |
| `targetId`      | No       | Resource identifier                                              |
| `correlationId` | No       | Distributed trace / request correlation ID                       |
| `metadata`      | No       | Arbitrary JSON string for extended context                       |
| `integrityHash` | Auto     | HMAC-SHA256 over canonical fields — computed at ingest           |
| `occurredAtUtc` | No       | Time in source system (defaults to ingest time if omitted)       |

---

## Configuration Reference

### Quick-start (Development — InMemory)

No additional configuration required. The service starts with InMemory storage by default.

### Quick-start (Production — MySQL)

Set these environment variables:

```bash
# Required
Database__Provider=MySQL
ConnectionStrings__AuditEventDb="Server=<host>;Port=3306;Database=audit_event_db;User=<user>;Password=<pass>;SslMode=Required;"
Integrity__HmacKeyBase64=<base64-32-byte-key>   # openssl rand -base64 32

# Recommended
Database__VerifyConnectionOnStartup=true
Integrity__VerifyOnRead=true
AuditService__AllowedCorsOrigins__0=https://your-portal.example.com
```

### Full configuration sections

#### `AuditService`

| Key | Default | Notes |
|-----|---------|-------|
| `ServiceName` | "Platform Audit/Event Service" | Shown in Swagger/health |
| `Version` | "1.0.0" | Shown in Swagger |
| `ExposeSwagger` | false | Force Swagger outside Development |
| `AllowedCorsOrigins` | [] | Empty = deny all cross-origin |

#### `Database`

| Key | Default | Notes |
|-----|---------|-------|
| `Provider` | "InMemory" | "InMemory" or "MySQL" |
| `ConnectionString` | null | Overridden by `ConnectionStrings:AuditEventDb` |
| `ServerVersion` | "8.0.0-mysql" | Pomelo server version hint |
| `MaxPoolSize` | 100 | |
| `CommandTimeoutSeconds` | 60 | |
| `MigrateOnStartup` | false | Run migrations at startup (opt-in) |
| `VerifyConnectionOnStartup` | true | Non-fatal probe at startup |
| `StartupProbeTimeoutSeconds` | 10 | |
| `EnableSensitiveDataLogging` | false | NEVER true in production |
| `EnableDetailedErrors` | false | Dev only |

#### `Integrity`

| Key | Default | Notes |
|-----|---------|-------|
| `HmacKeyBase64` | "" | **Required**. 32-byte base64 key. |
| `VerifyOnRead` | false | Verify hash on every read |
| `FlagTamperedRecords` | true | Flag mismatches in responses |

#### `IngestAuth`

| Key | Default | Notes |
|-----|---------|-------|
| `Mode` | "None" | "None" \| "ApiKey" \| "Bearer" |
| `ApiKey` | null | Required when Mode=ApiKey |
| `ApiKeyHeader` | "X-Api-Key" | |
| `AllowedSources` | [] | Restrict by source value |

#### `QueryAuth`

| Key | Default | Notes |
|-----|---------|-------|
| `Mode` | "None" | "None" \| "ApiKey" \| "Bearer" |
| `PlatformAdminRoles` | ["platform-audit-admin"] | Cross-tenant access |
| `TenantAdminRoles` | ["tenant-admin","compliance-officer"] | Scoped access |
| `EnforceTenantScope` | true | Restrict to caller's tenantId claim |
| `MaxPageSize` | 500 | Hard cap on page size |

#### `Retention`

| Key | Default | Notes |
|-----|---------|-------|
| `DefaultRetentionDays` | 0 | 0 = indefinite |
| `CategoryOverrides` | {} | e.g. `{"system": 90}` |
| `JobEnabled` | false | Enable retention job |
| `JobCronUtc` | "0 2 * * *" | Daily 02:00 UTC |

#### `Export`

| Key | Default | Notes |
|-----|---------|-------|
| `Provider` | "None" | "None" \| "Local" \| "S3" \| "AzureBlob" |
| `MaxRecordsPerFile` | 100000 | |
| `SupportedFormats` | ["Json","Csv","Ndjson"] | |

---

## Database Migrations

```bash
cd apps/services/platform-audit-event-service

# Create initial migration
ConnectionStrings__AuditEventDb="<conn>" \
  dotnet ef migrations add InitialAuditSchema --output-dir Data/Migrations

# Apply to database
ConnectionStrings__AuditEventDb="<conn>" \
  dotnet ef database update
```

---

## Run Instructions

```bash
cd apps/services/platform-audit-event-service

# Restore packages
dotnet restore

# Development run (InMemory, port 5007)
dotnet run

# With MySQL
Database__Provider=MySQL \
ConnectionStrings__AuditEventDb="<conn>" \
Integrity__HmacKeyBase64="$(openssl rand -base64 32)" \
dotnet run
```

- Swagger UI: `http://localhost:5007/swagger`
- Health check: `http://localhost:5007/HealthCheck`

---

## Production Checklist

- [ ] Set `Database__Provider=MySQL` and provide connection string
- [ ] Generate and inject `Integrity__HmacKeyBase64` via secrets manager (never commit)
- [ ] Run `dotnet ef database update` before deploying (or set `MigrateOnStartup=true` with a controlled rollout)
- [ ] Set `IngestAuth__Mode=ApiKey` or `Bearer` — never run `Mode=None` publicly
- [ ] Set `QueryAuth__Mode=Bearer` with role enforcement
- [ ] Set `AuditService__AllowedCorsOrigins` to known origins (remove `*`)
- [ ] Set `Integrity__VerifyOnRead=true` for compliance environments
- [ ] Disable `EnableSensitiveDataLogging` and `EnableDetailedErrors`
- [ ] Set `AuditService__ExposeSwagger=false` (Swagger is Development-only by default)
