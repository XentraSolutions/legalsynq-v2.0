# Audit Service

Tamper-evident, append-only audit event log with query, export, integrity verification, and retention management.

**Port:** 5007

## Responsibilities

- Ingest audit events from all platform services (via `LegalSynq.AuditClient`)
- SHA-256 / HMAC-SHA256 hash chain integrity per event
- Event query with filtering (tenant, actor, entity, event type, time range)
- Integrity checkpoint management and verification
- CSV/JSON export with field-level control
- Retention and archival policy evaluation
- Anomaly detection and alerting on suspicious patterns
- Correlation engine for linking related events
- Event forwarding abstraction (disabled by default)

## Layer Structure

```
Controllers/     AuditEventIngestController, AuditEventQueryController,
                 AuditExportController, HealthController
Middleware/      ExceptionMiddleware, CorrelationIdMiddleware, IngestAuthMiddleware
Services/        AuditEventIngestionService, AuditEventQueryService, AuditExportService
Repositories/    EfAuditEventRecordRepository
Models/          AuditEventRecord, Enums (EventCategory, SeverityLevel, ActorType, ...)
Data/            AuditEventDbContext, EF migrations
Configuration/   AuditServiceOptions, IngestAuthOptions, IntegrityOptions, RetentionOptions
Docs/            architecture_overview.md, canonical-event-contract.md,
                 integrity-model.md, ingest-auth.md
```

## Key Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/internal/audit/events` | Service token | Ingest single event |
| `POST` | `/internal/audit/events/batch` | Service token | Ingest batch (1–500) |
| `GET` | `/api/auditevents` | Bearer | Query events |
| `GET` | `/api/auditevents/{id}` | Bearer | Event detail |
| `POST` | `/api/audit/export` | Bearer | Export events |
| `GET` | `/health` | Anonymous | Liveness probe |

## Client Integration

All services that publish audit events use `shared/audit-client`:

```csharp
services.AddAuditEventClient(configuration);
// Inject IAuditEventClient → await client.IngestAsync(request)
```

Config keys: `AuditClient:BaseUrl`, `AuditClient:ServiceToken`, `AuditClient:SourceSystem`.

## Ingest Auth Modes

| Mode | Header | Use |
|---|---|---|
| `None` (default) | None | Local development only |
| `ServiceToken` | `x-service-token: <token>` | Staging and production |

## Hash Chain

Each `AuditEventRecord` stores:
- `Hash` — SHA-256 or HMAC-SHA256 of this record's fields + `PreviousHash`
- `PreviousHash` — hash of the preceding record in the same `(TenantId, SourceSystem)` chain

Modifying any record invalidates all subsequent hashes. See `Docs/integrity-model.md`.

## Database

`AuditEventDb` (MySQL). Records are append-only — never modified after ingestion.

## Retention

Retention policy evaluation engine is wired but `JobEnabled=false` and `DryRun=true` by default. Three tiers: Hot (≤365 days), Warm, Cold. Per-category and per-tenant overrides supported. `DefaultRetentionDays=0` = indefinite.

## Production Checklist

- `Database__Provider=MySQL` with connection string
- `Integrity__Algorithm=HMAC-SHA256` + `HmacKeyBase64` from secrets manager
- `IngestAuth__Mode=ServiceToken` with at least one token entry
- `AuditService__ExposeSwagger=false`
- `Integrity__VerifyOnRead=true` for compliance environments
