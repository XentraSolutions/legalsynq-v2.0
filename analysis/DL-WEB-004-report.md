# DL-WEB-004 Implementation Report

## Ticket Summary

Add an `Open in App` action to the canonical Web Dashboard through the existing DL-WEB-001 deep-link builder.

## Initial State

- Branch: `feat(app)-LSV3-1193-Implement-Web-to-Mobile-Deep-Link`
- HEAD: `ebf0a6acc7a6c35d0f8a65b33050acfaedaf2c4f`
- Initial working tree: clean
- This report is the first DL-WEB-004 repository change.

## Governing Instructions

Root `AGENTS.md`, the repository implementation delivery workflow, and the user-provided DL-WEB-004 specification govern this task. The specification is treated as implementation requirements, not as a separate user request.

## Scope

Dashboard Web UI integration, focused tests, validation, and this report only. Mobile, Gateway, backend services, shared-route semantics, database, release infrastructure, native-app detection, fallbacks, and physical-device verification remain out of scope.

## Implementation Plan

1. Verify DL-WEB-001 and the authoritative parameterless `dashboard` route.
2. Inspect prior integrations and determine whether presentation can be reused without a domain dependency.
3. Inspect the canonical Dashboard surface, actions, permissions, loading/error states, tests, responsive layout, and tooling.
4. Record placement and unavailable-state decisions before modifying Dashboard code.
5. Implement the smallest builder-backed semantic HTTPS action and focused coverage.
6. Run builder regression, Dashboard/Web validation, source/boundary checks, documentation sync, and independent review.

## Findings

- DL-WEB-001 is present and complete at current HEAD. `apps/web/src/lib/deep-links.ts` exports `buildDeepLink({ routeKey, pathParams? })`, reads only `NEXT_PUBLIC_DEEP_LINK_BASE_URL`, validates an HTTPS origin, throws the shared typed `DeepLinkError`, and delegates route generation to the authoritative shared registry. Its nine-test suite covers registry loading, every registered route, parameter rules, encoding, invalid/missing configuration, and fallback isolation.
- `shared/contracts/deep-links/routes.json` defines enabled key `dashboard`, template `/dashboard`, and no required or optional parameters. Dashboard feature code will pass only `{ routeKey: "dashboard" }` and will not read the template.
- The canonical general Web Dashboard is `/dashboard`, owned by the async server component `apps/web/src/app/(platform)/dashboard/page.tsx` inside the authenticated/organization-guarded platform layout and shared `AppShell`.
- The page calls `requireOrg()` before rendering. Product tiles derive from the session's product access, administration shortcuts derive from existing admin flags, and product-specific portal hosts redirect before the canonical Dashboard UI appears. No new authorization gate is needed.
- The Dashboard has no local client loading/error state or data-fetching hooks. Authentication/organization redirects and server errors occur before its returned UI. Builder failure can be contained locally without altering those paths.
- The established top surface is the welcome header. Below it are responsive product quick-action cards and conditional administration cards; there are no Dashboard filters, charts, tables, date controls, refresh/export buttons, overflow menus, or existing header action group to preserve.
- The page is constrained by `max-w-4xl`; product and administration grids progress from one column to two at `sm` and three at `lg`. A stacked mobile header that becomes a justified row at `sm` is the smallest responsive addition.
- Prior Contact and Application integrations provide domain-specific link components only. Neither is a domain-neutral reusable component, so Dashboard will own a small wrapper rather than depend on another feature or broaden this ticket into a refactor.
- Web component tests use Vitest/jsdom/Testing Library. `apps/web/package.json` has `test`, `type-check`, and `build` scripts but no lint or formatting script.

## UI Decisions

