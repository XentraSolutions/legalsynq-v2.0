# DL-APP-008 — Restore Environment-Driven Native Deep-Link Configuration

## 1. Ticket Summary

Mobile configuration correction ticket. This report was created before repository investigation or implementation, as required.

## 2. Objective

Restore environment-provided, platform-correct native deep-link configuration without selecting deployment hosts in source.

## 3. Scope

Mobile Expo configuration, existing helpers/tests, focused documentation, fixture-based validation, and implementation reporting only.

## 4. Initial Implementation Plan

1. Record repository state and instructions.
2. Compare APP-001 architecture, current Mobile configuration, helpers/tests, and shared registry.
3. Document drift and intended correction before production-code edits.
4. Reconnect the smallest valid helper-based implementation and add focused regressions.
5. Run repository-supported tests, Expo config scenarios, lint/format/typecheck, documentation, diff, and boundary checks.
6. Review every acceptance criterion and record remaining DL-PLAT-002 dependencies.

## 5. Current Branch and Working-Tree State

- Branch: `feat(app)-LSV3-1189-Finalize-Deep-Link-Hosts-and-Mobile-Signing-Identities`.
- Initial HEAD: `47567cd19933c33ab1ca3b2323e0b0232a835739`.
- Initial worktree after mandatory report creation: only `analysis/DL-APP-008-report.md` was untracked.
- No pre-existing tracked user changes were observed.

## 6. Repository Instruction Review

Root `AGENTS.md` governs this area. No scoped tracked instruction exists under `apps/mobile`; dependency-local Sentry instruction files are irrelevant because generated dependencies are not being edited. Required constraints include preserving user work, using pnpm, avoiding generated outputs, keeping changes scoped, updating service-local documentation when runtime configuration changes, and running narrow Mobile validation.

## 7. DL-APP-001 Dependency Review

APP-001 established the exact intended architecture now retained in `app.config.helpers.js`: strict host-only validation, Production fail-closed, Development/QA safe omission, no fallback, iOS `applinks:` generation, structured Android filters, and direct shared-registry consumption. Historical commit `912d3a4c7` shows `app.config.js` originally called `createNativeDeepLinkConfig()` and applied its platform-specific outputs. The Mobile README still documents this behavior accurately.

## 8. DL-PLAT-002 Dependency Review

DL-PLAT-002 found the same current drift and left all candidate hosts unapproved. DL-APP-008 can complete with fixture-based config validation; approved Development/QA/Production hosts, Apple prefix, installed Android fingerprints, Play status, DNS/TLS, and deployment ownership remain external release inputs.

## 9. Current Mobile Configuration Review

`app.config.js` hard-codes `links.legalsynq.net` for Production and `links-qa.legalsynq.net` for every non-Production environment. It stores both as iOS-style `applinks:` strings and assigns the same array to `ios.associatedDomains` and `android.intentFilters`. It bypasses `EXPO_PUBLIC_DEEP_LINK_HOST`, safe omission, Production failure, helper validation, and registry-derived Android structure.

## 10. Current Helper Review

The helper remains valid and matches APP-001. It independently resolves app environment and host; validates a DNS hostname; returns `null` when non-Production lacks a host; throws clearly when Production lacks one; lowercases valid hosts; reads the actual shared registry; filters disabled routes; consumes `pathTemplate`; deduplicates exact/prefix claims; and returns distinct iOS/Android shapes. No helper production correction is currently required.

## 11. Current Test Review

Existing tests accurately exercise helper-level route derivation, Development/QA omission, injected QA/Production hosts, Production failure, and invalid hosts. They do not load `app.config.js`, so the wiring regression was invisible. The required correction is an integration-style config test plus targeted disabled-route and platform-shape assertions.

## 12. Shared Registry Review

`shared/contracts/deep-links/routes.json` is version 1 and contains five enabled `pathTemplate` routes: exact `/dashboard` and parameterized `/deals/:dealId`, `/contacts/:contactId`, `/applications/:applicationId`, and `/reports/:reportId`. It remains authoritative and must not change.

## 13. Mobile Identity Review

Current repository values match the ticket: Development/QA use iOS and Android identity `com.legalsynq.qa`; Production uses `com.legalsynq`. `eas.json` maps development, preview/QA, and production profiles without a UAT identity. These values will remain unchanged.

