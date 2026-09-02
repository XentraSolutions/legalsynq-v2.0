# DL-WEB-003 Implementation Report

## 1. Ticket Summary

Add an Application Details `Open in App` action using the committed DL-WEB-001 deep-link builder.

## 2. Objective

Provide a safe, accessible HTTPS handoff from the canonical Application Details experience without placing route semantics or host selection in Application feature code.

## 3. Scope

Application Web UI integration, focused tests, validation, and this report only. Other journeys, Mobile, Gateway, backend services, shared route semantics, database, release infrastructure, native detection, fallbacks, and physical-device validation are excluded.

## 4. Initial Implementation Plan

1. Verify DL-WEB-001, review DL-WEB-002 for presentation reuse, and inspect the shared Application route.
2. Identify the canonical Application Details surface, ID source, states, permissions, actions, responsive behavior, and tests.
3. Document placement and unavailable-state decisions before modifying Application code.
4. Integrate the builder through the smallest established action surface and add focused UI/state/regression tests.
5. Run builder regression tests, full TypeScript/build where available, documentation/source/boundary checks, and independent review.

## 5. Current Branch and Working-Tree State

- Branch: `feat(app)-LSV3-1193-Implement-Web-to-Mobile-Deep-Link`
- HEAD: `cd821bb0739523377c0a309f25b4d47ec5d0efaf`
- Initial working tree: clean
- This report was created before any other DL-WEB-003 repository change.

## 6. Repository Instruction Review

Root `AGENTS.md` and project delivery-mode implementation instructions govern this task. Scoped Application instructions have not yet been identified.

## 7. DL-WEB-001 Dependency Review

Complete at current HEAD. `apps/web/src/lib/deep-links.ts` exports route-intent `buildDeepLink`, reads only `NEXT_PUBLIC_DEEP_LINK_BASE_URL`, requires an HTTPS origin, exposes typed `DeepLinkError`, and delegates route semantics/encoding to the authoritative shared registry. Its focused tests cover the real registry and Application URL.

## 8. DL-WEB-002 Reuse Review

DL-WEB-002 is committed and establishes a useful presentation convention: a semantic same-context anchor, hidden for blank IDs or expected typed deep-link errors, with unexpected errors rethrown. Its component is Contact-specific (`contactId` and `contactDetails`) and will not be reused or generalized by this Application-only ticket.

## 9. Shared Application Route Review

The enabled `applicationDetails` route uses `pathTemplate: "/applications/:applicationId"` and declares exactly `applicationId`. Application code will use only the route key and parameter name, never the template.

## 10. Application Feature Architecture Review

The canonical authenticated SynqFund surface is the client page `apps/web/src/app/(platform)/fund/applications/[id]/page.tsx`. It obtains the route ID with `useParams`, waits for session state, loads through `fundApi.applications.getById`, stores `FundingApplicationDetail`, and renders `FundingApplicationDetailPanel` plus status/role-specific workflow panels.

## 11. Canonical Application Details Surface

Route `/fund/applications/[id]`, owned by `ApplicationDetailPage`; `FundingApplicationDetailPanel` owns the persistent record header containing application number, applicant identity, and status badge.

## 12. Application ID Contract

Use loaded `application.id`, the canonical `FundingApplicationDetail` identifier also used by submit/review API mutations. The route parameter is only the lookup input; the action becomes available from the successfully loaded model identity. The contract is string/GUID-shaped and no new parsing or transformation is needed.

## 13. Application Loading / Error / Not-Found Review

While session/application loading is active, the page renders a skeleton and no detail panel. API errors render the existing red error message; 404 becomes `Application not found.`, 403 becomes the access message, and unauthenticated/401 states redirect to login. A null application after loading renders `null`. Therefore placing the action inside the detail panel automatically excludes all loading/error/not-found/unavailable states.

## 14. Existing Application Action Patterns

The detail header has an established right-side status surface. Workflow actions remain separate below the detail card: referrers can submit Draft applications and funders can begin review/approve/deny according to role/status. Terminal applications show a read-only note.

## 15. Existing Authorization / Permission Boundary

No new authorization gate is needed. The action follows successful Application Details availability; existing session/role behavior stays unchanged and Backend/Mobile remain authoritative after navigation.

## 16. Existing Accessibility / Responsive Conventions

The detail header uses a flex layout with a right-side status badge, visible text, standard focus utilities, and responsive content grids below (`sm`/`lg`). A compact semantic anchor grouped with the status badge preserves the established header and avoids a new panel.

## 17. Existing Application Test Review

No Application-specific Web tests currently exist. Vitest/jsdom/Testing Library are the current component conventions. The detail panel is sufficiently pure for a focused integration test, while a small Application-owned link component can verify builder intent, canonical/encoded href, error handling, accessibility, and missing-ID behavior.

## 18. Open-in-App Placement Decision

