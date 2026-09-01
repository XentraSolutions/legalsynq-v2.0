# DL-APP-002 Implementation Report

## 1. Ticket Summary

DL-APP-002 — Implement Mobile Deep-Link Routing Engine.

## 2. Objective

Receive Mobile URLs, validate and match them against the shared registry, extract structural parameters, return navigation-independent typed results, and suppress rapid duplicate resolved events.

## 3. Scope

Mobile-only pure URL resolution, typed outcomes, in-memory deduplication, thin Expo Linking intake, tests, and documentation. Navigation, authentication, Backend/Web work, persistence, association hosting, analytics, and business validation are excluded.

## 4. Initial Implementation Plan

1. Document current state and inspect shared/Mobile/linking/lifecycle/navigation/test patterns.
2. Add pure resolver types and logic that consume `deepLinkRoutes` directly.
3. Add a deterministic, injectable, in-memory duplicate guard.
4. Extend `DeepLinkingService` only with runtime subscription support.
5. Add an intake orchestrator for initial/runtime events with no navigation or auth coupling.
6. Add comprehensive resolver, duplicate, and adapter tests.
7. Document boundaries and run formatter, type-check, lint, Jest, Expo export, and scope review.

## 5. Current Branch and Working-Tree State

- Branch: `feat(app)-LSV3-1055-Mobile-Configure-Native-iOS-Universal-Links`.
- Initial HEAD: `4d716a048` (`DL-APP-001`), whose parent is integrated foundation commit `078d8e6a9`.
- DL-APP-001 configuration, tests, Mobile README changes, and DL-APP/DL-INT reports were already tracked in `4d716a048` when this replacement prompt began.
- No DL-APP-002 code existed or was changed during the interrupted prior turn.

## 6. DL-APP-001 Dependency Review

- DL-APP-001 adds public `EXPO_PUBLIC_DEEP_LINK_HOST` typing and build-time native association configuration.
- Approved real hosts remain unavailable. Resolver/intake tests must inject fake hosts.
- No runtime route parsing or navigation was introduced by DL-APP-001.

## 7. Shared Contract Review

- `shared/contracts/deep-links/routes.json` is authoritative and contains five enabled routes.
- `route-registry.ts` validates version, unique keys, absolute templates, placeholder/required-parameter alignment, query metadata, and required route metadata.
- `deepLinkRoutes` exposes immutable validated definitions; Mobile already re-exports it from `src/shared/deepLinks`.
- All current routes declare no optional query parameters.

## 8. Existing DeepLinkingService Review

- `DeepLinkingService` wraps `Linking.createURL()` and `Linking.getInitialURL()` only.
- No existing tests, runtime URL subscription, parser, matcher, or router exist.
- Minimal extension will add a callback-based URL subscription returning an unsubscribe function.

## 9. Application Lifecycle / Bootstrap Review

- Root `App.tsx` initializes Sentry and wraps `src/App/App`.
- `src/App/App.tsx` uses effects for global service registration and hydration, then renders providers/navigation.
- No code consumes an initial URL or registers Linking URL listeners.
- This ticket will test the intake adapter in isolation rather than attach a destination-less callback to application UI/bootstrap.

## 10. Existing Navigation Architecture Review

- `RootNavigator` is rendered inside `AppProvider`.
- Navigation infrastructure is intentionally not imported by the new resolver/intake layer.
- Actual route-to-screen mapping remains an APP-004 responsibility.

## 11. Routing Architecture Decision

Create three separable pieces under the existing DeepLinking service boundary: a pure resolver, an in-memory duplicate guard, and an intake orchestrator that depends on the thin `DeepLinkingService`. No React or navigation type enters these modules.

## 12. Resolution Result Model

Use a discriminated union with `resolved`, `malformed`, `unsupported_scheme`, `unsupported_host`, `unsupported_route`, `invalid_parameters`, and `duplicate`. Resolved results contain shared route key, decoded path/query records, original URL, and stable normalized URL.

## 13. Scheme Validation Strategy

Accept HTTPS only in this routing engine. Existing custom-scheme URL creation remains unchanged, but no evidence shows those URLs are currently consumed for routing. Other schemes return `unsupported_scheme` without throwing.

## 14. Host Validation Strategy

The pure resolver requires an injected expected verified-link host and compares HTTPS hostname case-insensitively. Missing/invalid resolver configuration is an internal configuration error; incoming user URLs with other hosts or ports return `unsupported_host`. API origins are never consulted.

## 15. URL Normalization Strategy

Normalize scheme/hostname casing through URL parsing, percent-decode and canonically re-encode path/query values, sort approved query keys, omit fragments from identity only by rejecting any incoming fragment, preserve strict path shape, and reject dot segments, duplicate/blank segments, malformed encoding, and trailing slashes that do not match the contract.

