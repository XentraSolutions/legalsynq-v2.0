# TB-MERGE-01 — Import Tenant Billing Source Into Commerce

**Status:** complete (with documented baseline failures)
**Date:** 2026-04-29

## 1. Summary

Source-code intake of the Tenant Billing service (`monksearch-billing-service`)
into the Commerce repository as a separate, self-contained bounded context at
`services/tenant-billing/`. No Commerce code was modified, no platform-billing /
tenant-billing entities were merged, no routes were renamed, no schema was
redesigned. Build is green; tests run with **9 known pre-existing failures**
honestly documented below.

## 2. Uploaded Source Archive Used

- **Path:** `attached_assets/monksearch-billing-service.tar_1778806793648.gz`
- **Size:** 354 451 bytes
- **Top-level extracted folder:** `billing/`
- **Inspection location:** `/tmp/tb-inspect/billing` (read-only)

Archive root:

```
Billing.sln
Directory.Build.props
Directory.Packages.props
Dockerfile
README.md
global.json
openapi/
scripts/
src/
tests/
```

## 3. Extracted Source Structure

### .NET projects

| Project | Path |
| --- | --- |
| Solution | `Billing.sln` |
| API | `src/Billing.Api/Billing.Api.csproj` |
| Domain | `src/Billing.Domain/Billing.Domain.csproj` |
| Infrastructure | `src/Billing.Infrastructure/Billing.Infrastructure.csproj` |
| Domain tests | `tests/Billing.Domain.Tests/Billing.Domain.Tests.csproj` |
| Integration tests | `tests/Billing.Tests/Billing.Tests.csproj` |

### Domain modules (`src/Billing.Domain/`)

`Accounting`, `Csv`, `Entities`, `Exceptions`, `Projections`, `Rendering`,
`Reporting`, `Repositories`, `Services`, `Statements`, `StatementTemplates`.
Total: **113 `.cs` files**.

### Infrastructure modules (`src/Billing.Infrastructure/`)

`Accounting`, `Data`, `Delivery`, `Reporting`, `Repositories`, `Seed`. **20 EF
Core migrations** under `Data/Migrations/` from `20260424153104_InitialCreate`
onward.

### API surface (`src/Billing.Api/`)

20 DTO/contract files, 11 controllers (customers, invoices, payments, refunds,
statements, invoice templates, statement templates, delivery analytics,
accounting export, QuickBooks customer mappings, ERP reconciliation, ERP
remediation, ERP governance analytics).
Subfolders: `Tenancy/`, `Hosting/`, `OpenApi/`, `Security/`.

### Tests

- `tests/Billing.Domain.Tests/` — 326 test methods (xUnit `[Fact]`/`[Theory]`)
  with `Fakes/` and `Helpers/` support folders.
- `tests/Billing.Tests/` — 137 test methods covering API + domain via
  `WebApplicationFactory`.

### Other artifacts

- `Dockerfile` (root)
- `openapi/` (with its own README)
- `scripts/generate-openapi.sh`
- `.config/dotnet-tools.json`
- `appsettings.json` and `appsettings.Development.json` (no real secrets)
- **No `package.json`, no front-end artifact in this archive.**

## 4. Existing Commerce Structure Inspected

- `services/Commerce/` — platform billing service (`Commerce.sln`,
  Domain/Application/Contracts/Infrastructure/Api/Tests). Owns platform
  billing accounts, catalog, subscriptions, platform invoices, payments,
  account standing, integrations, admin/operability. **Not modified.**
- `services/tenant-billing-api/` — older in-house tenant billing snapshot
  (assemblies named `TenantBilling.*`, 56 Domain `.cs` files, same
  migration history up to `20260424222018`). Currently bound to the
  running `Tenant Billing API` workflow. **Not modified, not deleted.**
- `services/Commerce/Commerce.sln` — does NOT include any Billing.* or
  TenantBilling.* projects. Confirmed safe — adding them would mix
  bounded contexts, so per the spec they are intentionally **not added**.

## 5. Target Placement Decision

**Chosen path:** `services/tenant-billing/` (Option C, sibling service).

**Rationale:**

1. The archive ships its own `Billing.sln`, `Directory.Build.props`,
   `Directory.Packages.props`, `Dockerfile`, `global.json`, etc. Nesting
   it inside `services/Commerce/src/` would either break those props files
   or force them to merge with Commerce's, exactly the cross-context
   coupling the spec forbids.
2. The repo already uses `services/<name>/` as the convention for bounded
   standalone .NET services (`services/Commerce/`,
   `services/tenant-billing-api/`).
3. Option B (`services/Commerce/modules/TenantBilling`) would imply that
   Tenant Billing is a sub-module of Commerce, which contradicts the
   "do not mix platform billing with tenant billing" rule.
4. The folder name `services/tenant-billing/` is distinct from the
   existing `services/tenant-billing-api/`, so neither is overwritten.

## 6. Files / Folders Imported

Entire archive root copied byte-for-byte into `services/tenant-billing/`:

