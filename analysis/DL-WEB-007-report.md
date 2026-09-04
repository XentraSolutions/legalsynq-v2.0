# DL-WEB-007 Implementation Report

## 1. Ticket Summary

Conditionally consolidate duplicated Open-in-App presentation behavior across the implemented Contact, Application, and Dashboard Web journeys while preserving feature-owned route intent, resource state, visibility, and placement.

## 2. Objective

Remove material duplication in presentation and expected builder-failure handling while retaining DL-WEB-001 as URL owner and each feature as intent/state/placement owner.

## 3. Scope

One shared Web presentation component, narrow migration of Contact/Application/Dashboard wrappers, focused tests, validation, and this report. No new journey, route semantic, host, Mobile, backend, database, native detection, or release infrastructure work.

## 4. Initial Review Plan

1. Verify the actual committed DL-WEB-001 builder and DL-WEB-002/003/004 feature integrations, not merely their reports.
2. Inspect all three implementations/tests plus shared Web UI/link conventions and tooling.
3. Build the required concern-by-concern duplication table before choosing an abstraction.
4. Stop honestly if integrations are missing or duplication is immaterial; otherwise extract only common presentation and safe-error behavior.
5. Preserve feature-owned route keys, IDs, visibility/loading rules, placement, and all unrelated actions.
6. Run shared and per-feature regressions, builder tests, TypeScript/build checks, source/boundary checks, documentation sync, and independent review.

## 5. Current Branch and Working-Tree State

- Branch: `feat(app)-LSV3-1193-Implement-Web-to-Mobile-Deep-Link`
- HEAD: `e5891de8b4fe9b749fd6892dbfd672f90b915f75`
- Initial working tree: clean
- This report is the first DL-WEB-007 repository change.

## 6. Repository Instruction Review

Root `AGENTS.md`, the repository implementation delivery workflow, and the user-provided DL-WEB-007 specification govern this task. The attached specification is treated as implementation requirements, not as a separate user request.

## 7. DL-WEB-001 Dependency Review

Verified at commit `ac29aa446`. `apps/web/src/lib/deep-links.ts` owns HTTPS configuration validation and delegates registry lookup, parameter validation/encoding, and URL generation to shared contracts. It exports `buildDeepLink`, `BuildDeepLinkInput`, and `DeepLinkError`; nine focused tests cover the real registry and configuration contract.

## 8. DL-WEB-002 Contact Integration Verification

Verified at commit `cd821bb07`. The canonical Contact Actions menu renders `components/lien/contact-detail/open-in-app-link.tsx`, passing loaded `contact.id`; tests cover intent, canonical URL, missing/blank IDs, missing config, and placement/action preservation.

## 9. DL-WEB-003 Application Integration Verification

Verified at commit `ebf0a6acc`. The canonical Funding Application detail header renders `ApplicationOpenInAppLink` with loaded `application.id`; tests cover intent, canonical URL, missing/blank IDs, loading/not-found behavior, responsive placement, and workflow-action preservation.

## 10. DL-WEB-004 Dashboard Integration Verification

Verified at commit `e5891de8b`. The canonical `/dashboard` welcome header renders `DashboardOpenInAppLink`; tests cover the parameterless intent, canonical URL, missing config, responsive placement, and product/admin action preservation.

## 11. Shared Route Registry Review

The authoritative registry contains enabled `dashboard`, `contactDetails`, and `applicationDetails` routes. It also contains Deal/Report generation entries, but those journeys remain unavailable for positive Web integration. This ticket does not modify or mirror the registry.

## 12. Existing Contact Implementation

A `forwardRef` anchor wrapper owns blank-ID visibility, route intent, builder call, `DeepLinkError` hiding, icon/label markup, and prop/ref forwarding for Radix `asChild`. The Actions-menu placement and canonical Contact ID remain feature concerns.

## 13. Existing Application Implementation

A plain anchor wrapper owns blank-ID visibility, route intent, builder call, `DeepLinkError` hiding, icon/label markup, and header-button classes. Loading/error/not-found exclusion and the loaded Application ID remain feature concerns.

## 14. Existing Dashboard Implementation

A plain anchor wrapper owns parameterless route intent, builder call, `DeepLinkError` hiding, icon/label markup, and header-button classes. Authorization/portal redirect and header placement remain Dashboard concerns.

## 15. Existing Test Review

Each wrapper duplicates builder/config/error/accessibility tests. Feature-level Contact shell, Application panel/page, and Dashboard page tests already protect placement, surrounding actions, and resource-state behavior. Vitest/jsdom/Testing Library and injected builder seams are established conventions.

## 16. Duplication Comparison

