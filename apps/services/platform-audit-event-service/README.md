# Platform Audit/Event Service

Standalone, independently deployable microservice for ingesting, storing, querying, and exporting
tamper-evident audit records from distributed systems in the LegalSynq platform.

---

## Purpose

- Receive activity feeds from distributed microservices via authenticated internal endpoints
- Normalize events into a canonical audit model with rich context (scope, actor, entity, action)
- Persist immutable, tamper-evident records with HMAC-SHA256 cryptographic hash chain integrity
- Support secure retrieval for platform admin, tenant, user, reporting, and compliance interfaces
- Provide an event-ready foundation for future export, streaming, and downstream consumer integrations

---

## Quick Start

```bash
cd apps/services/platform-audit-event-service
dotnet run
# Swagger UI: http://localhost:5007/swagger
# Health:     http://localhost:5007/health
```

No additional setup needed for development — runs with in-memory storage and no auth by default.

---

## Architecture

```
platform-audit-event-service/
├── Controllers/         AuditEventIngestController (POST /internal/audit/*)
│                        AuditEventsController (legacy GET/POST /api/auditevents)
│                        HealthController
├── Middleware/          ExceptionMiddleware, CorrelationIdMiddleware, IngestAuthMiddleware
├── Services/            IAuditEventIngestionService, AuditEventIngestionService
│                        IIngestAuthenticator, ServiceTokenAuthenticator, NullIngestAuthenticator
│                        ServiceAuthContext, IngestAuthHeaders
├── Repositories/        IAuditEventRecordRepository, EfAuditEventRecordRepository
│                        IAuditEventRepository (legacy), InMemoryAuditEventRepository
├── Models/              AuditEventRecord (new), AuditEvent (legacy)
│                        Enums: EventCategory, SeverityLevel, ActorType, ScopeType, VisibilityScope
├── DTOs/
│   ├── Ingest/          IngestAuditEventRequest, BatchIngestRequest, BatchIngestResponse, IngestItemResult
│   ├── Query/           AuditEventQueryRequest, AuditEventRecordResponse
│   └── ApiResponse<T>, PagedResult<T>
├── Validators/          IngestAuditEventRequestValidator, BatchIngestRequestValidator
│                        AuditEventScopeDtoValidator, AuditEventActorDtoValidator, AuditEventEntityDtoValidator
├── Utilities/           AuditRecordHasher (SHA-256 + HMAC-SHA256 chain), TraceIdAccessor
├── Data/                AuditEventDbContext, EF Core entity configurations
├── Configuration/       AuditServiceOptions, DatabaseOptions, IntegrityOptions,
│                        IngestAuthOptions, ServiceTokenEntry, QueryAuthOptions,
│                        RetentionOptions, ExportOptions
├── Docs/                architecture_overview.md, canonical-event-contract.md,
│                        integrity-model.md, ingest-auth.md
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```

---

## API Endpoints

### Internal Ingestion (`/internal/audit/*`)

