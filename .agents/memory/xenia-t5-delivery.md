---
name: Xenia T5 delivery and test fix patterns
description: XENIA-P1-T5 complete — Phases A-G/J delivered; 444 tests passing; critical test compilation fix patterns.
---

## Delivery Summary

XENIA-P1-T5 (enterprise hardening, automation framework, observability) complete as of 2026-07-11.
Tests: 444 passing (up from 247 in T4). Build: 0 errors.

## New Infrastructure Directories

- `Xenia.Infrastructure/Automation/` — 12 files: registry, execution, discovery, dead-letter, scheduler, event publisher, worker, email provider
- `Xenia.Infrastructure/Observability/` — XeniaMetrics (34 counters/histograms/gauges via System.Diagnostics.Metrics)
- `Xenia.Infrastructure/Resilience/` — XeniaResiliencePolicy (exponential backoff) + CircuitBreaker (state machine)
- `Xenia.Infrastructure/Performance/` — XeniaPerformanceHarness (Stopwatch-based structured logging)
- `Xenia.Infrastructure/Security/` — XeniaSensitiveDataGuard (regex patterns, filename sanitization, audit detail redaction)
- `Xenia.Infrastructure/Adapters/Validation/` — AdapterValidationService (6 adapters, 3 criticality levels)

## Critical Test Compilation Fix Pattern

When Phase A injected new dependencies (`IAuditAdapter`, `IProviderCursorProtector`, `IEmailHtmlSanitizer`, `ILogger`) into infrastructure services, the existing unit tests stopped compiling. The test runner silently executed stale pre-compiled binaries (EXIT:0 with compile errors in output), reporting the old 247 count.

**Fix pattern:** Add private sealed noop stub classes inside each test class — never a separate file — then pass them as constructor args. Example:
```csharp
private sealed class NoopAuditAdapter : IAuditAdapter {
    public bool IsConfigured => false;
    public Task RecordEventAsync(XeniaAuditEvent e, CancellationToken ct = default) => Task.CompletedTask;
}
// ...
_sut = new EfMessagePersistenceService(_db, new NoopAuditAdapter(), NullLogger<EfMessagePersistenceService>.Instance);
```

## DurableLock InMemory Database Bug (Fixed)

`DbEmailSourceSyncLock` uses `IServiceScopeFactory` to create new EF scopes per call. The test setup had:
```csharp
services.AddDbContext<XeniaDbContext>(o => o.UseInMemoryDatabase($"db_{Guid.NewGuid():N}"));
```
The lambda generates a NEW GUID on every invocation (per scope), so each scope gets an empty DB and the second `TryAcquireAsync` always succeeds.

**Fix:** Capture the DB name outside the lambda:
```csharp
var dbName = $"db_{Guid.NewGuid():N}";
services.AddDbContext<XeniaDbContext>(o => o.UseInMemoryDatabase(dbName));
```

**Why:** EF InMemory shares state by name within the process, but only if all contexts use the same literal name string. Lambda capture is required.

## Sensitive Data Regex Pattern Notes

`XeniaSensitiveDataGuard` uses three regex patterns. Important gotcha:
- `\S{20,}` (non-space chars) does NOT match email bodies with spaces ("This is a long body...")
- Use `.{10,}` (any chars) for body patterns where spaces are expected
- Use `\S{10,}` (non-space) for cursor values (base64 has no spaces)

**How to apply:** When adding new sensitive-data test cases, ensure the test input after `=` matches the pattern's character class and minimum length.

## IEmailSyncService Correct Signature

`IEmailSyncService.RequestSyncAsync(tenantId, sourceId, actorId?, correlationId?, ct)` — 5 parameters. The `EmailAutomationProvider` uses this to trigger email syncs from the automation framework.

## DI Scoping Rules for Automation

- `IAutomationRegistry` — Singleton (holds all registered automations)
- `IAutomationDeadLetterStore` — Singleton (in-memory queue)
- `IAutomationEventPublisher` — Singleton (stateless)
- `IAutomationScheduler` — Singleton (timer-based)
- `IAutomationProvider` — Scoped (resolved per-operation via IServiceScopeFactory in worker)
- `IAutomationExecutionService` — Scoped
- `IAutomationDiscoveryService` — Scoped
- `IAutomationDiagnosticsService` — Scoped
- `AutomationRegistrationWorker` — Hosted service; uses IServiceScopeFactory to resolve scoped provider into singleton registry
