# Step 7 — Validation Layer Analysis

**Service**: Platform Audit/Event Service  
**Date**: 2026-03-30  
**Status**: COMPLETE

---

## Overview

This step implements the FluentValidation validation layer for the Platform Audit/Event Service. Seven validators were created covering the full ingest, batch, query, and export surface area. The old flat-DTO validator was replaced with a correct implementation targeting the canonical ingest DTO. Validator registration was switched from manual `AddScoped` calls to assembly-level auto-discovery.

---

## Validators Created

| File | Class | Validates |
|------|-------|-----------|
| `Validators/IngestAuditEventRequestValidator.cs` | `IngestAuditEventRequestValidator` | `DTOs.Ingest.IngestAuditEventRequest` — **replaces** the old flat-DTO validator |
| `Validators/BatchIngestRequestValidator.cs` | `BatchIngestRequestValidator` | `DTOs.Ingest.BatchIngestRequest` |
| `Validators/AuditEventQueryRequestValidator.cs` | `AuditEventQueryRequestValidator` | `DTOs.Query.AuditEventQueryRequest` |
| `Validators/ExportRequestValidator.cs` | `ExportRequestValidator` | `DTOs.Export.ExportRequest` |
| `Validators/AuditEventScopeDtoValidator.cs` | `AuditEventScopeDtoValidator` | `DTOs.Ingest.AuditEventScopeDto` |
| `Validators/AuditEventActorDtoValidator.cs` | `AuditEventActorDtoValidator` | `DTOs.Ingest.AuditEventActorDto` |
| `Validators/AuditEventEntityDtoValidator.cs` | `AuditEventEntityDtoValidator` | `DTOs.Ingest.AuditEventEntityDto` |

---

## Replaced Validator

The pre-existing `IngestAuditEventRequestValidator` targeted the **old** flat DTO (`PlatformAuditEventService.DTOs.IngestAuditEventRequest`) with fields: `Source`, `Category`, `Severity` (string), `Outcome`, `TenantId`, `ActorId`, `TargetId`, `IpAddress`.

The replacement targets `PlatformAuditEventService.DTOs.Ingest.IngestAuditEventRequest` (the canonical nested DTO introduced in Step 4), which uses:
- Typed enums for `EventCategory`, `Visibility`, `Severity`
- Nested `Scope`, `Actor`, `Entity` objects with child validators
- Explicit required fields per user specification

---

## Validation Rules

### IngestAuditEventRequestValidator

**Required fields** (produce validation errors when absent/empty):

| Field | Rule |
|-------|------|
| `EventType` | `NotEmpty`, max 200 chars |
| `EventCategory` | `IsInEnum` (Security through Performance) |
| `SourceSystem` | `NotEmpty`, max 200 chars |
| `SourceService` | `NotEmpty`, max 200 chars |
| `Visibility` | `IsInEnum` (Platform through Internal) |
| `Severity` | `IsInEnum` (Debug through Alert) |
| `OccurredAtUtc` | `NotNull`; not more than +5 min in future; not older than 7 years |

**Optional fields** (validated only when present):

| Field | Rule |
|-------|------|
| `SourceEnvironment` | Max 100 chars |
| `Action` | Max 200 chars |
| `Description` | Max 2000 chars |
| `Before` / `After` / `Metadata` | Max 1,048,576 chars (~1 MB) each |
| `CorrelationId` / `RequestId` / `SessionId` | Max 200 chars each |
| `IdempotencyKey` | Max 300 chars |
| `Tags` | Max 20 items; each item max 100 chars, non-empty |
| `Entity` | Applies `AuditEventEntityDtoValidator` when object is present |
| `Scope` | Applies `AuditEventScopeDtoValidator` (always — object is required) |
| `Actor` | Applies `AuditEventActorDtoValidator` (always — object is required) |

### AuditEventScopeDtoValidator

| Field | Rule |
|-------|------|
| `ScopeType` | `IsInEnum` |
| `PlatformId` | Must be valid GUID format if provided |
| `TenantId` | `NotEmpty` when ScopeType ∈ {Tenant, Organization, User}; max 100 chars |
| `OrganizationId` | `NotEmpty` when ScopeType = Organization; max 100 chars |
| `UserId` | `NotEmpty` when ScopeType = User; max 200 chars |

### AuditEventActorDtoValidator

| Field | Rule |
|-------|------|
| `Type` | `IsInEnum` (User through Support) |
| `Id` | Max 200 chars if provided |
| `Name` | Max 300 chars if provided |
| `IpAddress` | Max 45 chars if provided (IPv6 max) |
| `UserAgent` | Max 500 chars if provided |

### AuditEventEntityDtoValidator

| Field | Rule |
|-------|------|
| `Type` | Max 200 chars if provided |
| `Id` | Max 200 chars if provided |

### BatchIngestRequestValidator

| Rule | Detail |
|------|--------|
| `Events` | `NotNull`, `NotEmpty` |
| Batch size | Hard max: **500 events per batch** |
| Per-item | Each item validated with `IngestAuditEventRequestValidator` via `RuleForEach` |
| `BatchCorrelationId` | Max 200 chars if provided |

**Error format**: Per-item errors are prefixed with the zero-based index (`Events[2].EventType: EventType is required.`), enabling callers to pinpoint failing items without re-submitting the full batch.

### AuditEventQueryRequestValidator

