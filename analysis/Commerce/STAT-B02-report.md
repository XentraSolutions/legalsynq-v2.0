# STAT-B02 — Statement Templates, Persistence & Monthly Generation Foundation

This block adds the *persistence* layer to the customer-statement engine
shipped in STAT-B01. STAT-B01 produced statements on the fly (pure read,
JSON or HTML) — STAT-B02 turns the same engine into a *system of
record*: each generation is captured as an immutable
`CustomerStatement` snapshot, addressable by a deterministic
`STMT-YYYY-NNNNNN` number, and explicitly opt-in renderable to HTML.
Tenants may also configure reusable `StatementTemplate`s with branding
defaults, mirroring the INV-TPL-01 invoice-template catalogue but
tenant-scoped only (no Platform tier).

---

## 1. Goals & non-goals

### In scope
- Tenant-scoped `StatementTemplate` aggregate with `Draft`/`Active`/
  `Retired` lifecycle, `IsDefault` flag, and the same atomic-default
  uniqueness invariant proven out by `InvoiceTemplate`.
- Persisted `CustomerStatement` snapshots: every successful
  `generate` call materialises the full STAT-B01 document as JSON
  (and optionally pre-rendered HTML) and stores it under a
  monotonically-increasing `StatementNumber`.
- `IStatementNumberGenerator` producing `STMT-YYYY-NNNNNN` numbers,
  per tenant per calendar year, derived from
  `MAX(seq) + 1` with zero-padding.
- New endpoints under `/api/statement-templates` (CRUD + lifecycle)
  and additions under `/api/statements/...`:
  - `POST customers/{customerId}/generate`
  - `POST customers/{customerId}/monthly/generate`
  - `GET  customers/{customerId}/history`
  - `GET  history/{id}`
  - `GET  history/{id}/render/html`
  - `POST history/{id}/void`
- EF migration `StatementTemplatesAndPersistence` introducing two new
  tables and a `DefaultScopeKey` computed-column unique index on
  `statement_templates`.
- Comprehensive domain + integration tests; STAT-B01 endpoints remain
  unchanged.

### Strict exclusions
- No authentication / authorisation work (`X-Tenant-Id` middleware
  unchanged).
- No email / PDF rendering, no LegalSynq, no scheduled batch jobs,
  no portal, no UI — backend-only.
- No payment changes; `CustomerStatementService` (the pure builder
  from STAT-B01) is reused as-is.
- No Platform-scoped statement templates — `StatementTemplate` is
  always tenant-owned.

---

## 2. Domain model

### 2.1 `StatementTemplate`
Mirrors the structurally-similar `InvoiceTemplate` but stripped down
to a single (tenant-only) scope dimension. Fields:

- Identity: `Id`, `TenantId`, `CreatedAtUtc`, `UpdatedAtUtc`
- Catalogue: `Name`, `Description`, `Status` (Draft/Active/Retired),
  `IsDefault`
- Branding: `LogoUrl`, `AccentColor`, `HeaderText`, `FooterText`
- Rendering toggles: `DisplayOutstandingTable`, `DisplayPaymentInstructions`,
  `DisplayTransactionMemos`
- Numbering presentation: `StatementNumberPrefix` (defaults to `STMT`)
- Issuer overrides (optional): `IssuerDisplayName`, `IssuerAddressLine1/2`,
  `IssuerCity`, `IssuerStateRegion`, `IssuerPostalCode`,
  `IssuerCountry`, `IssuerEmail`, `IssuerPhone`, `IssuerWebsite`
- Footer / legal: `PaymentInstructions`, `TermsText`, `MemoPlaceholder`

Lifecycle and default-uniqueness rules follow `InvoiceTemplate`
verbatim:
- Draft is editable but not selectable / not eligible for default
- Active is editable + selectable + eligible for default
- Retired is locked, cannot be default, and can never be revived
- At most one default per tenant scope, enforced at the DB level
  by a stored computed-column unique index on `DefaultScopeKey`
  (= `TenantId` when `IsDefault=1`, else `NULL`).

### 2.2 `CustomerStatement`
The persisted snapshot. One row per generation. Once written, the
content fields are *immutable*; only `Status` and `Voided*` may
transition (and only `Generated -> Voided`).