| Concern | Contact | Application | Dashboard | Shared Candidate? |
|---|---|---|---|---|
| Open-in-App label | `Open in App` | `Open in App` | `Open in App` | Yes: identical |
| Builder invocation | Injected/default builder | Injected/default builder | Injected/default builder | Yes: one delegation layer |
| Missing-config behavior | Render nothing | Render nothing | Render nothing | Yes: identical |
| Builder error handling | Hide only `DeepLinkError` | Hide only `DeepLinkError` | Hide only `DeepLinkError` | Yes: identical |
| Link/Button component | Semantic anchor, ref/props | Semantic anchor | Semantic anchor | Yes: shared anchor supports ref/props |
| Accessibility props | Visible label; hidden icon | Visible label; hidden icon | Visible label; hidden icon | Yes: identical |
| Browser target behavior | Same context | Same context | Same context | Yes: identical |
| Styling | Menu-owned via props | Feature header classes | Feature header classes plus sizing | Partly: retain caller classes |
| Route key | `contactDetails` | `applicationDetails` | `dashboard` | No / feature-owned |
| Route parameters | Canonical `contactId` | Canonical `applicationId` | None | No / feature-owned |
| Visibility rules | Blank ID hidden | Blank ID hidden | Dashboard availability | No / feature-owned |
| Resource/loading rules | Loaded Contact shell | Loaded Application panel | Authorized server page | No / feature-owned |
| Placement | Contact Actions menu | Application detail header | Dashboard welcome header | No / feature-owned |

## 17. Material Duplication Assessment

Material duplication exists: all three components independently declare the builder type/default, invoke it, catch only `DeepLinkError`, render an anchor, hide the phone icon from assistive technology, emit the same visible label, and use same-context navigation. Consolidating this behavior removes repeated failure semantics and accessible markup without moving domain state.

## 18. Abstraction Decision

Select one shared presentation component. A hook would only wrap a synchronous call and still duplicate anchor/error markup; a new framework or combined hook/component is unnecessary.

## 19. Selected Shared API

`<OpenInAppLink intent={featureOwnedBuildInput} builder={optionalTestSeam} {...anchorProps} />`. The shared component accepts an opaque DL-WEB-001 `BuildDeepLinkInput`, delegates it unchanged, forwards standard anchor props/ref, owns common icon/label/error behavior, and does not know supported route keys or resource fields.

## 20. Ownership Boundary Review

DL-WEB-001 remains the sole URL-generation owner. Feature wrappers retain route keys, IDs, blank-ID rules, state, permissions, classes, and placement. The shared component contains no registry/template/configuration logic.

## 21. Intended Consolidation Architecture

Feature surface/wrapper → shared `OpenInAppLink` presentation → unchanged DL-WEB-001 builder → authoritative registry → canonical HTTPS anchor → browser/OS handoff.

## 22. Implementation Changes

- Created this report before any other DL-WEB-007 repository change.
- Added a shared ref-forwarding `OpenInAppLink` presentation component.
- Replaced repeated builder/error/anchor/icon/label code in all three feature wrappers with shared delegation.
- Reduced the three wrapper implementations from 115 changed-file lines to 75 while preserving their public names and feature call sites.

## 23. Contact Migration

Complete. The existing Contact wrapper retains blank-ID guarding and constructs `{ routeKey: "contactDetails", pathParams: { contactId } }`. It forwards ref/anchor props and the optional test builder to the shared component. The Actions-menu call site is unchanged.

## 24. Application Migration

Complete. The existing Application wrapper retains blank-ID guarding and constructs `{ routeKey: "applicationDetails", pathParams: { applicationId } }`. Its exact header classes and call site are unchanged.

## 25. Dashboard Migration

Complete. The existing Dashboard wrapper constructs only `{ routeKey: "dashboard" }`. Its exact welcome-header classes and call site are unchanged.

## 26. Missing-Configuration Behavior

Preserved and centralized: `DeepLinkError` yields no rendered action while each feature wrapper retains its prerequisite logic and surrounding UI remains usable.

## 27. Builder Error Handling

The shared presentation catches only `DeepLinkError`. Shared, Contact, and Dashboard tests verify unexpected exceptions continue to propagate.

## 28. Browser Navigation Behavior

Preserve semantic HTTPS anchors and same-context navigation; no click interception or fallback behavior.

## 29. Accessibility

Consolidate the visible `Open in App` name and decorative `aria-hidden` phone icon. Native anchor keyboard behavior and caller-provided focus classes remain intact.

## 30. Styling / Placement Preservation

The shared component will accept ordinary anchor props/className. Feature wrappers/surfaces retain their existing menu/header classes and locations.

## 31. Files Inspected

- DL-WEB-001/002/003/004 reports and actual implementation commits
- Shared route registry and Web builder/tests
- Contact/Application/Dashboard wrappers, integration surfaces, and tests
- Shared UI components/hooks, Vitest configuration, Web package scripts, and repository guidance

## 32. Files Added

