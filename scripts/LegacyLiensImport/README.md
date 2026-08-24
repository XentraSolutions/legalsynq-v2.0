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
3. Apply Liens migrations, including `20260726000001_AddLegacyImportControlPlane`,
   `20260727000001_AddLegacyImportApprovals`, and
   `20260731000001_AddLienPurchaseAndSettlementDates`.
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

## Program 1 law-firm repair

If the reviewed core import was run before it mapped `SL_CASE.CASE_LAW_FIRM`,
run [`backfill-sl-core-case-law-firms.sql`](backfill-sl-core-case-law-firms.sql)
only after the completed Program 1 contacts/facilities import. It uses the
existing `SL_CASE` and `SL_CONTACT` crosswalks to restore the target contact
UUID as `lawFirmId` in each target case's legacy metadata. It does not change
`OrgId`: organization ownership is not a law-firm relationship.

Run the whole script through **Execute SQL Script** (DBeaver Alt+X), then run
its preflight call with `p_apply = '0'` and `p_expected_updates = -1`. Review
the returned `CasesNeedingUpdate` and use that exact value for the apply call.
The repair is deliberately blocked by a missing/invalid crosswalk, a target
contact that is not a `LawFirm`, conflicting metadata, or a Notes overflow.
`CasesWithoutLegacyLawFirm` is reported but not guessed; resolve those cases
separately if it is nonzero.

## Program 1 lien medical-provider repair

Use [`backfill-sl-core-lien-medical-providers.sql`](backfill-sl-core-lien-medical-providers.sql)
after the Program 1 contacts/facilities import to restore `medicalProviderId`
metadata on a `LegacyMedicalFacilityInfo` servicing item for each imported
medical lien. It maps each non-deleted medical lien's
`SL_LEINS_MEDICAL_INFORMATION_FACILITY.LMI_MEDICAL_PROVIDER` value through the
existing active Provider contact and its `SL_CONTACT` crosswalk. A case may
have multiple providers across its liens; only multiple providers for one lien,
a missing or inactive source Provider, an invalid target crosswalk, conflicting
metadata, or a Notes overflow blocks the repair.

Run the complete file through **Execute SQL Script** (DBeaver Alt+X), then use
`CALL liens_backfill_sl_core_lien_medical_providers('<tenant-guid>', -1, '0');`
for preflight. It reports any blocking `Resolution` values without changing
data. The apply writes only the validated rows, leaving conflicts untouched and
reporting them as `UnresolvedConflicts`. Use the exact `ChangesToApply` count
with `p_apply = '1'` for the apply call, then reconcile the reported conflicts
separately.

## Program 1 facility-contact repair

The Contacts page lists `MedicalFacility` contacts, while the legacy facility
import stores `SL_FACILITY` rows in `liens_Facilities`. Use
[`backfill-sl-core-facility-contacts.sql`](backfill-sl-core-facility-contacts.sql)
to create the missing, active main `MedicalFacility` contact for each imported
facility. Each created contact is linked through `FacilityId`; facility staff
remain separate sub-contacts.

Open an **LS_QA_LIENS** connection in DBeaver before deploying or calling this
script; it deliberately has no `USE` statement. Run the complete file through
**Execute SQL Script** (DBeaver Alt+X), then run
`CALL liens_backfill_sl_core_facility_contacts('<tenant-guid>', -1, '0');`.
Resolve any returned conflicts, then rerun it with the exact `ContactsToInsert`
value and `p_apply = '1'.` Production execution requires explicit approval and
an intentionally selected `LS_LIENS` connection.

## Program 1 case-manager, accident-type, status-label, and facility repair

Use [`backfill-sl-core-case-relationships.sql`](backfill-sl-core-case-relationships.sql)
after both the Program 1 core import and the Program 1 contacts/facilities
import have completed for the same source fingerprint. It repairs only omitted
relationships and legacy case-status metadata:

- `SL_CASE.CASE_MANAGER` through its `SL_CONTACT` crosswalk to `caseManagerId`
  metadata on the target case.
- `SL_CASE.CASE_ACCIDENT_TYPE` through `SL_ACCIDENT_TYPE` to the matching
  system `liens_LookupValues` UUID plus the target `accidentType` name.
- `SL_CASE.CASE_STATUS` to `statusLabel` metadata for `New`, `Processing`, and
  `Litigation` rows whose target case remains in the corresponding collapsed
  canonical status (`PreDemand` or `InNegotiation`).
- `SL_LEINS_MEDICAL_INFORMATION_FACILITY` to a blank target lien `FacilityId`.

The facility relationship is lien-level, not a column on `liens_Cases`. The
script never creates contacts, facilities, lookups, or crosswalks; a missing or
ambiguous mapping blocks the entire apply. It also refuses to overwrite an
existing different case-manager, accident-type, case-status label, or facility
assignment. Cases whose status changed after import retain their current status
and are not given the historical label.