## 16. Route Matching Strategy

Compile/match each enabled shared template by path segments. Static segments must match exactly; `:parameter` segments capture one nonblank decoded segment. Segment counts must match, and multiple matches are rejected as ambiguous.

## 17. Path Parameter Strategy

Use placeholder names and required-parameter metadata from each shared definition. Decode values with `decodeURIComponent`, reject malformed/blank values, and perform no business/resource validation.

## 18. Query Parameter Strategy

Parse query keys/values safely, reject malformed encoding, duplicate keys, and every undeclared key. Since current routes approve none, any query produces `invalid_parameters`. Query values cannot change route identity.

## 19. Duplicate Event Strategy

Use normalized resolved URL as an in-memory key with an injectable clock and configurable short window. The same URL inside the window yields `duplicate` and is not delivered to the destination callback; different URLs or the same URL after expiry proceed. No persistence is used.

## 20. Cold-Start Intake Design

An intake method calls `DeepLinkingService.getInitialUrl()`, safely ignores null, resolves a URL, applies deduplication, and delivers only nonduplicate results to an injected callback. It performs no navigation.

## 21. Runtime Intake Design

An intake subscription delegates to `DeepLinkingService.subscribeToUrls()`, resolves each raw event identically, applies the same guard as cold start, and delivers nonduplicates through the callback.

## 22. Subscription Cleanup Design

The platform wrapper returns Expo's subscription removal as an idempotent function. Each intake subscription owns one wrapper subscription and returns its cleanup directly.

## 23. Files Inspected

- Ticket attachments for DL-APP-002
- `analysis/DL-APP-001-report.md`
- `analysis/DL-INT-001-report.md`
- `shared/contracts/deep-links/routes.json`
- `shared/contracts/deep-links/route-contract.ts`
- `shared/contracts/deep-links/route-registry.ts`
- `shared/contracts/deep-links/index.ts`
- `apps/mobile/src/shared/deepLinks/index.ts`
- `apps/mobile/src/shared/services/DeepLinking/DeepLinkingService.ts`
- `apps/mobile/src/shared/services/DeepLinking/index.ts`
- `apps/mobile/src/shared/services/Config/ConfigService.ts` and test
- `apps/mobile/App.tsx`
- `apps/mobile/index.js`
- `apps/mobile/src/App/App.tsx`
- `apps/mobile/src/App/AppProvider.tsx`
- `apps/mobile/src/navigation/RootNavigator/RootNavigator.tsx` and navigation exports/types
- `apps/mobile/src/shared/services/index.ts`
- Representative Mobile service tests
- `apps/mobile/package.json`
- `apps/mobile/jest.config.js`
- `apps/mobile/jest.setup.tsx`
- `apps/mobile/tsconfig.json`
- `apps/mobile/.eslintrc.js`
- `apps/mobile/.prettierrc`
- `apps/mobile/README.md`

## 24. Files Added

- `apps/mobile/src/shared/services/DeepLinking/DeepLinkTypes.ts`
- `apps/mobile/src/shared/services/DeepLinking/DeepLinkResolver.ts`
- `apps/mobile/src/shared/services/DeepLinking/DeepLinkDuplicateGuard.ts`
- `apps/mobile/src/shared/services/DeepLinking/DeepLinkIntakeService.ts`
- `apps/mobile/src/shared/services/DeepLinking/DeepLinkResolver.test.ts`
- `apps/mobile/src/shared/services/DeepLinking/DeepLinkIntakeService.test.ts`
- `apps/mobile/src/shared/services/DeepLinking/DeepLinkingService.test.ts`

## 25. Files Modified

- `analysis/DL-APP-002-report.md` — created before Mobile implementation.
- `apps/mobile/src/shared/services/DeepLinking/DeepLinkingService.ts` — adds thin runtime subscription/removal.
- `apps/mobile/src/shared/services/DeepLinking/index.ts` — exports routing components/types.
- `apps/mobile/src/shared/services/Config/ConfigService.ts` — exposes the active public host to runtime configuration.
- `apps/mobile/src/shared/services/Config/ConfigService.test.ts` — covers explicit/absent host access.
- `apps/mobile/README.md` — documents routing lifecycle, results, validation, deduplication, and handoffs.

## 26. Files Deleted

None.

## 27. Implementation Progress

- Created this report before modifying Mobile routing code/tests/docs.
- Completed prerequisite, registry, lifecycle, navigation-boundary, and test-pattern inspection.
- Recorded resolver, validation, normalization, matching, deduplication, and intake design decisions.
- Implemented the typed pure resolver, registry matching, strict URL/query handling, in-memory guard, configured factory, cold-start intake, runtime subscription, and cleanup.
- Added focused tests and Mobile routing documentation.
- Focused Jest, Mobile type-check, and Mobile lint pass; export and final review remain.

