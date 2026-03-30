# Step 13 — Query Services and Retrieval APIs: Report

Platform Audit/Event Service  
Date: 2026-03-30

---

## Overview

This step adds a canonical, domain-neutral query surface for persisted `AuditEventRecord`
entities. The implementation covers a service layer, repository extensions, an entity-to-DTO
mapper, and a controller with 7 endpoints covering all scoped access patterns.

---

## Endpoint Summary

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/audit/events` | Full filtered, paginated query |
| `GET` | `/audit/events/{auditId}` | Single record by stable public identifier |
| `GET` | `/audit/entity/{entityType}/{entityId}` | Events targeting a specific resource |
| `GET` | `/audit/actor/{actorId}` | Events performed by a specific actor |
| `GET` | `/audit/user/{userId}` | Events for a user (actorType=User enforced) |
| `GET` | `/audit/tenant/{tenantId}` | Events scoped to a tenant |
| `GET` | `/audit/organization/{organizationId}` | Events scoped to an organization |

### Scoped endpoint pattern

All scoped endpoints accept additional query-string parameters. The path segment always
takes precedence over the corresponding query-string value:

```
GET /audit/tenant/tenant-001?actorId=user-42&from=2026-01-01
```
→ Applies `TenantId = "tenant-001"` AND `ActorId = "user-42"` AND `From = 2026-01-01`.

---

## Filters

All 7 endpoints (except the single-record `GET /audit/events/{auditId}`) accept the
following query parameters. All are optional and AND-ed together.

### Scope

| Parameter | Type | Description |
|-----------|------|-------------|
| `tenantId` | string | Restrict to a specific tenant |
| `organizationId` | string | Restrict to a specific organization within a tenant |

### Classification

| Parameter | Type | Description |
|-----------|------|-------------|
| `eventTypes` | string[] | One or more dot-notation event type codes (OR-ed) |
| `category` | EventCategory | Broad category (Security, DataAccess, Administrative, etc.) |
| `sourceSystem` | string | Source system name (exact match) |
| `sourceService` | string | Source service name (exact match) |
| `sourceEnvironment` | string | Deployment environment tag (exact match) — **new in Step 13** |

### Actor / Identity

| Parameter | Type | Description |
|-----------|------|-------------|
| `actorId` | string | Actor's stable identifier (exact match) |
| `actorType` | ActorType | User, Service, System, Unknown |

### Entity / Resource

| Parameter | Type | Description |
|-----------|------|-------------|
| `entityType` | string | Resource type (exact match) |
| `entityId` | string | Resource identifier (exact match) |

### Correlation / Tracing

| Parameter | Type | Description |
|-----------|------|-------------|
| `correlationId` | string | Distributed trace correlation ID (exact match) |
| `requestId` | string | HTTP request identifier (exact match) — **new in Step 13** |
| `sessionId` | string | User session identifier (exact match) |

### Time Range

| Parameter | Type | Description |
|-----------|------|-------------|
| `from` | DateTimeOffset | OccurredAtUtc ≥ this value (inclusive) |
| `to` | DateTimeOffset | OccurredAtUtc < this value (exclusive) |

### Visibility

| Parameter | Type | Description |
|-----------|------|-------------|
| `visibility` | VisibilityScope | Exact visibility scope match — **new in Step 13** |
| `maxVisibility` | VisibilityScope | Return records at least as permissive as this level |

`Internal` records are always excluded regardless of visibility parameters.

### Severity Range

| Parameter | Type | Description |
|-----------|------|-------------|
| `minSeverity` | SeverityLevel | Minimum severity (inclusive) |
| `maxSeverity` | SeverityLevel | Maximum severity (inclusive) |

### Text Search

| Parameter | Type | Description |
|-----------|------|-------------|
| `descriptionContains` | string | Substring search in Description (case-insensitive) |

---

## Pagination

| Parameter | Default | Notes |
|-----------|---------|-------|
| `page` | `1` | 1-based page number |
| `pageSize` | `50` | Capped server-side at `QueryAuth:MaxPageSize` (default 500) |
| `sortBy` | `"occurredAtUtc"` | Accepted: `occurredAtUtc`, `recordedAtUtc`, `severity`, `sourceSystem` |
| `sortDescending` | `true` | `true` = newest first |

### Pagination metadata in response

Every list response includes:

```json
{
  "totalCount": 1234,
  "page": 1,
  "pageSize": 50,
  "totalPages": 25,
  "hasNext": true,
  "hasPrev": false,
  "earliestOccurredAtUtc": "2026-01-01T00:00:00Z",
  "latestOccurredAtUtc":   "2026-03-30T12:00:00Z"
}
```

`totalCount` counts the full filtered result set (all pages).
`earliestOccurredAtUtc` / `latestOccurredAtUtc` cover the full filtered set (not just the
current page). They are computed via a single aggregate DB query (`GROUP BY 1`) issued in
parallel with the paginated query.

---

## Response Model

### `ApiResponse<T>` envelope (all endpoints)

```json
{
  "success": true,
  "message": null,
  "traceId": "00-abc...-01",
  "data": { ... },
  "errors": []
}
```

### Single record: `AuditEventRecordResponse`

```json
{
  "auditId":       "01959f3a-...",
  "eventId":       "f47ac10b-...",
  "eventType":     "user.login.succeeded",
  "eventCategory": "Security",
  "sourceSystem":  "identity-service",
  "sourceService": "auth-api",
  "sourceEnvironment": "production",
  "scope": {
    "scopeType":      "Tenant",
    "platformId":     null,
    "tenantId":       "tenant-001",
    "organizationId": null,
    "userScopeId":    null
  },
  "actor": {
    "id":        "user-42",
    "type":      "User",
    "name":      "Alice",
    "ipAddress": "10.0.0.1",
    "userAgent": "Mozilla/5.0 ..."
  },
  "entity": {
    "type": "User",
    "id":   "user-42"
  },
  "action":      "LoginSucceeded",
  "description": "User authenticated successfully.",
  "before":      null,
  "after":       null,
  "metadata":    null,
  "correlationId": "abc-123",
  "requestId":     "req-456",
  "sessionId":     "sess-789",
  "visibility":  "Tenant",
  "severity":    "Info",
  "occurredAtUtc": "2026-03-30T12:00:00+00:00",
  "recordedAtUtc": "2026-03-30T12:00:01+00:00",
  "hash":    null,
  "isReplay": false,
  "tags":    ["pii"]
}
```

### List result: `ApiResponse<AuditEventQueryResponse>`

Wraps the items array and pagination envelope:

```json
{
  "success": true,
  "data": {
    "items":      [ { ... }, { ... } ],
    "totalCount": 1234,
    "page":       1,
    "pageSize":   50,
    "totalPages": 25,
    "hasNext":    true,
    "hasPrev":    false,
    "earliestOccurredAtUtc": "2026-01-01T00:00:00+00:00",
    "latestOccurredAtUtc":   "2026-03-30T12:00:00+00:00"
  }
}
```

### Field notes

| Field | Notes |
|-------|-------|
| `hash` | Only populated when `QueryAuth:ExposeIntegrityHash = true` |
| `actor.ipAddress` | Redaction supported via `redactNetworkIdentifiers` in mapper (future: role-based) |
| `actor.userAgent` | Same redaction support |
| `before` / `after` / `metadata` | Raw JSON strings; callers parse as needed |
| `tags` | Deserialized from `TagsJson`; empty list when none |

---

## Architecture

```
AuditEventQueryController
        │
        │  [FromQuery] AuditEventQueryRequest
        ▼
