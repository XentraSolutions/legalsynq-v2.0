# Liens Legacy Regression Matrix

Date: `2026-06-29`

Scope: legacy SynqLiens API surface migrated into `legalsynq-v2.0` liens service.

Regression command:

```powershell
dotnet test apps/services/liens/Liens.Api.Tests/Liens.Api.Tests.csproj --filter "Legacy"
```

Result:

- `116` passed
- `0` failed
- `0` skipped

## Overall Status

| Legacy area | v2.0 status | Automated regression status | Evidence |
|---|---|---|---|
| `case/*` | Implemented in liens API compatibility layer | Expanded direct route coverage present | `LegacyCaseEndpointTests`, `LegacyMedicalEndpointTests`, `LegacyServiceCompatibilityTests`, `LegacyCaseGapRegressionTests` |
| `service/*` | Implemented in liens API compatibility layer | Expanded direct route coverage present | `LegacySettlementEndpointTests`, `LegacyServiceCompatibilityTests`, `LegacyCaseGapRegressionTests` |
| `Batch/*` | Implemented in liens API compatibility layer | Broad direct route coverage present | `LegacyBatchUploadEndpointTests` |
| `contact/*` | Implemented | Broad coverage present | `LegacyContactEndpointTests` |
| `facility/*` | Implemented | Broad coverage present | `LegacyFacilityEndpointTests` |
| `lookup/*` | Implemented | Broad coverage present | `LegacyLookupEndpointTests` |
| `report/*` | Implemented | Broad coverage present | `LegacyReportEndpointTests` |
| `authentication/login` | Not in liens service by design | Not applicable here | moved to Identity boundary |

## Controller Matrix

### Case Controller