| Field | Rule |
|-------|------|
| `TenantId` / `OrganizationId` | Max 100 chars |
| `ActorId` / `EntityType` / `EntityId` / `SourceSystem` / `SourceService` / `CorrelationId` / `SessionId` | Max 200 chars each |
| `Category` / `MinSeverity` / `MaxSeverity` / `ActorType` / `MaxVisibility` | `IsInEnum` when provided |
| `MinSeverity` ≤ `MaxSeverity` | Cross-field check when both provided |
| `EventTypes` | Max 20 items; each max 200 chars, non-empty |
| `From` < `To` | Cross-field check when both provided |
| `DescriptionContains` | Max 500 chars |
| `Page` | ≥ 1 |
| `PageSize` | Between 1 and 500 |
| `SortBy` | Must be one of: `occurredatutc`, `recordedatutc`, `severity`, `sourcesystem` (case-insensitive) |

### ExportRequestValidator

| Field | Rule |
|-------|------|
| `ScopeType` | `IsInEnum` |
| `ScopeId` | `NotEmpty` when ScopeType ∈ {Tenant, Organization, User, Service}; max 200 chars |
| `Format` | `NotEmpty`; max 20 chars; must be one of `Json`, `Csv`, `Ndjson` (case-sensitive) |
| `Category` / `MinSeverity` | `IsInEnum` when provided |
| `EventTypes` | Max 20 items; each max 200 chars |
| `ActorId` / `EntityType` / `EntityId` / `CorrelationId` | Max 200 chars each |
| `From` < `To` | Cross-field check when both provided |
| Export span | Max 366 days (~1 year) when both `From` and `To` are provided |

---

## Batch Limits

| Limit | Value | Location |
|-------|-------|----------|
| Hard max events per batch | **500** | `BatchIngestRequestValidator.MaxBatchSize` |
| Recommended events per batch | ≤ 100 | Documentation / comment |
| Max `BatchCorrelationId` length | 200 chars | Validator |
| Max export time span | 366 days | `ExportRequestValidator` |

---

## Registration Change

**Before** (Step 7 scaffold):
```csharp
builder.Services.AddScoped<IValidator<IngestAuditEventRequest>, IngestAuditEventRequestValidator>();
```

**After** (this step):
```csharp
builder.Services.AddValidatorsFromAssemblyContaining<IngestAuditEventRequestValidator>();
```

`AddValidatorsFromAssemblyContaining<T>` (FluentValidation 11) auto-discovers all `AbstractValidator<T>` implementations in the assembly and registers them as `Scoped`. This covers all 7 validators, including the 3 nested child validators (Scope, Actor, Entity) required by `IngestAuditEventRequestValidator` via DI constructor injection.

---

## Design Decisions

1. **Domain-neutral validation**: No business rules enforced (e.g. "Security events must have ActorId"). The validator only checks structure, types, and length — domain rules belong in the service layer.

2. **Typed enums over string matching**: All enum fields use FluentValidation's `IsInEnum()` rather than string allow-lists. This prevents silent breakage when enum values are added.

3. **No dot-notation regex on EventType**: Per user specification, `EventType` accepts any non-empty string up to 200 chars. Naming conventions are documented but not enforced in the validator.

4. **Child validators via DI**: Nested object validators (`AuditEventScopeDtoValidator` etc.) are injected into `IngestAuditEventRequestValidator` through the constructor, enabling them to be independently registered and tested.

5. **Clock skew tolerance**: `OccurredAtUtc` allows up to +5 minutes in the future to accommodate minor drift between source systems and the audit service.

6. **Maximum event age**: `OccurredAtUtc` rejects timestamps older than 7 years. This prevents accidental submission of epoch-zero timestamps or obviously corrupt data.

7. **JSON field validation**: `Before`, `After`, and `Metadata` fields are length-capped only (max 1 MB). The audit service stores these verbatim — JSON parsing in the validator would be both expensive and contrary to the schema-agnostic design.

8. **Export time range cap**: 366-day span limit on exports prevents unbounded result sets without forcing callers to always supply a range (compliance officers may legitimately need full history).

---

## Contract Documentation

The canonical event contract has been documented at:

```
apps/services/platform-audit-event-service/Docs/canonical-event-contract.md
```

Contents:
- Full field reference table with types, required status, and max lengths
- Scope object conditional field requirements by ScopeType
- Actor and Entity object field tables
- All enum value tables with int values and descriptions
- Batch submission contract and semantics
- State snapshot field guidance (HIPAA PII redaction note)
- Timestamp constraints
- Idempotency semantics
- Tag constraints
- Minimal valid payload example
- Full DataChange payload example

---

## Files Changed

| File | Action |
|------|--------|
| `Validators/IngestAuditEventRequestValidator.cs` | Replaced — now targets `DTOs.Ingest.IngestAuditEventRequest` |
| `Validators/BatchIngestRequestValidator.cs` | Created |
| `Validators/AuditEventQueryRequestValidator.cs` | Created |
| `Validators/ExportRequestValidator.cs` | Created |
| `Validators/AuditEventScopeDtoValidator.cs` | Created |
| `Validators/AuditEventActorDtoValidator.cs` | Created |
| `Validators/AuditEventEntityDtoValidator.cs` | Created |
| `Docs/canonical-event-contract.md` | Created |
| `Program.cs` | Updated validator registration to `AddValidatorsFromAssemblyContaining` |

---

## Next Step

**Step 8**: Controller wiring — implement ingest, query, and export endpoints backed by the new repositories (`IAuditEventRecordRepository`, `IAuditExportJobRepository`, `IAuditSourceRegistrationRepository`, `IAuditIntegrityCheckRepository`).