Columns:
- `Id` (Guid), `TenantId`, `CustomerId`, `StatementNumber` (unique per tenant)
- `TemplateId` (nullable — the optional template used)
- `PeriodStart`, `PeriodEnd`, `GeneratedAtUtc`
- `Status` ∈ {`Generated`, `Voided`}
- `Currency`, `OpeningBalance`, `ClosingBalance`,
  `OutstandingBalance`, `TotalInvoiced`, `TotalPaid`
- `StatementSnapshotJson` (the full STAT-B01 `CustomerStatementDocument`)
- `TemplateSnapshotJson` (the template used at generation time, or
  `null` if no template was selected)
- `HtmlSnapshot` (nullable — populated when the caller asks for HTML
  to be pre-rendered at generate time)
- `VoidedAtUtc`, `VoidReason`

Indexes:
- Unique `(TenantId, StatementNumber)` so duplicate numbers are caught
  by the DB even if the in-memory generator races.
- Lookup `(TenantId, CustomerId, GeneratedAtUtc)` for the per-customer
  history endpoint.
- `(TenantId, GeneratedAtUtc)` for tenant-wide audit queries.

### 2.3 Why the snapshot is structured as JSON
Storing `CustomerStatementSnapshotJson` instead of denormalising every
line into a child table:
- The document already exists as a stable record-shape from STAT-B01
  (`CustomerStatementDocument`). Round-tripping it via
  `System.Text.Json` keeps the persistence boundary trivial and
  guarantees the renderer sees byte-identical input on every replay.
- Future blocks may add new optional fields to the document. A JSON
  blob accepts new fields without a migration, while still letting
  us add typed columns for the few aggregates we need indexed
  (currency, balances, period bounds).
- The contract is one-way: never *re-derive* from current invoices /
  payments; always read the snapshot. That guarantee is the whole
  point of persistence — invoices issued or paid after a statement
  is generated must NOT alter that statement's contents.

### 2.4 Number generation
`IStatementNumberGenerator` exposes
`Task<string> NextAsync(Guid tenantId, int year, CancellationToken)`.
The default implementation:
1. Reads `MAX(StatementNumber)` for the tenant + year prefix
   (`STMT-YYYY-`).
2. Parses the trailing zero-padded sequence, increments by one,
   re-pads to six digits.
3. Returns `STMT-{year:D4}-{seq:D6}`.

Concurrency caveat (documented loudly in the implementation): two
generators racing can both compute the same `seq + 1`. The
`(TenantId, StatementNumber)` UNIQUE index then rejects the second
write at `SaveChanges` time — the persistence service catches the
duplicate-key `DbUpdateException`, retries the number generation up
to 5 times, and surfaces a clean conflict to the controller after
exhausting retries. STAT-B02 deliberately stops short of a sequence
table or `SELECT ... FOR UPDATE` — the retry loop is enough for the
expected admin-driven workload.

---

## 3. Service layer

### 3.1 `IStatementTemplateService`
Mirrors `IInvoiceTemplateService` but with a single tenant-scope
parameter (no `Guid? tenantId` overload). Supports
`CreateAsync`, `GetAsync`, `ListAsync`, `GetDefaultAsync`,
`UpdateAsync`, `ActivateAsync`, `RetireAsync`, `MakeDefaultAsync`.
Default-uniqueness uses the same atomic transaction +
`UnsetDefaultsInScopeAsync` strategy.

### 3.2 `IStatementTemplateSelectionService`
Read-only counterpart used by the persistence path:
`SelectForStatementAsync(tenantId, explicitTemplateId)` returns the
explicit template (validated Active + in-scope) or the tenant's
current default (or `null` if none). Mirrors
`IInvoiceTemplateSelectionService`.

### 3.3 `ICustomerStatementPersistenceService`
The new write surface:
- `GenerateAsync(tenantId, customerId, periodStart, periodEnd,
  templateId?, renderHtml, ct)` — drives the STAT-B01 builder, picks
  the template, snapshots both, persists, returns the new
  `CustomerStatement`.
- `GenerateMonthlyAsync(tenantId, customerId, year, month,
  templateId?, renderHtml, ct)` — convenience composer.
