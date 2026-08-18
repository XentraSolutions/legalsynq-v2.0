# DL-APP-007 Implementation Report

## 1. Ticket Summary

DL-APP-007 — Implement Report-by-ID Mobile Destination and Complete Deep-Link Mapping.

## 2. Objective

Identify the authoritative business meaning and identifier contract of shared `reportDetails`, and only when unambiguous expose a real Mobile report-by-ID destination and complete APP-004 mapping.

## 3. Scope

Mobile-only domain/data-contract investigation, Dashboard comparison, destination feasibility, and—only if supported—navigation, data integration, screen states, tests, and documentation. URL parsing, auth continuation, Backend/Web/shared-contract implementation, report generation/export redesign, persistence, and unrelated destinations are excluded.

## 4. Initial Implementation Plan

1. Record repository state and review APP-004's gap plus APP-006's successful pattern.
2. Trace every Report model, ID, read endpoint, Mobile client, list/viewer, and internal navigation surface.
3. Explicitly compare Dashboard `reportType`/`dateRange` with persisted report resources.
4. Select a destination only if repository evidence uniquely identifies the shared route's owner and ID contract.
5. Otherwise stop before Mobile feature/navigation/service/test/documentation changes and document the exact Product/Backend/shared-contract decision required.

## 5. Current Branch and Working-Tree State

- Branch: `feat(app)-LSV3-1055-Mobile-Configure-Native-iOS-Universal-Links`.
- Initial HEAD: `bf90d04cdea450d81c1690fc17b5967e22c6145c` (`DL-APP-006`).
- Initial `git status --short`: clean.
- This report is the first and only planned DL-APP-007 working-tree change.

## 6. DL-APP-004 Dependency Review

APP-004 validates a nonblank `reportId` and returns controlled `destination_unavailable`. Its report branch does not parse URLs, authenticate, fetch data, or derive Dashboard parameters.

## 7. DL-APP-006 Analogous Destination Review

APP-006 could proceed because one explicit SynqFund Application aggregate, Guid contract, tenant-scoped GET-by-ID endpoint, gateway path, and existing Web consumer aligned. Report lacks that one-to-one evidence: several separately owned resources and IDs are all called reports.

## 8. Shared reportDetails Route Review

Read-only `shared/contracts/deep-links/routes.json` defines enabled key `reportDetails`, path `/reports/:reportId`, required opaque string `reportId`, and generic auth/fallback/analytics metadata. It supplies no product code, resource kind, service owner, identifier format, or endpoint mapping.

## 9. Report Domain Investigation

The dedicated Reports bounded context contains multiple persisted entities: `ReportTemplate`, `ReportExecution`, `TenantReportView`, `ReportSchedule`, and schedule runs. Separately, Liens exposes saved DIY reports through Mobile's existing `ReportsApi` and Web has Liens report routes. These are distinct contracts, not aliases.

The Reports service's persisted `ReportExecution.Id` is a `Guid`; `GET /api/v1/report-executions/{executionId:guid}` returns only execution summary/status fields. `ReportTemplate.Id` is also a `Guid` and has a different admin GET-by-ID endpoint. Tenant views and schedules have their own Guid IDs. Mobile's Liens `SavedReport` exposes both `id` and `reportId` strings and is retrieved from `/liens/api/liens/reports/diy/saved/{id}`.

## 10. Dashboard Reporting Review

Mobile Dashboard reports are generated views selected by a closed `DashboardReportType` union and a `{ startDate, endDate }` `DashboardDateRange`. `DashboardReportDetailScreen` calls product report queries using those values and has no stable `reportId`, persisted Report entity, report history, or ID conversion. No repository mapping translates shared `reportId` into `reportType` and `dateRange`.

## 11. Report Semantic Decision

Blocked: repository evidence does not uniquely define whether shared `reportDetails` means a Reports-service template, execution, tenant view, schedule, Liens saved DIY report, or another product report. Selecting any one would invent shared-route semantics.

## 12. Report Identifier Contract

Unresolved for the shared route. Candidate authoritative contracts conflict:

- Reports template: `Guid templateId`.
- Reports execution: `Guid executionId`.
- Tenant view/schedule: separate Guid IDs.
- Liens saved report: string `id`, while its DTO also contains a distinct string `reportId`.
- Dashboard report: no stable ID; uses `reportType` plus date range.

