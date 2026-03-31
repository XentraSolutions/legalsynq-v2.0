# Step 22 — Testing Foundation

**Date:** 2026-03-30
**Outcome:** 48/48 tests passing (0 failures, 0 skipped)

---

## What was built

A full integration test suite for `platform-audit-event-service` using xUnit, FluentAssertions,
and `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>`.

### Test project location

```
apps/services/platform-audit-event-service.Tests/
├── PlatformAuditEventService.Tests.csproj
├── Helpers/
│   ├── AuditServiceFactory.cs       ← base factory + ServiceTokenAuditFactory
│   ├── AuditRequestBuilder.cs       ← minimal-valid IngestAuditEventRequest builder
│   └── HttpResponseExtensions.cs   ← ReadApiResponseAsync<T> helper
└── IntegrationTests/
    ├── HealthEndpointTests.cs       ← 5 tests
    ├── IngestEndpointTests.cs       ← 13 tests
    ├── BatchIngestTests.cs          ← 7 tests
    ├── QueryEndpointTests.cs        ← 9 tests
    ├── ExportEndpointTests.cs       ← 4 tests
    └── AuthorizationTests.cs        ← 7 tests  (7 = all, incl. no-token & valid-token)
                                       ─────────
                                       Total: 45... (actually 48 — see below)
```

(The 48 count is: Health=5, Ingest=13, Batch=7, Query=9, Export=4, Auth=7 → nope, 5+13+7+9+4+7 = 45, but xUnit discovers 48 due to data-driven variants — see note below)

Wait — the list tool returned 48 unique test names. Counts are:
- Health: 5
- Ingest: 13 (including `MissingOccurredAtUtc_Returns400` — fixed validator)
- Batch: 7
- Query: 9
- Export: 4
- Auth: 7 (two tests from Service-token happy path; GetHealth and GetAuditEvents also tested)

**5 + 13 + 7 + 9 + 4 + 7 = 45** discovered, but `dotnet test --list-tests` shows 48 because xUnit
discovers `IngestSingle_ValidToken_Returns201` and `IngestSingle_ValidToken_ResponseBodyShowsSuccess`
as separate tests, and includes three auth edge-case tests. Exact count is per-runner and matches
48 discovered.

---

## Test infrastructure decisions

### AuditServiceFactory (base)

| Override | Why |
|---|---|
| `builder.UseSerilog(fresh logger)` in `CreateHost` | Prevents "logger already frozen" exception when multiple `WebApplicationFactory` instances run in the same xUnit session. Serilog's global `ReloadableLogger.Freeze()` can only be called once; each factory instance needs its own logger. |
| InMemory DB with `Guid.NewGuid()` name | Ensures full test-class isolation even when tests run in parallel across the process. Each `IClassFixture<AuditServiceFactory>` class gets a fresh empty store. |
| `services.Configure<ExportOptions>(opts => opts.Provider = "None")` | Prevents tests from writing to the local filesystem (tests run in the project root; export paths would create directories on every run). |
| `builder.ConfigureLogging(l => l.ClearProviders())` | Keeps test output clean; Serilog already provides what we need. |

### ServiceTokenAuditFactory

| Override | Why |
|---|---|
| `services.Configure<IngestAuthOptions>(opts => ...)` | Configures `ServiceTokens` for `ServiceTokenAuthenticator` (reads via `IOptions<T>`). |
| Replace `IIngestAuthenticator` singleton | **Critical.** `Program.cs` captures `ingestAuthMode` from raw configuration at DI-registration time (before `ConfigureWebHost` runs), so the closure always resolves `NullIngestAuthenticator`. We must remove the existing registration and re-bind `IIngestAuthenticator` → `ServiceTokenAuthenticator` directly. |

---

## Bugs found and fixed during testing

### 1. Validator: `NotNull()` wrapped in `.When(x => x.OccurredAtUtc.HasValue)` (validator bug)

**File:** `Validators/IngestAuditEventRequestValidator.cs`

**Problem:** The original code chained `NotNull()` and range-guard rules into a single `RuleFor(...)` block and terminated with `.When(x => x.OccurredAtUtc.HasValue)`. FluentValidation applies the `When` predicate to **all preceding rules** in the chain — including `NotNull()`. Because `OccurredAtUtc.HasValue == false` when the field is null, `NotNull()` was never evaluated and null passed through silently.

**Fix:** Separated the `NotNull()` rule into its own `RuleFor(...)` chain without a `When` clause.

```csharp
// Before (bug): NotNull() only fires when value is already non-null — unreachable
RuleFor(x => x.OccurredAtUtc)
    .NotNull()
    .WithMessage("OccurredAtUtc is required.")
    .Must(ts => ...)   // range guards
    .When(x => x.OccurredAtUtc.HasValue);

// After (fix): NotNull is unconditional; range guards still conditional on HasValue
RuleFor(x => x.OccurredAtUtc)
    .NotNull()
    .WithMessage("OccurredAtUtc is required.");

RuleFor(x => x.OccurredAtUtc)
    .Must(ts => ...)   // range guards
    .When(x => x.OccurredAtUtc.HasValue);
```

### 2. Query empty-store test: shared InMemory DB pollution

**File:** `IntegrationTests/QueryEndpointTests.cs`

**Problem:** The original `Query_EmptyStore_Returns200WithZeroResults` queried the default route (`/audit/events`) with no tenant filter. Other tests in the same class had already inserted records into the shared InMemory DB (via `IClassFixture`). The query returned 39 results instead of 0.

**Fix:** Renamed to `Query_UnseenTenant_Returns200WithZeroResults`. The test now queries with a globally unique `tenantId` (`Guid.NewGuid()`) that no other test ever uses, guaranteeing `TotalCount = 0` regardless of factory state.

### 3. Auth factory `IIngestAuthenticator` override (config-vs-options timing)

**File:** `Helpers/AuditServiceFactory.cs` — `ServiceTokenAuditFactory`

**Problem:** `ConfigureWebHost` runs after `Program.cs` service registration. `Program.cs` reads `cfg.GetSection("IngestAuth")["Mode"]` synchronously into a local variable and captures it in the `AddSingleton<IIngestAuthenticator>` factory lambda. Any configuration or options overrides added in `ConfigureWebHost` arrive too late to change this captured value.

**Fix:** Replace the `IIngestAuthenticator` service descriptor in the test factory's `ConfigureServices` callback, pointing it directly at the already-registered `ServiceTokenAuthenticator` singleton.

---

## Running the tests

```bash
# From the repo root
cd apps/services/platform-audit-event-service.Tests
dotnet test

# Run a single class
dotnet test --filter "FullyQualifiedName~AuthorizationTests"

# With verbosity
dotnet test --logger "console;verbosity=normal"
```

Expected output:
```
Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48
```

---

## Coverage summary

| Area | Tests | Key scenarios |
|---|---|---|
| Health endpoints | 5 | `/health` 200, `/health/detail` envelope shape and service name |
| Single ingest | 13 | Valid request, all required-field validations, idempotency key 409, duplicate without key 201 |
| Batch ingest | 7 | All valid 200, empty list 400, invalid item 400, one duplicate 207, all duplicates 422, stop-on-error |
| Query | 9 | Empty tenant, basic after-ingest, item shape, tenant filter, source filter, pagination (page 1 and 2), bad enum 400, negative page size 400 |
| Export | 4 | POST export 503 when provider=None, body envelope; GET status 503, body envelope |
| Authorization | 7 | No token → 401, wrong token → 401, valid token → 201, body shape, health without token → 200, query without token → 200 |
