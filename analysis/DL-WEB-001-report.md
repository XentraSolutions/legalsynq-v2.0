# DL-WEB-001 Implementation Report

## Status

Complete with constrained full-project validation.

## Repository Baseline

- Branch: `feat(app)-LSV3-1193-Implement-Web-to-Mobile-Deep-Link`
- HEAD: `e6d3049b71d330bb07a6ec1934120c337bb7e807`
- Initial working tree: clean
- Instructions reviewed: root `AGENTS.md` supplied for this task; project delivery-mode implementation workflow.

## Initial Plan

1. Review prior deep-link reports, the shared registry and its exports, and Web configuration/helper/test conventions.
2. Document current-state findings and choose the smallest compatible builder API and error behavior.
3. Implement registry-backed HTTPS URL generation and focused unit/integration/config coverage.
4. Update Web documentation and run targeted tests, type checking, lint/format/build checks, documentation sync, and boundary checks.
5. Review the final diff and acceptance criteria without changing Mobile, backend, gateway, or shared route semantics.

## Progress

| Area | Status | Completion |
|---|---|---:|
| Current Web review | Done | 100% |
| Shared registry integration | Done | 100% |
| Base URL validation | Done | 100% |
| Builder implementation | Done | 100% |
| Parameter handling | Done | 100% |
| Environment isolation | Done | 100% |
| Tests | Done | 100% |
| Documentation | Done | 100% |
| Boundary validation | Done | 100% |

## Findings and Decisions

### Current State

- The authoritative `shared/contracts/deep-links/routes.json` is schema version 1 and currently defines five enabled keys: `dashboard`, `dealDetails`, `contactDetails`, `applicationDetails`, and `reportDetails`. Templates use `pathTemplate`; required placeholders are also declared in `requiredPathParameters`.
- Shared TypeScript exports already provide immutable registry loading/validation, `DeepLinkError`, URL generation, path-segment encoding, unknown/disabled route rejection, required/blank/extra parameter rejection, trailing-slash normalization, and unresolved schema protection. The Web should adapt these exports rather than copy route templates or create a second catalog.
- A partial Web adapter already exists at `apps/web/src/lib/deep-links.ts`, with tests at `apps/web/src/lib/__tests__/deep-links.test.ts`. It requires both `NEXT_PUBLIC_ENV` and `NEXT_PUBLIC_DEEP_LINK_BASE_URL`, permits HTTP outside Production through the shared generator, and exposes the shared query-parameter input even though this ticket excludes general query support.
- The existing tests consume the production registry and cover several shared behaviors, but do not cover every current route, all required parameter edge cases, malformed base components, or an explicit unresolved-placeholder assertion.
- Web documentation currently lists the environment variable and points to shared documentation, but does not yet provide the ticket's feature-integration rules or distinguish Deal/Report generation support from Mobile destination readiness.
- Repository history shows the partial adapter arrived with the cross-runtime deep-link foundation (`b9f2cd541`); there is no separate Web feature integration and no current `Open in App` UI.
- No approved deployment host exists. DL-PLAT-002 leaves Development/QA/Production host approval, DNS/TLS, and deployment ownership blocked. Fixture `.example.test` URLs are suitable only for tests.
- `apps/web/package.json` uses Node's test runner for `src/lib/__tests__/*.test.ts`, followed by Vitest for the wider suite; TypeScript is strict with JSON module resolution. There is no Web-local ESLint or Prettier script in `package.json`, so validation must use repository-installed binaries/configuration without inventing package scripts.
- `rg` is unavailable in this environment; repository searches use `find`/`grep` as the documented fallback.

### Selected Design

- Keep the existing Web utility location and shared `DeepLinkError` convention.
- Expose a route-intent API, `buildDeepLink({ routeKey, pathParams })`, with no raw-path, host-override, or query-parameter surface.
- Read only `NEXT_PUBLIC_DEEP_LINK_BASE_URL`; do not select or infer a host from an environment value. Validate it as an HTTPS origin with no credentials, path, query, or fragment, normalize one trailing slash through `URL.origin`, and pass the normalized configuration to the shared registry-backed generator.
- Retain the existing configuration resolver's source injection for deterministic base-URL tests. Exercise disabled-route behavior through the exact shared generator delegated to by the Web builder, using a synthetic route without changing the real registry.
- Treat caller values as raw path-segment values and encode them once using the shared generator; already percent-encoded input is therefore encoded again, avoiding ambiguous decode/re-encode behavior.
- Reject unexpected path parameters and verify the generated result has no `:placeholder` token. General query generation remains unavailable from the Web API.