## 14. Configuration Drift Analysis

APP-001 helper, tests, registry, and documentation remain aligned. Only `app.config.js` wiring drifted: commit `d417f1ad4` replaced the helper import/call with a source-level environment-to-host ternary and reused its iOS string output for Android. Thus the current helper is correct, existing helper tests describe intended behavior, and `app.config.js` bypasses them.

## 15. Root Cause

The root cause is an isolated wiring override in commit `d417f1ad4` (`chore: Update associatedDomains`). It removed `createNativeDeepLinkConfig()` from composition and introduced one untyped, platform-agnostic array. Lack of a test that evaluates the exported Expo config allowed helper tests to remain green while production wiring regressed.

## 16. Intended Architecture

`EXPO_PUBLIC_DEEP_LINK_HOST` supplies the host to the existing Mobile helper; iOS receives `applinks:<host>`, while Android receives structured registry-derived HTTPS intent filters.

## 17. Host Input Contract

Use only `EXPO_PUBLIC_DEEP_LINK_HOST`, trimmed and validated as a DNS hostname without scheme, port, path, query, or fragment. Valid values are normalized to lowercase. There is no default and no transformation from a URL or another environment variable.

## 18. Environment Isolation Design

Application identity remains selected by `EXPO_PUBLIC_APP_ENV`; association host remains independently supplied by `EXPO_PUBLIC_DEEP_LINK_HOST`, with no source mapping or cross-environment fallback.

Non-Production without a host emits empty native association arrays. Production without a host throws. A host supplied to one config evaluation cannot be sourced from another environment because there is no mapping or fallback state.

## 19. Implementation Changes

- Reconnected `app.config.js` to `createNativeDeepLinkConfig()`.
- Applied `iosAssociatedDomains` only to iOS and `androidIntentFilters` only to Android.
- Added exported-config integration tests, disabled-route coverage, platform-shape assertions, identity checks, non-Production omission, and Production failure coverage.

## 20. Hard-Coded Host Removal

The source-level QA/Production ternary and both candidate domains were removed from `app.config.js`. Final repository search remains pending.

## 21. iOS Associated Domains Implementation

`ios.associatedDomains` now receives `nativeDeepLinkConfig.iosAssociatedDomains`, which is empty for missing non-Production host or exactly `applinks:<validated-host>` when configured.

## 22. Android Intent Filter Implementation

`android.intentFilters` now receives `nativeDeepLinkConfig.androidIntentFilters`, restoring structured `VIEW`, `autoVerify`, `BROWSABLE`/`DEFAULT`, HTTPS, host, and registry-derived route data.

## 23. Shared Registry Integration

The existing helper directly requires `../../shared/contracts/deep-links/routes.json`; reconnecting `app.config.js` restores this path without a copied Mobile registry.

## 24. pathTemplate Compatibility

The helper already consumes `route.pathTemplate`. No obsolete schema field was found.

## 25. Bundle / Package Identity Preservation

Baseline verified: `com.legalsynq.qa` for non-Production and `com.legalsynq` for Production on both platforms. The planned correction does not edit identity expressions.

## 26. Regression Test Changes

Added a real exported-Expo-config loader and tests that would fail if `app.config.js` again bypasses the helper or reuses iOS strings for Android. Added disabled-route fixture coverage while retaining a real-registry integration assertion.

## 27. Files Inspected

- User-provided DL-APP-008 ticket attachment.
- Repository delivery-mode skill instructions.
- Root `AGENTS.md`.
- `analysis/DL-APP-001-report.md` and `analysis/DL-PLAT-002-report.md`.
- Current and historical APP-001 versions of `apps/mobile/app.config.js`, `app.config.helpers.js`, and `app.config.test.js`.
- `apps/mobile/package.json`, `eas.json`, `README.md`, Jest/ESLint/Prettier/TypeScript configuration.
- `shared/contracts/deep-links/routes.json` and `README.md`.
- Relevant local Git history and blame.

## 28. Files Added

- `analysis/DL-APP-008-report.md`

## 29. Files Modified

- `apps/mobile/app.config.js`
- `apps/mobile/app.config.test.js`

## 30. Files Deleted

None.

## 31. Implementation Progress

