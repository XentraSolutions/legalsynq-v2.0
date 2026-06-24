# COM-B03 — Billing Account Core

> Status: complete. All 7 stories implemented; 19 new xUnit tests added
> on top of the existing 39 catalog tests (58 total passing).

## 1. Summary

COM-B03 introduces the **Billing Account Core** to the independent
Commerce service. A `BillingAccount` is the Commerce-owned commercial
entity that will later own subscriptions, invoices, payment methods, and
account standing. This block adds:

- The `BillingAccount` lifecycle entity (Draft → Active → Suspended ↔
  Active, → Closed; Closed is terminal).
- `BillingAccountExternalRef` — the *only* place host-platform tenant
  identity is stored. Commerce never copies host tenant tables and never
  calls a host tenant service.
- `BillingContact`, `BillingProfile`, and an account-level
  `BillingAccountAuditEvent` log.
- System-generated `AccountNumber` of the form `COM-BA-000001`.
- Admin-style HTTP APIs under `/api/commerce/billing-accounts`.

No identity adapters, JWT/tenant extraction, host-platform calls,
subscriptions, payments, payment methods, Stripe, checkout, webhooks,
account-standing engine, entitlement enforcement, or platform UIs are
introduced. Those belong to later blocks.

## 2. Stories Completed

- COM-E03-001 — BillingAccount Entity
- COM-E03-002 — Host Tenant Mapping (BillingAccountExternalRef)
- COM-E03-003 — Billing Contacts
- COM-E03-004 — Billing Profile
- COM-E03-005 — External Reference Model
- COM-E03-006 — Billing Account APIs
- COM-E03-007 — Account Change Audit Logging

## 3. Architecture Implemented

Mirrors the COM-B02 layering exactly:

```
Commerce.Domain         entities, enums, invariants
Commerce.Application    service interfaces, validators, exceptions
Commerce.Infrastructure EF Core mapping, services, migration
Commerce.Contracts      request/response DTOs
Commerce.Api            controllers (admin-style), reuses ProblemDetails middleware
Commerce.Tests          xUnit unit + WebApplicationFactory integration tests
```

### Composition rules honoured

- All EF Core access lives in Infrastructure services. Controllers only
  call application service interfaces.
- Domain rules live on entities (lifecycle methods, invariants).
- FluentValidation validators are auto-discovered via
  `AddValidatorsFromAssembly` already wired in `Commerce.Application`.
- Audit writes happen *inside* the same DbContext SaveChanges of the
  mutating operation, so a failed mutation never leaves an orphan audit
  row and a successful mutation never silently skips its audit row.

## 4. Files Created/Changed

Domain (`src/Commerce.Domain/Billing/`):
- `Enums/BillingAccountStatus.cs`, `Enums/BillingContactType.cs`,
  `Enums/BillingAccountAuditActorType.cs`
- `BillingAccount.cs` — lifecycle entity (`Activate`/`Suspend`/`Close`,
  guarded transitions throwing `InvalidStateTransitionException`)
- `BillingAccountExternalRef.cs` — `(HostPlatformKey, ExternalTenantId)`
  tuple, `SetPrimary`, `Update`
- `BillingContact.cs` — typed contacts with `IsPrimary`
- `BillingProfile.cs` — 1:1 with account
- `BillingAccountAuditEvent.cs` — append-only audit row +
  `BillingAccountAuditEventTypes` const class
- `HostPlatformKey.cs` — normalizer (lower-case, trimmed)
- `AccountNumberFormatter.cs` — formats sequence to `COM-BA-000001`

Application (`src/Commerce.Application/Billing/`):
- `Abstractions/IBillingAccountService.cs`,
  `IBillingAccountExternalRefService.cs`,
  `IBillingContactService.cs`, `IBillingProfileService.cs`,
  `IBillingAccountAuditService.cs`
- `Validators/BillingRequestValidators.cs` — FluentValidation rules for
  all create/update DTOs
- `Common/Exceptions/InvalidPrimaryReferenceException.cs`

