# COM-B05 — Payment Provider Integration

> Status: complete.

## 1. Summary

COM-B05 introduces a provider-agnostic payment integration layer for the
Commerce service. A clean `IPaymentProvider` abstraction lives in the
Application layer; a Stripe adapter (HTTP-based, no Stripe.NET dependency)
lives in Infrastructure. The block adds 4 new entities, hosted Checkout
Session creation, a Stripe webhook endpoint with HMAC-SHA256 signature
verification and idempotency, FluentValidation rules, and a full xUnit
test suite. Stripe is **disabled by default** and never requires real
credentials in tests. SDK types do **not** leak into Domain, Application,
or Contracts.

## 2. Stories Completed

- COM-E05-001 — Payment Provider Interface
- COM-E05-002 — Stripe Adapter
- COM-E05-003 — Customer Sync
- COM-E05-004 — Checkout Session
- COM-E05-005 — Payment Method Handling
- COM-E05-006 — Webhook Endpoint
- COM-E05-007 — Webhook Verification
- COM-E05-008 — Webhook Idempotency
- COM-E05-009 — Event Persistence
- COM-E05-010 — Payment Result Mapping

## 3. Architecture Implemented

- **Application abstractions** (`Commerce.Application.Payments.Abstractions`):
  - `IPaymentProvider` — provider adapter contract using POCO records
    (`ProviderCustomerRequest/Result`, `ProviderCheckoutRequest/Result`,
    `NormalizedProviderEvent`) so SDK types stay inside Infrastructure.
  - `IPaymentProviderRegistry` — resolves adapters by `PaymentProviderType`.
  - `IPaymentCheckoutService`, `IPaymentWebhookService`,
    `IPaymentProviderCustomerService`, `IPaymentMethodReferenceService`.
- **Stripe adapter** (`Commerce.Infrastructure.Payments.Stripe`):
  - `StripeSignatureVerifier` — Stripe-compatible
    `t=<unix>,v1=<hex_hmac_sha256>` scheme; signed payload
    `"{t}.{rawBody}"`; HMAC-SHA256 with the webhook secret; constant-time
    compare; configurable timestamp tolerance (default 5 min).
  - `StripeEventTranslator` — parses raw JSON, recognizes
    `checkout.session.completed/expired`,
    `customer.subscription.created/updated/deleted`,
    `payment_method.attached`; unknown types map to `Unsupported`.
  - `StripePaymentProvider` — direct REST calls to
    `/v1/customers` and `/v1/checkout/sessions` via injected `HttpClient`
    with bearer auth. Disabled by default; throws
    `PaymentProviderDisabledException` / `PaymentProviderConfigurationException`
    when not enabled or misconfigured.
- **Services** translate cleanly between contracts and EF entities;
  customer + mapping rows are upserted; webhook idempotency is enforced
  by a unique index on `(Provider, ProviderEventId)`.
- **Controllers** under `/api/commerce/payments` and
  `/api/commerce/billing-accounts/{id}/payment-*` call services only.
- **Cross-aggregate FKs** to `Subscription` / `BillingAccount` use
  `Restrict` per project convention.

## 4. Files Created/Changed

**Domain (`services/Commerce/src/Commerce.Domain/Payments/`):**
- `Enums/PaymentEnums.cs` — `PaymentProviderType`,
  `ProviderSubscriptionStatus`, `PaymentProviderEventProcessingStatus`.
- `PaymentProviderCustomer.cs`
- `PaymentProviderSubscription.cs`
- `PaymentMethodReference.cs`
- `PaymentProviderEventLog.cs`

**Contracts (`services/Commerce/src/Commerce.Contracts/Payments/`):**
- `PaymentDtos.cs` — request/response records.

**Application (`services/Commerce/src/Commerce.Application/`):**
- `Common/Exceptions/PaymentExceptions.cs` — five new exceptions all
  derived from `CatalogException` (mapped by middleware).
- `Payments/Abstractions/IPaymentProvider.cs`,
  `Payments/Abstractions/IPaymentServices.cs`.
- `Payments/Validators/PaymentValidators.cs`.