| Area | Status | Completion |
|---|---|---:|
| Drift analysis | Done | 100% |
| Host source correction | Done | 100% |
| iOS configuration | Done | 100% |
| Android configuration | Done | 100% |
| Registry integration | Done | 100% |
| Environment isolation | Done | 100% |
| Tests | Done | 100% |
| Expo config validation | Done | 100% |
| Documentation | Done | 100% |
| Boundary validation | Done | 100% |

## 32. Validation Commands and Results

- `pnpm exec prettier --write app.config.test.js app.config.js app.config.helpers.js && pnpm exec jest --runInBand app.config.test.js src/shared/deepLinks/index.test.ts` from `apps/mobile`; purpose: format and run focused tests; result: FAIL before either tool executed; exit 1. pnpm attempted registry metadata access and a dependency-directory reconciliation, then aborted because network/TTY were unavailable. Warning: pnpm 11.1.2 differs from the repository declaration and reported an update. This was not introduced by DL-APP-008. Rerun required via existing project-local binaries.
- `./node_modules/.bin/prettier --write app.config.test.js app.config.js app.config.helpers.js && ./node_modules/.bin/jest --runInBand app.config.test.js src/shared/deepLinks/index.test.ts` from `apps/mobile`; purpose: format modified config files and run focused config/shared-registry tests; PASS, exit 0; 2 suites and 17 tests passed. No warnings; successful rerun.
- Development `expo config --type prebuild --json` with `EXPO_PUBLIC_APP_ENV=development` and host unset, filtered to native fields; PASS, exit 0; QA bundle/package and empty association arrays.
- QA fixture `expo config --type prebuild --json` with `links.qa-fixture.example.test`; PASS, exit 0; QA identities, one iOS domain, and one structured Android filter with five registry-derived route claims.
- Production fixture `expo config --type prebuild --json` with `links.production-fixture.example.test`; PASS, exit 0; Production identities and isolated platform-correct claims.
- Production `expo config --type prebuild --json` with host unset; expected fail-closed result, exit 1; explicit `EXPO_PUBLIC_DEEP_LINK_HOST is required for production Mobile builds.` diagnostic. No rerun required because failure is the required outcome.
- `./node_modules/.bin/eslint src --ext .ts,.tsx --max-warnings 0`, focused config-test ESLint, and Node syntax checks from `apps/mobile`; PASS, exit 0; no output/warnings.
- `./node_modules/.bin/prettier --check app.config.js app.config.helpers.js app.config.test.js README.md` from `apps/mobile`; PASS, exit 0; all matched files formatted.
- `./node_modules/.bin/tsc --noEmit -p tsconfig.json` from `apps/mobile`; PASS, exit 0; no warnings.
- After final test de-duplication review, `./node_modules/.bin/prettier --write app.config.test.js && ./node_modules/.bin/jest --runInBand app.config.test.js src/shared/deepLinks/index.test.ts && ./node_modules/.bin/eslint app.config.helpers.js app.config.test.js --max-warnings 0` from `apps/mobile`; PASS, exit 0; formatting unchanged, 2 suites/17 tests passed, lint emitted no warnings. No failure/rerun required.
- Final repository checks from root: `python3 scripts/check-doc-sync.py` PASS exit 0; `git diff --check` PASS exit 0; untracked-report `git diff --no-index --check` produced no whitespace errors (difference exit is expected); report heading count is 57; tracked-Mobile hard-coded candidate search returned no matches; Backend/Web/shared boundary diff returned no paths.

## 33. Focused Test Validation

PASS: 2 suites, 17 tests, 0 snapshots. The real shared registry test and exported-config integration regressions executed successfully.

## 34. Development No-Host Config Validation

PASS: `com.legalsynq.qa` iOS/Android identities with empty `associatedDomains` and `intentFilters`; valid Expo config, no fallback.

## 35. QA Fixture-Host Config Validation

PASS: `com.legalsynq.qa`; iOS `applinks:links.qa-fixture.example.test`; structured Android HTTPS filter with `VIEW`, `autoVerify: true`, both categories, exact `/dashboard`, and four slash-terminated prefixes.

## 36. Production Fixture-Host Config Validation

PASS: `com.legalsynq`; isolated `links.production-fixture.example.test` iOS/Android claims with the same five registry-derived route entries and no QA fallback.