- `analysis/DL-WEB-007-report.md`
- `apps/web/src/components/open-in-app-link.tsx`
- `apps/web/src/components/open-in-app-link.test.tsx`

## 33. Files Modified

- `apps/web/src/components/lien/contact-detail/open-in-app-link.tsx`
- `apps/web/src/components/fund/application-open-in-app-link.tsx`
- `apps/web/src/components/dashboard-open-in-app-link.tsx`

## 34. Files Deleted

None planned.

## 35. Implementation Progress

## Progress

| Area | Status | Completion |
|---|---|---:|
| Journey presence verification | Done | 100% |
| DL-WEB-001 boundary review | Done | 100% |
| Duplication analysis | Done | 100% |
| Abstraction decision | Done | 100% |
| Shared presentation implementation | Done | 100% |
| Contact migration | Done | 100% |
| Application migration | Done | 100% |
| Dashboard migration | Done | 100% |
| Accessibility | Done | 100% |
| Tests | Done | 100% |
| Validation | Done | 100% |
| Boundary checks | Done | 100% |

## 36. Test Changes

Added five shared tests for real-builder HTTPS output, exact intent delegation, ref/anchor-prop forwarding, missing configuration, expected typed failure containment, accessibility, same-context behavior, and unexpected failure propagation. Existing feature and surface tests were retained unchanged.

## 37. Validation Commands and Results

- `../../node_modules/.bin/vitest run src/components/open-in-app-link.test.tsx src/components/lien/contact-detail/open-in-app-link.test.tsx src/components/lien/contact-detail/shell.test.tsx src/components/fund/application-open-in-app-link.test.tsx src/components/fund/funding-application-detail-panel.test.tsx 'src/app/(platform)/fund/applications/[id]/page.test.tsx' src/components/dashboard-open-in-app-link.test.tsx 'src/app/(platform)/dashboard/page.test.tsx'` from `apps/web`: PASS, exit 0; Vitest 4.1.5, 8 files/35 tests. React printed expected caught-render diagnostics for deliberate unexpected-error assertions; no test or unhandled error failed.
- `../../node_modules/.bin/tsx --test src/lib/__tests__/deep-links.test.ts` from `apps/web`: PASS, exit 0; tsx 4.21.0, 9/9 tests. Required approved IPC access; Node printed its existing `module.register()` deprecation warning.
- `./node_modules/.bin/tsc --noEmit -p apps/web/tsconfig.json` from repository root: PASS, exit 0; TypeScript 6.0.3, no diagnostics.
- `pnpm --dir apps/web build` from repository root: PASS, exit 0; Next.js 16.2.6 compiled, type-checked, generated 29 static pages, and collected routes. Required approved external fetch access; existing optional CareConnect hostname and Node deprecation warnings only.
- `../../node_modules/.bin/vitest run` from `apps/web`: FAIL, exit 1; 73 files/346 tests passed and the same 3 files/23 unrelated baseline tests failed in funding notifications, CareConnect network (`localStorage` unavailable), and referral-thread accessible-name expectations. All consolidation/feature tests passed; DL-WEB-007 did not touch failing areas.
- Route/host/native searches, `git diff --check`, and Mobile/Backend/shared boundary diff: PASS with no findings.

## 38. Shared Presentation Test Validation

PASS: five shared tests verify shared behavior and unchanged DL-WEB-001 delegation.

## 39. Contact Regression Validation

PASS: Contact wrapper plus canonical shell suites passed 11/11 within the focused run.

## 40. Application Regression Validation

PASS: Application wrapper, detail panel, and page-state suites passed 13/13 within the focused run.

## 41. Dashboard Regression Validation

PASS: Dashboard wrapper and canonical page suites passed 6/6 within the focused run.

## 42. DL-WEB-001 Regression Validation

PASS: the complete nine-test builder/config/registry suite passed.

## 43. Typecheck Validation

PASS: full Web TypeScript project emitted no diagnostics.

## 44. Lint Validation

Not applicable: `apps/web/package.json` exposes no lint script and no supported Web lint command was identified.

## 45. Formatting Validation

No supported formatting/Prettier script exists. Modified code follows adjacent formatting; tracked and untracked whitespace checks pass.

## 46. Web Build Validation

PASS: the production build completed successfully.

## 47. Documentation Validation

PASS: `python3 scripts/check-doc-sync.py` reported no doc-sensitive changes. The consolidation changes no runtime/configuration contract, commands, ports, service boundaries, or public behavior, so no durable README update is required.

## 48. Route-Duplication Search

PASS: shared presentation contains no `/contacts/`, `/applications/`, `/dashboard`, `pathTemplate`, or `routes.json` reference.

## 49. Host-Construction Search

PASS: presentation/wrapper source contains no `NEXT_PUBLIC_DEEP_LINK_BASE_URL` access or candidate QA/Production host.

