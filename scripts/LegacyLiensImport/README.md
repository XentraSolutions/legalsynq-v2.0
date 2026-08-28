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
   `20260731000001_AddLienPurchaseAndSettlementDates`. For the report-parity
   import and repair described below, also apply
   `20260825160000_AddLegacyReportParityFields`.
   `20260825180000_AddLienImportedCreatedByName` is also required to retain
   `SL_LEINS_MEDICAL.LM_CREATE_BY` as legacy text. Legacy creator text is never
   treated as a V2 user ID.
   When the normal EF migration path is unavailable, run the idempotent
   [`apply-v3-report-parity-schema.sql`](apply-v3-report-parity-schema.sql)
   against the selected `LS_QA_LIENS` or `LS_LIENS` schema first. It applies
   the schema only and intentionally leaves EF migration history for the normal
   application deployment to record.
4. Set connection strings outside source control:

```powershell
$env:LegacySlCoreConnectionString = 'Server=localhost;Database=sl_core_staging;User ID=...;Password=...'
$env:ConnectionStrings__LiensDb = 'Server=localhost;Database=liens_db;User ID=...;Password=...'
```

## Restore legacy case and lien creators

For an SL-CORE core import completed before creator names were retained, run
[`backfill-sl-core-imported-creators.sql`](backfill-sl-core-imported-creators.sql)
after applying `20260825180000_AddLienImportedCreatedByName`. It maps only
through the completed core-import crosswalks, fills blank target values only,
and stops if an existing nonblank target value conflicts with the source.
Legacy rows without a core-import crosswalk were never migrated to V2 and are
outside this backfill's scope.

Run the file with **Execute SQL Script** in DBeaver, then dry run:

```sql
CALL liens_backfill_sl_core_imported_creators('<tenant-guid>', -1, '0');
```

Review `ChangesToApply` and `Conflicts`. Apply only with that exact count:

```sql
CALL liens_backfill_sl_core_imported_creators('<tenant-guid>', <ChangesToApply>, '1');
```

## Restore state of incident

For an SL-CORE core import completed before `SL_CASE.CASE_ACCIDENT_STATE` was
copied to the typed V2 case field, run
[`backfill-sl-core-incident-state.sql`](backfill-sl-core-incident-state.sql).
It updates only blank `liens_Cases.IncidentState` values. A populated different
V2 value is a conflict and blocks the complete apply; it is never overwritten.
Legacy rows without a core-import case crosswalk were never migrated into V2 and
are outside this backfill's scope.

Run the complete file in DBeaver first, then dry-run:

```sql
CALL liens_backfill_sl_core_incident_state('<tenant-guid>', -1, '0');
```

Resolve any conflicts. Re-run the dry run immediately before applying, then
copy its exact `ChangesToApply` count into:

```sql
CALL liens_backfill_sl_core_incident_state('<tenant-guid>', <ChangesToApply>, '1');
```

## Restore plaintiff address and law-firm email

[`backfill-sl-core-plaintiff-address-and-lawfirm-email.sql`](backfill-sl-core-plaintiff-address-and-lawfirm-email.sql)
maps legacy plaintiff address, city, state, and ZIP values to both the typed
V3 case columns and the legacy-compatible full case-address field, and
law-firm email to the linked canonical contact. It fills blank values only and
blocks a conflicting existing V3 value.

```sql
CALL liens_backfill_sl_core_plaintiff_address_and_lawfirm_email('<tenant-guid>', -1, '0');
CALL liens_backfill_sl_core_plaintiff_address_and_lawfirm_email('<tenant-guid>', <ChangesToApply>, '1');
```

## Restore case phone, email, and sex

[`backfill-sl-core-case-contact-and-sex.sql`](backfill-sl-core-case-contact-and-sex.sql)
maps legacy case phone and email to their V3 case columns and maps legacy gender
to the case's `gender` metadata. It fills blank values only and blocks conflicts.

```sql
CALL liens_backfill_sl_core_case_contact_and_sex('<tenant-guid>', -1, '0');
CALL liens_backfill_sl_core_case_contact_and_sex('<tenant-guid>', <ChangesToApply>, '1');
```

## Restore report party and medical-facility details

