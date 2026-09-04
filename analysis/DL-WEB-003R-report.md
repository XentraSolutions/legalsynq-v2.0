# DL-WEB-003R Implementation Report

## 1. Ticket Summary

Remove explicit Application Details Open-in-App presentation while preserving Application behavior and deep-link infrastructure.

## 2. Objective

Return the canonical Application Details Web experience to association-driven handoff with no explicit Open-in-App action.

## 3. Scope

Application-specific UI/wrapper/test cleanup, appropriate header restoration, focused validation, and this report only.

## 4. Initial Rollback Plan

1. Verify current Application page, detail panel, wrapper/tests, shared presentation dependency, and prior rollback direction.
2. Inventory all Application-specific Open-in-App references and compare the pre-DL-WEB-003 header.
3. Remove only Application presentation code/tests and restore the smallest appropriate header.
4. Preserve status, identity, loading/error/auth states, and role/status workflow actions.
5. Run focused Application/workflow and builder regressions, TypeScript/build/broad tests, source/boundary/whitespace checks, documentation sync, and independent review.

## 5. Current Branch and Working-Tree State

- Branch: `feat(app)-LSV3-1193-Implement-Web-to-Mobile-Deep-Link`
- HEAD: `96d9e1f874b9bf28fe69d0f3b59d50b7f5a0a539`
- Initial working tree: clean
- This report is the first DL-WEB-003R repository change.

## 6. Repository Instruction Review

Reviewed the root `AGENTS.md`, repository boundaries, frontend conventions, and validation expectations. The delivery-modes implementation workflow is active.

## 7. DL-WEB-003 Current Implementation Review

DL-WEB-003 added an Application-only adapter and placed it beside the status badge in `FundingApplicationDetailPanel`. It also changed the header layout to stack/wrap responsively around that new action.

## 8. DL-WEB-007 Shared Presentation Review

The shared `OpenInAppLink` remains a live cross-surface primitive and is still consumed by Dashboard. It is outside this rollback.

## 9. DL-WEB-002R Rollback Direction Review

DL-WEB-002R removed only Contact-specific presentation while preserving the shared registry/builders and sibling surfaces. DL-WEB-003R follows that same narrow rollback boundary for Application.

## 10. Canonical Application Surface Review

The canonical route is `apps/web/src/app/(platform)/fund/applications/[id]/page.tsx`. It loads through `fundApi.applications.getById`, handles session and API states, renders the detail panel, and selects workflow panels by role/status.

## 11. Application ID Contract Review

The route ID is passed to the existing Application API; the loaded model's `application.id` currently feeds the Application-only wrapper. No route/model/API contract needs removal.

## 12. Application Loading / Error / Not-Found Review

The existing skeleton, login redirect, 403 access message, 404 not-found message, generic error surface, and null-data behavior are independent of the header action and must remain unchanged.

## 13. Current Application Header Review

The current header contains the Application identity on the left and a responsive action/status group on the right. The group exists to accommodate `ApplicationOpenInAppLink`.

## 14. Target Application Header Structure

Restore the pre-DL-WEB-003 structure from `cd821bb07`: `flex items-start justify-between gap-4`, a plain identity container, and a standalone status badge.

## 15. Application Workflow Review

Draft referrer applications render `SubmitApplicationPanel`; submitted/in-review funder applications render `ReviewDecisionPanel`; terminal referrer states render the existing read-only note. These gates remain untouched.

## 16. Application Open-in-App Dependency Inventory

- `apps/web/src/components/fund/application-open-in-app-link.tsx`: Application-only wrapper.
- `apps/web/src/components/fund/application-open-in-app-link.test.tsx`: wrapper-only tests.
- `apps/web/src/components/fund/funding-application-detail-panel.tsx`: wrapper import/render and action-driven header layout.
- Panel/page tests: deep-link assertions that should become non-action/state/workflow regressions.

## 17. Implementation Changes

- Created this report before any other DL-WEB-003R change.
- Removed the Application-only action from `FundingApplicationDetailPanel`.
- Deleted the now-unused Application wrapper and its deep-link-only test.
- Restored the exact pre-DL-WEB-003 header structure.
- Reworked retained tests into Application content, state, authorization, and workflow regressions.

## 18. Application Action Removal

