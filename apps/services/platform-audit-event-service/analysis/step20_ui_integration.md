# Step 20 — UI Integration Documentation

**Service**: Platform Audit Event Service  
**Date**: 2026-03-30  
**Status**: Complete  
**Build impact**: Documentation only — no code changes.

---

## Objective

Create a comprehensive, framework-agnostic UI integration guide for developers building audit log interfaces that consume the Platform Audit Event Service query API. Cover all interface tiers, endpoint recommendations, filter guidance, visibility constraints, and view layout patterns.

---

## Files Created

| File | Purpose |
|---|---|
| `Docs/ui-integration.md` | Complete UI integration guide (primary deliverable) |
| `analysis/step20_ui_integration.md` | This report |

---

## Coverage Summary

### Interface Tiers Documented

| Tier | Caller Scope | Primary Endpoint(s) | Key Visibility Level |
|---|---|---|---|
| Platform Admin | `PlatformAdmin` | `GET /audit/events` | Platform + all below |
| Tenant Admin | `TenantAdmin`, `Restricted` | `GET /audit/tenant/{tenantId}` | Tenant + below |
| Organization Admin | `OrganizationAdmin` | `GET /audit/organization/{organizationId}` | Organization + below |
| Individual User | `UserSelf`, `TenantUser` | `GET /audit/user/{userId}` | User only |

Each section covers:
- Which caller scope it applies to
- What the server enforces automatically (tenant lock, org lock, actorId lock)
- Recommended primary endpoint
- Recommended default filter combinations for common views
- Key filter scenario table with parameter mappings
- Recommended table column set

### Endpoints Covered

All 9 query/export endpoints are addressed:

| Endpoint | Where covered |
|---|---|
| `GET /audit/events` | All tiers; primary for Platform Admin |
| `GET /audit/events/{auditId}` | Detail view guidance; deep linking section |
| `GET /audit/entity/{entityType}/{entityId}` | Organization Admin; timeline guidance |
| `GET /audit/actor/{actorId}` | Platform Admin scenarios |
| `GET /audit/user/{userId}` | Individual User section |
| `GET /audit/tenant/{tenantId}` | Tenant Admin section |
| `GET /audit/organization/{organizationId}` | Organization Admin section |
| `POST /audit/exports` | Export section |
| `GET /audit/exports/{exportId}` | Export polling guidance |

### Filter Parameters Documented

All 19 query filter parameters from `AuditEventQueryRequest` are documented in a structured reference table, organized by:
- Scope filters (tenantId, organizationId)
- Classification filters (category, minSeverity, maxSeverity, eventTypes, sourceSystem, sourceService)
- Actor filters (actorId, actorType)
- Entity filters (entityType, entityId)
- Correlation/trace filters (correlationId, sessionId, requestId)
- Time range (from, to)
- Text search (descriptionContains)
- Visibility (maxVisibility, visibility)
- Pagination and sorting (page, pageSize, sortBy, sortDescending)

### Response Shape Documentation

Full annotated response shape for both the paginated list (`AuditEventQueryResponse`) and single record (`AuditEventRecordResponse`). Every field in the record has a "UI usage" note explaining how to present or use it in a view.

Specific notes on:
- `earliestOccurredAtUtc` / `latestOccurredAtUtc` for time-range bar rendering
- `auditId` as the deep-link stable identifier
- `actor.ipAddress` / `actor.userAgent` redaction at lower scopes
- `before` / `after` as raw JSON strings for diff rendering
- `hash` conditionality (only for PlatformAdmin + ExposeIntegrityHash=true)
- `isReplay` badge display
- `correlationId` as a clickable filter trigger

---

## Key Design Decisions

### Server-side vs client-side filtering
The guide emphasizes throughout that **the server enforces all scope constraints automatically** — callers do not need to filter by tenantId, organizationId, or actorId in the client for isolation purposes. These are enforced by the query authorization middleware regardless of what is submitted.

