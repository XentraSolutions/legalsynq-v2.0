# DL-WEB-002 Implementation Report

## 1. Ticket Summary

Add a Contact Details `Open in App` action by consuming the committed DL-WEB-001 route-intent builder.

## 2. Objective

Expose a safe, accessible HTTPS handoff for the canonical Contact Details journey without placing route semantics or host selection in Contact feature code.

## 3. Scope

Contact Web UI integration, focused tests, validation, and this report only. Mobile, Gateway, backend services, shared route semantics, release infrastructure, native detection, other journeys, and physical-device validation are excluded.

## 4. Initial Implementation Plan

1. Verify and document the committed DL-WEB-001 dependency and shared `contactDetails` route.
2. Inspect the canonical Contact Details page, ID source, action patterns, responsive/accessibility behavior, and tests.
3. Select the smallest existing action surface and safe unavailable-state behavior.
4. Integrate `buildDeepLink({ routeKey: "contactDetails", pathParams: { contactId } })` without manual path or host construction.
5. Add focused UI/integration coverage, rerun builder tests, and perform scoped TypeScript, formatting, documentation, search, and boundary checks.
6. Complete an independent final review and record honest validation constraints.

## 5. Current Branch and Working-Tree State

- Branch: `feat(app)-LSV3-1193-Implement-Web-to-Mobile-Deep-Link`
- HEAD: `ac29aa44651174d14c1c3339b1af517517a9e30e`
- Initial working tree: clean
- This report was created before modifying any other repository file for DL-WEB-002.

## 6. Repository Instruction Review

Root `AGENTS.md` and the project delivery-mode implementation workflow govern this task. No scoped Contact instruction has yet been identified. Required constraints include preserving user changes, using existing Web components/BFF boundaries, avoiding generated artifacts, updating only necessary documentation, and running the narrowest meaningful validation.

## 7. DL-WEB-001 Dependency Review

Complete and committed at current HEAD. `apps/web/src/lib/deep-links.ts` exports `buildDeepLink({ routeKey, pathParams })`, reads only `NEXT_PUBLIC_DEEP_LINK_BASE_URL`, requires an HTTPS origin, throws shared `DeepLinkError` values for configuration/route/parameter failures, and delegates path semantics to the immutable shared registry. Its focused test suite covers the real registry and Contact URL generation.

## 8. Shared Contact Route Review

The enabled `contactDetails` entry uses `pathTemplate: "/contacts/:contactId"` and declares exactly one required path parameter, `contactId`. The Contact feature will use only the key and parameter name, not the template.

## 9. Contact Feature Architecture Review

The canonical SynqLien Contact feature uses an App Router layout under `apps/web/src/app/(platform)/lien/contacts/[id]`. The layout passes the route `id` into the client-side `ContactDetailShell`, which loads the canonical `ContactDetail` through `contactsService.getContact(id)`. The loaded shell owns the header, stats, Actions dropdown, tabs, edit/delete behavior, and child-page context. Selling's `/selling/contacts/[id]` is a company-specific surface with a separate data model and is not selected for this Contact Mobile route.

## 10. Canonical Contact Details Surface

Route `/lien/contacts/[id]` redirects to `/lien/contacts/[id]/overview`; `apps/web/src/components/lien/contact-detail/shell.tsx` is the persistent detail surface and existing action owner.

## 11. Contact ID Contract

Use `contact.id` from the successfully loaded `ContactDetail`. This is the canonical value displayed as `Contact ID`, passed into detail context, and used by related Contact cases/staff behavior. Do not substitute the display name, route index, email, or another entity identifier.

## 12. Existing Contact Action Patterns

The header's three-column responsive stats grid contains an existing Radix `DropdownMenu` labelled `Actions`. It preserves conditional Edit and Send Email items plus Delete. This is the smallest established action surface.

## 13. Existing Accessibility / Responsive Conventions

The existing menu trigger is keyboard-operable through Radix and menu links use `DropdownMenuItem asChild` with semantic anchors. The header collapses to one column below `lg`, while the actions remain inside the existing three-column stats grid. Adding one menu item does not change the closed-menu layout or action-bar width.

## 14. Existing Contact Test Review

No Contact Detail shell test exists. Web component tests use Vitest, jsdom, Testing Library, `@/` aliases, and accessible role/name queries. A narrow Contact-owned link component can be tested without duplicating the shell's data/query/auth setup.

## 15. Open-in-App Placement Decision

Add `Open in App` to the existing Contact Details Actions dropdown, between optional Send Email and the separator before destructive Delete. Existing actions and permissions remain unchanged; the deep-link action follows page availability and needs no new permission.

## 16. Configuration-Unavailable UX Decision

Hide the action. The link component catches builder failures and returns `null`, preventing a broken `href` or Contact page crash without exposing configuration details.