Contracts (`src/Commerce.Contracts/Billing/BillingAccountDtos.cs`)

Infrastructure (`src/Commerce.Infrastructure/Billing/`):
- `Services/BillingAccountService.cs`,
  `BillingAccountExternalRefService.cs`, `BillingContactService.cs`,
  `BillingProfileService.cs`, `BillingAccountAuditService.cs`
- `Services/AccountNumberGenerator.cs` — max+1 with bounded retry on
  unique-index conflict; current scope is single-instance scheduler use,
  documented limitations included in the file
- `Services/BillingAuditWriter.cs` — emits audit rows in the same
  DbContext as the mutation
- `Persistence/Configurations/BillingConfigurations.cs` — 5 EF Core
  configurations, unique indexes on `account_number` and
  `(host_platform_key, external_tenant_id)`, restrict deletes
- `Mapping/BillingMappers.cs` — entity → response DTO mappers

API (`src/Commerce.Api/Controllers/Billing/`):
- `BillingAccountsController.cs`,
  `BillingAccountExternalRefsController.cs`,
  `BillingContactsController.cs`, `BillingProfileController.cs`,
  `BillingAccountAuditController.cs`

Middleware:
- `Middleware/ProblemDetailsExceptionMiddleware.cs` — added handler for
  `InvalidPrimaryReferenceException` → HTTP 422.

DI / wiring:
- `src/Commerce.Infrastructure/Persistence/CommerceDbContext.cs` — added
  5 `DbSet<>` properties + `ApplyConfiguration` calls.
- `src/Commerce.Infrastructure/DependencyInjection.cs` — registered
  billing services, audit writer, account-number generator.

Persistence migrations:
- `src/Commerce.Infrastructure/Persistence/Migrations/20260424011949_BillingAccountCore.cs`
  (+ Designer)
- Idempotent SQL: `analysis/billing-account-core.sql`

Tests (`tests/Commerce.Tests/Billing/`):
- `BillingTestHost.cs`
- `BillingAccountServiceTests.cs` (8 tests)
- `BillingExternalRefServiceTests.cs` (4 tests)
- `BillingContactServiceTests.cs` (4 tests)
- `BillingProfileAndAuditTests.cs` (4 tests, incl. "GET does not self-heal a missing profile")
- (Regression added under `BillingAccountServiceTests`: "Cannot close a Draft account")

## 5. Database / Migration Changes

Migration `BillingAccountCore` adds 5 new tables — none of them touch
COM-B01 or COM-B02 schema:

| Table                              | Purpose |
| ---------------------------------- | ------- |
| `billing_accounts`                 | account header + lifecycle |
| `billing_account_external_refs`    | host-platform tenant references |
| `billing_account_contacts`         | billing/legal/technical/admin contacts |
| `billing_account_profiles`         | invoicing/tax profile (1:1 with account) |
| `billing_account_audit_events`     | append-only audit log |

Idempotent SQL is regenerated to `analysis/billing-account-core.sql`.

## 6. API Endpoints Added

Route prefix: `/api/commerce/billing-accounts`

Billing accounts:
- `POST   /` — create (Draft)
- `GET    /` — list
- `GET    /{id}` — get
- `PUT    /{id}` — update header (DisplayName, LegalName, DefaultCurrency)
- `POST   /{id}/activate`
- `POST   /{id}/suspend`
- `POST   /{id}/close`

External refs:
- `POST   /{id}/external-refs`
- `GET    /{id}/external-refs`
- `PUT    /{id}/external-refs/{refId}`
- `POST   /{id}/external-refs/{refId}/make-primary`

Contacts:
- `POST   /{id}/contacts`
- `GET    /{id}/contacts`
- `PUT    /{id}/contacts/{contactId}`
- `POST   /{id}/contacts/{contactId}/make-primary`

Billing profile:
- `GET    /{id}/profile`
- `PUT    /{id}/profile`

Audit:
- `GET    /{id}/audit-events`