## 50. Native-Detection Search

PASS: presentation/wrapper source contains no user-agent, location, timer, custom-scheme, or store-URL behavior.

## 51. Boundary / Scope Validation

PASS: only the report, one shared component/test, and the three existing Web wrapper files changed. No Mobile, Gateway, backend, shared registry, database, Deal, or Report implementation changed.

## 52. Positive Web-to-Mobile Journey Coverage

| Journey | Route Key | Implementation Status |
|---|---|---|
| Dashboard | `dashboard` | Verified implemented |
| Contact | `contactDetails` | Verified implemented |
| Application | `applicationDetails` | Verified implemented |
| Deal | `dealDetails` | Blocked / unavailable |
| Report | `reportDetails` | Blocked / unavailable |

## 53. Acceptance-Criteria Status

| AC | Status | Evidence |
|---|---|---|
| AC-001–004 | Complete | Builder and all three committed integrations verified in sections 7–10. |
| AC-005 | Complete | Section 16 documents concrete repeated behavior across all three journeys. |
| AC-006 | Complete | One presentation component selected; no hook/framework added. |
| AC-007–008 | Complete | Shared component delegates opaque intent to DL-WEB-001 and contains no route catalog/template. |
| AC-009–014 | Complete | Thin feature wrappers retain route intent, canonical IDs, state rules, and unchanged call-site placement. |
| AC-015–017 | Complete | Typed errors centrally render nothing; unexpected errors propagate and are tested. |
| AC-018–020 | Complete | Semantic HTTPS anchor remains; source search found no native/store behavior. |
| AC-021–022 | Complete | Shared visible label/hidden icon/ref/props; feature className and placements preserved. |
| AC-023 | Complete | Contact regression passed 11/11. |
| AC-024 | Complete | Application regression passed 13/13. |
| AC-025 | Complete | Dashboard regression passed 6/6. |
| AC-026–030 | Complete | Missing IDs, Application states, Dashboard no-param contract, and surrounding actions remain covered by feature tests. |
| AC-031 | Complete | Five focused shared tests added. |
| AC-032–034 | Complete | Contact/Application/Dashboard focused suites all pass. |
| AC-035–036 | Complete | Builder 9/9 and full TypeScript pass. |
| AC-037 | Not applicable | No supported Web lint command exists. |
| AC-038–039 | Complete | Whitespace checks and production build pass. |
| AC-040–045 | Complete | Boundary/source checks show no Mobile/backend/registry/Deal/Report/host approval changes. |
| AC-046 | Complete | Material duplication was evidenced before extracting the smallest component. |
| AC-047 | Complete | This report distinguishes passing targeted/build checks from the unrelated failing broad-suite baseline and makes no device claim. |

## 54. Issues and Failures

- Broad Vitest retains 23 unrelated failures across three pre-existing test files; 346 tests pass, including every DL-WEB-007 and supported-journey regression.
- React/jsdom writes caught-render diagnostics during deliberate unexpected-error propagation tests despite scoped console spies. Tests pass and no exception is left unhandled.
- Independent review found no critical, high, or medium issues and approved release readiness after report finalization; its independent focused rerun passed all 8 files/35 tests.

## 55. Blockers and External Dependencies

- Release host/platform association configuration and physical-device verification remain outside this consolidation ticket.

## 56. Security Review

No security issue identified. The shared layer accepts builder intent and anchor props, forces `href` to the validated builder output after prop spread, exposes no token/data beyond existing IDs, and owns no host, auth, authorization, native, or fallback behavior.

## 57. Architecture Risks and Concerns

No current architecture defect identified. Thin wrappers and caller-owned className/placement avoid over-generalizing feature state. The optional builder prop remains a narrow established test seam.

## 58. Known Gaps

Physical-device/OS association behavior is not validated by this ticket.

## 59. DL-PLAT-002 Handoff

Approved environment base URLs, DNS/TLS, association deployment, and signing inputs remain external.

## 60. QA Handoff

Live-domain and physical-device Web-to-App verification remain required after release configuration.

## 61. Deal Contract Blocker

Deal remains unavailable for positive Web integration until its domain contract is resolved; no UI is added here.

## 62. Report Contract Blocker

Report remains unavailable for positive Web integration until its domain contract is resolved; no UI is added here.

## 63. Out-of-Scope Confirmation

No new journey, Deal/Report UI, native detection, custom scheme/store fallback, QR/deferred/campaign/notification/analytics behavior, host/DNS/TLS/signing change, Gateway/backend/Mobile/database work, shared-route redesign, or physical-device verification is included.

## 64. Follow-Up Recommendations

- Resolve the unrelated broad Vitest baseline separately.
- Complete DL-PLAT-002 and live-device QA before claiming OS handoff readiness.

## 65. Final Status

Complete — Shared Open-in-App Presentation Consolidated.
