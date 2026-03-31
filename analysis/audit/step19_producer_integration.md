# Step 19 — Producer Integration Assets

**Service**: Platform Audit Event Service  
**Date**: 2026-03-30  
**Status**: Complete  
**Build**: 0 errors, 0 warnings

---

## Objective

Create comprehensive integration assets for upstream service teams (producers) to correctly and efficiently submit audit events to the Platform Audit Event Service.

Deliverables:
1. `Docs/producer-integration.md` — full producer guide
2. `Examples/AuditEventClientExample.cs` — ready-to-adapt .NET 8 C# client
3. `analysis/step19_producer_integration.md` — this report

---

## Files Created

| File | Purpose |
|---|---|
| `Docs/producer-integration.md` | Comprehensive producer integration guide covering all canonical fields, guidance sections, 11 JSON examples, and HTTP response reference |
| `Examples/AuditEventClientExample.cs` | Self-contained .NET 8 C# client: `IAuditEventClient`, `HttpAuditEventClient`, `IdempotencyKey` builder, `AuditEventExamples` factories (all 11 scenarios), `AuditClientOptions`, DI registration extension, usage walkthrough |
| `analysis/step19_producer_integration.md` | This report |

---

## Examples Covered

All 11 required event scenarios are implemented as both JSON examples (in the guide) and C# factory methods (in the client file):

| # | Event Type | Category | Severity | Notes |
|---|---|---|---|---|
| 1 | `user.login.succeeded` | Security | Info | Captures actor IP, user-agent, session ID |
| 2 | `user.login.failed` | Security | Warn | Anonymous actor; metadata includes reason + attempt count |
| 3 | `user.authorization.denied` | Security | Warn | Entity-targeted; includes required permission in metadata |
| 4 | `tenant.created` | Administrative | Notice | Platform visibility; service account actor; `after` snapshot |
| 5 | `organization.relationship.created` | Administrative | Notice | Org scope; `after` snapshot with relationship metadata |
| 6 | `user.role.assigned` | Administrative | Notice | DataChange semantics with `before`/`after` role lists |
| 7 | `patient.record.updated` | DataChange | Notice | `before`/`after` snapshots; Organization visibility; PHI note |
| 8 | `referral.created` | Business | Info | Org scope; metadata includes specialty and urgency |
| 9 | `appointment.scheduled` | Business | Info | Org scope; `after` with scheduled time |
| 10 | `document.viewed` | Access | Info | Actor IP + user-agent; idempotency key includes user+timestamp |
| 11 | `workflow.approved` | Compliance | Notice | Tenant visibility; `after` with outcome and approvedAt |

---

## Guidance Summary

### Required Fields (from validator)
`EventType`, `EventCategory`, `SourceSystem`, `SourceService`, `Visibility`, `Severity`, `OccurredAtUtc`, `Scope`, `Actor`.

`Action` and `Description` are optional by the validator but strongly recommended for readability in audit log displays.

### Idempotency Strategy
- Always supply `IdempotencyKey` on events that can be retried.
- Use a deterministic key built from stable event fields: `{sourceSystem}:{eventType}:{entityId}:{timestamp}`.
- The `IdempotencyKey` helper in the C# client enforces max 280 chars (well within the 300 limit), lowercased, and URL-encoded.
- `409 Conflict` responses are treated as accepted — the client returns `IngestResult.Accepted = true` for duplicates.

### Replay Strategy
- Set `isReplay: true` only for migration/re-processing pipelines — not for retries.
- Always supply `IdempotencyKey` on replay events to prevent double-submission if the replay pipeline retries.
- Downstream event forwarding skips replays by default (`ForwardReplayRecords=false`).

### Correlation / Request / Session
- Populate all three (`correlationId`, `requestId`, `sessionId`) for user-initiated HTTP requests.
- `correlationId` = W3C `traceparent` or `X-Correlation-Id` header, propagated from the gateway.
- `requestId` = originating request identifier from the load balancer or gateway.
- `sessionId` = user's session identifier (JWT `jti`, session cookie, or session store key).
- For background jobs, `correlationId` = job run ID; `requestId`/`sessionId` null.

### Visibility Selection
- Default: `Tenant` — visible to tenant admins and above.
- For organization-specific events (patient records, appointments): `Organization`.
- For user's own activity: `User`.
- For platform-level events (tenant provisioning, system alerts): `Platform`.
- For internal diagnostics: `Internal` (not queryable via the API).