- 296 files / 2.6 MB on disk
- All `.cs`, `.csproj`, `.sln`, `.json`, `.md`, EF migrations, OpenAPI
  artifacts, Dockerfile, scripts and `.config/dotnet-tools.json`

Nothing was renamed and nothing was deleted. The legacy
`services/tenant-billing-api/` was left exactly as it was.

## 7. Solution / Project Changes

| Solution | Touched? | Notes |
| --- | --- | --- |
| `services/tenant-billing/Billing.sln` | imported as-is | references the 5 imported projects |
| `services/Commerce/Commerce.sln` | **not touched** | adding Billing.* would mix bounded contexts |
| `services/tenant-billing-api/TenantBilling.sln` | **not touched** | unrelated service |

## 8. Dependency Changes

Three minimal package fixes were required to make the imported source
compile. Each is the *smallest safe* intervention; none are business
logic changes.

| File | Change | Reason |
| --- | --- | --- |
| `services/tenant-billing/Directory.Packages.props` | added `<PackageVersion>` for `Microsoft.Extensions.Http` 8.0.0 and `Microsoft.Extensions.Options.ConfigurationExtensions` 8.0.0 | `Billing.Infrastructure.csproj` references both, but the archive's central package list omitted them — `dotnet restore` failed with `NU1010` |
| `services/tenant-billing/src/Billing.Domain/Billing.Domain.csproj` | added `<PackageReference>` for `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.Options.ConfigurationExtensions` | `AccountingExportService.cs`, `NoOpStatementDeliveryProvider.cs`, `StatementDeliveryService.cs` use `ILogger<>` and `IOptionsMonitor<>` directly; the csproj had no package refs at all |
| `services/tenant-billing/tests/Billing.Domain.Tests/Billing.Domain.Tests.csproj` | added `<PackageReference Include="FluentAssertions" />` | the test files use `Should().Be(...)` etc. via FluentAssertions, but the csproj only referenced xUnit — `CS0246: FluentAssertions could not be found` |

No package versions were upgraded. All new versions match what is already
pinned in `services/tenant-billing/Directory.Packages.props`.

## 9. Configuration Changes

None at the runtime/configuration level.

`appsettings.json` and `appsettings.Development.json` were imported
verbatim. **There are no real secrets in either file** — all sensitive
fields (`Billing:Delivery:Ncm:ApiKey`, `Billing:Erp:QuickBooks:ClientSecret`,
`Billing:Erp:QuickBooks:RefreshToken`, etc.) ship with empty-string
placeholders accompanied by inline `"// …"` comments instructing operators
to source the real values from the platform secret store.

The Development file contains a single dev-only fallback
`Billing:InternalToken = "dev-only-change-me"` which is explicitly labeled
"NEVER use these in production" by the archive author.

## 10. Frontend / Admin Artifact Handling

The archive contains no `package.json` and no front-end artifact. The
existing `artifacts/tenant-billing-admin/` was **not modified**. No
front-end work was performed.

## 11. Source-only fixes applied to enable a build baseline

Two non-package, source-level edits were required because the archive
itself was inconsistent. Both are documented inline in the touched files
with `// TB-MERGE-01 import fix:` comments and neither changes domain
behaviour:

| File | Edit | Reason |
| --- | --- | --- |
| `src/Billing.Domain/Accounting/Erp/AccountingExportModels.cs` (line 224) | `public sealed record AccountingExportRunRequest(...)` → `public record AccountingExportRunRequest(...)` | The archive ships `TenantScopedAccountingExportRunRequest : AccountingExportRunRequest` in `AccountingExportService.cs:401`, which fails with `CS0509` against the original `sealed` base. Removing `sealed` is a one-keyword unblock. |
| `tests/Billing.Domain.Tests/InvoiceTransitionTests.cs` (line 110) | dropped `UpdatedAt = DateTime.UtcNow,` from a `Payment {…}` initializer | The archive's `Payment` entity has no `UpdatedAt` property; the test was written against a different shape. `CS0117`. |

## 12. Build / Test Commands Run

All commands run from `services/tenant-billing/`:

```
dotnet --info
dotnet restore Billing.sln
dotnet build Billing.sln -c Debug
dotnet test tests/Billing.Domain.Tests/Billing.Domain.Tests.csproj --no-build
dotnet test tests/Billing.Tests/Billing.Tests.csproj --no-build
```

Per the bounded-context rule, the imported projects were **not** added to
`services/Commerce/Commerce.sln`, so no `dotnet build Commerce.sln` runs
were required for the imported code (and Commerce's own builds are
unaffected by this merge).

## 13. Validation Results

### Build

| Step | Result |
| --- | --- |
| `dotnet --info` | OK — SDK 8.0.416 |
| `dotnet restore Billing.sln` | OK after section 8 fixes (NU1010 resolved) |
| `dotnet build Billing.sln -c Debug` | **Build succeeded — 0 errors, 2 warnings** (xUnit2031, xUnit1031 — pre-existing test analyzer warnings) |

### Tests

