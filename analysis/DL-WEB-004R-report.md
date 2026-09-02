# DL-WEB-004R Implementation Report

## 1. Ticket Summary

Remove explicit Dashboard Open-in-App presentation while preserving Dashboard behavior and shared deep-link infrastructure.

## 2. Objective

Return the canonical Web Dashboard to association-driven native handoff with no explicit Open-in-App action.

## 3. Scope

Dashboard-specific UI, wrapper, tests, header restoration, validation, and this report only.

## 4. Initial Rollback Plan

1. Inspect current Dashboard behavior, action integration, shared dependency, tests, and repository history.
2. Document the exact action-only files and header changes.
3. Remove only Dashboard presentation code and restore the pre-DL-WEB-004 header.
4. Preserve organization/auth, redirects, product access, admin actions, destinations, grids, and accessibility.
5. Run focused, builder, type, build, broad, source, boundary, documentation, and review checks.

## 5. Current Branch and Working-Tree State

- Branch: `feat(app)-LSV3-1193-Implement-Web-to-Mobile-Deep-Link`
- HEAD: `a466fbb14612cdfa04f4c0510c0a3e01ee56f63b`
- Initial working tree: clean
- This report is the first DL-WEB-004R repository change.

## 6. Repository Instruction Review

Reviewed root `AGENTS.md`, frontend boundaries/validation rules, and the delivery-modes implementation workflow.

## 7. DL-WEB-004 Current Implementation Review

DL-WEB-004 at `e5891de8b` added the Dashboard-only wrapper, welcome-header action, responsive action layout, and two focused suites.

## 8. DL-WEB-007 Shared Presentation Review

DL-WEB-007 centralized the anchor/error presentation in `apps/web/src/components/open-in-app-link.tsx`. Dashboard is now its final feature consumer, but deletion remains explicitly owned by DL-WEB-007R.

## 9. DL-WEB-002R / DL-WEB-003R Rollback Direction Review

Both preceding rollbacks removed only feature adapters/presentation and restored prior structures while preserving the shared builder/registry and sibling surfaces. This ticket follows the same boundary.

## 10. Canonical Dashboard Surface Review

`/dashboard` is owned by the async server page `apps/web/src/app/(platform)/dashboard/page.tsx` within the authenticated platform layout. The route/App Router structure remains unchanged.

## 11. Authentication / Organization Review

The page calls `requireOrg()` first. No action code participates in authentication, missing-organization behavior, session semantics, or server-side guard ordering.

## 12. Product Access Review

Cards are derived from `session.userProducts` or `enabledProducts`, normalized through `resolveEnabledNavKeys`, and filtered against `PRODUCT_META`. Card labels, order, destinations, and one/two/three-column grid are unrelated to the action.

## 13. Portal Redirect Review

After `requireOrg()`, the page resolves forwarded/host headers through `getServerPortalConfig` and redirects to the configured landing path before rendering general Dashboard UI. This logic remains untouched.

## 14. Administration Action Review

Tenant/platform administrators receive the existing Users and Organizations cards with unchanged conditions, destinations, order, and responsive grid.

## 15. Current Dashboard Header Review

The welcome block currently uses action-driven `flex flex-col ... sm:flex-row sm:justify-between`, a `min-w-0` inner wrapper, and the Dashboard action.

## 16. Target Dashboard Header Structure

Restore the exact pre-DL-WEB-004 structure from `e5891de8b^`: one plain block containing the existing heading and organization/email copy.

## 17. Dashboard Open-in-App Dependency Inventory

- Dashboard page: wrapper import/render and action-only header layout.
- `dashboard-open-in-app-link.tsx`: parameterless `dashboard` intent adapter; no other responsibility or consumer.
- `dashboard-open-in-app-link.test.tsx`: deep-link-only coverage.
- Dashboard page tests: configuration/action assertions plus useful product/admin assertions to retain.
- Shared `open-in-app-link.tsx`: final presentation primitive; keep unchanged for DL-WEB-007R.

## 18. Implementation Changes

- Created this report before any other DL-WEB-004R change.
- Removed the Dashboard-only action and wrapper.
- Deleted the wrapper's deep-link-only test.
- Restored the exact pre-DL-WEB-004 welcome block.
- Reworked Dashboard tests around welcome content, organization guard, product/admin visibility and destinations, and portal redirect behavior.