Machine-to-machine endpoints for trusted source systems. Requires authentication in non-dev modes.

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/internal/audit/events` | Ingest a single audit event |
| `POST` | `/internal/audit/events/batch` | Ingest a batch (1–500 events) |

**Auth header:** `x-service-token: <token>` (in ServiceToken mode)
**Optional headers:** `x-source-system`, `x-source-service`

See [Docs/ingest-auth.md](Docs/ingest-auth.md) for the full auth flow and extension guide.

### Legacy API (`/api/auditevents/*`)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/auditevents` | Ingest event (legacy model) |
| `GET` | `/api/auditevents/{id}` | Retrieve by ID |
| `GET` | `/api/auditevents` | Query with filters + pagination |

### Utility

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/health` | Liveness probe |
| `GET` | `/swagger` | API docs (dev / ExposeSwagger=true) |

---

## Ingest Auth Quick Reference

| Mode | Header | Use case |
|------|--------|---------|
| `None` (default) | None required | Local development only. Never use in production. |
| `ServiceToken` | `x-service-token: <token>` | Staging and production — one token per service. |
| `Bearer` (planned) | `Authorization: Bearer <jwt>` | JWT-based identity infrastructure. |

```bash
# Test single ingest (dev mode — no auth)
curl -X POST http://localhost:5007/internal/audit/events \
  -H "Content-Type: application/json" \
  -H "x-source-system: identity-service" \
  -d '{
    "eventType": "user.login.succeeded",
    "eventCategory": "Security",
    "sourceSystem": "identity-service",
    "sourceService": "auth-api",
    "scope": { "scopeType": "Tenant", "tenantId": "tenant-001" },
    "actor": { "type": "User", "id": "user-42", "name": "Alice" },
    "action": "LoginSucceeded",
    "description": "User authenticated successfully.",
    "occurredAtUtc": "2026-03-30T12:00:00Z",
    "severity": "Info",
    "visibility": "Tenant"
  }'

# Test with token (ServiceToken mode)
curl -X POST http://localhost:5007/internal/audit/events \
  -H "Content-Type: application/json" \
  -H "x-service-token: dev-service-token-identity-REPLACE-IN-PROD" \
  -H "x-source-system: identity-service" \
  -d '{ ... }'
```

---

## Configuration Reference

All config is in `appsettings.json` with environment overrides via `appsettings.{Env}.json` or env vars (`__` separator).

### IngestAuth

| Key | Default | Description |
|-----|---------|-------------|
| `Mode` | `"None"` | Auth mode: `None` \| `ServiceToken` \| `Bearer` (planned) |
| `ServiceTokens` | `[]` | Named token registry for ServiceToken mode |
| `RequireSourceSystemHeader` | `false` | Enforce `x-source-system` header presence |
| `AllowedSources` | `[]` | Source allowlist. Empty = allow any. |

**Production token injection:**
```bash
IngestAuth__Mode=ServiceToken
IngestAuth__ServiceTokens__0__Token=$(openssl rand -base64 32)
IngestAuth__ServiceTokens__0__ServiceName=identity-service
IngestAuth__ServiceTokens__0__Enabled=true
```

### Integrity

| Key | Default | Description |
|-----|---------|-------------|
| `Algorithm` | `"HMAC-SHA256"` | `SHA-256` (keyless) or `HMAC-SHA256` (keyed) |
| `HmacKeyBase64` | `""` | 32-byte base64 HMAC key. Required for HMAC-SHA256. |
| `VerifyOnRead` | `false` | Recompute hash on every read |
| `FlagTamperedRecords` | `true` | Include tamper flag in query responses |

```bash
Integrity__Algorithm=HMAC-SHA256
Integrity__HmacKeyBase64=$(openssl rand -base64 32)
```

### Database

| Key | Default | Description |
|-----|---------|-------------|
| `Provider` | `"InMemory"` | `InMemory` or `MySQL` |
| `ConnectionString` | `null` | MySQL connection string |
| `MigrateOnStartup` | `false` | Run EF migrations at startup |
| `VerifyConnectionOnStartup` | `true` | Non-fatal connectivity probe |

---

## Hash Chain Integrity

Every persisted `AuditEventRecord` carries:

- **`Hash`** — canonical hash of this record's fields plus `PreviousHash`.
- **`PreviousHash`** — hash of the preceding record in the same `(TenantId, SourceSystem)` chain.

This forms a singly-linked cryptographic chain: modifying any record invalidates all subsequent hashes.

See [Docs/integrity-model.md](Docs/integrity-model.md) for the complete specification.

---

## Database Migrations

```bash
# Create migration
ConnectionStrings__AuditEventDb="<conn>" \
  dotnet ef migrations add InitialAuditSchema --output-dir Data/Migrations

# Apply
ConnectionStrings__AuditEventDb="<conn>" \
  dotnet ef database update
```

---

## Production Checklist

- [ ] `Database__Provider=MySQL` with connection string
- [ ] `Integrity__HmacKeyBase64` injected via secrets manager (never commit)
- [ ] `IngestAuth__Mode=ServiceToken` with at least one `ServiceTokens` entry
- [ ] Service tokens injected via environment variables (never committed)
- [ ] `QueryAuth__Mode=Bearer` or appropriate auth for query surface
- [ ] `AuditService__AllowedCorsOrigins` set to known origins
- [ ] `Integrity__VerifyOnRead=true` for compliance environments
- [ ] `EnableSensitiveDataLogging=false` and `EnableDetailedErrors=false`
- [ ] `AuditService__ExposeSwagger=false`

---

## Documentation Index

| File | Contents |
|------|---------|
| [Docs/architecture_overview.md](Docs/architecture_overview.md) | System design, service boundaries |
| [Docs/canonical-event-contract.md](Docs/canonical-event-contract.md) | Event schema, field definitions |
| [Docs/integrity-model.md](Docs/integrity-model.md) | Hash chain spec, algorithm reference |
| [Docs/ingest-auth.md](Docs/ingest-auth.md) | Auth flow, headers, modes, extension guide |