## 28. Tests Added

- Resolver tests cover all five routes, decoded parameters, normalized host/path, missing/blank/extra segments, trailing/duplicate slashes, unknown route, malformed URL/encoding, scheme/host/authority rejection, absent configured host, query rejection, fragments, and invalid configuration.
- Intake tests cover null/valid/invalid initial URLs, multiple runtime events, cleanup, duplicate initial/runtime events, different URLs, and expiry.
- Platform adapter tests cover existing custom URL creation, initial delegation, runtime forwarding, and Expo subscription removal.
- Config test covers explicit trimmed and absent runtime host values.

## 29. Validation Commands and Results

- Initial focused Jest run: passed 4 suites / 37 tests.
- Initial Mobile type-check: failed only because new tests used Jest APIs omitted from the repository's minimal ambient declarations (`test.each`, asymmetric matchers, `jest.Mock`). Tests were adapted to existing local typing patterns.
- `apps/mobile/node_modules/.bin/tsc --noEmit -p tsconfig.json` rerun: passed.
- `apps/mobile/node_modules/.bin/eslint src --ext .ts,.tsx --max-warnings 0`: passed.
- Focused Jest rerun for DeepLinking and Config: passed 4 suites / 37 tests.
- Focused Prettier check: passed.
- QA/fake-host `expo export --platform ios --output-dir /private/tmp/dl-app-002-ios-export --clear`: passed; 2,926 modules bundled.
- Broader `jest --runInBand`: attempted; six suites reported passing before Node exhausted its 4 GB heap and exited 134 after about 90 seconds. This is a broader-suite resource limitation, not a DL-APP-002 assertion failure. No rerun with a larger heap was attempted to avoid increasing system memory pressure.
- Final `tsc --noEmit`, Mobile ESLint, focused Jest, Prettier check, and `git diff --check`: all passed after duplicate-map pruning.
- Boundary searches: no navigation/auth/API/persistence import or call in production DeepLinking code; no production Mobile route-template literals; no Web/Backend/shared working-tree diff.

## 30. Route Resolution Validation

All five shared routes resolve with their authoritative keys; four parameterized routes extract the declared required ID. Missing, blank, trailing, duplicate, unknown, and extra path shapes are rejected.

## 31. URL Validation

HTTPS and configured host checks, case normalization, malformed input/encoding, unsupported schemes/authorities, fragments, and undeclared/malformed query parameters are covered and passing.

## 32. Lifecycle Intake Validation

Mocked adapter tests prove null/valid/invalid cold-start handling, multiple runtime events, and cleanup. Platform-wrapper tests prove Expo initial/subscription delegation. No device lifecycle claim is made.

## 33. Duplicate Event Validation

Injected-clock tests prove immediate normalized-equivalent suppression across initial/runtime intake, independent processing of different URLs, and reprocessing after 2,001 ms.

## 34. Boundary / Scope Validation

Production routing modules import only the Mobile Config service, validated shared definitions, and their own adapter/types. Searches found no navigation, auth, API, analytics, storage, or persistence coupling. No Web, Backend, shared-contract, screen, or navigator file changed.

## 35. Acceptance-Criteria Status