- Place `Open in App` beside the welcome copy in a new responsive header action position: stacked/full-width-safe on narrow screens and aligned right from `sm` upward. Product/admin quick actions remain untouched.
- Use visible wording `Open in App`, a decorative phone icon, a semantic same-context anchor, native keyboard behavior, and existing border/focus utilities. Do not force a new tab.
- Hide the action for expected typed builder/configuration errors. The welcome copy and all Dashboard tiles remain functional; unexpected programming errors are rethrown.
- Because Dashboard has no identifier and the shared route declares no parameters, call the builder with only `routeKey: "dashboard"`. Do not add path parameters, query state, tenant/user context, or a fallback host.
- The action is rendered only as part of the successfully authorized canonical Dashboard result. Existing authentication, organization, portal-host redirect, and server-error behavior remains unchanged.

## Implementation Changes

- Created this report before any other DL-WEB-004 change.
- Added `apps/web/src/components/dashboard-open-in-app-link.tsx`, a Dashboard-owned semantic link wrapper that calls DL-WEB-001 with only the parameterless Dashboard route intent.
- Updated the canonical Dashboard welcome header to stack safely on narrow screens and align the action to the right from `sm` upward.
- Added focused component and canonical-page integration tests without changing Dashboard authorization, portal redirects, product/admin filtering, or quick-action destinations.

## Tests and Validation

- Focused Dashboard Vitest: 2 files and 6 tests passed. Coverage verifies visible label, semantic same-context anchor, canonical registry-backed href, exact `{ routeKey: "dashboard" }` invocation with no parameters, missing-configuration hiding, unexpected-error propagation, responsive header classes, and preservation of product/admin actions and Dashboard content.
- DL-WEB-001 regression: `tsx --test src/lib/__tests__/deep-links.test.ts` passed 9/9.
- Full Web TypeScript: `./node_modules/.bin/tsc --noEmit -p apps/web/tsconfig.json` passed with no diagnostics.
- Broad Vitest: 72 files/341 tests passed; 3 files/23 unrelated existing tests failed. Failures remain in SynqLien funding notifications, CareConnect `PublicNetworkView` (`localStorage.getItem` unavailable), and referral-thread accessible-name expectations. The new Dashboard suites passed within this run.
- Production build: `pnpm --dir apps/web build` passed; Next compiled, type-checked, generated 29 static pages, and collected routes. Only existing Node deprecation and optional `CC_COMMON_PORTAL_HOSTNAME` warnings appeared.
- Source guard: Dashboard integration source contains no manual `"/dashboard"` path, environment read, `pathTemplate`, registry access, candidate release host, host concatenation, path parameters, or query state. Existing normal Web Dashboard routes were left unchanged.
- Scope guard: Git reports no changes under Mobile, Gateway, backend services, or the shared registry.
- Whitespace: `git diff --check` passed. No supported lint or formatting command exists in `apps/web/package.json`; modified files follow adjacent formatting conventions.
- Documentation review: root/Web README and agent guidance were reviewed. The feature does not alter project shape, commands, ports, runtime behavior, or the already-documented `NEXT_PUBLIC_DEEP_LINK_BASE_URL` contract, so no durable product/runtime documentation update is required. `scripts/check-doc-sync.py` identified the Dashboard page as doc-sensitive and will be satisfied by the required final `Documentation impact:` declaration.
- Independent review: approved with no critical, high, medium, or low code findings. The reviewer independently reran the six focused Dashboard tests and TypeScript successfully.

## Issues, Risks, and Blockers

- DL-PLAT-002 remains responsible for approved per-environment hosts, association deployment, and physical-device QA.
- The intentional unexpected-builder-error test passes but React/jsdom emits its caught render diagnostic to stderr despite a scoped console spy; this is test-runner noise, not an unhandled or failed assertion.
- The broad Web suite has the 23 unrelated failures described above. No failing test imports or exercises the Dashboard changes.

## Acceptance Criteria

AC-001 through AC-028 and AC-030 through AC-034 are satisfied. AC-029 is not applicable because the app exposes no supported lint command. Documentation impact was reviewed and independent review approved the implementation.

## Final Status

Complete — approved for merge/release subject to DL-PLAT-002 base URL/platform association configuration and physical-device QA.