[`backfill-sl-core-report-party-and-facility-details.sql`](backfill-sl-core-report-party-and-facility-details.sql)
restores the crosswalk-bound case-manager, law-firm, and lien-facility details
used by reports. It fills only blank V3 fields and refuses conflicting values.

```sql
CALL liens_backfill_sl_core_report_party_and_facility_details('<tenant-guid>', -1, '0');
CALL liens_backfill_sl_core_report_party_and_facility_details('<tenant-guid>', <ChangesToApply>, '1');
```

## Restore legacy manual medical-code entries

[`backfill-sl-core-medical-code-amounts.sql`](backfill-sl-core-medical-code-amounts.sql)
transfers each active `SL_LEINS_MEDICAL_CODE` row into its linked V3 lien as a
deterministic `LegacyMedicalCode` servicing item. This preserves each code and
its Medicare, billing, and purchase amounts; it does not populate the separate
tenant-wide manual-code catalogue. It only inserts missing source-bound rows and
blocks conflicting or manually-created legacy-code tasks.

Run the complete file in DBeaver, then dry-run:

```sql
CALL liens_backfill_sl_core_medical_code_amounts('<tenant-guid>', -1, '0');
```

Apply only when `Conflicts = 0`, using the exact `TasksToInsert` count:

```sql
CALL liens_backfill_sl_core_medical_code_amounts('<tenant-guid>', <TasksToInsert>, '1');
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

Never reuse a receipt after the dump file changes. Recompute the dump SHA-256,
restore that exact file into controlled staging, and atomically replace the
receipt through the approved restore process before preflight. A dump whose
current hash differs from its recorded receipt is not eligible for apply.

## Program 1 contacts and facilities import

[`import-sl-core-contacts-facilities-tenant-only.sql`](import-sl-core-contacts-facilities-tenant-only.sql)
uses mapping version `sl-core-contact-facility-v3`. Its procedure requires the
tenant, the lowercase SHA-256 of the approved signed mapping manifest, and the
apply flag:

```sql
CALL liens_import_sl_core_contacts_facilities_tenant_only(
  '<tenant-guid>', '<approved-manifest-sha256>', '0');