This is called out explicitly at the interface tier level to prevent developers from building redundant (and potentially incorrect) client-side scope logic.

### Visibility table
A single clear table shows which `VisibilityScope` values each interface tier can see. This avoids confusion about why, for example, an `OrganizationAdmin` cannot see security events that were submitted with `Visibility=Tenant` — the server excludes them, and no client-side workaround exists.

### Timeline vs table guidance
Provides concrete decision criteria for when to use each view type:
- Timeline: resource history, user activity feed, correlation trace
- Table: compliance investigation, cross-resource views, platform monitoring

### `earliestOccurredAtUtc` / `latestOccurredAtUtc` usage
These response fields (added for UI convenience) are specifically called out as the mechanism for rendering time-range scrubbers and context labels without a separate metadata query.

### Correlation trace pattern
A concrete pattern for building a correlation trace view: clicking any `correlationId` value navigates to:
```
GET /audit/events?correlationId={id}&sortBy=occurredAtUtc&sortDescending=false
```
With ascending sort so the trace reads in execution order from the triggering event through all downstream services.

### Export UX guidance
Key constraints called out:
- Always require a date range before allowing export submission
- Format selection: CSV for compliance/legal, JSON for integrations, NDJSON for pipelines
- `includeStateSnapshots` guidance per category (off for access/security, on for DataChange)
- `includeHashes` only for PlatformAdmin in integrity verification workflows
- Scope-to-scopeType/scopeId mapping table for each interface tier

### Pagination vs infinite scroll
Concrete guidance:
- Page-based pagination for compliance review tables and dense views
- Load-more for timelines
- Not cursor-based (the API is page/pageSize-based)

---

## Visibility Considerations Summary

These are the facts that UI developers most commonly misunderstand:

1. **A non-`PlatformAdmin` caller can never see another tenant's records**, regardless of `tenantId` query param — the server overrides it.
2. **`OrganizationAdmin` callers cannot see `Tenant`-visibility records** — login/auth events are typically `Tenant` scope, so they are invisible at org level. This is by design.
3. **`Internal`-scope records are never returned** via any query endpoint to any caller.
4. **`actor.ipAddress` and `actor.userAgent` are redacted** for callers below `TenantAdmin`. Do not show these columns in user-facing views where the caller is `TenantUser` or `UserSelf`.
5. **The `hash` field is always null unless `ExposeIntegrityHash=true`** in configuration. Do not render a "Verify integrity" UI element unless this is enabled.

---

## Filter Bar Component Recommendations

A recommended set of filter bar components is documented, including:
- Date range picker → `from` / `to`
- Category dropdown → `category`
- Min severity dropdown → `minSeverity`
- Event type multi-select → `eventTypes`
- Actor user search → `actorId`
- Resource type + ID pair → `entityType` + `entityId`
- Text search with debounce → `descriptionContains` (with performance warning)
- Source system dropdown → `sourceSystem`

---

## Severity and Category Display

Color conventions for severity badges and category badges are documented with specific color suggestions and behavioral notes:
- Apply severity color to badges only, not full row backgrounds, for dense tables
- Use `minSeverity=Warn` as the default filter for operations/security dashboards
- Show category as a removable filter chip when active

---

## Error Handling

All 5 relevant HTTP error codes are documented with recommended UI actions:
- 400 → show validation message near the offending filter
- 401 → redirect to login
- 403 → "You do not have permission" message (do not distinguish 404 from 403 for deep links)
- 404 → "Record not found or not accessible"
- 503 → "Export is not available on this instance"

---

## Impact on Existing Documentation

No changes to existing files. This document is a new addition to `Docs/`.

The guide cross-references:
- `query-authorization-model.md` — for the full scope/role/enforcement reference
- `canonical-event-contract.md` — for all field definitions
- `producer-integration.md` — for upstream context
- `event-forwarding-model.md` — for downstream context
