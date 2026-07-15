# LS-LIENS-DASHBOARD-REPORT-001 — Restore medical provider report export date compatibility

## 1. Ticket Information

| Field | Value |
| --- | --- |
| Ticket | LS-LIENS-DASHBOARD-REPORT-001 |
| Title | Restore medical provider report export date compatibility |
| Type | Bug Fix |
| Repository | `legalsynq-v2.0` |
| Status | READY FOR VERIFICATION |
| Author | Codex |
| Date | 2026-07-03 |

## 2. Objective

Fix `POST /api/liens/cases/dashboard/medical-provider-report-export/v3` so it returns data for the
existing admin portal payload and preserves legacy filtering semantics for date-based dashboard
exports.

## 3. Architecture Summary

The change is constrained to the Liens service compatibility layer in `Liens.Api`. No cross-service
contracts or schema changes were required. The fix reuses the existing dashboard report builder and
aligns request handling with the legacy portal payload instead of introducing a new endpoint.

## 4. Files Created

- `analysis/Liens/LS-LIENS-DASHBOARD-REPORT-001-report.md`

## 5. Files Modified

- `apps/services/liens/Liens.Api/Endpoints/CaseEndpoints.cs`
- `apps/services/liens/Liens.Api.Tests/Tests/DashboardReportEndpointTests.cs`

## 6. Implementation Summary

1. Traced the QA endpoint to `apps/services/liens/Liens.Api/Endpoints/CaseEndpoints.cs`.
2. Confirmed the current v3 request model reads `startDate` / `endDate`, while the admin portal
   still posts `purchaseDateFrom` / `purchaseDateTo`.
3. Compared the new implementation with the legacy SynqLiens controller/context and confirmed the
   old medical provider report filtered on legacy purchase-date semantics rather than lien
   `CreatedAtUtc`.
4. Implemented a backward-compatible fix by:
   - adding `purchaseDateFrom` / `purchaseDateTo` aliases to the dashboard report request model,
   - resolving those aliases before `startDate` / `endDate`, and
   - applying the medical-provider report date window to legacy purchase-date semantics via
     `Lien.IncidentDate` instead of `Lien.CreatedAtUtc`.
5. Updated the dashboard report regression test to exercise the alias payload path and seeded the
   lien with a deterministic incident date so the compatibility behavior is asserted directly.

## 7. Governance Summary

- Reuse First: reused the existing endpoint and report builder rather than adding a new route.
- Service Boundary Protection: kept all changes inside the Liens API compatibility layer.
- Incremental Delivery: targeted only the request parsing and filter behavior causing the
  regression.
- Auditability: recorded the investigation, implementation, and validation in this report.
- Validation Before Completion: ran targeted Liens API tests and a direct build after the code
  change.
- Multi-Tenant Safety: preserved tenant and org scoping behavior.
- Truthful Reporting: documented the initial file-lock validation issue and the successful rerun
  separately.

## 8. Validation Results

- `dotnet build apps/services/liens/Liens.Api/Liens.Api.csproj` succeeded on 2026-07-03 with
  `0 Error(s)` and existing package warnings unrelated to this fix.
- `dotnet test apps/services/liens/Liens.Api.Tests/Liens.Api.Tests.csproj --filter "DashboardReportEndpointTests"`
  passed on 2026-07-03 with `5` tests passed and `0` failed.
- A first parallel validation attempt hit a local file lock on `Liens.Api.dll` while `dotnet build`
  and `dotnet test` were writing the same output path at the same time. Re-running the test
  independently completed successfully.

## 9. Known Gaps

- This fix is scoped to the medical provider dashboard export path.
- The law-firm dashboard export appears to use the same legacy payload naming in the admin portal
  and may need the same compatibility review.

## 10. Risks

- Other dashboard report endpoints may have similar payload alias drift and may need follow-up if
  they share the same regression pattern.

## 11. Final Status

Conditionally Ready — the medical provider dashboard export compatibility fix is implemented and
covered by targeted tests, but the sibling law-firm export should be reviewed before considering the
dashboard export family fully aligned.

## 12. Recommended Next Ticket

- Audit the other dashboard export endpoints for legacy payload alias compatibility, starting with
  the law-firm case allocation export.