```

Version 3 maps law firms, providers, and other organizations from `SL_CONTACT`,
but maps the people referenced by `SL_CASE.CASE_MANAGER` and
`SL_CASE.CASE_ATTORNEY` from `SL_CASE_MANAGER`. Their owning law firm comes from
`SL_CASE_MANAGER.CM_LAWFIRM`. When that reference is not an eligible Program 1
law firm, version 3 may fall back to `SL_CASE.CASE_LAW_FIRM` only if every
active Program 1 case referencing that person agrees on exactly one eligible
law firm. Missing, invalid, or ambiguous case-derived parents still block the
wave. The case person's crosswalk source table is `SL_CASE_MANAGER`; do not
substitute an `SL_CONTACT` crosswalk for these people. Review the dry-run counts
and conflicts before repeating the exact call with `p_apply = '1'` under the
approved change record.

Version 3 also contains one source-fingerprint-bound identity resolution:
`SL_CASE_MANAGER:792` is an approved alias of canonical record
`SL_CASE_MANAGER:602`. The importer creates or reuses one V3 contact using
record 602's populated values, while retaining separate crosswalks and source
hashes for both legacy IDs so all six case-manager relationships remain
traceable. The preflight must report exactly one `MergedContactAliasRows` for
this source snapshot. Every other duplicate natural key still blocks with
`LSLTC-011`, and existing crosswalks for 602 and 792 must not point to different
target contacts.
If either approved alias already has a crosswalk from an older contact-import
run, v3 blocks with `LSLTC-032`; do not repoint it automatically. A completed
v3 run must retain both alias crosswalks, and every existing crosswalk must
resolve to a real contact owned by the same tenant and organization.

For a tenant that already completed an earlier contact wave, v3 revalidates and
reuses its immutable `SL_CONTACT`, facility, facility-person, and lien-facility
crosswalks only when their completed run has the same tenant, organization, and
source fingerprint. The new `SL_CASE_MANAGER` rows and crosswalks belong to the
v3 run. The supplied manifest hash must exactly match the completed core import;
an arbitrary well-formed SHA-256 is rejected.

Lien-facility crosswalks have two reviewed historical hash contracts. The
complete v2 importer hashed the five fields that define the lien/facility
relationship, while the tenant-only importer also included facility-contact,
email, phone, and provider evidence. Version 3 accepts either exact hash only
for completed v1/v2 contact runs; a v3 crosswalk must match the expanded current
hash. The target lien, facility assignment, tenant, organization, source
fingerprint, entity type, and completed-run checks remain mandatory. Existing
crosswalk hashes and run IDs are never rewritten. Review
`LegacyLienFacilityHashMatches` in the preflight and summary before apply.

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

## Program 1 case relationship and report-parity repair

Use [`backfill-sl-core-case-relationships.sql`](backfill-sl-core-case-relationships.sql)
after both the Program 1 core import and the Program 1 contacts/facilities
import have completed for the same source fingerprint. It repairs only blank
canonical fields, omitted relationships, and legacy case-status metadata:

The repair accepts the completed v2 or v3 contact run that owns the required
`SL_CASE_MANAGER` crosswalks; facility and lien-facility mappings may come from
an approved completed v1, v2, or v3 run for the same tenant, organization, and
source fingerprint.

- `SL_CASE.CASE_MANAGER` and `SL_CASE.CASE_ATTORNEY` through their
  `SL_CASE_MANAGER` crosswalks to compatibility case-manager and attorney IDs
  in the target case metadata.
- `SL_CASE.CASE_LAW_FIRM` through its approved `SL_CONTACT` crosswalk to the
  compatibility law-firm ID in the target case metadata. The crosswalk may be
  owned by the completed v1, v2, or v3 contact wave for the same immutable
  source fingerprint; the case-person crosswalks still require v2 or v3.
- `SL_CASE.CASE_ACCIDENT_TYPE` through `SL_ACCIDENT_TYPE` to the matching
  system `liens_LookupValues` UUID plus the target `accidentType` name.
- `SL_CASE.CASE_STATUS` to `statusLabel` metadata for `New`, `Processing`, and
  `Litigation` rows whose target case remains in the corresponding collapsed
  canonical status (`PreDemand` or `InNegotiation`).
- `SL_LEINS_MEDICAL_INFORMATION_FACILITY` to a blank target lien `FacilityId`.
- The source address components, incident state, medical status, tracking
  follow-up date, minor-comp flag, dropped-case flag, and imported creator name
  to their typed `liens_Cases` fields.

The facility relationship is lien-level, not a column on `liens_Cases`. The
script never creates contacts, facilities, or lookups. It creates only its
dedicated `SL_CASE_NOTES_LAST_ACTIVITY` crosswalks; a missing or ambiguous
prerequisite mapping blocks the entire apply. It also refuses to overwrite an
existing different case-manager, attorney, typed parity value, accident-type,
case-status label, or facility assignment. Cases whose status changed after
import retain their current status and are not given the historical label.
Every applied parity group records source, preimage, and applied-value hashes
in `liens_LegacyFieldMigrationStates` so reruns and rollback investigation are
auditable without retaining sensitive plaintext in the control ledger.

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
  --backfill-v3-report-fields `
  --tenant-id 019f6ae6-4348-784a-aae0-f4d636f843ad `
  --target-connection '<LS_QA_LIENS connection string>'
```

Copy all five exact dry-run counts, then run the apply:

```powershell
dotnet run --project scripts/LegacyLiensImport -- `
  --backfill-v3-report-fields `
  --tenant-id 019f6ae6-4348-784a-aae0-f4d636f843ad `
  --target-connection '<LS_QA_LIENS connection string>' `
  --expected-case-updates <dry-run-case-count> `
  --expected-lien-facility-updates <dry-run-lien-facility-count> `
  --expected-medical-code-inserts <dry-run-medical-code-count> `
  --expected-provider-changes <dry-run-provider-count> `
  --expected-activity-inserts <dry-run-activity-count> `
  --apply
