# Step 1 — Scaffold Report: Platform Audit/Event Service

**Date:** 2026-03-30  
**Phase:** Initial scaffold  
**Status:** Complete

---

## Architecture Summary

The Platform Audit/Event Service is a **standalone, independently deployable** ASP.NET Core 8 Web API. It is:

- **Domain-agnostic** — not tied to any product, tenant model, UI, or identity provider
- **Append-only** — audit records are write-once; no update or delete operations on the repository interface
- **Tamper-evident** — every record carries an HMAC-SHA256 integrity hash over its canonical fields
- **Portable** — single-project (no multi-layer solution), injectable persistence adapter, environment-config-driven

### Layer responsibilities

| Layer          | Responsibility |
|----------------|----------------|
| `Controllers/` | HTTP surface, input validation delegation, response envelope wrapping |
| `Services/`    | Ingestion orchestration, mapping, logging |
| `Repositories/`| Persistence contract (`IAuditEventRepository`) + InMemory adapter |
| `Models/`      | Immutable `AuditEvent` domain record + category/severity/outcome constants |
| `DTOs/`        | `IngestAuditEventRequest`, `AuditEventResponse`, `ApiResponse<T>`, `PagedResult<T>` |
| `Validators/`  | FluentValidation rules for ingestion payload |
| `Middleware/`  | `ExceptionMiddleware` (centralized error handling) + `CorrelationIdMiddleware` |
| `Utilities/`   | `IntegrityHasher` (HMAC-SHA256), `AuditEventMapper`, `TraceIdAccessor` |
| `Data/`        | `AuditEventDbContext` (EF Core, InMemory placeholder, ready for durable migration) |
| `Configuration/` | `AuditServiceOptions` (strongly-typed settings, bound from appsettings) |
| `Jobs/`        | `RetentionPolicyJob` placeholder |
| `Docs/`        | Architecture documentation |
| `Examples/`    | Sample ingestion payloads |

---

## Files Created

### Project root
- `PlatformAuditEventService.csproj`
- `Program.cs`
- `appsettings.json`
- `appsettings.Development.json`
- `README.md`

### Controllers/
- `HealthController.cs` — `GET /HealthCheck`
- `AuditEventsController.cs` — `POST /api/auditevents`, `GET /api/auditevents/{id}`, `GET /api/auditevents`

### Services/
- `IAuditEventService.cs`
- `AuditEventService.cs`

### Repositories/
- `IAuditEventRepository.cs`
- `InMemoryAuditEventRepository.cs`

### Models/
- `AuditEvent.cs` — canonical immutable audit event record
- `EventCategory.cs` — well-known category constants
- `EventSeverity.cs` — severity constants + `IsValid()`
- `EventOutcome.cs` — outcome constants + `IsValid()`

### DTOs/
- `IngestAuditEventRequest.cs`
- `AuditEventResponse.cs`
- `ApiResponse.cs` — standardized `ApiResponse<T>` envelope
- `AuditEventQueryRequest.cs` — query filter + pagination parameters
- `PagedResult.cs` — paginated result wrapper

### Validators/
- `IngestAuditEventRequestValidator.cs` — FluentValidation rules for all ingest fields

### Middleware/
- `ExceptionMiddleware.cs` — centralized exception handler → structured JSON error
- `CorrelationIdMiddleware.cs` — reads/writes `X-Correlation-ID` header

### Utilities/
- `IntegrityHasher.cs` — HMAC-SHA256 compute + constant-time verify
- `AuditEventMapper.cs` — `IngestAuditEventRequest` → `AuditEvent` → `AuditEventResponse`
- `TraceIdAccessor.cs` — current Activity/trace ID accessor

### Data/
- `AuditEventDbContext.cs` — EF Core context with full entity config + indexes

### Configuration/
- `AuditServiceOptions.cs` — `IntegrityHmacKeyBase64`, `PersistenceProvider`, `MaxPageSize`

### Jobs/
- `RetentionPolicyJob.cs` — placeholder with implementation guidance

### Docs/
- `architecture_overview.md` — design principles, layer diagram, extension points

### Examples/
- `ingest_request_minimal.json`
- `ingest_request_full.json`
- `ingest_request_security_failure.json`

### analysis/
- `step1_scaffold.md` — this file

---

## NuGet Packages Added

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.AspNetCore.OpenApi` | 8.0.0 | Minimal API OpenAPI support |
| `Swashbuckle.AspNetCore` | 6.5.0 | Swagger UI + OpenAPI generation |
| `FluentValidation.AspNetCore` | 11.3.0 | Model validation framework |
| `Serilog.AspNetCore` | 8.0.1 | Structured logging host integration |
| `Serilog.Sinks.Console` | 5.0.1 | Console log sink |
| `Serilog.Enrichers.Environment` | 2.3.0 | Machine name enrichment |
| `Serilog.Enrichers.Thread` | 3.1.0 | Thread ID enrichment |
| `Microsoft.EntityFrameworkCore` | 8.0.0 | ORM framework |
| `Microsoft.EntityFrameworkCore.InMemory` | 8.0.0 | Dev/test in-memory provider |

---

## Run Instructions

```bash
cd apps/services/platform-audit-event-service

# Restore dependencies
dotnet restore

# Development run (default port 5007)
dotnet run

# With explicit URLs
ASPNETCORE_URLS=http://0.0.0.0:5007 dotnet run

# Production build
dotnet publish -c Release -o ./publish
```

**Endpoints available after start:**
- Swagger UI: `http://localhost:5007/swagger`
- Health check: `http://localhost:5007/HealthCheck`
- Ingest event: `POST http://localhost:5007/api/auditevents`
- Query events: `GET http://localhost:5007/api/auditevents?category=security&page=1&pageSize=20`

---

## Next Steps (Step 2+)

1. **Durable persistence** — implement `IAuditEventRepository` against PostgreSQL or SQL Server; run EF Core migration from `AuditEventDbContext`
2. **Authentication** — JWT bearer middleware; role-based access on read endpoints
3. **Batch ingestion** — `POST /api/auditevents/batch` endpoint
4. **Retention job** — flesh out `RetentionPolicyJob` with configurable windows
5. **Streaming output** — publish new events to Redis Stream or message bus for downstream consumers
6. **OpenTelemetry** — add distributed tracing export
7. **Gateway routing** — register port 5007 route in the YARP gateway (`apps/services/gateway`)