Place `Open in App` in the existing `FundingApplicationDetailPanel` header beside the status badge. It is record-level navigation, not a workflow mutation, so it remains available across valid statuses without changing role gates or displacing submit/review controls.

## 19. Configuration-Unavailable UX Decision

Hide the action on typed builder/configuration failure. Do not expose a broken href, fallback host, or configuration error; preserve the rest of the detail panel.

## 20. Missing-Application-ID UX Decision

Hide the action for absent or whitespace-only `application.id` and do not call the builder. The loaded model normally guarantees the ID; this is defensive UI containment.

## 21. Loading-State UX Decision

No action is rendered during loading because the detail panel does not exist until a loaded Application model is available. Existing error, not-found, access-denied, redirect, and null states remain untouched.

## 22. Intended Integration Architecture

Application Details action → DL-WEB-001 builder → shared `applicationDetails` route → configured HTTPS base URL → standard browser/OS navigation.

## 23. Implementation Changes

- Created this report as the first DL-WEB-003 change.
- Added an Application-owned `ApplicationOpenInAppLink` wrapper.
- Added the action to the existing detail-header action/status group.
- Added component, detail-panel integration, and page-state regression tests.

## 24. Builder Integration

`ApplicationOpenInAppLink` invokes `buildDeepLink({ routeKey: "applicationDetails", pathParams: { applicationId } })`. It accepts the loaded model ID unchanged, does not access the registry/configuration itself, and catches only the builder's expected `DeepLinkError`; unexpected errors remain visible to error handling rather than being silently swallowed.

## 25. Browser Navigation Behavior

A semantic `<a href>` uses the builder output unchanged in the current browsing context. No `target`, native-app detection, user-agent branching, timer, custom-scheme fallback, or store redirect was added.

## 26. Accessibility Implementation

The anchor has visible `Open in App` text, an icon hidden from assistive technology, keyboard-native link behavior, and the existing visible focus-ring utility convention.

## 27. Responsive / Regression Review

The header now stacks on narrow layouts (`flex-col`) and restores the established horizontal layout at `sm`. Record text can shrink with `min-w-0`; the action/status group is full-width and wrapping on narrow screens, then becomes right-aligned and content-width at `sm`. A focused class assertion protects this breakpoint behavior. Submit/review panels remain untouched below the detail card.

## 28. Files Inspected

- User-provided DL-WEB-003 specification
- Root repository instructions
- Delivery-mode implementation instructions
- `analysis/DL-WEB-001-report.md`
- `analysis/DL-WEB-002-report.md`
- `shared/contracts/deep-links/routes.json`
- `apps/web/src/lib/deep-links.ts` and its tests
- Application routes, API/model types, detail components, action panels, session/permission helpers, package scripts, and test configuration

## 29. Files Added

- `analysis/DL-WEB-003-report.md`
- `apps/web/src/components/fund/application-open-in-app-link.tsx`
- `apps/web/src/components/fund/application-open-in-app-link.test.tsx`
- `apps/web/src/components/fund/funding-application-detail-panel.test.tsx`
- `apps/web/src/app/(platform)/fund/applications/[id]/page.test.tsx`

## 30. Files Modified

- `apps/web/src/components/fund/funding-application-detail-panel.tsx`

## 31. Files Deleted

None.

## 32. Implementation Progress

| Area | Status | Completion |
|---|---|---:|
| Application feature review | Done | 100% |
| Builder dependency | Done | 100% |
| Application ID contract | Done | 100% |
| UI placement | Done | 100% |
| Builder integration | Done | 100% |
| Missing-config behavior | Done | 100% |
| Missing-ID/loading/error behavior | Done | 100% |
| Accessibility | Done | 100% |
| Responsive/regression | Done | 100% |
| Tests | Done | 100% |
| Validation | Done | 100% |
| Boundary checks | Done | 100% |

## 33. Test Changes

- Link tests cover visible label/semantic href, exact route intent, canonical URL, encoded parameter behavior, missing configuration, and undefined/null/empty/blank IDs.
- Detail-panel tests cover established header placement and preservation of Application content when configuration is missing.
- Page tests cover loading, 404, canonical loaded ID use, route lookup behavior, and preservation of the existing referrer Submit action.

## 34. Validation Commands and Results

- Focused Vitest: 3 files, 13 tests passed.
- DL-WEB-001 regression: 9 tests passed.
- Complete Node test phase: 91 tests passed.
- Complete Vitest phase: 70 files/335 tests passed; 3 files/23 unrelated pre-existing CareConnect/referral tests failed (see section 50).
- Direct TypeScript validation: passed.
- Next production build: passed after network-enabled rerun.
- Source, scope, and whitespace checks: passed.

## 35. Application UI Test Validation

Passed: `vitest run` for the Application page, link, and detail-panel suites; 13/13 tests, including the narrow-to-`sm` header layout contract.

## 36. Builder Regression Validation