**Infrastructure (`services/Commerce/src/Commerce.Infrastructure/`):**
- `Payments/Configuration/PaymentProvidersOptions.cs` (+ `StripeOptions`).
- `Payments/Stripe/StripeSignatureVerifier.cs`
- `Payments/Stripe/StripeEventTranslator.cs`
- `Payments/Stripe/StripePaymentProvider.cs`
- `Payments/PaymentProviderRegistry.cs`
- `Payments/Services/PaymentProviderCustomerService.cs`
- `Payments/Services/PaymentCheckoutService.cs`
- `Payments/Services/PaymentWebhookService.cs`
- `Payments/Services/PaymentMethodReferenceService.cs`
- `Payments/Mapping/PaymentMappers.cs`
- `Persistence/Configurations/PaymentConfigurations.cs`
- Updated: `Persistence/CommerceDbContext.cs` (4 DbSets + configs),
  `DependencyInjection.cs` (registers options, `HttpClient`, registry,
  services), `ApiBehavior/ProblemDetailsExceptionMiddleware.cs`
  (maps the 5 new exceptions to 503/503/502/400 status codes).

**Api (`services/Commerce/src/Commerce.Api/`):**
- `Controllers/Payments/PaymentsController.cs` (checkout +
  `PaymentEventLogsController`, `BillingAccountPaymentMethodsController`,
  `BillingAccountPaymentCustomersController`).
- `Controllers/Payments/StripeWebhookController.cs` (raw-body read with
  `Request.EnableBuffering()`, signature header lookup).
- `appsettings.json` — added `PaymentProviders.Stripe` section
  (Enabled=false by default, blank secret/keys).

**Migration / SQL:**
- `Persistence/Migrations/20260424022726_PaymentProviderIntegration.cs`
- `analysis/payment-provider-integration.sql` — idempotent script
  generated from the migration.

**Tests (`services/Commerce/tests/Commerce.Tests/Payments/`):**
- `PaymentTestHost.cs`
- `PaymentDomainTests.cs`
- `StripeSignatureVerifierTests.cs`
- `PaymentCheckoutServiceTests.cs`
- `PaymentWebhookServiceTests.cs`
- `PaymentApiTests.cs`

**Build:** `Directory.Packages.props` and `Commerce.Infrastructure.csproj`
gained `Microsoft.Extensions.Http` for `AddHttpClient<T>()` registration.

## 5. Database / Migration Changes

Migration: `20260424023034_PaymentProviderIntegration` (baseline:
`20260424014922_SubscriptionEngine`).

**New tables** (MySQL via Pomelo):
- `PaymentProviderCustomers` — `(BillingAccountId, Provider)` unique;
  `(Provider, ProviderCustomerId)` unique; FK to BillingAccount Restrict.
- `PaymentProviderSubscriptions` — unique on
  `(SubscriptionId, Provider)`, and unique on
  `(Provider, ProviderCheckoutSessionId)` /
  `(Provider, ProviderSubscriptionId)` (MySQL unique indexes allow
  multiple NULLs, so unset/pending mappings co-exist while real
  provider IDs remain deterministic); FK to Subscription Restrict.
- `PaymentMethodReferences` — unique on
  `(Provider, ProviderPaymentMethodId)`; non-unique index on
  `(BillingAccountId, IsDefault)`; FK Restrict.
- `PaymentProviderEventLogs` — unique on
  `(Provider, ProviderEventId)` (the idempotency key); index on
  `(Provider, ProcessingStatus, CreatedAtUtc)` for listing.

The idempotent script lives at
`analysis/payment-provider-integration.sql` and only applies the new
migration when not already recorded in `__EFMigrationsHistory`.

