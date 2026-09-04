# DL-APP-009 — Generic Portal-Root App Association Report

## 1. Ticket Summary
Implementation investigation started; this report is the first DL-APP-009 repository change.
## 2. Objective
Implement exact-root OS association and benign Mobile entry while preserving normal auth/default navigation.
## 3. Simplified Phase-1 Contract
Configured HTTPS host, exact `/`, no parameters, no resource intent, Dashboard when authenticated and Login then Dashboard when unauthenticated.
## 4. Initial Implementation Plan
Inspect current config, association helper, resolver/bootstrap/auth/navigation, tests, and Platform generator boundary; implement the smallest Mobile-only scope and validate it.
## 5. Current Branch and Working-Tree State
- Branch: `feat(app)-LSV3-1193-Implement-Web-to-Mobile-Deep-Link`
- HEAD: `2f5b4c2a89b3656dea6e8f6547f2af2d6e63340e`
- Pre-existing user change preserved: `analysis/DL-PLAT-002-report.md` modified.
- This report did not alter that prior change.
## 6. Repository Instruction Review
Root `AGENTS.md` and implementation-mode skill reviewed. Mobile has no scoped `AGENTS.md` identified yet.
## 7. DL-PLAT-002 Findings Review
Mobile can implement exact `/` independently; approved hosts, signing identities, DNS/TLS, deployment, and device QA remain external.
## 8. DL-APP-008 Current State Review
APP-008 correctly restored external host input, non-production omission, Production fail-closed behavior, and platform-correct shapes, but Android still derived resource paths.
## 9. Current Mobile App Configuration
Expo config selects QA/Production identities and delegates native claims to `createNativeDeepLinkConfig()`.
## 10. EXPO_PUBLIC_DEEP_LINK_HOST Review
It remains the sole validated host input; there is no environment-name fallback.
## 11. Current iOS Associated Domains
Configured builds retain exactly `applinks:<host>`. AASA owns iOS path narrowing.
## 12. Current Android Intent Filters
Before this ticket the valid verified filter claimed `/dashboard` and four resource prefixes.
## 13. Current Association Helper
The helper mixed OS association scope with the shared resource registry.
## 14. AASA Generator Boundary
Platform-owned `scripts/deep-links/generate-association-files.py` still derives AASA components from the resource registry. It is not changed here.
## 15. Current Resolver / Parser
The resolver retains strict HTTPS/host/resource parsing and existing resource route support.
## 16. Current Root URL Behavior
Exact `/` previously became `unsupported_route` because empty path segments were rejected.
## 17. Current Unsupported-Link Behavior
Failures are passed through intake but ignored by auth coordination; no user-visible renderer was found. Root is now explicitly benign rather than relying on that incidental behavior.
## 18. Current Auth Bootstrap
Auth hydrates before rendering RootNavigator. Only `resolved` resource intents can be pending or emitted.
## 19. Current Default Authenticated Landing
`Main → Tabs → Dashboard` (Home).
## 20. Current Unauthenticated Landing
`Auth → Login`; successful auth naturally selects Main and its Dashboard default.
## 21. Generic Association Scope Design
Done: `PHASE_ONE_ASSOCIATION_PATH = '/'` and root Android claim are distinct from parser/registry semantics.
## 22. Root Resolver Decision
Option A-style typed benign result: exact host root returns `portal_entry`. Auth treats all non-`resolved` results as no-op, so it stores no pending intent and dispatches no navigation.
## 23. Resource Parser Preservation Decision
Existing shared registry and resource parser behavior will remain unchanged unless inspection proves a narrow compatibility change necessary.
## 24. Implementation Changes
Replaced Android registry-derived claims with exact root; added `portal_entry` result and resolver handling; added config/resolver/auth tests; updated Mobile documentation.
## 25. iOS Generic Association
Host entitlement is preserved. Exact `/` AASA generation is documented as Platform follow-up.
## 26. Android Generic Association
One HTTPS exact `path: '/'` entry with VIEW, DEFAULT, BROWSABLE, and `autoVerify: true`.
## 27. Generic Root Launch Behavior
Exact root is recognized without resource route/parameters and causes no explicit navigation.
## 28. Authenticated Root Launch
Portal entry is ignored by auth coordinator; existing Main/Tabs/Dashboard default remains authoritative.
## 29. Unauthenticated Root Launch
Portal entry is not stored; Login appears normally and successful auth uses Dashboard default.
## 30. Pending Intent Behavior
Verified across hydrating, unauthenticated, and authenticated states: pending remains null and no ready event is emitted.
## 31. Files Inspected
Ticket, instructions, prior reports, Mobile config/helper/tests, resolver/intake/auth/navigation code/tests, README/package tooling, shared registry, and Platform association scripts/tests.
## 32. Files Added
`analysis/DL-APP-009-report.md`.
## 33. Files Modified
`apps/mobile/app.config.helpers.js`, `app.config.test.js`, `README.md`, `DeepLinkTypes.ts`, `DeepLinkResolver.ts`, `DeepLinkResolver.test.ts`, `DeepLinkAuthCoordinator.test.ts`, and this report.
## 34. Files Deleted
None.
## 35. Tests Added / Updated
Config exact-root/resource-exclusion assertions; root resolver/query/fragment cases; auth no-op checks in all three auth states.
## 36. Validation Commands and Results
- `./node_modules/.bin/prettier --write ...` — PASS; files already formatted.
- `./node_modules/.bin/jest --runInBand app.config.test.js src/shared/services/DeepLinking src/navigation/DeepLinkNavigation` — PASS, 9 suites/87 tests.
- `./node_modules/.bin/tsc --noEmit -p tsconfig.json` — PASS after correcting unsupported `it.each` typing in the new test.
- `./node_modules/.bin/eslint src --ext .ts,.tsx --max-warnings 0` and focused config lint — PASS.
- `./node_modules/.bin/prettier --check ...` — PASS.
- QA, Production, and Development `expo config --type prebuild --json` fixture runs — PASS with expected root-only/omission shapes.
- Production missing-host Expo config — expected FAIL, exit 1 with explicit required-host message.
- `bash scripts/tests/test-deep-link-association-tools.sh` — PASS; confirms unchanged Platform tooling still works, though it retains resource AASA scope.
- `python3 scripts/check-doc-sync.py` and `git diff --check` — PASS.
- Web/Gateway/services/shared-registry diff guards — PASS; no DL-APP-009 changes.
## 37. App Config Validation
PASS: root-only, host isolation, omission, and fail-closed tests.
## 38. Root Resolver Validation
PASS: exact root returns `portal_entry`; unknown non-root remains unsupported; resource routes remain resolved.
## 39. Auth / Navigation Regression
PASS: portal entry never becomes pending or ready navigation in hydrating/unauthenticated/authenticated states; existing navigation suites pass.
## 40. Resource Parser Regression
PASS: existing dashboard/contact/application/deal/report resolver coverage remains green.
## 41. Expo Config Validation
PASS: QA and Production fixture hosts emit iOS host plus one Android exact-root entry; Development without host emits none; Production without host exits 1 explicitly.
## 42. Association Tooling Validation
PASS: existing tool test. Generator itself remains unchanged and still needs Platform root-scope follow-up.
## 43. Source Guard
No hard-coded candidate host, resource association claim, custom scheme/detection/store logic introduced.
## 44. Boundary Validation
PASS: shared registry, Web, Gateway, and Backend remain unchanged by DL-APP-009. The pre-existing `analysis/DL-PLAT-002-report.md` modification was preserved.
## 45. Acceptance-Criteria Status
| Criteria | Status |
|---|---|
| AC-001–AC-007 | Complete |
| AC-008 | Partially complete — exact `/` defined; Platform AASA generation pending |
| AC-009–AC-034 | Complete |
| AC-035 | Complete — Platform boundary and undeployed AASA state are explicit |
| AC-036 | Complete — no physical handoff claimed |
## 46. Issues and Failures
Initial TypeScript run failed because repository Jest globals do not type `it.each`; test was rewritten as the established loop pattern, then all checks passed. A Production missing-host Expo run exited 1 as expected.
## 47. Platform Dependencies
Approved hosts/signing identities/deployment remain external and are not required for fixture-based Mobile implementation.
## 48. Required Platform Artifact Follow-Up
Required: update the Platform AASA generator/validator to consume generic exact `/` association scope independently of the resource registry, then generate/deploy only after approved host and signing inputs exist.
## 49. Security Review
No secret or signing material introduced.
## 50. Known Gaps
Approved host/Apple prefix/Android fingerprints, deployed artifacts, and physical verification remain external DL-PLAT-002 gaps.
## 51. QA Handoff
Physical-device validation is not part of this implementation.
## 52. Out-of-Scope Confirmation
No Web, Backend, Gateway, shared-registry, DNS/TLS, signing, store, or custom-scheme change made.
## 53. Final Status
**Complete — Mobile Generic Portal Association Implemented, Platform Artifact Follow-Up Required.**

| Area | Status | Completion |
|---|---|---:|
| Current association review | Done | 100% |
| Generic scope design | Done | 100% |
| iOS association | Done | 100% |
| Android association | Done | 100% |
| Root resolver behavior | Done | 100% |
| Authenticated launch | Done | 100% |
| Unauthenticated launch | Done | 100% |
| Resource parser preservation | Done | 100% |
| Config tests | Done | 100% |
| Navigation tests | Done | 100% |
| Type/lint validation | Done | 100% |
| Boundary checks | Done | 100% |