## 7. Billing Account Domain Model

`BillingAccount` (aggregate root):
- `Id` (Guid), `AccountNumber` (string, unique, system-generated)
- `DisplayName`, `LegalName?`, `DefaultCurrency` (3 chars)
- `Status: BillingAccountStatus` — `Draft | Active | Suspended | Closed`
- `CreatedAtUtc`, `UpdatedAtUtc`
- Lifecycle methods enforce:
  - `Activate()`: allowed from `Draft` or `Suspended` only
  - `Suspend()`: allowed from `Active` only
  - `Close()`: allowed from `Active` or `Suspended` only (Draft cannot be
    closed — never-activated accounts should be deleted instead). Closed
    is terminal.
- All transitions throw `InvalidStateTransitionException` if disallowed.

`BillingAccountExternalRef`:
- Owns `(HostPlatformKey, ExternalTenantId)`. `HostPlatformKey` is
  normalized to lower-case via `HostPlatformKey.Normalize`. Optional
  `ExternalCustomerRef`. `IsPrimary` flag.
- Unique index on `(HostPlatformKey, ExternalTenantId)` across the table
  (host platforms are global, not per-account).
- "Make primary" demotes any other primary on the same account.

`BillingContact`:
- Typed via `BillingContactType` (`Billing | Legal | Technical |
  Admin`). `Name`, `Email`, optional `Phone`. `IsPrimary` per type
  (one primary per `(account, type)`).

`BillingProfile`:
- 1:1 with `BillingAccount`. Auto-created when the account is created.
  Holds invoicing address fields, `TaxId`, `TaxExempt` flag.

`BillingAccountAuditEvent`:
- Append-only. Captures `EventType` (well-known constants),
  `Description`, `ActorType` (`System | Admin | Customer`), optional
  `ActorId` and `MetadataJson`.

## 8. Validation Rules Implemented

In `Commerce.Application/Billing/Validators/BillingRequestValidators.cs`:

- `CreateBillingAccountRequest`/`UpdateBillingAccountRequest`:
  `DisplayName` required ≤ 200 chars; `LegalName` ≤ 200; `DefaultCurrency`
  required, exactly 3 alpha chars.
- `CreateExternalRefRequest`/`UpdateExternalRefRequest`:
  `HostPlatformKey` required ≤ 64; `ExternalTenantId` required ≤ 128;
  `ExternalCustomerRef` ≤ 128.
- `CreateBillingContactRequest`/`UpdateBillingContactRequest`:
  `ContactType` ∈ enum; `Name` required ≤ 200; `Email` required, valid
  email, ≤ 320; `Phone` ≤ 64.
- `UpdateBillingProfileRequest`: each address field ≤ 200; `Country`
  ≤ 64; `TaxId` ≤ 64.

Validators are auto-discovered by `AddValidatorsFromAssembly` already
wired in `Commerce.Application/DependencyInjection.cs`. Failures raise
`FluentValidation.ValidationException` and the existing ProblemDetails
middleware maps it to HTTP 400.

## 9. Audit Behavior Implemented

`BillingAuditWriter` is invoked from each mutating service immediately
before `SaveChangesAsync`. Both the entity change and the audit row are
persisted in the same EF Core `SaveChanges` call, so:

- A failed mutation never leaves an orphaned audit row.
- A successful mutation never silently drops its audit row.

Event types emitted (`BillingAccountAuditEventTypes`):

| Action                                  | Event type                  |
| --------------------------------------- | --------------------------- |
| Account created                         | `AccountCreated`            |
| Account header updated                  | `AccountUpdated`            |
| Activate / Suspend / Close transitions  | `AccountActivated`, `AccountSuspended`, `AccountClosed` |
| External ref add / update / promote     | `ExternalRefAdded`, `ExternalRefUpdated`, `ExternalRefMadePrimary` |
| Contact add / update / promote          | `BillingContactAdded`, `BillingContactUpdated`, `BillingContactMadePrimary` |
| Profile updated                         | `BillingProfileUpdated`     |