Complete. Loaded Application Details exposes no accessible action/link named `Open in App`.

## 19. Application Wrapper Cleanup

Deleted `application-open-in-app-link.tsx`; its only consumer was the detail panel and its only responsibility was deep-link presentation.

## 20. Header / Responsive Restoration

Restored the exact header markup/classes from `cd821bb07`: identity content and the existing status badge in `flex items-start justify-between gap-4`. Removed action-only stacking, wrapping, full-width, and shrink classes.

## 21. Application Test Cleanup

Deleted the wrapper-only suite. Retained panel/page tests now assert Application content, status, no action, loading, 401 redirect, 403, 404, route-ID loading, referrer submit, and funder review behavior.

## 22. Accessibility Review

Remaining identity text/headings and the status badge are unchanged. Existing workflow components remain responsible for keyboard semantics. Source inspection found no orphaned action aria/focus/icon code.

## 23. Files Inspected

- User-provided DL-WEB-003R specification
- Root `AGENTS.md` and delivery-mode instructions
- DL-WEB-003, DL-WEB-007, and DL-WEB-002R reports
- Application page, panel, wrapper, tests, workflows, auth/session, API/model types
- Shared `OpenInAppLink`, Dashboard consumer, package scripts, and repository history

## 24. Files Added

- `analysis/DL-WEB-003R-report.md`

## 25. Files Modified

- `apps/web/src/app/(platform)/fund/applications/[id]/page.test.tsx`
- `apps/web/src/components/fund/funding-application-detail-panel.test.tsx`
- `apps/web/src/components/fund/funding-application-detail-panel.tsx`

## 26. Files Deleted

- `apps/web/src/components/fund/application-open-in-app-link.tsx`
- `apps/web/src/components/fund/application-open-in-app-link.test.tsx`

## 27. Implementation Progress

| Area | Status | Completion |
|---|---|---:|
| Current Application review | Done | 100% |
| UI removal | Done | 100% |
| Wrapper cleanup | Done | 100% |
| Header restoration | Done | 100% |
| Test cleanup | Done | 100% |
| Application regression | Done | 100% |
| Workflow preservation | Done | 100% |
| Source guards | Done | 100% |
| Type/build validation | Done | 100% |
| Boundary checks | Done | 100% |

## 28. Validation Commands and Results

- `../../node_modules/.bin/vitest run src/components/fund/funding-application-detail-panel.test.tsx 'src/app/(platform)/fund/applications/[id]/page.test.tsx'`: pass, 2 files / 8 tests.
- `../../node_modules/.bin/tsx --test src/lib/__tests__/deep-links.test.ts`: pass, 9 tests (rerun outside sandbox after expected IPC `EPERM`).
- `./node_modules/.bin/tsc --noEmit -p apps/web/tsconfig.json`: pass.
- `../../node_modules/.bin/vitest run`: 71 files / 332 tests pass; 3 files / 23 tests fail on the established unrelated baseline.
- `../../node_modules/.bin/next build`: Application compilation was not implicated; build stops on the unrelated re-exported `dynamic` field in `src/app/careconnect/referral/layout.tsx`.
- Source guard, boundary diff, historical header comparison, and `git diff --check`: pass.
- Independent read-only reviewer: approved with no blocking findings; noted only low-severity test-selector brittleness and the intentionally class-based responsive verification.

## 29. Application Regression Validation

APP-001 through APP-006 and APP-008 through APP-010 are covered by the focused panel/page tests and source inspection. All focused tests pass.

## 30. Workflow Regression Validation

Draft referrer submit and in-review funder review gates are explicitly covered and pass. Production workflow selection code was not changed; approve/deny remain owned by the untouched review component.

## 31. DL-WEB-001 Regression Validation

The unchanged authoritative builder suite passes all 9 tests, including `applicationDetails` route generation.

## 32. Typecheck Validation

Pass: full tenant-portal TypeScript check completed with no diagnostics.

## 33. Web Build Validation

Partially complete: the production build executed but is blocked by an unrelated existing Next.js route-config re-export in `careconnect/referral/layout.tsx`. No changed Application file appears in the build error.

## 34. Documentation Validation