| Suite | Total | Passed | Failed | Skipped |
| --- | --- | --- | --- | --- |
| `Billing.Domain.Tests` | 446 | **443** | 3 | 0 |
| `Billing.Tests` | 137 | **131** | 6 | 0 |
| **Combined** | **583** | **574 (98.5 %)** | **9** | 0 |

#### Failing tests — pre-existing in the archive, NOT caused by import

**Domain (3) — `InvoiceTransitionTests`:** all three assert that
`result!.PreviousStatus` equals the *source* state, but the implementation
returns the *target* state. Same root cause for all three:

- `Draft_to_Issued_dispatches_to_IssueAsync_and_returns_previous_status`
  — expected `"Draft"`, got `"Issued"`
- `Issued_to_Voided_dispatches_to_VoidAsync` — expected `"Issued"`,
  got `"Voided"`
- `Issued_to_Overdue_when_due_date_passed` — expected `"Issued"`, got
  `"Overdue"`

**Integration (6) — `InvoiceTemplatesConflictMappingApiTests`:** all six
expect HTTP 409 but receive HTTP 401 Unauthorized, indicating the archive's
test host now requires a security header these particular tests don't supply:

- `CreateTenant_DefaultConflict_Returns409`
- `CreatePlatform_DefaultConflict_Returns409`
- `MakeDefaultTenant_DefaultConflict_Returns409`
- `MakeDefaultPlatform_DefaultConflict_Returns409`
- `UpdateTenant_DefaultConflict_Returns409`
- `UpdatePlatform_DefaultConflict_Returns409`

These are honest failures inherited from the archive. Per spec
("must NOT change business logic yet" / "do not hide the failure")
they were left as-is and are flagged for a later block.

## 14. Conflicts or Risks Found

1. **Archive ships a CS0509 build-blocker** (sealed base + derived record).
   Mitigated with the one-keyword fix in section 11. Risk: future archive
   re-imports may overwrite the fix; future blocks should either patch
   upstream or apply the same edit on import.
2. **Archive ships a CS0117 test compile error** (test references a
   non-existent `Payment.UpdatedAt`). Mitigated with the test-only edit
   in section 11. Same re-import risk as above.
3. **Archive's `Directory.Packages.props` is incomplete** (missing 2
   versions referenced by `Billing.Infrastructure.csproj`). Mitigated in
   section 8.
4. **Archive's test projects do not declare FluentAssertions** even
   though the test code uses it. Mitigated in section 8.
5. **Pre-existing failing tests** (3 domain + 6 integration). Not
   addressed — flagged for a later block.
6. **Duplicate-ish service alongside `services/tenant-billing-api/`.**
   The two services overlap in scope but use different assembly names
   (`Billing.*` vs `TenantBilling.*`), have different feature surfaces
   (the new one adds Accounting/Csv/Projections/Reporting/ERP/QuickBooks),
   and share migration timestamps up to `20260424222018`. There is no
   port collision because only `services/tenant-billing-api/` is wired
   to a workflow; the new `services/tenant-billing/` is **not started
   automatically**. Reconciliation between the two belongs to a later
   block.
7. **No production secrets present** — the archive ships placeholder
   strings + inline operator instructions only. No secret was committed.

## 15. Confirmation of Strict Exclusions

None of the following were performed in this block:

- ❌ Commerce `BillingAccount` mapping
- ❌ `TenantBillingProfile` mapping
- ❌ API route namespace migration
- ❌ LegalSynq Identity integration
- ❌ JWT integration
- ❌ Control Center integration
- ❌ Tenant Portal integration
- ❌ UI merge into Commerce admin (no UI artifact in archive; existing
  `artifacts/tenant-billing-admin/` untouched)
- ❌ Payment provider changes
- ❌ Database model redesign
- ❌ Invoice/payment table consolidation
- ❌ ERP behaviour changes (the archive's ERP code was imported
  unchanged, no behaviour was added or modified)
- ❌ Notifications integration
- ❌ Documents/PDF storage integration

The imported projects were also intentionally **not added** to
`services/Commerce/Commerce.sln` to keep the bounded contexts separate,
and the new `services/tenant-billing/` is not wired to any workflow yet.

## 16. Recommended Next Block

The most natural next block is **TB-MERGE-02 — Reconcile the two
tenant-billing services**. Specifically:

1. Decide which of `services/tenant-billing/` (`Billing.*`, the imported
   superset) and `services/tenant-billing-api/` (`TenantBilling.*`, the
   in-house subset currently bound to the workflow) is canonical.
2. Migrate the canonical workflow to the chosen service (port, env vars,
   `Tenant Billing API` workflow command).
3. Delete the loser only after demonstrating no behaviour regressions.
4. Address the 9 pre-existing test failures inherited from the archive
   (3 `InvoiceTransitionTests` PreviousStatus bugs + 6
   `InvoiceTemplatesConflictMappingApiTests` 401-vs-409 auth-header
   gaps).

Subsequent blocks (per the original plan) can then proceed to
`Commerce.BillingAccount ↔ TenantBillingProfile` mapping, route namespace
migration, and the LegalSynq / Control Center / Tenant Portal
integrations.