## 37. Production Missing-Host Validation

PASS as an expected failure: Expo config exited 1 with the deterministic missing `EXPO_PUBLIC_DEEP_LINK_HOST` reason and no fallback.

## 38. Generated iOS Config Review

Resolved Expo prebuild config contains only `applinks:<fixture-host>` when configured and an empty array for Development without a host. Bundle IDs remain environment-correct. No protocol/path, Android object, Team ID, native prebuild, OS verification, or device verification is claimed.

## 39. Generated Android Config Review

Resolved Expo prebuild config contains one structured `VIEW` filter with `autoVerify: true`, `BROWSABLE` and `DEFAULT`, HTTPS, active fixture host, exact `/dashboard`, and prefixes `/deals/`, `/contacts/`, `/applications/`, `/reports/`. No iOS string appears. Package IDs remain environment-correct.

## 40. Lint Validation

PASS: repository Mobile source lint, focused config helper/test lint, and Node syntax checks all exited 0.

## 41. Formatting Validation

PASS for files governed by the existing formatting pattern: final Prettier check exited 0 for helper, test, and existing aligned README. `app.config.js` intentionally retains its pre-existing JSON-style formatting to avoid unrelated whole-file churn; Node syntax validation passed. The package defines no general format script and its `lint-staged` Prettier rule targets only TypeScript files.

## 42. Typecheck Validation

PASS: Mobile TypeScript project check exited 0.

## 43. Documentation Validation

The existing Mobile README already describes the restored behavior and validation commands; no Mobile documentation edit was necessary. `python3 scripts/check-doc-sync.py` passed.

## 44. Hard-Coded Host Search

PASS: tracked-Mobile `git grep` found neither `links-qa.legalsynq.net` nor `links.legalsynq.net`, and found no remaining `EXPO_PUBLIC_APP_ENV`-to-host mapping or array-style native fallback. Fixture `.example.test` hosts remain only in tests/validation documentation.

## 45. Boundary / Scope Validation

The scoped diff contains only `apps/mobile/app.config.js`, `apps/mobile/app.config.test.js`, and this report. No `apps/gateway/**`, `apps/web/**`, Backend service, or `shared/contracts/deep-links/**` file changed. Shared registry semantics and generated native projects are unchanged.

## 46. Acceptance-Criteria Status

| Criterion | Status | Evidence |
|---|---|---|
| AC-001 | Complete | Sections 9, 14, and 15 identify the bypass and platform-shape drift. |
| AC-002 | Complete | Candidate host ternary removed; tracked-Mobile search is empty. |
| AC-003 | Complete | `app.config.js` calls the helper that reads `EXPO_PUBLIC_DEEP_LINK_HOST`. |
| AC-004 | Complete | Production/no-host Expo config exits 1 with explicit diagnostic. |
| AC-005 | Complete | Development/no-host config exits 0 with empty claim arrays. |
| AC-006 | Complete | No host map/default exists; isolated fixture configs contain only supplied host. |
| AC-007 | Complete | Configured iOS output is exactly `applinks:<host>`. |
| AC-008 | Complete | Strict hostname validation rejects protocol/path; generated entry contains neither. |
| AC-009 | Complete | Android output is a structured Expo intent-filter object. |
| AC-010 | Complete | Resolved QA/Production filters contain `autoVerify: true`. |
| AC-011 | Complete | Resolved filters contain `BROWSABLE` and `DEFAULT`. |
| AC-012 | Complete | Every generated Android data entry uses `https`. |
| AC-013 | Complete | Every generated Android data entry uses the injected active host. |
| AC-014 | Complete | Helper directly requires real `shared/contracts/deep-links/routes.json`; integration test executes it. |
| AC-015 | Complete | Helper and isolated regression fixture use `pathTemplate`. |
| AC-016 | Complete | Disabled-route fixture is excluded by passing test. |
| AC-017 | Complete | No Mobile route catalog added; real-registry expectations are data-driven. |
| AC-018 | Complete | Expo scenario outputs preserve QA/Production bundle IDs. |
| AC-019 | Complete | Expo scenario outputs preserve QA/Production package IDs. |
| AC-020 | Complete | Existing APP-001 helper is reused; no duplicate config logic added. |
| AC-021 | Complete | Exported-config/platform-shape, identity, omission, failure, registry, and disabled-route tests added. |
| AC-022 | Complete | 2 focused suites / 17 tests pass. |
| AC-023 | Complete | Development/no-host Expo config is valid with empty claims. |
| AC-024 | Complete | QA fixture Expo config has correct identity and native shapes. |
| AC-025 | Complete | Production fixture Expo config has correct identity and native shapes. |
| AC-026 | Complete | Production/no-host Expo config fails deterministically. |
| AC-027 | Complete | Boundary diff shows no Backend/Gateway change. |
| AC-028 | Complete | Boundary diff shows no Web change. |
| AC-029 | Complete | Shared registry has no diff. |
| AC-030 | Complete | Only fixture hosts are used; report retains DL-PLAT-002 blockers. |
| AC-031 | Complete | Existing Mobile README already matches restored behavior; Prettier/doc checks pass. |
| AC-032 | Complete | Only generated Expo config is claimed; no live HTTPS, OS, or device result is claimed. |