APP-002's opaque structural `reportId` does not disambiguate these contracts or authorize conversion.

## 13. Existing Report Data Architecture

The Reports service has domain/application/repository/API layers for templates, executions, views, exports, and schedules. Mobile has an existing Liens-specific `ReportsApi` for saved DIY reports and report execution, while Dashboard uses Cases/Liens reporting queries. No Mobile abstraction or repository metadata binds shared `reportDetails` to one of them.

## 14. Existing Report API / Service Review

Several GET-by-ID capabilities exist, but none is established as the shared route target:

- Reports `GET /api/v1/templates/{templateId}` is platform/tenant-admin template management.
- Reports `GET /api/v1/report-executions/{executionId}` requires SynqInsights product access and report-view permission, is tenant-scoped in service logic, and returns execution summary only.
- Reports tenant-effective/view/schedule endpoints use other IDs and semantics.
- Liens `GET /api/liens/reports/diy/saved/{id}` retrieves a saved DIY configuration.

The gateway exposes the Reports service under `/reports/**`, but gateway routing does not resolve business semantics.

## 15. Existing Report Navigation Review

Mobile has only `DashboardReportDetail`, requiring `{ reportType, dateRange }`; it is reached from Dashboard cards. No typed `ReportDetail: { reportId }` route or report-by-ID internal navigation exists. Web Insights `/insights/reports/[id]` treats `id` as a template ID, whereas Web Liens `/lien/reports/[id]` belongs to another reporting domain; this reinforces rather than resolves ambiguity.

## 16. Existing Report Details Destination Review

No valid generic report-by-ID Mobile destination exists. `DashboardReportDetailScreen` is incompatible, and no hidden/unregistered execution, template, saved-report, document, or report viewer screen was found.

## 17. Backend / Data Availability

Partially available but semantically blocked. Multiple real read contracts exist; the missing dependency is an authoritative Product/shared-route decision naming the resource kind, owning service/product, identifier, and read response that `/reports/:reportId` represents. A shell cannot honestly choose fields or endpoint behavior without it.

## 18. Dashboard Report Relationship Decision

Dashboard reporting remains separate. `DashboardReportType` and `DashboardDateRange` are query/view inputs, are not persisted report identifiers, and cannot be derived from or substituted for `reportId`.

## 19. Report Details Architecture Decision

No destination architecture is selected. The ticket's critical stop rule applies before feature code because a generic screen or an arbitrarily chosen candidate would encode an unsupported product contract.

## 20. Navigation Route Design

Deferred. Once ownership is decided, add a typed direct `MainStack` route using the selected repository contract's exact identifier representation. Do not reuse `DashboardReportDetail` unless a future authoritative mapping explicitly supplies its required type/date range.

## 21. Report Details Screen Design

Blocked. Title, status, timestamps, ownership, rows, configuration, and download behavior differ between candidate resources; rendering any assumed fields would be fabricated.

## 22. Data Loading Design

Blocked. A future implementation should reuse the selected feature's existing endpoint/query abstraction, not add direct component HTTP logic.

## 23. Loading State Design

Blocked with the destination. A future screen should use the standard Mobile `Spinner` pattern.

## 24. Success State Design

Blocked because no single authoritative response contract is tied to shared `reportId`.

## 25. Error / Unavailable Design

APP-004 currently provides the safe `destination_unavailable` outcome. A future screen should use standard `EmptyState`, respond to actual service errors, and leave tenant/product/resource authorization to Backend.

## 26. APP-004 Mapping Design

No change is safe. Keep nonblank `reportId` as controlled `destination_unavailable` and missing/blank ID as `invalid_parameters` until a real typed destination exists. Do not derive `reportType`, `dateRange`, template ID, execution ID, or saved-report ID.

## 27. Files Inspected