- `ListHistoryAsync(tenantId, customerId, ct)` — paginated history
  for one customer.
- `GetHistoryAsync(tenantId, statementId, ct)` — fetch a single
  persisted statement.
- `RenderHtmlAsync(tenantId, statementId, ct)` — returns
  `HtmlSnapshot` if non-null; else rehydrates the
  `CustomerStatementDocument` from `StatementSnapshotJson` and runs
  the existing `ICustomerStatementHtmlRenderer`.
- `VoidAsync(tenantId, statementId, reason, ct)` — flips
  `Generated → Voided`, idempotent.

Validation surface uses `StatementValidationException` (already
defined in STAT-B01) and a new
`StatementTemplateException` family that mirrors
`InvoiceTemplateException` (all derive `InvalidOperationException`
so the controller's existing 400-mapping continues to work).

### 3.4 STAT-B01 endpoints unchanged
The original `/api/statements/customers/{customerId}` (JSON GET),
`/render/html`, and `/monthly` GET paths continue to call
`ICustomerStatementService` directly. They are *deliberately* not
folded into the persisted-history workflow — they remain the
"preview" path. The new `POST .../generate` endpoint is the
authoritative way to materialise a statement for the audit trail.

---

## 4. HTTP surface

### 4.1 `/api/statement-templates` (tenant-only)
| Method | Path                              | Purpose                       |
|--------|-----------------------------------|-------------------------------|
| POST   | `/`                               | Create (Draft or Active)      |
| GET    | `/`                               | List in tenant scope          |
| GET    | `/{id}`                           | Get one                       |
| PUT    | `/{id}`                           | Update editable fields        |
| POST   | `/{id}/activate`                  | Draft → Active                |
| POST   | `/{id}/retire`                    | * → Retired                   |
| POST   | `/{id}/make-default`              | Atomic default switch         |
| GET    | `/default`                        | Current default               |

### 4.2 `/api/statements/...` additions
| Method | Path                                                  | Purpose                                |
|--------|-------------------------------------------------------|----------------------------------------|
| POST   | `customers/{customerId}/generate`                     | Materialise + persist a statement      |
| POST   | `customers/{customerId}/monthly/generate`             | Materialise for a calendar month       |
| GET    | `customers/{customerId}/history`                      | Per-customer history list              |
| GET    | `history/{id}`                                        | Read a persisted statement             |
| GET    | `history/{id}/render/html`                            | Render persisted snapshot to HTML      |
| POST   | `history/{id}/void`                                   | Soft-void a generated statement        |

All controllers use the same `Problem(...)` mapping idiom as
STAT-B01 / INV-TPL: `StatementValidationException` and
`ArgumentException` → 400; missing / cross-tenant id → 404;
`StatementTemplateDefaultConflictException` → 409 (must come BEFORE
the catch-all `InvalidOperationException` → 400).

---

## 5. Persistence

### 5.1 EF mapping additions
Two new `DbSet`s on `TenantBillingDbContext`:
`StatementTemplates`, `CustomerStatements`. The `OnModelCreating`
configuration:
- Sets `ToTable("statement_templates")` and `("customer_statements")`.
- Mirrors the column-width rules from `InvoiceTemplate` for any
  shared fields.
- Stores monetary aggregates with `HasPrecision(18, 2)`.
- Adds `(TenantId, StatementNumber)` UNIQUE index on
  `customer_statements`.
- Adds `(TenantId, CustomerId, GeneratedAtUtc)` and
  `(TenantId, GeneratedAtUtc)` indexes for history reads.
- For `statement_templates`, adds the same `DefaultScopeKey`
  computed-column unique index pattern as `InvoiceTemplate`, but
  scoped to tenant alone:
  ```sql
  (CASE WHEN `IsDefault` = 1 THEN `TenantId` ELSE NULL END)
  ```
  Index name: `UX_statement_templates_DefaultScopeKey`. Skipped on
  non-relational providers (InMemory) for the same reason as
  `invoice_templates`.

### 5.2 Migration
`StatementTemplatesAndPersistence` (single migration). Creates the
two new tables with the columns + indexes above. No mutations to any
existing table.

---

## 6. Concurrency & immutability

### 6.1 Default uniqueness
The relational unique index on `DefaultScopeKey` prevents two
concurrent `make-default` writers from leaving two defaults. The
service still does the unset-then-set inside an `IUnitOfWork`
transaction, but the DB-level guard is the ultimate authority. A
duplicate-key error is translated to
`StatementTemplateDefaultConflictException` and surfaced as 409.

### 6.2 Statement number races
The generator uses `MAX(seq) + 1`, so two generators running in
parallel on the same tenant can compute identical numbers. The
service:
1. Generates a number.
2. Tries to persist.
3. On `DbUpdateException` matching the `(TenantId, StatementNumber)`
   unique index name (`UX_customer_statements_TenantId_StatementNumber`),
   re-generates the number and retries.
4. After 5 failed attempts, surfaces a 409 with a deliberate "try
   again" message — well above the worst-case admin-driven
   contention.

### 6.3 Snapshot immutability
`StatementSnapshotJson`, `TemplateSnapshotJson`, `HtmlSnapshot`,
all monetary aggregates, and period bounds are written exactly once
inside the create transaction and never updated. The render path
*never* re-runs the STAT-B01 builder against current invoice /
payment state — it deserialises the stored JSON. That means an
invoice issued after generation does NOT change the statement
content, even if rendered ten years later.

### 6.4 Voiding
`VoidAsync` only changes `Status`, `VoidedAtUtc`, `VoidReason`. It
never deletes or rewrites snapshot data. A voided statement
continues to render to HTML / serve via the history GET; consumers
inspect `Status` to decide whether to display it as void in their
UI.

---

## 7. HTML rendering from stored snapshot

`RenderHtmlAsync(statementId)` flow:
1. Load the persisted `CustomerStatement` (tenant-scoped, returns
   404 on miss).
2. If `HtmlSnapshot` is non-null, return it verbatim.
3. Otherwise, deserialise `StatementSnapshotJson` into a
   `CustomerStatementDocument` and pass it to the existing
   `ICustomerStatementHtmlRenderer`. Return the result without
   writing it back — the lazy render path is *stateless* by design;
   if the caller wants a stored HTML snapshot, they should pass
   `renderHtml=true` at generate time.

This separation keeps two concerns apart: persistence (immutable,
written once) vs. presentation (deterministic, derivable from the
snapshot).

---

## 8. Tests

### 8.1 Domain unit tests
- `StatementTemplateServiceTests`: lifecycle (Draft → Active →
  Retired), default uniqueness (only one default per tenant, atomic
  switch unsets prior), validation (name required, status enum,
  retired-cannot-edit, retired-cannot-default).
- `StatementNumberGeneratorTests`: first number for a year is
  `STMT-YYYY-000001`; subsequent numbers increment by one;
  per-year reset; pure logic via in-memory list.
- `CustomerStatementPersistenceServiceTests`: generate writes a
  snapshot with the expected aggregates; renderHtml=true populates
  `HtmlSnapshot`; renderHtml=false leaves it null and lets the
  render endpoint rebuild from JSON; void is idempotent;
  cross-tenant id returns null.

### 8.2 Integration tests (HTTP)
- `StatementTemplatesApiTests`: 201 on create, 409 on second default
  promotion concurrent attempt (simulated via two interleaved
  requests), 404 on cross-tenant get, 400 on invalid status
  transition.
- `StatementsGenerateApiTests`: POST generate returns the persisted
  statement with a non-empty `StatementNumber`; GET history returns
  it; GET render/html returns matching content; void flips status to
  `Voided`.
- `StatementsHistoryRenderApiTests`: render uses stored HTML when
  present; falls back to JSON deserialisation when not; never
  re-derives from current invoices.

### 8.3 STAT-B01 regression coverage
The existing `CustomerStatementApiTests` and
`CustomerStatementServiceTests` continue to pass with no edits.
Their endpoints behave identically — STAT-B02 strictly *adds*
routes.

---

## 9. Deviations & follow-ups

- **No `Default` status.** `IsDefault` remains a separate boolean —
  same as `InvoiceTemplate`. The status enum is purely lifecycle.
- **No background regeneration.** A statement is generated by an
  explicit `POST .../generate` call; STAT-B02 has no scheduler.
  Monthly close jobs are deferred.
- **No PDF / email.** Snapshot HTML is the only render output here;
  PDF lands in a later block.
- **No template-driven `OutstandingTable` filtering today.** The
  toggles are persisted on the template but the renderer in this
  block ignores them — STAT-B01's renderer renders the full table
  unconditionally and that behaviour is preserved. A later block
  will wire the toggles into the renderer.
- **JSON serialisation contract.** Statement snapshots use
  `System.Text.Json` defaults plus
  `JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }`
  so `CustomerStatementTransactionType` round-trips as a string,
  surviving renames in the future.

---

## 10. Migration ordering

Migration name: `StatementTemplatesAndPersistence`. Idempotent SQL
script generated via `dotnet ef migrations script --idempotent` and
checked in alongside the migration .cs files. Applied in order
after `20260424222018_InvoiceIssuerAddressEnrichment`.

### 10.1 Generated DDL (MySQL, idempotent)

The migration `20260429053936_StatementTemplatesAndPersistence`
emits the following DDL when run via
`dotnet ef migrations script --idempotent`. Reproduced verbatim
(comments / `MigrationsScript` boilerplate elided).

```sql
CREATE TABLE `customer_statements` (
    `Id`                       char(36)        NOT NULL,
    `TenantId`                 char(36)        NOT NULL,
    `CustomerId`               char(36)        NOT NULL,
    `StatementNumber`          varchar(32)     NOT NULL,
    `TemplateId`               char(36)        NULL,
    `PeriodStart`              datetime(6)     NOT NULL,
    `PeriodEnd`                datetime(6)     NOT NULL,
    `GeneratedAtUtc`           datetime(6)     NOT NULL,
    `Status`                   varchar(16)     NOT NULL,
    `Currency`                 varchar(3)      NOT NULL,
    `OpeningBalance`           decimal(18,2)   NOT NULL,
    `ClosingBalance`           decimal(18,2)   NOT NULL,
    `OutstandingBalance`       decimal(18,2)   NOT NULL,
    `TotalInvoiced`            decimal(18,2)   NOT NULL,
    `TotalPaid`                decimal(18,2)   NOT NULL,
    `StatementSnapshotJson`    LONGTEXT        NOT NULL,
    `TemplateSnapshotJson`     LONGTEXT        NULL,
    `HtmlSnapshot`             LONGTEXT        NULL,
    `VoidedAtUtc`              datetime(6)     NULL,
    `VoidReason`               varchar(1000)   NULL,
    CONSTRAINT `PK_customer_statements` PRIMARY KEY (`Id`)
);

CREATE TABLE `statement_templates` (
    `Id`                          char(36)      NOT NULL,
    `TenantId`                    char(36)      NOT NULL,
    `Name`                        varchar(200)  NOT NULL,
    `Description`                 varchar(2000) NULL,
    `Status`                      varchar(16)   NOT NULL,
    `IsDefault`                   tinyint(1)    NOT NULL DEFAULT FALSE,
    -- branding / copy / display toggles / issuer fields elided
    `StatementNumberPrefix`       varchar(20)   NULL,
    `CreatedAtUtc`                datetime(6)   NOT NULL,
    `UpdatedAtUtc`                datetime(6)   NOT NULL,
    -- The DefaultScopeKey computed column is the heart of the
    -- one-default-per-tenant guarantee:
    `DefaultScopeKey`             varchar(36)   AS (
        CASE WHEN `IsDefault` = 1
             THEN CAST(`TenantId` AS CHAR(36))
             ELSE NULL END
    ) STORED NULL,
    CONSTRAINT `PK_statement_templates` PRIMARY KEY (`Id`)
);

-- Hot-path lookup indexes
CREATE INDEX  `IX_customer_statements_TemplateId`
    ON `customer_statements` (`TemplateId`);
CREATE INDEX  `IX_customer_statements_TenantId_CustomerId_GeneratedAtUtc`
    ON `customer_statements` (`TenantId`, `CustomerId`, `GeneratedAtUtc`);
CREATE INDEX  `IX_customer_statements_TenantId_GeneratedAtUtc`
    ON `customer_statements` (`TenantId`, `GeneratedAtUtc`);
CREATE INDEX  `IX_statement_templates_Status`
    ON `statement_templates` (`Status`);
CREATE INDEX  `IX_statement_templates_TenantId`
    ON `statement_templates` (`TenantId`);
CREATE INDEX  `IX_statement_templates_TenantId_IsDefault`
    ON `statement_templates` (`TenantId`, `IsDefault`);

-- Hard uniqueness guarantees
CREATE UNIQUE INDEX `UX_customer_statements_TenantId_StatementNumber`
    ON `customer_statements` (`TenantId`, `StatementNumber`);
CREATE UNIQUE INDEX `UX_statement_templates_DefaultScopeKey`
    ON `statement_templates` (`DefaultScopeKey`);
```

The `UX_statement_templates_DefaultScopeKey` index uses the
nullable computed column trick: only rows where
`IsDefault = 1` produce a non-NULL value, so MySQL enforces "at
most one default per tenant" without a partial-index feature.

The `UX_customer_statements_TenantId_StatementNumber` index is
the database-level safety net behind
`IStatementNumberGenerator` — even if two concurrent generators
land on the same number, the second `INSERT` fails and the
persistence service catches the unique-violation and retries
(see §6).

---

## 11. Endpoints (final HTTP surface)

All routes are tenant-scoped via `TenantResolutionMiddleware`
(`X-Tenant-Id` header). STAT-B01 routes are unchanged; STAT-B02
adds:

| Method | Route | Purpose | Success | Errors |
|--------|-------|---------|---------|--------|
| `POST` | `/api/statement-templates` | Create template | `201 Created` | `400` validation, `409` duplicate-default |
| `GET`  | `/api/statement-templates` | List templates in tenant scope | `200 OK` | — |
| `GET`  | `/api/statement-templates/default` | Get the active default template | `200 OK` / `404` | — |
| `GET`  | `/api/statement-templates/{id}` | Fetch one template | `200 OK` / `404` | `400` empty id |
| `PUT`  | `/api/statement-templates/{id}` | Patch fields | `200 OK` / `404` | `400`, `409` |
| `POST` | `/api/statement-templates/{id}/activate` | Draft → Active | `200 OK` / `404` | `400` invalid transition |
| `POST` | `/api/statement-templates/{id}/retire` | * → Retired (clears default) | `200 OK` / `404` | `400` |
| `POST` | `/api/statement-templates/{id}/make-default` | Promote (demotes prior) | `200 OK` / `404` | `400` retired, `409` duplicate |
| `POST` | `/api/statements/customers/{customerId}/generate` | Build + persist snapshot for an explicit period | `201 Created` | `400`, `404` customer, `409` number-conflict |
| `POST` | `/api/statements/customers/{customerId}/monthly/generate` | Same, year/month shortcut | `201 Created` | `400`, `404`, `409` |
| `GET`  | `/api/statements/customers/{customerId}/history` | List persisted snapshots for the customer | `200 OK` | `400` |
| `GET`  | `/api/statements/history/{id}` | Fetch one persisted snapshot (full body) | `200 OK` / `404` | `400` |
| `GET`  | `/api/statements/history/{id}/render/html` | Render HTML from snapshot (cached or rehydrated) | `200 OK` / `404` | `400` |
| `POST` | `/api/statements/history/{id}/void` | Idempotent soft-void | `200 OK` / `404` | `400` |

The `Location` header on the two `Created` responses points at
`/api/statements/history/{id}` so a client that just generated a
statement can re-fetch it without parsing the response body.

---

## 12. Test inventory

### 12.1 Domain unit tests (`TenantBilling.Domain.Tests`)

Three new test classes; everything else is preserved.

* `StatementTemplateServiceTests` (12 tests) — create defaults,
  auto-default-first-active, second-active-not-defaulted,
  explicit-default-requires-active, bad-accent-color,
  tenant isolation, make-default promotes / demotes,
  make-default rejects retired, retire clears default,
  update rejects retired, selection fallback to default and to
  null, selection rejects draft / unknown / cross-tenant.
* `StatementNumberGeneratorTests` (3 tests) — first-of-year,
  per-tenant per-year increment, empty tenant rejection.
* `CustomerStatementPersistenceServiceTests` (10 tests) —
  monthly persist + number assignment, retry on transient
  number conflict (`SimulateNumberConflictOnce`), default-template
  stamping, draft-template rejection, optional HTML capture,
  cross-tenant returns null, render prefers cached HTML, render
  rehydrates from JSON snapshot when no cached HTML, idempotent
  void (first reason and `VoidedAtUtc` win), cross-tenant void
  returns null.

### 12.2 Integration tests (`TenantBilling.Tests`)

* `StatementTemplatesApiTests` (8 tests) — header guard,
  ownership stamping + auto-default, cross-tenant 404 isolation
  for list/get/promote/retire, make-default + previous-id echo,
  partial PUT, retired update → 400, retired make-default → 400,
  bad accent → 400.
* `StatementsPersistenceApiTests` (9 tests) — monthly generate
  201 + number + Location header, generate with `RenderHtml`
  flag captures HTML and re-renders, draft template → 400,
  unknown / cross-tenant customer → 404, history list isolation,
  cross-tenant snapshot fetch → 404, lazy rehydrate when no
  cached HTML, idempotent void with cross-tenant 404, default
  template stamping.

All 17 STAT-B02 integration tests pass against the in-memory
`TenantBillingWebApplicationFactory`. All STAT-B01 tests
continue to pass unchanged.

### 12.3 Smoke check against the running workflow

`Tenant Billing API` workflow restarted on `:5001`. A direct
`curl` against `POST /api/statement-templates` with a synthetic
tenant id returns `201` with a normalized `accentColor`
(`#1F4FFF`) and `isDefault: true` (auto-promoted), confirming
the runtime DI graph wires the new service / repository /
generator end-to-end.

---

## 13. Concurrency story (recap)

1. **Number generation** — `IStatementNumberGenerator` issues
   `STMT-YYYY-NNNNNN` from
   `MAX(StatementNumber) WHERE TenantId=@t AND StatementNumber LIKE 'STMT-YYYY-%'`.
   Two writers can race here.
2. **Database safety net** — the unique index
   `UX_customer_statements_TenantId_StatementNumber` rejects the
   loser's INSERT.
3. **Service-level retry** — `CustomerStatementPersistenceService`
   wraps the build/insert in a loop bounded by
   `MaxNumberRetries = 5` and re-issues a fresh number on each
   `CustomerStatementNumberConflictException`.
4. **Default-template safety** — the computed-column unique
   index `UX_statement_templates_DefaultScopeKey` rejects any
   second `IsDefault = 1` row for the same tenant; the service
   demotes the prior default in the same UoW commit so legitimate
   promotions never trip it.

---

## 14. Snapshot immutability

A persisted `CustomerStatement` is **append-only**:

* `StatementSnapshotJson` (`LONGTEXT NOT NULL`) is the
  authoritative payload — set once at insert and never touched
  thereafter.
* `TemplateSnapshotJson` is captured alongside if a template
  was selected at generation time; it stores the *resolved*
  template document, not a foreign key to the live template.
* `HtmlSnapshot` is optional; it is either populated at insert
  (when `RenderHtml = true`) or filled in lazily on the first
  `GET .../render/html` and then frozen.
* `Status` only ever moves `Generated → Voided`. Voiding is
  idempotent: the first `VoidedAtUtc` and `VoidReason` win;
  subsequent calls return the existing values unchanged.
* No `UPDATE` path mutates monetary totals — a regenerate is a
  brand-new row with a brand-new number.

---

## 15. Out-of-scope reaffirmation

Explicitly **not** delivered in STAT-B02 (deferred per the
block's contract):

* Authentication / authorization on any of the new routes
  (still tenant-header only).
* Email delivery and PDF rendering.
* LegalSynq / e-signature integration.
* Any UI work — admin, portal, or canvas.
* Scheduled / background statement generation jobs.
* Payment-system changes (no new payment fields, no
  cross-talk with the payments domain beyond the existing
  STAT-B01 read path).
* Customer portal exposure of the persisted history.

These remain owned by their respective downstream blocks.