## 47. Issues and Failures

- Initial pnpm-mediated validation exited 1 before Prettier/Jest because the local dependency state prompted pnpm to fetch metadata and purge/reinstall without a TTY. No dependency mutation was authorized or performed; project-local binaries will be used for the rerun.
- Initial Prettier write reformatted all of `app.config.js`; scoped review caught this and the baseline style was restored with only the functional eight-line diff retained. All tests were rerun afterward and passed.

## 48. Blockers and External Dependencies

DL-APP-008 has no remaining Mobile configuration blocker. Release operation still depends on DL-PLAT-002 resolving: Development verified-link strategy; approved QA and Production hosts; Apple Team ID/App Identifier Prefix; QA installed Android fingerprint/distribution; Production installed app-signing fingerprint; Play App Signing status; DNS/TLS ownership; and association deployment ownership. None is encoded or inferred here.

## 49. Security Review

No secret, signing key, certificate, provisioning profile, token, or credential was inspected or added. The host variable is intentionally public but validated. Fixture domains are reserved under `.test` and appear only in tests/command-local validation. Production fails closed rather than binding an unknown host.

## 50. Architecture Risks and Concerns

The corrected integration tests now guard cross-environment fallback, platform-shape reuse, identity drift, and bypass of helper wiring. Residual operational risk remains until DL-PLAT-002 supplies approved host and association identities; config correctness alone does not establish OS trust or public availability.

## 51. Known Gaps

No native prebuild, Xcode/Gradle build, live HTTPS request, domain verification, Apple/Android OS verification, or physical-device test ran; these are out of scope. Approved release values remain unavailable. The repository pnpm wrapper attempted dependency reconciliation, so validation used existing project-local binaries without modifying dependencies.

## 52. DL-PLAT-002 Handoff

Mobile is ready to consume a separately approved `EXPO_PUBLIC_DEEP_LINK_HOST` per environment. DL-PLAT-002 must still provide the release inputs listed in Section 48; no candidate domain was promoted.

## 53. APP-001 Readiness

**READY — fixture-validated.** APP-001 environment-driven helper wiring, fail-closed/omission behavior, platform shapes, shared-registry integration, and identities are restored. Release builds remain awaiting DL-PLAT-002 inputs.

## 54. DL-QA-001 Handoff

**READY for configuration-level fixture review; BLOCKED for live/device verification by DL-PLAT-002 inputs and association deployment.** QA can use the scenario evidence in Sections 34–39, then repeat with approved artifacts when available.

## 55. Out-of-Scope Confirmation

No implementation was performed for Platform host approval, DNS, TLS, Apple Team ID/Prefix, Android signing fingerprints, Play Console, Gateway, Backend services, Web, shared route semantics, database, Deal, Report, association deployment, live HTTPS, physical-device validation, campaigns, notifications, or analytics.

## 56. Follow-Up Recommendations

1. Merge this narrow Mobile correction before supplying any approved release host.
2. Have DL-PLAT-002 deliver approved per-environment hosts and association identities through release configuration, never source mappings.
3. After association deployment, execute DL-QA-001 live endpoint and physical-device verification without changing this host contract.

## 57. Final Status

**Complete — Awaiting DL-PLAT-002 Release Inputs**