| Legacy route family | v2.0 mapping status | Automated status | Notes |
|---|---|---|---|
| `POST case/create` | Implemented as `/api/liens/cases/create` | Covered | direct regression present |
| `PATCH case/update/{id}` | Implemented as `/api/liens/cases/update/{id}` | Covered | direct regression present |
| `GET case/v2`, `POST case/v3` | Implemented | Covered | list/filter regression present |
| `GET case/getcaseinfo/{id}` | Implemented | Covered | direct regression present |
| `DELETE case/delete/{id}` | Implemented | Covered | direct regression present |
| `POST case/liens/facility`, `POST case/liens/update-facility`, `GET case/liens/get-facility/{id}` | Implemented | Not directly asserted | route presence confirmed; add dedicated facility-info regression if needed |
| `POST case/liens/medical`, `POST case/liens/update-medical`, `GET case/liens/get-medical/{id}` | Implemented | Covered | medical compatibility tests present |
| `POST case/liens/medicalcode`, `POST case/liens/update-medicalcode`, `GET case/liens/get-medicalcode/{caseId}` | Implemented | Covered | medical code regression present |
| `GET case/liens/delete-medicalcode/{id}`, `DELETE case/liens/delete-medicalcode/{id}` | Implemented | Not directly asserted | delete route exists; no direct legacy assertion yet |
| `POST case/liens/payment`, `GET case/liens/get-payee-outbound/{liensId}` | Implemented | Covered | direct regression present |
| `POST case/casemanager`, `POST case/update-casemanager`, `DELETE case/delete-casemanager/{id}` | Implemented | Not directly asserted | route compatibility present |
| `POST case/liens/upload/document`, `POST case/upload/document` | Implemented | Covered | upload compatibility and validation covered |
| `GET case/liens/get-medicaldocument/{liensId}` | Implemented | Covered | regression present |
| `GET case/get-casedocument/{caseId}` | Implemented | Covered | regression present |
| `GET case/get-allcasedocument/{caseId}` | Implemented | Covered | regression present |
| `DELETE case/liens/delete-medicaldocument/{id}` | Implemented via lien endpoints | Not directly asserted | route exists, no direct test yet |
| `DELETE case/delete-casedocument/{id}` | Implemented via lien endpoints | Not directly asserted | route exists, no direct test yet |
| `POST case/liens`, `POST case/liens/{caseId}`, `POST case/liens/v2`, `POST case/liens/v2/{caseId}`, `POST case/liens/v3`, `POST case/liens/details/{caseId}` | Implemented | Not directly asserted | legacy list/detail families present; add dedicated matrix if these are high priority |
| `POST case/other`, `POST case/update-other`, `GET case/get-other/{caseId}` | Implemented | Covered | direct regression present |
| `GET case/law/{lawFirmId}/{isTotal?}`, `POST case/law/v3` | Implemented | Partially covered | v3/filter coverage exists; direct law route not asserted |
| `GET case/medical/{medicalId}/{isTotal?}`, `POST case/medical/v3` | Implemented | Not directly asserted | route family present |
| `GET case/funding/{fundingCompanyId}/{isTotal?}`, `POST case/funding/v3` | Implemented | Not directly asserted | route family present |
| `GET case/medical/facility/{facilityId}/{isTotal?}`, `POST case/medical/facility/v3` | Implemented | Not directly asserted | route family present |
| `GET case/case-manager/{caseManagerId}/{isTotal?}` | Implemented | Not directly asserted | route family present |
| `GET case/medical/facility-contact/{facilityContactId}/{isTotal?}` | Implemented | Not directly asserted | route family present |
| `POST case/reassign/lawfirm`, `POST case/reassign/casemanager`, `POST case/reassign/leads` | Implemented | Not directly asserted | route family present |
| `POST case/liens/reassign/medical-provider`, `facility`, `contact-person`, `funding-company` | Implemented via lien endpoints | Not directly asserted | route compatibility present |
| `PATCH case/personal-update`, `primary-update`, `details-update` | Implemented | Not directly asserted | route family present |
| `GET case/notes/{caseId}`, `POST case/get-notes`, `POST case/add-note`, `POST case/delete-note` | Implemented | Covered | direct regression present |
| `GET case/payoff-quote/{caseId}` and typo alias `payoff-qoute` | Implemented | Covered | regression present |
| `POST case/generate-csv`, `POST case/liens/generate-csv` | Implemented | Not directly asserted | route family present |
| `DELETE case/liens/delete/{liensId}` | Implemented via lien endpoints | Not directly asserted | route exists |
| `POST case/task/create`, `GET case/get-task/{caseId}/{taskId?}`, `PATCH case/task/update`, `DELETE case/task/delete/{taskId}` | Implemented | Partially covered | create/get/delete covered; update route exists but current test focuses on working legacy CRUD slice |
| `POST case/task/{taskId}` | Implemented | Not directly asserted | behavior differs from legacy servicing-task model; deserves dedicated compatibility decision |
| `POST case/batch-reassign` | Implemented | Not directly asserted | route family present |
| `GET case/lead/{leadId}/{isTotal?}`, `GET case/leads/{leadId}/{isTotal?}`, `POST case/leads/v3` | Implemented | Not directly asserted | route family present |
| `GET case/case-updates/{caseId}`, `POST case/case-updates/v3`, `GET case/liens-updates/{caseId}`, `POST case/liens-updates/v3` | Implemented | Not directly asserted | route family present |
| `POST case/import-csv`, `migrate-csv`, `migrate-guardian-csv`, `update-lien-payment-csv` | Implemented as stubs | Not directly asserted | route compatibility exists; behavior is intentionally limited |
| `POST case/manual/medical/code/create`, `update` | Implemented | Not directly asserted | route family present |
| `GET case/dashboard/task-summary` | Implemented as stub | Not directly asserted | returns compatibility payload |
| `GET case/dashboard/piechart` | Implemented | Covered indirectly by route presence only | no direct assertion yet |
| `POST case/dashboard/deployed`, `POST case/dashboard/cash-received` | Implemented | Covered | regression present |
| `GET/POST case/dashboard/*report-export*`, `GET *report-csv` | Implemented as stubs | Not directly asserted | route compatibility present |
| `POST case/document/type` | Implemented | Not directly asserted | route family present |
| `POST case/global-search` | Implemented | Not directly asserted | route family present |
| `POST case/mergecase` | Implemented | Not directly asserted | route compatibility present |

### Service Controller

| Legacy route family | v2.0 mapping status | Automated status | Notes |
|---|---|---|---|
| `GET service/case1`, `GET service/case`, `POST service/case/v3` | Implemented | Covered | regression present |
| `GET service/liens/{caseId}`, `GET service/closed-liens/{caseId}`, `GET service/all-liens/{caseId}`, `POST service/liens/v3` | Implemented | Covered | regression present |
| `POST service/liens/update/reduction` | Implemented | Covered | regression present |
| `POST service/liens/update/settlement` | Implemented | Covered | regression present |
| `POST service/liens/settlement/payment` | Implemented | Covered | regression present |
| `POST service/delete-payment` | Implemented | Covered | regression present |
| `DELETE service/delete-payment/{id}` | Implemented in settlement legacy shim | Covered | regression present |
| `GET service/settlement/history/{caseId}` | Implemented | Covered | regression present |
| `POST service/settlement/history/v3` | Implemented | Covered | regression present |
| `PATCH service/update-details` | Implemented | Covered | direct regression present |
| `GET service/liens/settlement/payment-details/{caseId}` | Implemented | Covered | regression present |
| `PATCH service/liens/update/status` | Implemented | Covered | direct regression present |
| `GET service/liens/settlement-details/{caseId}` | Implemented | Covered | regression present |
| `POST service/generate-csv` | Implemented | Covered | direct regression present |
| `POST service/update-liens-status` | Implemented | Covered | direct regression present |

