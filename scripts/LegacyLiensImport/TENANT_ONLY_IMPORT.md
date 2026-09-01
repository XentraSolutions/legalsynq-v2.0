# Tenant-only SL-CORE core import

`import-sl-core-core-tenant-only.sql` defines a MySQL 8 stored procedure for
the approved core scope only: cases, medical-lien headers, and case notes. It
does not import credentials, documents, contacts, facilities, medical-code
detail, payments, settlements, reports, or workflow state.

## Deployment and use

A DBA deploys the procedure using a dedicated, reviewed least-privilege
definer account. The migration operator receives `EXECUTE` on the procedure
only; do not grant DDL, source `SELECT`, or direct table-write access. After
deployment, verify the actual definer and `SQL SECURITY DEFINER` mode with
`SHOW CREATE PROCEDURE liens_import_sl_core_core_tenant_only` before granting
operator access.

```sql
CALL liens_import_sl_core_core_tenant_only('<tenant-guid>', '0'); -- preflight
CALL liens_import_sl_core_core_tenant_only('<tenant-guid>', '1'); -- apply
```

The tenant ID is the only caller-selected business value. The procedure fails
unless exactly one active `liens_LegacyImportApprovals` row supplies the
organization, migration actor, program, amount policy, status mappings, and
source fingerprint. It also requires the controlled `SL-CORE` staging receipt
to match that approval.

The SQL procedure cannot independently query Identity. Before an approval is
made active, the Identity-owned release process must verify that its `OrgId`
belongs to the tenant, that the migration actor is active and tenant-scoped,
and that the organization has `SYNQ_LIENS` entitlement. Only that release
process may insert or approve `liens_LegacyImportApprovals` records.

For this QA snapshot, when the release endpoint is not available,
`create-qa-sl-core-program-1-approval.sql` is the reviewed one-time release
script. It verifies the active tenant organization, migration-user membership,
administrator role, SynqLien entitlement, staging receipt, and absence of an
existing active approval before inserting a two-hour program-1 approval. The
operator must explicitly select an authorized approver and the approved
`billing` or `purchase` amount policy; the script will not guess either.
For the supplied QA dumps, Identity is read from the separate same-server
`LS_QA_IDENTITY` schema, while the approval itself is written to
`LS_QA_LIENS`. The script validates that schema against its required `idt_`
tables and lists `IdentitySchemaCandidate` values. If a different verified QA
Identity schema is needed, the operator must set
`@identity_schema_override` to one of those candidates.

For tenant `019f6ae6-4348-784a-aae0-f4d636f843ad`, use the separate guarded
script `create-qa-sl-core-program-1-approval-019f6ae6.sql`. It is bound to the
verified tenant organization and active TenantAdmin for that tenant and still
requires an explicit `billing` or `purchase` policy. It does not create an
approval until that policy is selected.

## Controlled restore receipt

The supplied dump does not contain the receipt table. The isolated staging
restore workflow must create it outside the dump and write the one current row
atomically with the approved restore:

```sql
CREATE TABLE `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE` (
  PROVENANCE_KEY varchar(50) NOT NULL,
  SOURCE_FINGERPRINT char(64) NOT NULL,
  IMPORT_SCOPE varchar(100) NOT NULL,
  RESTORE_REFERENCE varchar(200) NOT NULL,
  RESTORED_AT_UTC datetime(6) NOT NULL,
  PRIMARY KEY (PROVENANCE_KEY)
);
```

Its `SOURCE_FINGERPRINT` must equal the approved dump hash and its
`IMPORT_SCOPE` must be exactly `sl-core-core-liens-v1`. The staging restore is
immutable after this write; the procedure definer receives only `SELECT` on
the five named source tables. The procedure reads legacy `TIMESTAMP` fields in
UTC, restores the caller's session time zone, and drops all temporary tables
before returning so preflight data cannot leak to the operator session.

`'0'` performs no permanent writes. `'1'` imports the core rows, import run,
crosswalks, and approval consumption in one transaction. The staging restore
must be immutable and the source account must have read-only access.

For the supplied SL-CORE snapshot, the approved program-1 mapping is legacy
lien status `1` (`Open`) to `Active`, and status `2` (`Closed`) to `Settled`.
The importer preserves a `Settled` lien's legacy update timestamp as its target
`ClosedAtUtc` and sets its target `CurrentBalance` to zero. It does not import
settlement financial-history rows; those are a separate migration wave.

Blank legacy case-note records are excluded because they have no target
content. They do not receive a target note or a crosswalk. Preflight and the
completed import summary report this as `BlankCaseNotesSkipped`; nonblank notes
still fail validation if they exceed the target content or author-name limits.
For each imported note, `CN_USER_ID IS NULL` maps to the tracking category
`general`; a non-null `CN_USER_ID` maps to `feed`. The discriminator and source
fingerprint are included in the `case-note-v2:` crosswalk hash. Older completed
imports must run the guarded reconciliation documented in the main importer
README before the Case Notes History API is enabled for that tenant.

## Medical-code amount backfill

The core import deliberately creates lien headers only. The Liens grid derives
its `Billing Amount` and `Purchase Amount` fields from `LegacyMedicalCode`
servicing records, so an already completed core import needs the separate
`backfill-sl-core-medical-code-amounts.sql` procedure to populate those values.
It accepts a tenant ID and `'0'` (preflight) or `'1'` (apply), requires exactly
one completed Program 1 import for that tenant, and creates a deterministic
record for each matching `SL_LEINS_MEDICAL_CODE` source row. It never changes
the lien header amounts or deletes existing import provenance.

Deploy the procedure with DBeaver **Execute SQL Script**. Then run:

```sql
CALL liens_backfill_sl_core_medical_code_amounts('<tenant-guid>', -1, '0');
CALL liens_backfill_sl_core_medical_code_amounts('<tenant-guid>', <TasksToInsert>, '1');
```

For the supplied Program 1 snapshot, preflight should report `21688`
`SourceMedicalCodes`, `$112137936.06` `TotalBilling`, and `$25208935.11`
`TotalPurchase`. Apply only after those values are confirmed.

## Contacts, facilities, and lien-facility links

`import-sl-core-contacts-facilities-tenant-only.sql` is the separate Program 1
contact migration wave. It requires exactly one completed core import with its
case and lien crosswalks, then imports active legacy contacts, facilities, and
facility contact people. It also fills a migrated lien's empty `FacilityId`
from its legacy facility link. It never overwrites an existing contact,
facility, or lien facility assignment; an existing natural-key collision or a
manual change causes preflight to stop for reconciliation.

If an existing `SL_CONTACT` crosswalk has a blank or malformed target UUID but
is still a `Contact` mapping, the contact wave reports it as
`ContactCrosswalksToRepair` in preflight and repairs that mapping atomically as
it creates the replacement contact. A crosswalk with another target entity is
not repaired automatically and stops with `LSLTC-030` for reconciliation.

Deploy it in DBeaver with **Execute SQL Script** (`Alt+X`). Run preflight
before apply:

```sql
CALL LS_QA_LIENS.liens_import_sl_core_contacts_facilities_tenant_only('<tenant-guid>', '0');
CALL LS_QA_LIENS.liens_import_sl_core_contacts_facilities_tenant_only('<tenant-guid>', '1');
```

For the supplied Program 1 source, source counts include 200 active law firms,
52 active providers, and 109 active facilities. The preflight output gives the
exact contact-person and lien-facility-link totals for the tenant's completed
core run. Do not apply if it returns an `LSLTC-*` error; resolve the stated
collision or completed-core-import issue first.