Passed: `tsx --test src/lib/__tests__/deep-links.test.ts`; 9/9 tests. The complete Node test phase also passed 91/91.

## 37. Configuration-Unavailable Validation

Passed through both the link-level missing-config test and detail-panel preservation test.

## 38. Missing-ID Validation

Passed for undefined, null, empty, and whitespace IDs; the builder is not invoked.

## 39. Loading / Error / Not-Found Validation

Page tests prove loading emits no action and makes no API request, 404 preserves `Application not found.`, and neither state exposes an Open-in-App link. The valid-state test proves the action appears only after the loaded model is available.

## 40. Accessibility Validation

Passed by semantic-role/name assertions; the implementation uses native anchor keyboard behavior, visible text, decorative-icon hiding, and visible focus styles.

## 41. Typecheck Validation

Passed: `./node_modules/.bin/tsc --noEmit -p apps/web/tsconfig.json`.

## 42. Lint Validation

Not applicable: `apps/web/package.json` exposes no lint script and no repository ESLint configuration/binary was identified for this app.

## 43. Formatting Validation

No formatting script or Prettier check is exposed by `apps/web/package.json`. Modified code follows adjacent conventions; `git diff --check` passed.

## 44. Web Build Validation

Passed: `pnpm --dir apps/web build`. The initial sandboxed run failed with `fetch failed`; permission-enabled runs compiled, type-checked, generated all static pages, and exited successfully, including the final reviewed code. Existing `CC_COMMON_PORTAL_HOSTNAME` warnings were informational.

## 45. Documentation Validation

Passed: `python3 scripts/check-doc-sync.py` reported no doc-sensitive changes. This implementation report is the ticket-required engineering artifact; no product/runtime documentation required updates.

## 46. Manual Path-Construction Search

Passed. The modified Application feature source contains no `/applications/`, `NEXT_PUBLIC_DEEP_LINK_BASE_URL`, `pathTemplate`, direct registry access, or manual host construction. Expected URL strings exist only in tests.

## 47. Hard-Coded Host Search

Passed. No `links-qa.legalsynq.net` or `links.legalsynq.net` was added.

## 48. Boundary / Scope Validation

Passed. Git status contains only this report and `apps/web` Application files. No Mobile, Gateway, backend service, shared-contract, database, route-structure, API, permission, or Contact changes were made.

## 49. Acceptance-Criteria Status

AC-001 through AC-032 and AC-034 through AC-038 are satisfied. AC-033 is not applicable because the app has no supported lint command. Physical-device/release-host behavior remains explicitly outside this ticket.

## 50. Issues and Failures

- The first detail-panel test run had one test-only ambiguity because the existing layout renders the applicant name twice; the assertion was corrected to reflect the established UI, after which all focused tests passed.
- `pnpm --dir apps/web test` cannot resolve its declared `tsx` executable in this checkout. Running the installed root binaries directly completed both underlying phases.
- The complete Vitest phase has 23 failures in three unrelated pre-existing CareConnect/referral test files. Failures include `localStorage.getItem` being unavailable in `PublicNetworkView` tests and existing accessible-name expectations in referral thread tests. The DL-WEB-003 focused suites all pass and do not touch those areas.
- The sandboxed build failed at a required fetch; the exact build passed when rerun with network permission.
- Independent review identified a narrow-layout overflow risk in the initial header integration. The header now stacks below `sm`, record text can shrink, and the action/status group wraps at full width; focused tests and TypeScript passed after the correction.
- Reviewer re-review approved the correction with no remaining critical, high, medium, or blocking low findings.

## 51. Blockers and External Dependencies

- DL-PLAT-002 remains responsible for approved per-environment hosts, DNS/TLS/association deployment, and physical-device QA.

## 52. Security Review

Expected configuration failures are contained without exposing configuration details or substituting another origin. The URL contains only the already-authorized canonical Application ID, and no client-side authorization prediction or token handling was introduced.

## 53. Architecture Risks and Concerns

No structural risk identified. The release remains dependent on a valid approved HTTPS base URL and platform association deployment; until configured, the action intentionally remains hidden.

## 54. Known Gaps

Physical-device and OS-level behavior is out of scope and will not be claimed.

## 55. DL-PLAT-002 Handoff

Release configuration must eventually supply an approved `NEXT_PUBLIC_DEEP_LINK_BASE_URL` per environment.

## 56. DL-WEB-004 Handoff

No Dashboard or other journey integration will be performed here.

## 57. Out-of-Scope Confirmation

No out-of-scope implementation has been performed.

## 58. Follow-Up Recommendations

- Configure the approved per-environment base URL through DL-PLAT-002.
- Perform browser/physical-device association verification in release QA.
- Address the unrelated Web test-runner binary resolution and existing CareConnect/referral suite failures separately.

## 59. Final Status

Complete — Awaiting DL-PLAT-002 Release Base URL / Platform Association Configuration and Physical-Device QA. Independent review approved with no blocking findings.