- Ticket attachment, root `AGENTS.md`, delivery-mode instructions
- `analysis/DL-APP-002-report.md` through `analysis/DL-APP-006-report.md`, plus `analysis/DL-001-report.md`
- `shared/contracts/deep-links/routes.json` and its README
- Mobile APP-004 mapper/coordinator/service and tests
- Mobile RootNavigator/MainStack/navigation types
- Mobile report-related feature and API inventory
- `DashboardReportType`, `DashboardDateRange`, Dashboard hooks, screen, tests, and navigation call site
- Mobile `shared/api/endpoints/Reports/{types.ts,endpoints.ts,index.ts}` and process-flow endpoint tests
- Reports service README, domain entities, execution/template DTOs, endpoints, services, repositories, and persistence implementations
- Reports gateway routes
- Web Insights report catalog/viewer routes and reports client/service/types (read-only semantic evidence)
- Web Liens report routes/client types (read-only semantic evidence)
- Mobile Spinner/EmptyState/detail conventions, package/Jest/TypeScript configuration, and README

## 28. Files Added

- `analysis/DL-APP-007-report.md`.

## 29. Files Modified

None.

## 30. Files Deleted

None.

## 31. Implementation Progress

- Created and populated the required report before Mobile feature/navigation/service/test/documentation changes.
- Confirmed the APP-004 controlled gap and APP-006 comparison.
- Identified multiple distinct persisted Report domains and identifier contracts.
- Proved Dashboard type/date-range reporting has no report ID mapping.
- Found no authoritative evidence selecting one candidate for shared `reportDetails`.
- Stopped before implementation as required.

## 32. Tests Added

None. No production behavior was changed. Existing APP-004 tests cover nonblank Report as `destination_unavailable` and missing Report ID as `invalid_parameters`.

## 33. Validation Commands and Results

- `pnpm --dir apps/mobile exec prettier --write ../../analysis/DL-APP-007-report.md` from repository root: passed and formatted this report; no rerun required.
- `pnpm --dir apps/mobile exec jest --runInBand src/navigation/DeepLinkNavigation/DeepLinkNavigationMapper.test.ts` from repository root: passed 1 suite / 9 tests, including Report destination-unavailable and missing-ID behavior; no failure or rerun required.
- `git diff --check` from repository root: passed; no whitespace errors.
- `python3 scripts/check-doc-sync.py` from repository root: passed (`no doc-sensitive changes detected`).
- `git status --short` / `git diff --name-only` from repository root: confirms only this untracked report; no Mobile, Backend, Web, or shared-contract change.
- Mobile type-check, lint, and Expo export were not run because no Mobile source/config/test/documentation file changed; they cannot validate the missing semantic contract.
- Live Backend/resource authorization and physical-device/E2E validation could not run without an authoritative resource mapping and are not claimed.

## 34. Report Domain Validation

Blocked for the shared route. Multiple valid models exist, but no evidence selects one as `reportDetails`.

## 35. Dashboard Report Validation

Complete: source inspection confirms Dashboard reports require `DashboardReportType` plus `DashboardDateRange`, have no stable report ID, and cannot consume the shared route parameter.

## 36. Report Data Validation

Blocked at semantic selection. Candidate APIs and DTOs exist but are mutually distinct and cannot be chosen safely.

## 37. Report Screen Validation

Blocked because no honest destination/data contract can be implemented.

## 38. Report Navigation Validation

Existing controlled APP-004 behavior remains appropriate. No typed report-by-ID destination exists to validate.

## 39. Deep-Link Mapping Validation

Focused mapper regression passed all 9 tests, including nonblank Report as `destination_unavailable`, missing Report ID as `invalid_parameters`, and unchanged Dashboard/Contact/Application/Deal behavior.

## 40. Boundary / Scope Validation

Only this analysis report is added. No Mobile feature, navigation, API, URL parsing, auth, Backend, Web, persistence, shared-route, Dashboard, Deal, or Application implementation is changed.

## 41. Acceptance-Criteria Status