IAuditEventQueryService
        │
        ├── IAuditEventRecordRepository.QueryAsync()            → PagedResult<AuditEventRecord>
        └── IAuditEventRecordRepository.GetOccurredAtRangeAsync() → (DateTimeOffset?, DateTimeOffset?)
                │                          (both queries issued in parallel via Task tuples)
                ▼
        AuditEventRecordMapper.ToResponseList()
                │
                ▼
        AuditEventQueryResponse
```

### Query parallelism

`AuditEventQueryService.QueryAsync` issues two DB queries in parallel:

```csharp
var (pagedTask, rangeTask) = (
    _repository.QueryAsync(request, ct),
    _repository.GetOccurredAtRangeAsync(request, ct)
);
var paged              = await pagedTask;
var (earliest, latest) = await rangeTask;
```

Both use the same `ApplyFilters` pipeline — behaviour is guaranteed consistent.

---

## Repository Changes

### `EfAuditEventRecordRepository` additions

| Change | Details |
|--------|---------|
| `ApplyFilters` — `SourceEnvironment` | `WHERE source_environment = ?` |
| `ApplyFilters` — `RequestId` | `WHERE request_id = ?` |
| `ApplyFilters` — `Visibility` (exact) | `WHERE visibility_scope = ?` (Internal always excluded) |
| `ApplyFilters` — `Visibility` vs `MaxVisibility` precedence | Exact match takes precedence when both are set |
| `GetOccurredAtRangeAsync` | Single `GROUP BY 1` aggregate: `MIN(occurred_at_utc)`, `MAX(occurred_at_utc)` |

### `IAuditEventRecordRepository` additions

- `GetOccurredAtRangeAsync(filter, ct)` → `(DateTimeOffset? Earliest, DateTimeOffset? Latest)`

---

## `AuditEventQueryRequest` additions (Step 13)

| Property | Type | Filter |
|----------|------|--------|
| `SourceEnvironment` | `string?` | `WHERE source_environment = ?` |
| `RequestId` | `string?` | `WHERE request_id = ?` |
| `Visibility` | `VisibilityScope?` | `WHERE visibility_scope = ?` (exact match, takes precedence over MaxVisibility) |

---

## Files Created

| File | Description |
|------|-------------|
| `Mapping/AuditEventRecordMapper.cs` | Static mapper: `AuditEventRecord` → `AuditEventRecordResponse` |
| `Services/IAuditEventQueryService.cs` | Query service interface |
| `Services/AuditEventQueryService.cs` | Query service implementation |
| `Controllers/AuditEventQueryController.cs` | 7 retrieval endpoints |
| `analysis/step13_query_api.md` | This report |

## Files Modified

| File | Change |
|------|--------|
| `DTOs/Query/AuditEventQueryRequest.cs` | Added `SourceEnvironment`, `RequestId`, `Visibility` |
| `Repositories/IAuditEventRecordRepository.cs` | Added `GetOccurredAtRangeAsync` |
| `Repositories/EfAuditEventRecordRepository.cs` | New filter predicates + `GetOccurredAtRangeAsync` implementation |
| `Program.cs` | Registered `IAuditEventQueryService`; updated Swagger description |

---

## Example Requests

```bash
# List all events for a tenant, newest first
GET /audit/events?tenantId=tenant-001&sortDescending=true&pageSize=25

# List high-severity security events in the last 7 days
GET /audit/events?category=Security&minSeverity=Warning&from=2026-03-23T00:00:00Z

# Entity history: all events for a specific document
GET /audit/entity/Document/doc-abc-123

# Actor history: all events by a specific service
GET /audit/actor/fund-service?category=DataAccess

# User history: all login events for a user
GET /audit/user/user-42?eventTypes=user.login.succeeded&eventTypes=user.login.failed

# Tenant scoped with date range
GET /audit/tenant/tenant-001?from=2026-01-01T00:00:00Z&to=2026-04-01T00:00:00Z

# Single record by AuditId
GET /audit/events/01959f3a-0000-7000-9000-abcdef012345

# Correlation trace — find all events from a distributed request
GET /audit/events?correlationId=trace-abc-123
```

---

## Build Status

- PlatformAuditEventService: ✅ 0 errors, 0 warnings (verified post-Step 13)