```

The target connection must select `LS_QA_LIENS` (or the explicitly approved
`LS_LIENS` production schema), and `SL-CORE` must be on the same MySQL server.
The runner refuses an apply when any count differs or any preflight/conflict
check fails. The compatibility option `--backfill-case-relationships` executes
the same complete v3 repair, but new operations should use
`--backfill-v3-report-fields`.

For the complete v3 DIY-report data set, run the repairs in dependency order:

1. Apply `20260825160000_AddLegacyReportParityFields` and complete
   `sl-core-contact-facility-v3`.
2. Run this guarded all-fields repair. It now includes medical-code servicing
   rows, medical-provider metadata, and deterministic last-activity records in
   the same assertion-bound transaction as the case and facility updates.
3. If the completed core import predates case-note v2, run
   `reconcile-sl-core-case-note-categories.sql` separately when you also need
   the legacy tracking/feed categories corrected. The all-fields repair itself
   creates the deterministic internal row used by `last_activity` and
   `last_activity_date`.

The repair also fills a blank plaintiff date of birth from `SL_CASE.CASE_DOB`;
an existing different value is treated as a blocking conflict.
Law-firm and medical-facility address/phone/email fields are read from the v3
contact/facility targets after the relationships above make them reachable.

## Litigation case-status repair

[`update-litigation-case-statuses.sql`](update-litigation-case-statuses.sql)
applies the reviewed `Litigation Status.xlsx` list. The workbook identifies
1,136 liens but contains a case-status correction: 377 unique cases remain
at `InNegotiation` before the repair, then `liens_Cases.Status` is set directly
to either `Litigation (Open)` (166 cases) or `Litigation (Pending)` (211
cases). It does not change lien lifecycle statuses, notes, or legacy metadata.

Open an explicitly selected `LS_QA_LIENS` or approved `LS_LIENS` connection in
DBeaver. Set the target tenant and audit actor IDs in the script, leave
`@apply = 0`, and execute the entire file. Review its 377-case preflight output
and copy `ChangesToApply` into `@expected_case_updates`; then set `@apply = 1`
and execute the whole script again. It updates only when every target still has
case status `InNegotiation`, the tenant-owned preimages match, and
postconditions succeed; otherwise it rolls back.

## Memet Hussein 25-02030 imported-payment repair

[`repair-memet-hussein-25-02030-imported-payments.sql`](repair-memet-hussein-25-02030-imported-payments.sql)
soft-deletes the two reviewed SL-CORE payment-detail artifacts that incorrectly
make each open lien show `$1,795` received. Because QA and production target
UUIDs differ, the repair resolves the reviewed legacy case (`27208`), liens
(`59915`/`59916`), and payment details (`41411`/`41412`) through exact,
tenant-scoped SL-CORE crosswalks. It also verifies the schema/tenant pair,
target ownership, amount, receipt fields, note, and audit preimages. It
preserves the rows and crosswalks for audit and does not change balances,
reductions, settlements, or payment numbering.

Open an explicitly selected `LS_QA_LIENS` or approved `LS_LIENS` connection in
DBeaver, select the tenant line matching that database, set `@actor_user_id`,
leave `@apply = 0`, and execute the entire file. Review both planned rows and
copy `ChangesToApply` and `PlanChecksum` into `@expected_updates` and
`@expected_checksum`. Set `@apply = 1` and execute the whole file again. Any
missing or changed preimage, ambiguous legacy crosswalk, unexpected row count,
checksum mismatch, or failed postcondition rolls back the transaction.

## Darin Tellis 25-01967 lien-05 closed-at-zero repair

[`repair-darin-tellis-25-01967-lien-05.sql`](repair-darin-tellis-25-01967-lien-05.sql)
is a production-only manual repair for lien `25-01967-05`. It keeps the lien
closed, changes its current balance and payoff to `$0`, soft-deletes the
reviewed malformed imported `$16,000` payment, and retains the real zero-dollar
closure record after removing its No Recovery classification. The case, its
four earlier liens, `$33,500` in legitimate payments and settlements, legacy
crosswalks, and status history remain unchanged.

Open an explicitly selected `LS_LIENS` connection in DBeaver, replace the
single `<identity-user-guid>` placeholder with an active Identity user for the
production tenant, leave `@apply = 0`, and execute the complete file. Review
the exact targets and copy `ChangesToApply` and `PlanChecksum` into
`@expected_updates` and `@expected_checksum`. Set `@apply = 1` and execute the
complete file again. The repair commits only if the case, lien, both payment
rows, source crosswalks, sibling totals, receipt fields, notes, timestamps, and
audit fields still match the reviewed preimages; otherwise it rolls back. If
the SQL client reports an execution error before the final `Result` row, issue
`ROLLBACK` in that same connection before investigating or rerunning.

The emergency companion
[`rollback-darin-tellis-25-01967-lien-05-repair.sql`](rollback-darin-tellis-25-01967-lien-05-repair.sql)
reverses only those three repaired values. It intentionally restores the false
`$16,000` received amount and No Recovery classification, so use it only under
an approved rollback decision. The rollback has its own dry-run, count, actor,
checksum, and postcondition gates. It also requires the lien and both payment
rows to retain the identical repair timestamp and actor written by the forward
script; any later edit blocks rollback. Run it with `@apply = 0`, review
`ChangesToApply = 3` and `BlockingRows = 0`, copy its new `PlanChecksum`, then
set `@apply = 1`, `@expected_updates = 3`, and its exact checksum before
executing the complete rollback file again. If the SQL client reports an
execution error before the final `Result` row, issue `ROLLBACK` in that same
connection before investigating or rerunning.

## Hector Zaldana 26-31912 imported-payment repair

[`repair-hector-zaldana-26-31912-imported-payment.sql`](repair-hector-zaldana-26-31912-imported-payment.sql)
soft-deletes the reviewed SL-CORE No Recovery artifact that incorrectly makes
open lien `26-31912-01` show `$17,228` received. It verifies the production
schema and tenant, active audit actor, case, both open liens, the exact payment
preimage, empty receipt fields, zero settlement/reduction context, and all four
source crosswalk hashes. It does not update the case or either lien.

Run the complete file with `@apply = 0`, review `ChangesToApply = 1` and
`BlockingRows = 0`, and copy its `PlanChecksum`. Then set `@apply = 1`,
`@expected_updates = 1`, and `@expected_checksum` to that checksum before
executing the complete file again. The emergency companion
[`rollback-hector-zaldana-26-31912-payment-repair.sql`](rollback-hector-zaldana-26-31912-payment-repair.sql)
intentionally restores the false receipt and No Recovery classification, so
use it only under an approved rollback decision. Both files require a
same-connection `ROLLBACK` if the SQL client errors before the final `Result`
row.

## Julio De Anda Fajardo 26-32723 No Recovery amount repair

[`repair-julio-de-anda-fajardo-26-32723-no-recovery-amount.sql`](repair-julio-de-anda-fajardo-26-32723-no-recovery-amount.sql)
is a production-only manual repair for lien `26-32723-01`. The imported
SL-CORE status-4 declaration has no receipt evidence, but its `$3,700` face
amount was stored as received cash. The repair changes only that payment
amount to `$0`; it keeps the row active so V3 continues to show **No Recovery**
and does not modify the Closed case or Settled lien.

Open an explicitly selected `LS_LIENS` connection in DBeaver, replace
`<identity-user-guid>` with an active Identity user for the production tenant,
leave `@apply = 0`, and execute the complete file. Review
`ChangesToApply = 1` and `BlockingRows = 0`, then copy `PlanChecksum` into
`@expected_checksum`, set `@expected_updates = 1` and `@apply = 1`, and execute
the complete file again. Exact source hashes, target preimages, tenant actor,
serializable locks, and postconditions guard the one-row update. If the SQL
client errors before the final `Result` row, issue `ROLLBACK` in the same
connection before investigating or rerunning.

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
maps rows with a nonblank `SLS_SETTLE_AMOUNT` to `liens_LienSettlements`, and
also preserves rows whose settle amount is blank when either
`SLS_REDUCTION_AMOUNT` or `SLS_TOTAL_SETTLED_AMOUNT` is present. These
metadata-only rows use `Amount = 0` and `Status = 'Pending'`, so they do not
change Cash Received or amount-to-settle totals. A nonblank invalid or
out-of-range `SLS_SETTLE_AMOUNT` still blocks the import. Reduction dates are
carried only from `SLS_REDUCTION_DATE`; the importer does not infer one from a
settlement date. The reductions API reads dated preserved metadata when a
canonical `liens_LienReductions` row is unavailable. Source rows without a
reduction date remain preserved for audit but are omitted from the reductions
API. The procedure also retains non-deleted settlement payment details.

### Existing-import settlement metadata repair

Do not rerun the complete import to repair tenants that were imported before
metadata-only settlement rows were preserved. Deploy
[`backfill-sl-core-settlement-metadata.sql`](backfill-sl-core-settlement-metadata.sql)
to an explicitly selected `LS_QA_LIENS` or approved `LS_LIENS` schema while the
same immutable `SL-CORE` source restore is available. The repair uses the
completed core import and lien crosswalks to insert only missing source
settlement rows whose settle amount is blank but whose reduction or
total-settled metadata is present.

Run preflight first; the `NULL` assertion parameters are intentional:

```sql
CALL liens_backfill_sl_core_settlement_metadata(
  '<tenant-guid>', '1', NULL,
  NULL, NULL, NULL, NULL, NULL, '0');