## Changes

- Created this report before modifying any other repository file, as required by the ticket.
- Reworked `apps/web/src/lib/deep-links.ts` into a route-intent-only `buildDeepLink({ routeKey, pathParams })` API backed by the real shared registry.
- Removed Web's dependency on `NEXT_PUBLIC_ENV` for link selection and enforced HTTPS-origin-only validation for every generated Web deep link.
- Removed query parameters and per-call environment/host injection from the feature-facing builder contract.
- Expanded focused tests across all five current routes, real-registry loading, disabled/unknown routes, required and unexpected parameters, encoding, missing configuration, unsafe base URLs, trailing slash normalization, and fallback isolation.
- Expanded `apps/web/README.md` with configuration, usage, safety, destination-readiness, and follow-up integration guidance.

## Validation

- Focused test: `./node_modules/.bin/tsx --test apps/web/src/lib/__tests__/deep-links.test.ts` passed 9/9 tests. Node emitted only the existing `module.register()` deprecation warning.
- Focused TypeScript: direct `tsc` invocation over the changed helper/test and imported shared modules passed with no diagnostics.
- Web library suite: direct `tsx --test apps/web/src/lib/__tests__/*.test.ts` ran 90 tests: 58 passed, 22 failed, and 10 were cancelled. The new 9-test deep-link suite passed. Unrelated suites failed because the direct runner could not resolve existing `@/` imports such as `@/lib/normalize-utc`, `@/lib/careconnect-login-url`, and `@/types`.
- Package-script TypeScript: `pnpm --dir apps/web type-check` failed with `[ERROR] fetch failed` in the current incomplete dependency environment. Direct full-project `tsc` confirmed extensive pre-existing missing-package diagnostics (`@tanstack/react-query`, `@tanstack/react-table`, `sonner`, and others) and unrelated application typing errors; it reported no diagnostic in either changed deep-link TypeScript file.
- Formatting: the repository has no installed Prettier binary and Web defines no format script, so an automated Prettier check could not run. Changed TypeScript was manually aligned to the repository's existing Prettier style, and `git diff --check` passed.
- Lint: Web defines no lint script/configuration and no ESLint binary is installed, so no repository-supported lint command was available.
- Build: not run because the same missing installed dependencies that block full-project type checking would make a production build non-diagnostic for this scoped change.
- Documentation sync: `python3 scripts/check-doc-sync.py` passed.
- Security/source searches: no candidate QA/Production host, `NEXT_PUBLIC_DEEP_LINK_BASE_URL` fallback expression, or base-plus-feature-path construction was found in Web source/docs.
- Boundary validation: `git diff --name-only -- apps/mobile apps/gateway apps/services shared/contracts/deep-links/routes.json` returned no paths. `git status --short` contains only the Web helper, focused test, Web README, and this report.
- Whitespace: `git diff --check` passed.

## Risks, Blockers, and Remaining Work

- The approved production/QA deep-link host remains outside this ticket and depends on DL-PLAT-002/release configuration.
- Approved environment hosts, DNS/TLS, and deployment ownership remain blocked on DL-PLAT-002/release coordination; no candidate host was promoted by this implementation.
- Physical-device and OS-level Web-to-App behavior was not tested and is not claimed.
- Full Web typecheck/build/lint/format validation remains constrained by the current dependency/tooling state described above; focused implementation checks pass.
- The shared deep-link README still describes the older Web adapter name and non-Production HTTP behavior. That shared area was explicitly read-only for this ticket, so aligning its cross-runtime documentation should be tracked separately rather than reopening query/host override behavior in the Web API.

## Final Review

- Independent reviewer: approved the implementation's correctness, HTTPS-origin security, environment isolation, route-intent-only API, shared registry use, path-segment encoding, parameter validation, and scope boundaries.
- Review follow-ups applied: restored all environment variables mutated by tests, renamed disabled-route coverage to avoid overstating the test seam, and corrected this report's description of dependency injection.
- Review follow-up deferred: shared cross-runtime README alignment is outside the ticket's allowed file boundary and is recorded above.
- Post-review rerun: focused tests passed 9/9, focused TypeScript passed, documentation sync passed, and `git diff --check` passed.