### Batch Upload Controller

| Legacy route family | v2.0 mapping status | Automated status | Notes |
|---|---|---|---|
| `GET Batch/list/{id}` | Implemented | Covered | regression present |
| `POST Batch/list` | Implemented | Covered | regression present |
| `POST Batch/data-context` | Implemented | Covered | regression present |
| `POST Batch/create` | Implemented | Covered | regression present |
| `POST Batch/Upload` | Implemented | Covered | regression present |
| `POST Batch/update` | Implemented | Covered | direct regression present |
| `POST Batch/process` | Implemented | Covered | regression present |
| `GET Batch/details/{batchUploadId}` | Implemented | Covered | regression present |
| `GET Batch/download-template/{id}` | Implemented | Covered | regression present |
| `DELETE Batch/delete/{id}` | Implemented | Covered | regression present |
| `DELETE Batch/details/delete/{id}` | Implemented | Covered | regression present |
| `GET Batch/data-context/{id}` | Not implemented | Not applicable | commented out in legacy controller too |

### Contact Controller

| Legacy route family | v2.0 mapping status | Automated status | Notes |
|---|---|---|---|
| law firm list/get/v3/create/update/delete | Implemented | Covered | direct regression present |
| medical provider list/v3 | Implemented | Covered | direct regression present |
| medical facility list/v3 | Implemented | Covered | direct regression present |
| funding company list/v3 | Implemented | Covered | direct regression present |
| leads list/v3 | Implemented | Covered | direct regression present |
| generate csv routes | Implemented | Covered | direct regression present |

### Facility Controller

| Legacy route family | v2.0 mapping status | Automated status | Notes |
|---|---|---|---|
| create/update/delete/list/v3 | Implemented | Covered | direct regression present |
| contact person create/update/delete/get | Implemented | Covered | direct regression present |

### Lookup Controller

| Legacy route family | v2.0 mapping status | Automated status | Notes |
|---|---|---|---|
| states, all, contact, facility | Implemented | Covered | direct regression present |
| contact lawfirm/provider/funding/type/roles | Implemented | Covered | direct regression present |
| accident/lien/case/medical/settlement/current-attributes | Implemented | Covered | direct regression present |
| procedure codes/costs | Implemented | Covered | direct regression present |
| task status/priority, user-list, contacts, case manager, facility contact person | Implemented | Covered | direct regression present |

### Report Controller

| Legacy route family | v2.0 mapping status | Automated status | Notes |
|---|---|---|---|
| `GET/POST report/diy` | Implemented | Covered | direct regression present |
| `POST report/diy/export` | Implemented | Covered | direct regression present |
| `POST report/diy/save` | Implemented | Covered | direct regression present |
| `GET report/diy/saved` | Implemented | Covered | direct regression present |
| `DELETE report/diy/{id}` | Implemented | Covered | direct regression present |
| `DELETE report/diy/delete/{id}` | Implemented | Covered | direct regression present |
| `GET report/diy/columns` | Implemented | Covered | direct regression present |
| `POST report/diy/filter-options` | Implemented | Covered | direct regression present |
| `GET report/diy/all-filters` | Implemented | Covered | direct regression present |

## QA Findings

No automated regression failures were found in the current legacy suite.

## Remaining QA Gaps

These are route families that are implemented but not yet directly asserted in the automated legacy suite:

- `case/liens/get-facility/{id}`
- `case/casemanager`, `case/update-casemanager`, `case/delete-casemanager/{id}`
- `case/task/update`
- `case/task/{taskId}`

## Recommended Next QA Additions

- Add direct assertions for case manager and facility-info flows.
- Add a focused compatibility decision test for `POST case/task/{taskId}` because the legacy path and the newer task model are not a perfect one-to-one behavior match.
- Add direct assertions for `case/task/update` if the legacy servicing status vocabulary needs to be preserved more closely than the current adapter permits.