| Criterion | Status   | Evidence                                                                                                                                    |
| --------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| AC-001    | Complete | `processInitialUrl()` delegates to the platform adapter, handles null, resolves, and conditionally delivers; focused cold-start tests pass. |
| AC-002    | Complete | `subscribe()` consumes runtime URL events through the thin adapter; multiple-event test passes.                                             |
| AC-003    | Complete | Adapter/intake return cleanup functions; Expo `remove()` and single-subscription behavior are tested.                                       |
| AC-004    | Complete | Resolver imports `deepLinkRoutes`; production route-literal search is empty.                                                                |
| AC-005    | Complete | `/dashboard` resolves to `dashboard` in focused tests.                                                                                      |
| AC-006    | Complete | Deal route resolves `dealDetails` with `dealId`.                                                                                            |
| AC-007    | Complete | Contact route resolves `contactDetails` with `contactId`.                                                                                   |
| AC-008    | Complete | Application route resolves `applicationDetails` with `applicationId`.                                                                       |
| AC-009    | Complete | Report route resolves `reportDetails` with `reportId`.                                                                                      |
| AC-010    | Complete | Missing, trailing, and blank/encoded-blank ID tests return failure.                                                                         |
| AC-011    | Complete | Extra-segment test returns `unsupported_route`.                                                                                             |
| AC-012    | Complete | Invalid URL and malformed percent/UTF-8 encoding tests return `malformed`.                                                                  |
| AC-013    | Complete | HTTP/custom schemes return `unsupported_scheme`; existing `createUrl()` delegation remains tested.                                          |
| AC-014    | Complete | Unexpected host, port, userinfo, absent configuration, and host casing behavior are tested.                                                 |
| AC-015    | Complete | Undeclared, duplicate, and malformed query cases return `invalid_parameters` according to current no-query registry metadata.               |
| AC-016    | Complete | `ResolvedDeepLink` contains only route/parameter/original/normalized data and no navigation object.                                         |
| AC-017    | Complete | Discriminated failure statuses cover expected invalid input without uncontrolled exceptions.                                                |
| AC-018    | Complete | Injected-clock tests cover immediate/equivalent/cold-runtime duplicates, different URLs, and expiry; expired map entries are pruned.        |
| AC-019    | Complete | No navigation import/call or screen change exists.                                                                                          |
| AC-020    | Complete | No auth/login/pending-route logic exists.                                                                                                   |
| AC-021    | Complete | No API/HTTP client import or call exists.                                                                                                   |
| AC-022    | Complete | `git diff -- apps/web` is empty.                                                                                                            |
| AC-023    | Complete | No storage/database import or persistent state exists.                                                                                      |
| AC-024    | Complete | Focused suite passes 4 suites / 37 tests; type-check and lint pass.                                                                         |
| AC-025    | Complete | Mobile README documents every required routing topic and APP-003/APP-004 boundary.                                                          |
| AC-026    | Complete | Broader-suite OOM and lack of device testing are recorded; no unexecuted validation is claimed.                                             |

## 36. Issues and Failures

- `rg` is unavailable; inspection uses `find`/`grep`.
- Initial test typing used Jest APIs missing from the repository's minimal ambient declarations; new tests were adapted and type-check then passed.
- The broader Mobile Jest run exhausted Node's 4 GB heap after six passing suites; focused routing/config tests remain passing.

## 37. Blockers and External Dependencies

- Approved real deep-link hosts remain unavailable, but injected-host resolver validation is in scope and unblocked.
- No blocker prevents routing/intake completion. Real lifecycle/device validation remains dependent on a configured host and signed association-capable build.

## 38. Security Review

Untrusted input is constrained to HTTPS, one injected DNS host, no alternate port/userinfo, exact shared-route segment counts, safe percent decoding, declared queries, and no fragments/dot/blank/extra segments. Expected failures are explicit. No input triggers navigation, auth, APIs, storage, or logging. Final review found no blocking security issue.

## 39. Architecture Risks and Concerns

- JavaScript URL parsing can normalize dot segments before matching, so raw-path validation must reject them before canonical resolution.
- Encoded path separators must remain a single structural segment and be canonically re-encoded.
- Resolver acceptance is deliberately HTTPS-only. If future evidence requires custom-scheme routing, add an explicit scheme/authority model rather than weakening verified-host checks.
- Intake is not attached to application bootstrap because APP-003/APP-004 have not defined a safe consumer; isolated adapter tests prove the lifecycle boundary without discarding results or creating premature state.
- Final in-process code review approved the implementation after adding expired-entry pruning to bound duplicate-guard session memory.

## 40. Known Gaps

- No signed-device cold-start/background lifecycle run was performed.
- The broader unrelated Mobile Jest suite cannot complete under Node's default 4 GB heap in this environment; all focused routing/config tests pass.
- Current routes approve no optional query parameters, so successful approved-query parsing is implemented but cannot be exercised without altering/injecting the authoritative registry.

## 41. APP-003 Dependency / Handoff

Ready. APP-003 can consume `DeepLinkResolution` callbacks and decide how resolved intents interact with authentication and pending-login continuation. No auth state is embedded here.

## 42. APP-004 Dependency / Handoff

Ready. APP-004 can map `routeKey` plus path/query parameters to actual navigators/screens after APP-003 policy. This ticket imports no navigation objects.

## 43. Out-of-Scope Confirmation

Confirmed: no AASA/asset-links hosting, domain/device verification, navigation, authentication continuation, authorization, resource validation, Backend call, workflow, campaign, notification, analytics, Web, database, or persistent state was implemented.

## 44. Follow-Up Recommendations

1. APP-003 should create one configured intake instance for cold/runtime paths and own callback lifecycle relative to auth hydration.
2. APP-004 should exhaustively map shared route keys to navigation destinations without changing resolver behavior.
3. Once approved hosts and association hosting exist, run signed-device cold-start, foreground, and background-resume checks.

## 45. Final Status

Complete for the scoped routing engine and testable intake boundary. All required focused routing/intake validation, type-check, lint, formatting, and Expo bundling pass. The broader Mobile Jest suite has a documented environment OOM, and real-device lifecycle validation was not performed or claimed.
