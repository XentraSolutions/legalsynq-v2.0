# SL-CORE Core Liens Import

`LegacyLiensImport` is a tenant-scoped, dry-run-first migration runner for a
restored `SL-CORE` MySQL staging database. It only imports the approved core
scope: cases, medical-lien headers, and case notes.

It does **not** execute the legacy dump, create tenants/organizations/users,
or import documents, detailed medical charges/facility links, settlement data,
contacts, or workflow state. Those source areas need their separate approved
target mappings and service-owned import paths.

## Preconditions

1. Restore the dump into an isolated, access-controlled staging database. Do
   not run `dump-SL-CORE-202607262247.sql` against a LegalSynq service database.
   The controlled restore process—not the importer—must atomically write the
   current restore receipt below after a successful restore. The importer
   database principal must have read-only access to this table and the staging
   database; it must not be able to change the receipt.
2. Obtain an Identity-issued tenant mapping manifest that authorizes the exact
   tenant, owning organization, migration actor, and legacy program. The
   Identity signing certificate must be installed in the protected
   `LocalMachine\TrustedPeople` certificate store with subject
   `CN=LegalSynq Identity Migration Signing`. The runner verifies the detached
   Base64 RSA-SHA256 (PKCS#1 v1.5) signature before any `--apply` write, then
   binds the approved organization, mapping version, approval reference, and
   manifest hash to the import run.
3. Apply Liens migrations, including `20260726000001_AddLegacyImportControlPlane`
   and `20260727000001_AddLegacyImportApprovals`.
4. Set connection strings outside source control:

```powershell
$env:LegacySlCoreConnectionString = 'Server=localhost;Database=sl_core_staging;User ID=...;Password=...'
$env:ConnectionStrings__LiensDb = 'Server=localhost;Database=liens_db;User ID=...;Password=...'
```

The restore receipt table is intentionally in the staging database, outside the
legacy dump. It binds the approved snapshot to the database that the importer
will query:

```sql
CREATE TABLE SL_MIGRATION_SOURCE_PROVENANCE (
  PROVENANCE_KEY varchar(50) NOT NULL,
  SOURCE_FINGERPRINT char(64) NOT NULL,
  IMPORT_SCOPE varchar(100) NOT NULL,
  RESTORE_REFERENCE varchar(200) NOT NULL,
  RESTORED_AT_UTC datetime(6) NOT NULL,
  PRIMARY KEY (PROVENANCE_KEY)
);

INSERT INTO SL_MIGRATION_SOURCE_PROVENANCE
  (PROVENANCE_KEY, SOURCE_FINGERPRINT, IMPORT_SCOPE, RESTORE_REFERENCE, RESTORED_AT_UTC)
VALUES
  ('sl-core-current', '<sha256-of-restored-dump>', 'sl-core-core-liens-v1', '<restore-change-id>', UTC_TIMESTAMP(6));
```

The restore process must replace this one current row only as part of an
approved restore workflow. The tool checks its fingerprint and scope before it
reads source business rows.

## Run

Run preflight first. `TenantId`, `OrgId`, `MigrationUserId`, and the source
program are intentionally separate so the runner never guesses tenant or
organization ownership.

```powershell
dotnet run --project scripts/LegacyLiensImport -- `
  --tenant-id <tenant-guid> `
  --org-id <owning-org-guid> `
  --migration-user-id <identity-user-guid> `
  --legacy-program 1 `
  --source-dump C:\Users\Serrano\Downloads\dump-SL-CORE-202607262247.sql
```

After an approved preflight, add `--apply` and explicitly choose whether a
lien's `OriginalAmount` / `CurrentBalance` comes from summed legacy billing or
purchase amounts. An Identity owner must first produce the signed mapping
manifest and signature. The manifest contains only this release metadata:

```json
{
  "tenantId": "<tenant-guid>",
  "orgId": "<owning-org-guid>",
  "migrationUserId": "<identity-user-guid>",
  "legacyProgram": 1,
  "mappingVersion": "sl-core-core-v1",
  "approvalReference": "<change-or-approval-id>",
  "sourceFingerprint": "<sha256-of-the-approved-dump>",
  "importScope": "sl-core-core-liens-v1"
}
```

The manifest fields must exactly match the CLI arguments and the local SHA-256
of `--source-dump`. `--apply` therefore requires `--source-dump`; the
`--source-fingerprint` shortcut is only for dry-run discovery.

```powershell
dotnet run --project scripts/LegacyLiensImport -- `
  --tenant-id <tenant-guid> `
  --org-id <owning-org-guid> `
  --migration-user-id <identity-user-guid> `
  --legacy-program 1 `
  --source-dump C:\Users\Serrano\Downloads\dump-SL-CORE-202607262247.sql `
  --mapping-manifest C:\secure\sl-core-program-1.mapping.json `
  --mapping-manifest-signature C:\secure\sl-core-program-1.mapping.sig `
  --lien-amount-source billing `
  --apply
```

The default case/lien-number collision policy is `fail`. It must remain the
default for a production migration. If a business owner explicitly approves
preserving duplicate source numbers by suffixing their legacy primary key, add
both `--case-number-collision suffix-legacy-id` and
`--lien-number-collision suffix-legacy-id`.

## Safety and idempotency

The Liens migration ledger stores an import run, a SHA-256 fingerprint of the
source dump (or explicit fingerprint), and source-ID-to-target-GUID crosswalks.
Rerunning the exact source is idempotent. A changed row with the same legacy ID
is rejected: this runner has no delta-import policy and must not silently
overwrite LegalSynq data. Before relying on a crosswalk, the runner verifies
that its target row still exists in the same tenant and is the expected entity;
it also verifies that case, lien, and note ownership belongs to the approved
organization (notes are checked through their owning case). Stale, malformed,
or cross-organization crosswalks block the run and require an audited repair.

Only `Draft`, `Open`, and `Active` lien lifecycle values are supported by this
core header importer. Sales, offers, settlements, disputes, cancellations, and
other lifecycle states are blocked because their required relationships and
financial transitions are outside this scope. Negative, out-of-range, or
unparseable monetary values also block the run; no credit/reversal mapping is
implemented here.

The ledger records hashes and approved mapping evidence only; exception storage
is reserved for future redacted, structured exception handling and is not
written by this all-or-nothing runner. It must not be removed by an EF rollback:
use an audited compensation or database restore procedure. Keep raw source data
and document bytes in protected staging, not in migration logs.

## Tenant-bound SQL runner

For a MySQL-only rehearsal or controlled one-time import, use
[`import-sl-core-core-to-019ea7f6-21e9-7421-ab54-7846cdc6bc76.sql`](import-sl-core-core-to-019ea7f6-21e9-7421-ab54-7846cdc6bc76.sql).
It is hard-bound to both supplied target IDs:

- Tenant: `019ea7f6-21e9-7421-ab54-7846cdc6bc76`
- Organization: `019ea7f6-21e9-7421-ab54-7846cdc6bc76`

The script is intentionally not a general-purpose bulk loader. It requires a
single trusted `liens_LegacyImportApprovals` row created by an Identity-owned
release process. That row binds the tenant, organization, migration actor,
legacy program, dump fingerprint, billing/purchase policy, signed mapping
evidence, and the approved mappings for legacy lien statuses `1` and `2`.
The runner account must not be allowed to create or alter approvals. The source
dump fingerprint is
`3adccecf8a38114a14cd500240aab2a4db3d9bf45f00945c659dc3b5252663fe`.

Run it only when the target LiensDb and the controlled `SL-CORE` staging schema
are on the same MySQL server; connect with the target LiensDb selected. The
script starts in `@apply = 0` preflight mode. Set its `@approval_id` to the
approved row and change `@apply` to `1` only after the output is accepted. An
apply consumes the approval in the same transaction as the target rows,
control-plane run, and crosswalks. It refuses a second run for the tenant
rather than trying to overwrite or merge records, and blocks unapproved status,
amount, date, and number-collision mappings.