You may use DBeaver to inspect the SQL file in dry-run mode (`@apply = 0`),
but use the checked-in .NET runner for every apply. It owns the transaction and
rolls it back on a script error, cancellation, an unexpected updated-row count,
or a failed postcondition—none of those conditions can leave a partial repair.
Before it stages data, the runner acquires the core-import lock and then the
contacts-import lock for the tenant, holding both through commit or rollback so
an import or another relationship repair cannot change its mappings mid-run.

Run the dry run from the repository root:

```powershell
dotnet run --project scripts/LegacyLiensImport -- `
  --backfill-case-relationships `
  --tenant-id 019f6ae6-4348-784a-aae0-f4d636f843ad `
  --target-connection '<LS_QA_LIENS connection string>'
```

Copy the exact `Case rows to update` and `Lien facility rows to update` output,
then run the apply:

```powershell
dotnet run --project scripts/LegacyLiensImport -- `
  --backfill-case-relationships `
  --tenant-id 019f6ae6-4348-784a-aae0-f4d636f843ad `
  --target-connection '<LS_QA_LIENS connection string>' `
  --expected-case-updates <dry-run-case-count> `
  --expected-lien-facility-updates <dry-run-lien-facility-count> `
  --apply
```

The target connection must select `LS_QA_LIENS` (or the explicitly approved
`LS_LIENS` production schema), and `SL-CORE` must be on the same MySQL server.
The runner refuses an apply when the counts differ or any preflight/conflict
check fails.

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

For the complete Program import used by the dashboard reconciliation, deploy
[`import-sl-core-complete.sql`](import-sl-core-complete.sql), then run:

```sql
CALL liens_migrate_sl_core_complete('<tenant-guid>', '0'); -- preflight
CALL liens_migrate_sl_core_complete('<tenant-guid>', '1'); -- apply
```

The complete procedure maps `LM_PURCHASE_DATE` to `liens_Liens.PurchaseDate`,
maps nonblank `SLS_SETTLE_AMOUNT` and `SLS_SETTLE_DATE` rows to
`liens_LienSettlements`, and retains non-deleted settlement payment details.
Case-note staging carries `SL_CASE_NOTES.CN_USER_ID`: a null value maps to the
tracking category `general`, while a non-null value maps to `feed`. The note
crosswalk hash is prefixed with `case-note-v2:` and includes that discriminator
and the approved source fingerprint. The .NET runner, canonical complete SQL,
tenant-only SQL, and hard-bound rehearsal SQL all follow this rule.
It excludes `CASE_IS_DELETED = 'Y'`, `LM_IS_DELETED = 'Y'`, and
`SLSPD_IS_DELETED = 'Y'`; medical-code amounts and servicing rows require the
legacy active status `LMC_STATUS = 'A'`, matching the dashboard calculations.
Its temporary staging tables are indexed before the high-volume joins. Apply
remains single-use and tenant-scoped through the approval and crosswalk guards.
The preflight result includes case/lien/settlement counts plus the all-time
purchase, billing, and cash-received totals that the migrated dashboard should
reconcile against.

### Existing-import case-note reconciliation

Imports completed before the v2 discriminator was added stored every case note
as `general`. The Case Notes History API intentionally returns
`409 legacy_history_not_reconciled` for a tenant that still has an unversioned
`SL_CASE_NOTES` crosswalk hash. With the same immutable `SL-CORE` restore used
by the approved import available, deploy
[`reconcile-sl-core-case-note-categories.sql`](reconcile-sl-core-case-note-categories.sql)
and run preflight before apply:

```sql
CALL liens_reconcile_sl_core_case_note_categories(
  '<tenant-guid>', '<approved-source-fingerprint>', '<approval-reference>',
  NULL, NULL, '0');
CALL liens_reconcile_sl_core_case_note_categories(
  '<tenant-guid>', '<approved-source-fingerprint>', '<approval-reference>',
  <preflight-eligible-notes>, '<preflight-checksum>', '1');
```

Retain the preflight `EligibleNotes` and `ExpectedChecksum` with the release
record; apply requires both values and stops if the snapshot has changed. The
procedure accepts either a consumed SQL-import approval or the signed-manifest
evidence recorded by the supported .NET importer, plus exactly one completed
note-owning import run and the immutable source-provenance receipt. It verifies
exact note and case crosswalk ownership, tenant ownership on both target rows,
source content and deletion state, unedited target notes, and an allowed
current category. It shares the importer's tenant lock, locks and stages inside
one transaction, guards writes against concurrent changes, updates both
`Category` and the versioned `SourceHash`, verifies complete left-join and
row-count postconditions, and is safe to rerun after a new preflight.

`import-sl-core-complete.sql` is the supported complete SQL runner. Files named
`import-sl-core-complete - Copy*.sql` are archival working copies and must not
be deployed or used for a new import.

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