| Criterion | Status             | Evidence                                                                                                 |
| --------- | ------------------ | -------------------------------------------------------------------------------------------------------- |
| AC-001    | Blocked            | Several real Report resources exist, but the shared route does not identify which one it means.          |
| AC-002    | Complete           | Dashboard type/date-range flow was explicitly assessed and not substituted.                              |
| AC-003    | Blocked            | Candidate IDs conflict; no authoritative shared `reportId` contract exists.                              |
| AC-004    | Blocked            | A destination cannot be selected without inventing semantics.                                            |
| AC-005    | Blocked            | The correct route parameter contract depends on the unresolved resource.                                 |
| AC-006    | Not applicable     | No route is safely addable.                                                                              |
| AC-007    | Blocked            | Several existing data layers exist; ownership selection is missing.                                      |
| AC-008    | Complete           | No duplicate/direct API layer was introduced.                                                            |
| AC-009    | Blocked            | No safe screen exists.                                                                                   |
| AC-010    | Blocked            | No single supported response/field set is established.                                                   |
| AC-011    | Blocked            | Screen service outcomes depend on the selected contract.                                                 |
| AC-012    | Blocked            | APP-004 cannot map to a nonexistent unambiguous destination.                                             |
| AC-013    | Blocked            | Forwarding type/meaning is unresolved.                                                                   |
| AC-014    | Complete           | Existing missing/blank-ID controlled failure remains unchanged.                                          |
| AC-015    | Complete           | No raw URL parsing or route matching was added.                                                          |
| AC-016    | Complete           | No auth continuation or pending-login behavior was added.                                                |
| AC-017    | Complete           | APP-004 remains navigation-only and unchanged.                                                           |
| AC-018    | Complete           | No Backend implementation was added.                                                                     |
| AC-019    | Complete           | No `apps/web/**` file was modified.                                                                      |
| AC-020    | Complete           | Shared route registry remains unchanged.                                                                 |
| AC-021    | Partially complete | Existing mapping failure behavior will receive focused regression validation; feature tests are blocked. |
| AC-022    | Blocked            | Mobile docs cannot claim a semantic mapping that Product has not defined; this report documents the gap. |
| AC-023    | Complete           | The gap is marked Blocked and no feature, fields, endpoint mapping, or tests were fabricated.            |
| AC-024    | Complete           | No device, Backend-resource, authorization, build, or E2E success is claimed.                            |

## 42. Issues and Failures

- `rg` is unavailable in this environment, so repository searches used `find` and `grep`.
- Naming alone is misleading: several unrelated screens/routes/resources contain “report” and accept different IDs.

## 43. Blockers and External Dependencies

Product/architecture must define what `/reports/:reportId` identifies. That decision must include owning product/service, resource kind, identifier format, user-facing read contract, and expected authorization/error semantics. If Reports execution is selected, Product must also confirm whether a summary-only destination satisfies “details” or whether Backend must expose persisted result rows.

## 44. Security Review

No access-control logic was added. Candidate APIs have materially different policies: template management is admin-oriented, executions require SynqInsights product/ReportsView permission and tenant scoping, and Liens saved reports belong to a different product boundary. Arbitrarily selecting one risks authorization confusion or cross-product IDOR assumptions; Backend must remain authoritative after Product selects the resource.

## 45. Architecture Risks and Concerns

Encoding an implicit resource choice in Mobile would make the shared route contract diverge across Web/Mobile and could send the same URL to unrelated data. A resource discriminator or explicit documented ownership may be necessary before downstream clients implement it.

## 46. Known Gaps

- Shared route lacks resource/product ownership metadata.
- No generic Mobile report-by-ID viewer exists.
- Reports execution GET returns summary rather than persisted result rows.
- Mobile Liens saved-report DTO contains both `id` and `reportId`, whose distinction is not tied to the shared route.
- No physical-device or live Backend test was run.

## 47. Backend / Product Handoff

Decide one authoritative target, for example (without presuming the answer): Reports template, Reports execution, or Liens saved report. Document the canonical ID and endpoint. If execution details must include tabular results, assess a separate Backend read-contract enhancement because current GET returns only summary metadata.

## 48. DL-QA-001 Handoff

Not ready for positive reportDetails E2E. QA can retain negative coverage for missing ID and controlled destination-unavailable. Positive resource/navigation scenarios require the Product/Backend contract decision and a subsequent Mobile implementation ticket/update.

## 49. Out-of-Scope Confirmation

No Deal Details, Application change, report generation/export redesign, Dashboard redesign, URL parsing, auth continuation, Backend/database/Web, campaign, notification, analytics-tracking, or shared-route implementation was performed.

## 50. Follow-Up Recommendations

Create a Product/architecture decision record that names the `reportDetails` resource and ID. Then either implement Mobile against an existing read contract or create a narrowly scoped Backend ticket first. Consider adding explicit product/resource metadata to a future shared-contract version rather than relying on generic `reportId` naming.

## 51. Final Status

Blocked. Real report resources and read APIs exist, but shared `reportDetails` semantics and its identifier ownership remain unresolved. No destination or mapping was fabricated.