## 17. Missing-Contact-ID UX Decision

Hide the action when `contactId` is absent or whitespace-only and do not invoke the builder. The rest of Contact Details remains unchanged.

## 18. Intended Integration Architecture

Contact Details action → DL-WEB-001 builder → shared `contactDetails` registry route → configured HTTPS base URL → semantic browser navigation.

## 19. Implementation Changes

- Created this report as the first DL-WEB-002 repository change.
- Added `open-in-app-link.tsx`, a Contact-owned semantic-link adapter that calls DL-WEB-001 and safely omits itself for invalid IDs or expected typed deep-link failures. Unexpected programming errors are rethrown rather than silently masked.
- Added the action to the existing Contact Details Actions dropdown without changing Edit, Send Email, Delete, tabs, data loading, or permissions.
- Added focused Vitest UI/integration coverage.

## 20. Builder Integration

`OpenInAppLink` invokes `buildDeepLink({ routeKey: "contactDetails", pathParams: { contactId } })`. It does not import the registry, inspect `pathTemplate`, read environment variables, accept a host, or construct a path. A narrow injected-builder test seam verifies the exact call; production defaults to DL-WEB-001. The Web adapter additively re-exports its existing `DeepLinkError` contract so Contact can contain expected configuration/validation errors while surfacing unexpected defects.

## 21. Browser Navigation Behavior

The rendered action is an `<a href={builderOutput}>` with no `target`, custom protocol, click interception, native detection, or fallback. Radix `asChild` supplies existing dropdown keyboard/focus behavior, and the component forwards its ref and anchor props.

## 22. Accessibility Implementation

Visible text is `Open in App`; the decorative phone icon is `aria-hidden`. The semantic anchor has the accessible name `Open in App` and retains Radix-injected focus and interaction properties.

## 23. Responsive / Regression Review

The action is inside the existing dropdown, so the closed responsive header width and three-column stats layout are unchanged. Existing Edit, Send Email, separator, and Delete items remain in their original order apart from insertion of the new non-destructive item before the separator. No data loading, route, editing, deletion, or permission logic changed.

## 24. Files Inspected

- User-provided DL-WEB-002 specification
- Root repository instructions
- Delivery-mode implementation instructions
- `analysis/DL-WEB-001-report.md`
- `shared/contracts/deep-links/routes.json`
- `apps/web/src/lib/deep-links.ts` and focused tests
- `apps/web/package.json`, `tsconfig.json`, and `vitest.config.ts`
- canonical Lien and Selling Contact route/layout/detail components
- Contact detail shell, context, tabs, action patterns, and current component-test conventions

## 25. Files Added

- `analysis/DL-WEB-002-report.md`
- `apps/web/src/components/lien/contact-detail/open-in-app-link.tsx`
- `apps/web/src/components/lien/contact-detail/open-in-app-link.test.tsx`

## 26. Files Modified

- `apps/web/src/components/lien/contact-detail/shell.tsx`

## 27. Files Deleted

None.

## 28. Implementation Progress

| Area | Status | Completion |
|---|---|---:|
| Contact feature review | Done | 100% |
| Builder dependency | Done | 100% |
| UI placement | Done | 100% |
| Builder integration | Done | 100% |
| Missing-config behavior | Done | 100% |
| Missing-ID behavior | Done | 100% |
| Accessibility | Done | 100% |
| Responsive/regression | Done | 100% |
| Tests | Done | 100% |
| Validation | Done | 100% |
| Boundary checks | Done | 100% |

## 29. Test Changes

Added nine focused link assertions across six cases: visible label/semantic link/canonical href/same-context navigation, exact builder intent and canonical ID, encoded ID integration, missing configuration, unexpected-error propagation, and undefined/null/empty/blank ID behavior without builder invocation. Added two shell-level cases that render the canonical `ContactDetailShell` action area, verify Open in App beside Edit/Email/Delete, and prove missing configuration removes only the deep-link action.

## 30. Validation Commands and Results