### Severity Selection
- `Info` — successful operations, normal activity.
- `Notice` — significant events (tenant created, role assigned, record updated).
- `Warn` — recoverable failures (login failed, authorization denied).
- `Error` / `Critical` — failures requiring investigation or immediate attention.
- Never use `Debug` for production events. It is excluded from event forwarding by default.

### PHI / HIPAA Guidance
- Redact PHI from `before`/`after` snapshots before submitting.
- The audit service stores these fields verbatim without parsing or indexing.
- Use `Organization` or lower visibility for events involving patient data.
- Consider adding `"phi"` and `"hipaa"` tags for HIPAA-regulated record mutations.

### Before / After Snapshots
- Use `EventCategory.DataChange` with `before`/`after` for mutation events.
- Both fields are raw JSON strings, stored verbatim, max 1 MB each.
- `before` is null for creation events; `after` is null for deletion events.
- `metadata` is useful for change summaries (e.g. `{"changedFields": ["email", "phone"]}`).

---

## C# Client Architecture

### Classes and Interfaces

| Type | Kind | Lifetime | Purpose |
|---|---|---|---|
| `AuditClientOptions` | Config POCO | — | Bound from `AuditClient` config section in producer |
| `IAuditEventClient` | Interface | — | Producer-facing contract; inject this, not the concrete class |
| `HttpAuditEventClient` | Class | Scoped (via IHttpClientFactory) | HTTP implementation; handles auth headers, serialisation, error mapping |
| `IdempotencyKey` | Static class | — | Deterministic idempotency key builder |
| `AuditEventExamples` | Static class | — | Factory methods for all 11 canonical event patterns |
| `AuditClientServiceCollectionExtensions` | Static class | — | `services.AddAuditEventClient(config)` registration helper |
| `AuditUsageWalkthrough` | Class | — | Runnable usage examples showing single and batch ingest patterns |

### Key Design Decisions

**`IAuditEventClient` returns results, never throws for delivery failures.** Audit calls must never block or fail business operations. The client maps all transport and HTTP errors to `IngestResult.Accepted = false` and logs them as Warnings.

**`409 Conflict = Accepted`**. A duplicate idempotency key means the event is already recorded — this is the correct outcome. The client returns `Accepted = true` with `RejectionReason = "DuplicateIdempotencyKey"` so callers can distinguish new ingests from duplicates if needed.

**`IHttpClientFactory` integration**. The client uses named `HttpClient` via `AddHttpClient<IAuditEventClient, HttpAuditEventClient>("AuditEventClient")`, enabling connection pooling, retry policies (Polly), and lifecycle management without manual `HttpClient` disposal.

**Enum serialisation as strings**. `JsonStringEnumConverter` is used so that `EventCategory.Security` serialises as `"Security"` rather than `1`. This matches the API's accepted format and makes payloads human-readable in logs and broker messages.

**`AuditEventExamples` is stateless and parameterised**. Each factory method takes the minimum required identifiers and constructs a complete, idempotency-keyed request. Producers adapt these methods by adding their own domain fields or overriding defaults.

---

## Batch Ingest Pattern

The walkthrough's `EmitDocumentBatchViewedAsync` shows the recommended batch pattern:
- Build events from factory methods.
- Submit with `StopOnFirstError = false` (partial acceptance is fine for audit).
- Propagate `batchCorrelationId` so all batch events are traceable as a unit.
- Log rejected items as Warnings but do not fail the business operation.

---

## EventType Naming Conventions (Summary)

Documented in the guide. Pattern: `{domain}.{resource}.{verb}` in lowercase dot-separated form.

Examples: `user.login.succeeded`, `appointment.scheduled`, `workflow.approved`, `tenant.created`.

---

## Integration Checklist for Producer Teams

- [ ] Add `AuditClient` config section to producer's `appsettings.json`
- [ ] Inject `ServiceToken` via environment variable (`AuditClient__ServiceToken`)
- [ ] Call `services.AddAuditEventClient(configuration)` in `Program.cs`
- [ ] Inject `IAuditEventClient` into service classes
- [ ] Supply `idempotencyKey` on all events that can be retried
- [ ] Propagate `correlationId`, `requestId`, `sessionId` from the originating HTTP context
- [ ] Choose `visibility` appropriate to the event's data sensitivity
- [ ] Redact PHI from `before`/`after` snapshots before submitting
- [ ] Never gate business operations on the audit call's success — fire-and-observe
