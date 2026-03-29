# Phase 6 — Observability & Compliance Logging

## Structured Logging (Pino)

All log entries are JSON-structured with consistent fields:

```json
{
  "level":         "info",
  "time":          1711900000000,
  "name":          "docs-service",
  "correlationId": "uuid",
  "method":        "POST",
  "path":          "/documents",
  "msg":           "Incoming request"
}
```

### HIPAA-Safe Redaction

Pino `redact` config strips:
- `req.headers.authorization` → `[REDACTED]`
- `req.headers.cookie` → `[REDACTED]`
- `password`, `token`, `secret`, `accessKeyId`, `secretAccessKey`

No file contents, no PHI, no PII in log lines.

### Log Levels

| Level | Usage |
|---|---|
| `trace` | Dev only — query params |
| `debug` | File uploads, storage ops |
| `info`  | All inbound requests, provider init, startup |
| `warn`  | Auth failures, validation issues |
| `error` | DB errors, storage errors, unhandled exceptions |
| `fatal` | Startup failures |

### Dev Mode

`NODE_ENV=development` → `pino-pretty` with colorized output. Production → raw JSON for log aggregator (Datadog, CloudWatch, GCP Logging, Splunk).

## Request Correlation IDs

- `X-Correlation-Id` header accepted from upstream gateway
- New UUID generated if absent
- Present in every log line, every audit entry, every error response
- Enables full request trace across services

## Audit Log

`document_audits` table captures all critical events:

| Event | Trigger |
|---|---|
| `DOCUMENT_CREATED` | POST /documents |
| `DOCUMENT_UPDATED` | PATCH /documents/:id |
| `DOCUMENT_STATUS_CHANGED` | PATCH with `status` field |
| `DOCUMENT_DELETED` | DELETE /documents/:id |
| `VERSION_UPLOADED` | POST /documents/:id/versions |
| `VIEW_URL_GENERATED` | POST /documents/:id/view-url |
| `DOWNLOAD_URL_GENERATED` | POST /documents/:id/download-url |
| `ACCESS_DENIED` | Auth middleware rejection |

Audit entries are INSERT-only. A PostgreSQL trigger (`trg_audit_immutable`) raises an exception on any UPDATE or DELETE — making the audit trail tamper-resistant at the database level.

## Health Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /health` | Liveness — always returns 200 if process is alive |
| `GET /health/ready` | Readiness — checks DB connectivity + reports storage provider |

## Metrics Hooks (Future)

- Instrument `DocumentRepository` methods with counters (uploads, deletes, version count)
- Expose `/metrics` endpoint for Prometheus scraping
- Alert on: auth failure rate > threshold, storage error rate, DB pool exhaustion