- `./node_modules/.bin/vitest run apps/web/src/components/lien/contact-detail/open-in-app-link.test.tsx --config apps/web/vitest.config.ts` from repository root, Vitest 4.1.5: FAIL, exit 1; no tests discovered because the Web config's relative include was resolved from the repository root. This was invocation-context error, not introduced by DL-WEB-002; rerun from `apps/web` was required.
- `../../node_modules/.bin/vitest run src/components/lien/contact-detail/open-in-app-link.test.tsx` from `apps/web`, Vitest 4.1.5: PASS, exit 0; 1 file and 8 tests passed. Node emitted the existing `module.register()` deprecation warning.
- `../../node_modules/.bin/tsx --test src/lib/__tests__/deep-links.test.ts` from `apps/web`, tsx 4.21.0 / Node 26.0.0: PASS, exit 0; 9 tests passed. Node emitted the existing `module.register()` deprecation warning.
- Attempted focused `tsc` CLI command from root: FAIL, exit 1, because TypeScript does not permit `paths` mapping directly on the command line. This was a validation-command limitation, not a product diagnostic; no code changes resulted.
- `pnpm --dir apps/web type-check` from root, pnpm 10.26.1: FAIL, exit 1 after approximately 65 seconds with `[ERROR] fetch failed`. The repository dependency environment remains incomplete as documented by DL-WEB-001; no TypeScript diagnostic was emitted and the focused Vitest transform compiled the new component/test successfully.
- `pnpm install --frozen-lockfile` and `pnpm --dir apps/web install --frozen-lockfile` from root, pnpm 10.26.1: PASS after approved network access; restored locked dependencies only. No manifest or lockfile changed. Warnings: root install ignored existing esbuild/MSW/sharp build scripts; Web install ran sharp's check and ignored MSW's build script.
- First two shell-test attempts: FAIL because missing Web dependencies initially prevented Radix resolution, then duplicate React resolution affected Next Link/Radix after dependency restoration. These were validation-environment/test-harness failures, not product failures. The test now mocks Next Link and the existing dropdown primitives narrowly to exercise shell composition deterministically; corrected rerun passed.
- `./node_modules/.bin/tsc --noEmit -p apps/web/tsconfig.json` from root, TypeScript 6.0.3: PASS, exit 0, no diagnostics.
- `pnpm --dir apps/web build` from root, Next.js 16.2.6: PASS, exit 0; compiled, ran TypeScript, generated 29 static pages, and completed route output. Warnings: Node `module.register()` deprecation, non-blocking `ENOSPC` webpack cache write, and expected missing optional `CC_COMMON_PORTAL_HOSTNAME` warning.
- Post-review final Contact run: 2 Vitest files and 11 tests passed; the DL-WEB-001 regression suite passed 9 tests. The expected unexpected-error propagation case caused React to report the caught render error to stderr; the assertion passed and was subsequently quieted with a scoped console spy.
- Post-review final `tsc`: PASS, exit 0, no diagnostics. Post-review final Next.js build: PASS, exit 0; compilation, TypeScript, static-page generation, and route collection completed. Only the expected optional CareConnect hostname and Node deprecation warnings remained.
- `python3 scripts/check-doc-sync.py` from root: PASS, exit 0; no doc-sensitive changes detected.
- `git diff --check` plus `git diff --no-index --check` for untracked files from root: PASS for whitespace; `--no-index` returns its expected difference status with no output.
- Boundary and source searches from root: PASS; no Mobile/Gateway/service/shared-registry diff, candidate release host, Contact-feature base URL use, manual `/contacts/` deep-link path, or Contact-ID string concatenation.

## 31. Contact UI Test Validation

PASS: final Contact validation covers 2 test files and 11 tests, including the canonical shell action menu, isolated link behavior, and unexpected-error propagation.

## 32. Builder Regression Validation

PASS: the committed DL-WEB-001 suite passed all 9 tests after Contact integration.

## 33. Configuration-Unavailable Validation

PASS: with `NEXT_PUBLIC_DEEP_LINK_BASE_URL` absent, builder failure was contained and no link rendered.

## 34. Missing-ID Validation

PASS: undefined, null, empty, and whitespace-only IDs rendered no link and did not invoke the builder.

## 35. Accessibility Validation

PASS in focused UI tests: semantic link role/name and absence of forced new browsing context. Ref/anchor props are forwarded for Radix menu interaction.

## 36. Typecheck Validation

PASS: direct repository TypeScript 6.0.3 invocation over `apps/web/tsconfig.json` exited 0 with no diagnostics. The package-script wrapper itself remains unable to locate `tsc` because TypeScript is provided from the repository root rather than the Web-local install.

## 37. Lint Validation

No Web lint script/configuration or installed ESLint binary is available, matching the DL-WEB-001 tooling finding. No lint result is claimed.

## 38. Formatting Validation

No installed Prettier binary or Web format script is available. Files were aligned to local style; all tracked and untracked whitespace checks passed.

## 39. Web Build Validation

PASS: Next.js 16.2.6 production build completed successfully, including its TypeScript phase and Contact routes. Non-blocking environment/cache warnings are recorded in Section 30.

## 40. Documentation Validation

No product documentation change was necessary because the committed Web README already documents the follow-up feature pattern. Documentation sync passed.

## 41. Manual Path-Construction Search

PASS: the new production Contact component contains no `/contacts/`, base URL, or Contact-ID concatenation. Existing normal Web routes elsewhere were classified as unrelated.