## 6. API Endpoints Added

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/commerce/payments/checkout-sessions` | Create hosted Checkout session for a Subscription |
| POST | `/api/commerce/payments/webhooks/stripe` | Stripe webhook receiver (HMAC verified) |
| GET  | `/api/commerce/payments/event-logs` | List webhook event logs (filter by provider/status, take ≤500) |
| GET  | `/api/commerce/payments/event-logs/{id}` | Fetch a single event log |
| GET  | `/api/commerce/billing-accounts/{id}/payment-customers` | List provider customers |
| GET  | `/api/commerce/billing-accounts/{id}/payment-methods` | List safe card references |
| POST | `/api/commerce/billing-accounts/{id}/payment-methods/{pmId}/make-default` | Promote a method to default |

Status codes:
- `201` checkout success; `400` validation;
  `404` missing related entity;
  `502` provider call failure (`PaymentProviderException`);
  `503` provider disabled (`PaymentProviderDisabledException`) or
  misconfigured (`PaymentProviderConfigurationException`).
- Webhook always returns `200` once recorded; `400` on signature
  failure; `503` if Stripe is disabled.

## 7. Payment Provider Domain / Persistence Model

- `PaymentProviderCustomer` (BillingAccount ↔ provider customer) — only
  email/name plus the provider customer id. No PII beyond what an
  account already supplies.
- `PaymentProviderSubscription` (Commerce Subscription ↔ provider
  subscription/checkout) — tracks `Pending → Active → Cancelled / Failed
  / Unknown`. **The Commerce Subscription lifecycle is not mutated from
  webhooks in this block** — that is reserved for COM-B06.
- `PaymentMethodReference` — stores only Stripe-public fields: brand,
  last4 (≤4 chars enforced), expiry month (1–12), expiry year
  (2000–2100), and `IsDefault`. Card numbers / CVC / IBAN are never
  persisted.
- `PaymentProviderEventLog` — append-only history of webhook deliveries
  (`Received → Processed | Ignored | Duplicate | Failed`) with the raw
  body and a sanitized error message capped at 1 000 chars.

## 8. Stripe Adapter Behavior

- `IsEnabled` reads the live `PaymentProviders:Stripe:Enabled` flag via
  `IOptionsMonitor`. Hot-reload friendly.
- All API calls require `SecretKey`; missing settings throw
  `PaymentProviderConfigurationException` (mapped to 503).
- Customer + checkout requests are sent as
  `application/x-www-form-urlencoded` with bearer auth. Metadata
  includes `billing_account_id` and `subscription_id` so we can
  cross-reference incoming events.
- Non-success HTTP responses surface as `PaymentProviderException`
  carrying a 500-char snippet of the body — secrets never logged.
- Webhook signature verification: parses `t` and one or more `v1`
  values, enforces a configurable timestamp tolerance (default 300 s),
  and uses constant-time HMAC-SHA256 comparison.

## 9. Checkout Behavior Implemented

1. Validate request via FluentValidation (LineItems required,
   non-empty `ProviderPriceId`, positive quantity).
2. Resolve provider; reject 503 if disabled.
3. Load Subscription; reject if it does not belong to the requested
   BillingAccount, or if its status is `Cancelled` / `Expired`
   (`InvalidRelationshipException` → 409).
4. Create-or-get the provider customer (idempotent on
   `(BillingAccount, Provider)`).
5. Resolve success/cancel URLs from the request, falling back to
   configured defaults; missing both is a configuration error.
6. Call provider for hosted Checkout session passing line items,
   then upsert the `PaymentProviderSubscription` mapping with the new
   session id. The Stripe adapter emits
   `line_items[i][price]` / `line_items[i][quantity]` form fields as
   required by the Stripe Checkout Sessions API.
7. Return `CheckoutSessionResponse` (provider, session id, URL,
   customer id, expiry).

## 10. Webhook Behavior Implemented

1. Read raw body verbatim (controller calls `Request.EnableBuffering()`).
2. Resolve provider; 503 if disabled.
3. Verify signature; throws `InvalidWebhookSignatureException` (400) on
   any failure (missing header, malformed header, stale timestamp,
   wrong secret, tampered body).
4. Translate raw payload to `NormalizedProviderEvent`; unparsable
   payloads are logged as `Failed` and return 200 with that status —
   Stripe will not retry beyond malformed deliveries.
5. Look up the `(Provider, ProviderEventId)` index; existing rows
   short-circuit to `Duplicate` (also handled if a concurrent insert
   raises `DbUpdateException`).
6. Insert the log row in `Received` state, then apply state changes
   based on `Kind`:
   - Checkout session completed → mapping `Active`.
   - Checkout session expired → mapping `Failed`.
   - Subscription created/updated → mapping `Active`.
   - Subscription deleted → mapping `Cancelled`.
   - Payment method attached → upsert `PaymentMethodReference`
     (locating the BillingAccount via the customer mapping).
   - Anything else → `Ignored` (logged with reason).
7. Errors during application surface as `Failed` on the log row but
   still return 200 with the failure reason — provider won't retry
   forever, and we have a record.

## 11. Validation Rules Implemented

- `CreateCheckoutSessionRequestValidator`:
  - `BillingAccountId`, `SubscriptionId` not empty.
  - `SuccessUrl` / `CancelUrl` must be absolute http(s) URIs when
    supplied.
  - `CustomerEmail` is a pragmatic non-blank email.
  - `CustomerName` ≤ 200 chars.
  - `MetadataJson` parses as JSON when supplied.

Validators are auto-registered in `AddApplicationValidators` via
`AddValidatorsFromAssembly` already wired by COM-B02.

## 12. Tests Added

28 new xUnit tests under
`services/Commerce/tests/Commerce.Tests/Payments/`:

- `PaymentDomainTests` (4) — entity invariants and lifecycle.
- `StripeSignatureVerifierTests` (7) — accepts valid; rejects tampered
  body, wrong secret, stale timestamp, missing/malformed header;
  configuration error when secret missing.
- `PaymentCheckoutServiceTests` (5) — happy path, customer reuse,
  disabled provider, cross-account rejection, validator failure.
- `PaymentWebhookServiceTests` (7) — signature failure, disabled
  provider, checkout-completed mapping update, idempotent duplicate
  handling, payment-method attach upsert, unparsable payload, listing.
- `PaymentApiTests` (5) — checkout returns 503/404/400 (Stripe
  disabled in test config), event-logs list/get, payment-methods list,
  webhook 400/503.

A `PaymentTestHost` provides an in-memory `CommerceDbContext`, a
`FakePaymentProvider` (driver-controlled customer/checkout ids and
translator), and a deterministic clock — no real Stripe credentials are
ever required.

## 13. Validation Results

- `dotnet restore`, `dotnet build` — succeed (0 warnings, 0 errors).
- `dotnet test` — **138 passed / 0 failed** (110 prior + 28 new).
- EF migration applied cleanly; idempotent SQL generated.
- No SDK types referenced from Domain / Application / Contracts
  (verified by build, no Stripe.NET package was ever added).

## 14. Known Gaps / Deferred Items

- Stripe is implemented; no other providers (PayPal, Adyen, etc.).
- Webhook handlers do **not** yet update Commerce Subscription
  lifecycle (Active/PastDue/Cancelled). That mapping belongs to
  COM-B06 (Subscription ↔ Provider state reconciliation).
- No background reconciliation loop (e.g., polling Stripe to recover
  missed events). The webhook log gives operators visibility; an
  outbox or replay endpoint can be added later.
- No customer-portal session creation (Stripe Billing Portal) —
  out of scope here.

## 15. Confirmation of Strict Exclusions

The following remain explicitly **not** implemented in this block, as
required:
- No invoices, refunds, credits, dunning, or revenue recognition.
- No standing-payment / saved-instrument billing automation.
- No entitlements or feature flags driven by provider state.
- No identity, JWT, or auth changes.
- No UI work.
- No LegalSynq integration.
- No real Stripe credentials in tests; Stripe disabled by default.
- No Stripe SDK types in Domain / Application / Contracts.

## 16. Recommended Next Block

**COM-B06 — Subscription / Provider State Reconciliation.** Now that
provider events are persisted and a mapping exists, the next block
should: (a) translate `PaymentProviderSubscription.Status` into Commerce
`Subscription.Status` transitions safely (idempotent, audited via
`SubscriptionChange`), (b) introduce an outbox / dispatcher so missed
webhook events can be replayed, and (c) add a `customer-portal-sessions`
endpoint for self-serve payment-method updates. After that COM-B07 can
layer invoicing on top.