Reviewed root and relevant Web documentation. No product shape, startup behavior, ports, service boundaries, commands, runtime configuration, or public contract changed, so no README/AGENTS update is warranted. `python3 scripts/check-doc-sync.py` reports the changed route test as doc-sensitive because this new report is untracked and therefore not recognized by its tracked-diff check; this report records the completed documentation review.

## 35. Application Source Guard

Pass. Application production files contain none of the guarded Open-in-App labels, imports, builder/config references, error types, or action-only icon references.

## 36. Boundary / Scope Validation

Pass. The scoped diff contains only three Application test/panel modifications, two Application wrapper deletions, and this report. Contact, Dashboard, shared presentation, Mobile, Gateway/services, and shared registry have no diff.

## 37. Acceptance-Criteria Status

| Criterion | Status |
|---|---|
| AC-001 — Application Open-in-App UI Removed | Complete |
| AC-002 — Application Wrapper Removed or Unused | Complete |
| AC-003 — Application No Longer Invokes DL-WEB-001 for Presentation | Complete |
| AC-004 — Application No Longer Depends on Shared OpenInAppLink | Complete |
| AC-005 — Application Route Preserved | Complete |
| AC-006 — Canonical Application ID Preserved | Complete |
| AC-007 — Loading Behavior Preserved | Complete |
| AC-008 — 404 Behavior Preserved | Complete |
| AC-009 — Authorization/Login Behavior Preserved | Complete |
| AC-010 — Status Presentation Preserved | Complete |
| AC-011 — Workflow Actions Preserved | Complete |
| AC-012 — Header Layout Restored Appropriately | Complete |
| AC-013 — Responsive Layout Valid | Complete |
| AC-014 — Accessibility Preserved | Complete |
| AC-015 — No Native-App Detection Added | Complete |
| AC-016 — No Custom Scheme Added | Complete |
| AC-017 — No Store Fallback Added | Complete |
| AC-018 — No Host Fallback Added | Complete |
| AC-019 — DL-WEB-001 Preserved | Complete |
| AC-020 — Shared Registry Preserved | Complete |
| AC-021 — Application Tests Updated | Complete |
| AC-022 — Focused Application Regressions Pass | Complete |
| AC-023 — DL-WEB-001 Regression Passes | Complete |
| AC-024 — Typecheck Passes | Complete |
| AC-025 — Web Build Passes | Partially complete |
| AC-026 — Application Source Guard Passes | Complete |
| AC-027 — Contact Unchanged | Complete |
| AC-028 — Dashboard Unchanged | Complete |
| AC-029 — No Mobile Changes | Complete |
| AC-030 — No Backend Changes | Complete |
| AC-031 — No Shared Route Changes | Complete |
| AC-032 — Honest Validation | Complete |

## 38. Issues and Failures

- The package-level `pnpm test` command cannot resolve `tsx` in the current installation; its two constituent runners were invoked directly.
- Broad Vitest retains 23 unrelated failures in three files: `funding-notifications.test.tsx`, `public-network-view.test.tsx`, and `thread-client.test.tsx`.
- The production build retains an unrelated Next.js error in `src/app/careconnect/referral/layout.tsx` because route config `dynamic` is re-exported.
- Independent review found no correctness, security, architecture, or regression blockers.

## 39. Blockers and External Dependencies

Platform association configuration and physical-device QA remain outside this rollback.

## 40. Security Review

No authorization, API, navigation, external URL, or client-side handoff behavior was added. Removing the explicit link reduces the Application surface that can initiate cross-context navigation.

## 41. Architecture Risks and Concerns

The primary risks were removing shared presentation still required by Dashboard or altering Application workflow/state behavior. Boundary inspection confirms Dashboard remains the shared component consumer, and focused workflow regressions pass.

## 42. Known Gaps

Physical-device association behavior was not tested or claimed. Broad-suite and build baseline failures remain outside this ticket.

## 43. DL-WEB-004R Handoff

Dashboard UI removal is outside this ticket.

## 44. DL-WEB-007R Handoff

Shared presentation cleanup is outside this ticket while Dashboard consumes it.

## 45. Out-of-Scope Confirmation

No out-of-scope implementation performed. Contact, Dashboard, shared deep-link/presentation infrastructure, Mobile, Gateway/backend, APIs/models/routes, and registry semantics remain unchanged.

## 46. Final Status

Complete — Application Open-in-App UI Removed