## 42. Hard-Coded Host Search

PASS: no candidate QA/Production host or fallback exists in the modified Contact production source. Fixture `.example.test` values occur only in tests.

## 43. Boundary / Scope Validation

PASS: changes are limited to the Contact detail component/test and this report. No Mobile, Gateway, backend service, shared route, database, or other journey file changed.

## 44. Acceptance-Criteria Status

| Criterion | Status | Evidence |
|---|---|---|
| AC-001–AC-003 | Complete | Sections 7–11 identify the canonical shell, committed builder, shared route, and `contact.id`. |
| AC-004–AC-011 | Complete | The existing Actions menu renders a same-context semantic anchor produced only by `buildDeepLink` with `contactDetails` and `contactId`; source guards found no path/host construction. |
| AC-012–AC-015 | Complete | Focused tests cover missing config/ID; no fallback, native detection, custom scheme, or host default exists. |
| AC-016 | Complete | Accessible link-name assertion passes; anchor/ref/props preserve Radix behavior. |
| AC-017–AC-018 | Complete | Dropdown placement leaves closed responsive layout unchanged; shell-level tests preserve Edit/Email/Delete in valid and unavailable-config states. |
| AC-019–AC-023 | Complete | Eleven Contact tests and nine builder regression tests pass. |
| AC-024 | Complete | Direct full Web TypeScript project validation passes with no diagnostics. |
| AC-025 | Not applicable | The repository exposes no Web lint command/configuration; no applicable supported lint command exists. |
| AC-026 | Complete | Files follow local formatting conventions and all tracked/untracked whitespace checks pass; no Web Prettier command exists. |
| AC-027 | Complete | Next.js 16.2.6 production build completed successfully. |
| AC-028–AC-032 | Complete | Boundary/status/search checks show no Mobile, backend, shared, other-journey, or release-host changes. |
| AC-033 | Complete | Validation failures and the absence of physical-device verification are recorded without claiming success. |

## 45. Issues and Failures

- Initial focused Vitest invocation used the wrong working directory and found no tests; corrected rerun passed.
- Focused standalone TypeScript invocation used an unsupported CLI `paths` option; no diagnostic ran from that command.
- Repository package typecheck initially failed on restricted pnpm fetching and, after dependency restoration, its wrapper cannot locate the root-owned `tsc`; direct full-project TypeScript passes.
- Web has no available lint/format command or corresponding installed binaries.
- Shell integration tests initially exposed dependency and duplicate-React test-harness issues; narrow mocks of Next Link and dropdown primitives resolved the harness without changing production behavior.

## 46. Blockers and External Dependencies

- DL-PLAT-002 remains responsible for approved per-environment base URLs, association deployment, and QA physical verification.
- DL-PLAT-002 remains the only blocker to approved release-host configuration and physical-device verification. All repository-supported Web validation applicable to this change passes.

## 47. Security Review

The Contact component cannot override the host in production, accepts no raw path, catches only typed deep-link configuration/validation errors, rethrows unexpected defects, encodes the canonical ID through DL-WEB-001, uses HTTPS output, and reveals no stack trace or secret to users. Backend/Mobile remain authoritative for access after navigation.

## 48. Architecture Risks and Concerns

The injected builder prop is a narrow component-test seam; production does not supply it. The component remains Contact-owned and route semantics remain in DL-WEB-001/shared contracts. The only known cross-runtime concern is the stale shared README already recorded by DL-WEB-001 and not modified here.

## 49. Known Gaps

- Physical-device and OS-level behavior is out of scope and is not claimed.
- No Web lint or Prettier command exists; the applicable test, full TypeScript, production build, documentation, whitespace, source-guard, and boundary checks pass.

## 50. DL-PLAT-002 Handoff

Release configuration must supply an approved `NEXT_PUBLIC_DEEP_LINK_BASE_URL` per environment, deploy matching association files, and run physical Web-to-App QA.

## 51. DL-WEB-003 Handoff

Application integration should reuse `buildDeepLink` with `applicationDetails`; none was implemented here.

## 52. DL-WEB-004 Handoff

Dashboard and other journey integrations should reuse route intent through DL-WEB-001; none was implemented here.

## 53. Out-of-Scope Confirmation

Confirmed no implementation for Application, Dashboard, Deal, or Report Open-in-App; native detection; store fallback; QR codes; campaign attribution; notifications; host/DNS/TLS approval; Apple/Android identity; Gateway; backend; Mobile; shared routes; database; or physical-device validation.

## 54. Follow-Up Recommendations

- Complete DL-PLAT-002 release configuration and QA device validation before claiming end-to-end app opening.
- Keep future journey actions on the DL-WEB-001 route-intent API.

## 55. Final Status

Complete — Awaiting Release Base URL / QA Verification.