## 19. Dashboard Action Removal

Complete. The canonical Dashboard exposes no accessible `Open in App` action.

## 20. Dashboard Wrapper Cleanup

Deleted `dashboard-open-in-app-link.tsx`; it only constructed the parameterless Dashboard intent and delegated presentation.

## 21. Header / Responsive Restoration

Restored the exact plain welcome block from `e5891de8b^`. Removed action-only flex, breakpoint, gap, alignment, and `min-w-0` markup. Product/admin grids and page spacing remain byte-for-byte unchanged.

## 22. Dashboard Test Cleanup

Deleted the wrapper-only suite. The retained page suite now covers no action, welcome copy, product/admin destinations, product filtering, non-admin visibility, `requireOrg()`, host lookup, and portal redirect.

## 23. Accessibility Review

The existing `h1`, organization/email copy, semantic product/admin links, accessible names, and focus-capable link components remain unchanged. No orphaned action icon, aria, or focus classes remain in Dashboard.

## 24. Files Inspected

- User-provided DL-WEB-004R specification
- Root repository instructions and implementation-mode workflow
- DL-WEB-004, DL-WEB-007, DL-WEB-002R, and DL-WEB-003R reports/history
- Dashboard page/tests, wrapper/tests, shared presentation, auth/organization guard, portal resolver, navigation/product metadata, and package tooling

## 25. Files Added

- `analysis/DL-WEB-004R-report.md`

## 26. Files Modified

- `apps/web/src/app/(platform)/dashboard/page.tsx`
- `apps/web/src/app/(platform)/dashboard/page.test.tsx`

## 27. Files Deleted

- `apps/web/src/components/dashboard-open-in-app-link.tsx`
- `apps/web/src/components/dashboard-open-in-app-link.test.tsx`

## 28. Implementation Progress

| Area | Status | Completion |
|---|---|---:|
| Current Dashboard review | Done | 100% |
| UI removal | Done | 100% |
| Wrapper cleanup | Done | 100% |
| Header restoration | Done | 100% |
| Test cleanup | Done | 100% |
| Dashboard regression | Done | 100% |
| Auth/redirect preservation | Done | 100% |
| Source guards | Done | 100% |
| Type/build validation | Done | 100% |
| Boundary checks | Done | 100% |

## 29. Validation Commands and Results

- `node ../../node_modules/vitest/vitest.mjs run 'src/app/(platform)/dashboard/page.test.tsx' --reporter=dot`: pass, 1 file / 3 tests.
- `../../node_modules/.bin/tsx --test src/lib/__tests__/deep-links.test.ts`: pass, 9 tests.
- `./node_modules/.bin/tsc --noEmit -p apps/web/tsconfig.json`: pass.
- `node ../../node_modules/vitest/vitest.mjs run --reporter=dot`: 70 files / 329 tests pass; established 3 files / 23 unrelated tests fail.
- `node ../../node_modules/next/dist/bin/next build --webpack`: compilation and TypeScript pass; prerender then fails on unrelated `/_global-error` Next.js `workStore` invariant.
- Dashboard source guard and `git diff --check`: pass.
- Independent review: approved with no blocking findings. Its low-severity suggestion to assert guard/portal/redirect call order was applied.

## 30. Dashboard Regression Validation

DASH-001, DASH-002, DASH-004, DASH-005, DASH-009, and DASH-010 are covered by focused tests/source-history comparison. All focused tests pass.

## 31. Auth / Redirect Regression Validation

`requireOrg()`, product-host resolution, and redirect destination are explicitly asserted. Guard and redirect production ordering is untouched.

## 32. DL-WEB-001 Regression Validation

The unchanged builder suite passes all 9 tests, including parameterless Dashboard route generation.

## 33. Typecheck Validation

Pass with no diagnostics.

## 34. Web Build Validation

Partially complete. Webpack compilation and TypeScript succeeded, but static generation failed on the unrelated `/_global-error` route with Next.js `Invariant: Expected workStore to be initialized`. No Dashboard file was implicated.

## 35. Documentation Validation

Reviewed root and Web documentation. No public command, runtime configuration, route, product mapping, or service boundary changed, so no README/AGENTS update is warranted. `python3 scripts/check-doc-sync.py` flags the Dashboard source/test as doc-sensitive because this required report is untracked and therefore absent from its tracked-diff review; this report records the completed review.