```

Confirm that `DistinctLiens` and `ReductionTotal` reconcile to the approved
exception report. `EligibleCanonicalReductionRows` reports reductions that can
be written to `liens_LienReductions`. `BlankReductionDates` is calculated from
the authoritative `SL-CORE.SL_LIENS_SETTLEMENT.SLS_REDUCTION_DATE` value. Those
rows remain in preserved settlement metadata and are skipped by the canonical
reduction phase. `InvalidReductionDates` must be `0`; any nonblank source value
that cannot be parsed blocks both preflight approval and apply. The procedure
never infers a reduction date from a settlement date or workbook data. Retain
`SourceRows`, `DistinctLiens`, `BlankReductionDates`, `ReductionTotal`, and
`ExpectedChecksum`, obtain a change/approval reference, then copy those exact
values into the apply call:

```sql
CALL liens_backfill_sl_core_settlement_metadata(
  '<tenant-guid>', '1', '<change-or-approval-id>',
  <source-rows>, <distinct-liens>, <blank-reduction-dates>,
  <reduction-total>, '<checksum>', '1');
```

When `BlankReductionDates` is greater than `0`, apply remains safe: the
procedure skips those canonical rows and returns the count as
`SkippedReductionRowsWithBlankDate`. The settlement metadata remains
authoritative for audit, but undated metadata is omitted from the reductions
API until a separately approved repair materializes a canonical dated
reduction. This avoids inventing historical dates solely to satisfy the
canonical table constraint.

The repair holds the tenant core-import lock, verifies the completed import and
source fingerprint, rejects conflicting or uncrosswalked target rows, and
inserts zero-amount `Pending` settlements and eligible canonical lien
reductions plus their crosswalks in one transaction. Version 3 is safe to apply
after the original settlement-only repair or Version 2: its preflight reports
the existing settlement rows, plans only missing reductions with valid dates,
and skips missing-date rows for the API fallback. It records a separate
completed repair run when writes are required and is safe to rerun; an already
repaired tenant returns `settlement-metadata-backfill-already-complete`. It
never fabricates a reduction date or changes Cash Received. Existing business
rows are not deleted on rollback; reverse an applied repair only through an
approved compensating script or database restore.

Identity, provenance, checksum, and postcondition string comparisons use binary
semantics so the same reviewed procedure works when production target columns
use `utf8mb4_unicode_ci` and restored source or temporary columns use
`utf8mb4_0900_ai_ci`.

### Business-approved default reduction date

If the business has separately approved `2026-04-27` as the default reduction
date for the exact 192-row blank-date cohort, deploy
[`materialize-sl-core-approved-default-reductions.sql`](materialize-sl-core-approved-default-reductions.sql)
after the Version 3 metadata repair has completed. This is a separate,
tenant-bound repair: it does not claim the date came from SL-CORE, does not
change the preserved settlement metadata, and does not change Cash Received.
It creates canonical `liens_LienReductions` rows so the existing reductions API
and tenant portal can list the approved reductions.

Run preflight using the real ticket, change request, or other durable approval
reference. Replace the literal placeholder before execution:

```sql
CALL liens_materialize_sl_core_approved_default_reductions_v1(
  '019fb470-f161-7fbd-93a0-c808d43c43c3',
  '0ab1aa20-9e22-11f1-9a38-0a971fa4811b',
  '2026-04-27',
  '<change-or-approval-id>',
  NULL, NULL, NULL, NULL, NULL, NULL,
  '0'
);
```

For the initial approved cohort, confirm that preflight returns `SourceRows =
192`, `DistinctLiens = 192`, `BlankSourceReductionDates = 192`, `ExistingRows =
0`, `RowsToInsert = 192`, and `ReductionTotal = 467303.5100`. Copy the returned
values and checksum without editing them into apply:

```sql
CALL liens_materialize_sl_core_approved_default_reductions_v1(
  '019fb470-f161-7fbd-93a0-c808d43c43c3',
  '0ab1aa20-9e22-11f1-9a38-0a971fa4811b',
  '2026-04-27',
  '<same-change-or-approval-id>',
  192, 192, 0, 192, 467303.5100,
  '<checksum-from-immediately-preceding-preflight>',
  '1'
);
```

For the production cohort, deploy
[`materialize-sl-core-approved-default-reductions-prod.sql`](materialize-sl-core-approved-default-reductions-prod.sql)
only through an explicitly selected `LS_LIENS` connection. Its procedure has a
separate name and is bound to production tenant
`019f1a05-7459-7855-b46b-110a702e37a4` and completed Version 3 metadata repair
run `35cece1a-9e54-11f1-b823-12a7a8afef43`; it does not replace or weaken the
QA procedure. Run its production preflight with all assertion parameters null:

```sql
CALL liens_materialize_sl_core_approved_default_reductions_prod_v1(
  '019f1a05-7459-7855-b46b-110a702e37a4',
  '35cece1a-9e54-11f1-b823-12a7a8afef43',
  '2026-04-27',
  '<production-change-or-approval-id>',
  NULL, NULL, NULL, NULL, NULL, NULL,
  '0'
);
```

Copy all six assertions from the immediately preceding production preflight
into apply. Never reuse the QA counts or checksum even if the displayed totals
match.

The apply phase revalidates the immutable source fingerprint and all copied
assertions inside a transaction. It rejects a changed cohort, nonblank source
dates, unrelated canonical reductions, or conflicting crosswalks. New rows use
a dedicated approved-default crosswalk and record the approval reference plus
`reductionDateSource=business-approved-default` in the reduction note. A safe
rerun with the same approval reference returns
`approved-default-reductions-already-complete`; a different approval reference
is treated as a conflict.

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

### Existing-import case-note materialization

[`backfill-sl-core-case-notes.sql`](backfill-sl-core-case-notes.sql) inserts
missing `SL_CASE_NOTES` crosswalk targets into `liens_CaseNotes`. It preserves
the source content, category, deletion state, and creator name, and maps the
approved legacy creator names to their V3 user IDs. It requires the matching
completed import, source-provenance receipt, and both case and note crosswalks.
An existing target note with incompatible fields is a conflict that prevents
all writes. Numeric note and case crosswalks are staged into indexed temporary
maps so the source join remains bounded and does not apply functions to indexed
join columns. The dry run also verifies that every note crosswalk produced one
staged source row; missing source notes, cases, or content block the apply as
`CrosswalkCoverageErrors`. An otherwise-identical note still owned by the
completed import's migration user and carrying a known importer fallback name
has both `CreatedByUserId` and canonical `CreatedByName` updated to the mapped
V3 author in the same transaction. When legacy
`CN_CREATED_BY` is blank, the source has no recoverable creator identity, so the
procedure assigns the completed import's migration user ID and the name
`system-migration`. A legacy creator value of `migration` is normalized to the
same canonical name. Other nonblank legacy creator names are preserved. Names
present in the approved map receive their V3 user ID; other names retain the
migration user ID until an explicit mapping is approved.

Run the procedure in DBeaver, then dry run before inserting the exact returned
count. The dry run returns one summary row with `ChangesToApply`, `Conflicts`,
`InsertsToApply`, `AuthorUpdatesToApply`, and the remaining breakdown:

```sql
CALL liens_backfill_sl_core_case_notes('<tenant-guid>', -1, '0');
CALL liens_backfill_sl_core_case_notes('<tenant-guid>', <ChangesToApply>, '1');
```

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