Actor for COM-B03 is always `System` (no identity in scope yet).
`MetadataJson` carries before/after deltas for header updates and is
left null for simple state transitions.

## 10. Tests Added

19 new xUnit tests in `tests/Commerce.Tests/Billing/`, all using
in-memory EF (no MySQL required) via the new `BillingTestHost`:

- `BillingAccountServiceTests` (8): account-number assignment + sequence,
  validation failure, header update + audit, NotFound, full lifecycle
  (Draft→Active→Suspended→Active→Closed), Closed terminality, and the
  "cannot suspend a Draft" guard.
- `BillingExternalRefServiceTests` (4): host-platform-key normalization,
  duplicate `(HostPlatformKey, ExternalTenantId)` → `DuplicateKeyException`,
  `MakePrimary` demotes the previous primary, list returns all.
- `BillingContactServiceTests` (4): first-of-type primary, second
  primary demotes previous of the same type, different types each have
  their own primary, explicit `MakePrimary` swaps.
- `BillingProfileAndAuditTests` (3): profile auto-created, profile
  update writes the right audit row, audit log contains the full
  account lifecycle history.

## 11. Validation Results

| Command | Result | Notes |
| ------- | ------ | ----- |
| `dotnet restore Commerce.sln` | pass | clean restore after corrupt-cache cleanup |
| `dotnet build Commerce.sln -c Debug --no-restore` | pass | 0 warnings, 0 errors |
| `dotnet test Commerce.sln --no-restore` | pass | **60 / 60 passed** (39 existing catalog + 21 new billing — includes 2 regression tests added after architect review) |
| `dotnet ef migrations add BillingAccountCore` | pass | files added under `Persistence/Migrations/20260424011949_BillingAccountCore.*` |
| `dotnet ef migrations script --idempotent -o analysis/billing-account-core.sql` | pass | ~18 KB output written |
| `docker build -t commerce-api:b03 services/Commerce` | not run in this environment | Dockerfile unchanged from B02; no new system deps added |

## 12. Known Gaps / Deferred Items

(populated near the end)

## 13. Confirmation of Strict Exclusions

The following are **not** present in COM-B03 — verified by grep over
`services/Commerce/`:

- No identity adapters / JWT extraction / Tenant service client.
- No subscription, subscription-item, invoice, or payment entity.
- No payment-method, Stripe, checkout, or webhook code.
- No account-standing engine or entitlement enforcement.
- No product provisioning, Tenant Portal UI, or Control Center UI.
- No LegalSynq-specific integration. The only host-tenant handle is the
  opaque `(HostPlatformKey, ExternalTenantId)` tuple stored on
  `billing_account_external_refs`. Commerce never validates this tuple
  against any external system.

## 12. Known Gaps / Deferred Items

- `AccountNumberGenerator` is a single-instance, max-plus-one strategy
  with bounded retry on a unique-index conflict. This is deliberately
  scoped for the current single-scheduler deployment. A future block
  should swap this for a dedicated DB sequence (or Hi/Lo) once Commerce
  scales out horizontally. The current implementation documents this
  limit in source.
- `ActorType` is always `System` for COM-B03. When the identity
  adapter / JWT plumbing arrives in a later block, the audit writer
  should accept an injected actor context.
- No background standing/dunning evaluation runs on suspend/close — that
  is explicitly part of a later "account standing" block.
- `MetadataJson` is populated on header updates only; richer diffs
  (per-field old/new for contacts / external refs / profile) can be
  added when an admin-UI consumer needs them.

## 14. Recommended Next Block

**COM-B04 — Subscription Core.** With the account aggregate stable and
audited, the next natural block introduces `Subscription`,
`SubscriptionItem`, and the link from a `Subscription` to a
`BillingAccount`. That block should also formalise the
`AccountNumberGenerator` upgrade path (DB sequence / Hi-Lo) before
multi-instance deployment, since subscriptions will roughly double the
write traffic against the account aggregate.