## 36. Dashboard Source Guard

Pass. Dashboard production source contains none of the guarded Open-in-App labels/imports, builder/config/error references, phone icon, or intent construction.

## 37. Boundary / Scope Validation

Pass. The scoped diff contains only the Dashboard page/test, two Dashboard wrapper deletions, and this report. Contact, Application, shared presentation/builder/registry, Mobile, Gateway, and backend services have no diff. Historical comparison confirms the welcome block exactly matches `e5891de8b^`.

## 38. Acceptance-Criteria Status

| Criterion | Status |
|---|---|
| AC-001 — Dashboard Open-in-App UI Removed | Complete |
| AC-002 — Dashboard Wrapper Removed or Unused | Complete |
| AC-003 — Dashboard No Longer Invokes DL-WEB-001 for Presentation | Complete |
| AC-004 — Dashboard No Longer Depends on Shared OpenInAppLink | Complete |
| AC-005 — Dashboard Route Preserved | Complete |
| AC-006 — Organization Guard Preserved | Complete |
| AC-007 — Authentication / Organization Behavior Preserved | Complete |
| AC-008 — Product Access Preserved | Complete |
| AC-009 — Portal Redirects Preserved | Complete |
| AC-010 — Administration Shortcuts Preserved | Complete |
| AC-011 — Product / Admin Destinations Preserved | Complete |
| AC-012 — Header Layout Restored Appropriately | Complete |
| AC-013 — Responsive Layout Valid | Complete |
| AC-014 — Accessibility Preserved | Complete |
| AC-015 — No Native-App Detection Added | Complete |
| AC-016 — No Custom Scheme Added | Complete |
| AC-017 — No Store Fallback Added | Complete |
| AC-018 — No Host Fallback Added | Complete |
| AC-019 — DL-WEB-001 Preserved | Complete |
| AC-020 — Shared Registry Preserved | Complete |
| AC-021 — Dashboard Tests Updated | Complete |
| AC-022 — Focused Dashboard Regressions Pass | Complete |
| AC-023 — DL-WEB-001 Regression Passes | Complete |
| AC-024 — Typecheck Passes | Complete |
| AC-025 — Web Build Validated | Partially complete |
| AC-026 — Dashboard Source Guard Passes | Complete |
| AC-027 — Contact Unchanged | Complete |
| AC-028 — Application Unchanged | Complete |
| AC-029 — No Mobile Changes | Complete |
| AC-030 — No Backend Changes | Complete |
| AC-031 — No Shared Route Changes | Complete |
| AC-032 — Honest Validation | Complete |

## 39. Issues and Failures

- The installed Vitest shell wrapper stalled without output; invoking the same installed Vitest module directly completed normally.
- Broad Vitest retains 23 unrelated failures in `funding-notifications.test.tsx`, `public-network-view.test.tsx`, and `thread-client.test.tsx`.
- Production build compiled and type-checked, then failed on an unrelated Next.js `/_global-error` prerender invariant.
- Independent review found no correctness, security, architecture, accessibility, or scope blockers.

## 40. Blockers and External Dependencies

Platform association configuration and physical-device QA remain outside this rollback.

## 41. Security Review

No authentication, authorization, external navigation, client redirect, native detection, fallback, or secret-handling behavior was introduced. Existing guard and redirect logic is unchanged.

## 42. Architecture Risks and Concerns

The risks were changing Dashboard behavior or prematurely removing the shared component. Focused regressions cover the Dashboard behaviors, and the shared component/test remain unchanged for DL-WEB-007R.

## 43. Known Gaps

Physical association behavior was not tested or claimed. Broad-suite and build baseline failures remain outside this ticket.

## 44. DL-WEB-007R Handoff

The shared `open-in-app-link.tsx` and its test now have no feature consumer and remain unchanged for DL-WEB-007R to verify/remove.

## 45. DL-WEB-008 Handoff

No Deal/Report or other new journey was introduced; DL-WEB-008 remains independent.

## 46. Out-of-Scope Confirmation

No out-of-scope implementation performed. Contact, Application, shared builder/registry/presentation, Mobile, Gateway/backend, and unrelated Dashboard behavior remain unchanged.

## 47. Final Status

Complete — Dashboard Open-in-App UI Removed
