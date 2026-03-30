# Platform Audit/Event Service

Platform Audit/Event Service is a standalone, independently deployable, and portable platform service that ingests business, security, access, administrative, and system activity from distributed systems, normalizes it into a canonical event model, and persists immutable, tamper-evident audit records. It exposes secure, role-aware retrieval interfaces for administrative systems, tenant applications, individual users, reporting tools, compliance workflows, and future downstream consumers.

---

## Purpose

- Receive activity feeds from distributed microservices
- Normalize them into a canonical audit/event model
- Persist immutable, tamper-evident audit records with HMAC-SHA256 integrity hashing
- Support secure retrieval for platform admin, tenant, user, reporting, and compliance interfaces
- Provide an event-ready foundation for future integrations and downstream consumers

---

## Architecture

```
platform-audit-event-service/
├── Controllers/           API surface — HealthController, AuditEventsController
├── Services/              Business logic — AuditEventService, IAuditEventService
├── Repositories/          Persistence contracts + InMemory adapter
├── Models/                Domain types — AuditEvent, EventCategory, EventSeverity, EventOutcome
├── DTOs/                  Request/response shapes — IngestAuditEventRequest, ApiResponse<T>, PagedResult<T>
├── Validators/            FluentValidation — IngestAuditEventRequestValidator
├── Middleware/            ExceptionMiddleware, CorrelationIdMiddleware
├── Jobs/                  RetentionPolicyJob (placeholder — see below)
├── Utilities/             IntegrityHasher, AuditEventMapper, TraceIdAccessor
├── Data/                  AuditEventDbContext (EF Core, InMemory placeholder)
├── Configuration/         AuditServiceOptions (strongly-typed settings)
├── Docs/                  Extended documentation (reserved)
├── Examples/              Sample payloads for ingestion
├── analysis/              Architecture and analysis reports
├── Program.cs             Application bootstrap and DI wiring
├── appsettings.json
└── appsettings.Development.json
```

---

## API Endpoints

| Method | Path                      | Description                                   |
|--------|---------------------------|-----------------------------------------------|
| GET    | `/HealthCheck`            | Service health, version, and event count      |
| GET    | `/health`                 | ASP.NET Core built-in health endpoint         |
| POST   | `/api/auditevents`        | Ingest a single audit event                   |
| GET    | `/api/auditevents/{id}`   | Retrieve one event by ID                      |
| GET    | `/api/auditevents`        | Query events with filters + pagination        |
| GET    | `/swagger`                | Swagger UI (Development only)                 |

---

## Canonical Event Model — Key Fields

| Field           | Description                                                         |
|-----------------|---------------------------------------------------------------------|
| `id`            | Unique event identifier (GUID)                                      |
| `source`        | Originating service (e.g. `identity-service`)                       |
| `eventType`     | Dot-notation event code (e.g. `user.login`, `document.uploaded`)    |
| `category`      | `security` \| `access` \| `business` \| `admin` \| `system`        |
| `severity`      | `DEBUG` \| `INFO` \| `WARN` \| `ERROR` \| `CRITICAL`               |
| `tenantId`      | Scoping tenant (null for platform-level events)                     |
| `actorId`       | Acting user or service principal                                    |
| `targetType`    | Resource type (e.g. `User`, `Document`, `Application`)             |
| `targetId`      | Resource identifier                                                 |
| `outcome`       | `SUCCESS` \| `FAILURE` \| `PARTIAL` \| `UNKNOWN`                   |
| `correlationId` | Distributed trace / request correlation identifier                  |
| `metadata`      | Arbitrary JSON payload for extended context                         |
| `integrityHash` | HMAC-SHA256 over canonical fields — detects post-write tampering    |
| `occurredAtUtc` | When the event happened in the source system                        |
| `ingestedAtUtc` | When this record was received and persisted                         |

---

## Tamper-Evidence

Each persisted record carries an `integrityHash` — an HMAC-SHA256 digest computed over a canonical pipe-delimited string of immutable fields using the configured `IntegrityHmacKeyBase64` secret. Any post-write modification to the record's fields will produce a hash mismatch, detectable by `IntegrityHasher.Verify()`.

---

## Configuration

```json
{
  "AuditService": {
    "PersistenceProvider": "InMemory",
    "IntegrityHmacKeyBase64": "<base64-encoded 32-byte secret>",
    "MaxPageSize": 500
  }
}
```

Set `IntegrityHmacKeyBase64` via environment variable in production:
```
AuditService__IntegrityHmacKeyBase64=<your-base64-secret>
```

---

## Run Instructions

```bash
cd apps/services/platform-audit-event-service

# Restore packages
dotnet restore

# Run (defaults to port 5007)
dotnet run

# Or with explicit port
ASPNETCORE_URLS=http://0.0.0.0:5007 dotnet run
```

Swagger UI available at: `http://localhost:5007/swagger`
Health check: `http://localhost:5007/HealthCheck`

---

## Production Readiness Notes

1. **Replace InMemory repository** — implement `IAuditEventRepository` against PostgreSQL, SQL Server, or Cosmos DB. `AuditEventDbContext` contains EF Core entity configuration ready for migration.
2. **Set HMAC secret** — generate a cryptographically random 32-byte key and inject via secrets manager / environment variable. Never commit to source control.
3. **Lock down CORS** — replace `AllowAnyOrigin()` with explicit allowed origins.
4. **Add authentication** — protect write endpoints with JWT bearer / API key middleware. Read endpoints should be role-scoped.
5. **Implement RetentionPolicyJob** — wire to a scheduler (Quartz.NET or Hangfire) with configurable per-tenant retention windows.
6. **Add distributed tracing** — integrate OpenTelemetry for end-to-end request tracing across services.
7. **Disable Swagger in production** — already gated on `IsDevelopment()`.
