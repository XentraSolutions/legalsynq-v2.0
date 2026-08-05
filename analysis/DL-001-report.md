# DL-001 Implementation Report

## 1. Ticket Summary

- Ticket: DL-001
- Title: Deep-Link Architecture and Shared Route Contract
- Status: In progress
- Report created before application-code changes: Yes

## 2. Objective

Establish the shared, environment-aware foundation for registering, validating, documenting, and generating deep-link URLs across Web, Mobile, and Backend without implementing link handling or business navigation.

## 3. Scope

In scope: an authoritative language-neutral route contract, route metadata, environment configuration, URL generation, validation, consumption paths, documentation, and automated tests.

Out of scope: native app links, incoming-link handling, authentication or authorization routing, resource lookup, business-screen navigation, persistence, signed links, analytics delivery, and deployment.

## 4. Initial Implementation Plan

1. Inspect workspace, shared-library, configuration, validation, testing, and documentation conventions.
2. Decide how one authoritative route source can be consumed safely by TypeScript and .NET.
3. Add the route contract and initial registry in the narrowest appropriate shared location.
4. Add environment-aware configuration and centralized Web URL generation with strict validation.
5. Add non-executing Mobile and Backend contract-consumption wiring where architecture permits.
6. Add tests and documentation.
7. Search for conflicting deep-link construction, run targeted validations, and review the final diff.

Repository inspection checklist:

- [x] Root workspace and package-management configuration
- [x] Web package, environment, URL, route, schema, and test conventions
- [x] Mobile package, environment, route, schema, and test conventions
- [x] Backend and shared C# project/configuration conventions
- [x] Existing shared packages and language-neutral artifacts
- [x] Documentation and analysis-report conventions
- [x] Existing deep-link utilities or duplicated construction
- [x] Applicable lint, type-check, test, build, and formatting commands

## 5. Repository and Architecture Findings

- The repository is a mixed-runtime monorepo: Next.js/React/TypeScript for `apps/web`, Expo/React Native/TypeScript for `apps/mobile`, and ASP.NET Core/.NET 10 for backend services.
- The root declares `pnpm@10.26.1`, but there is no `pnpm-workspace.yaml`; application packages are independently scripted and share the root dependency installation rather than a formal JavaScript workspace package graph.
- Existing `shared/contracts` is the dependency-light, additive C# contract boundary. It is the appropriate conceptual contract location, but TypeScript applications cannot consume its compiled runtime directly.
- No existing cross-runtime deep-link contract exists.
- Web has an existing product-reference mapper in `apps/web/src/lib/product-deep-links.ts`. It creates relative business links for Support references and does not cover the DL-001 route set or environment-aware absolute URLs.
- Mobile has `DeepLinkingService`, a thin Expo Linking wrapper for creating app-scheme URLs and reading an initial URL. DL-001 explicitly excludes incoming-link handling, so this service will not be expanded into a native handler.
- Web uses native TypeScript validation and the platform `URL` class for environment URLs. Mobile uses typed configuration with Jest. No repository-wide schema-validation dependency is established or necessary for this ticket.
- Web unit tests use both Node's built-in test runner through `tsx --test` and Vitest. Mobile uses Jest. Shared C# tests use xUnit.
- JSON is supported by Web TypeScript (`resolveJsonModule: true`) and can be embedded/read by .NET. Metro can consume a shared source directory when it is explicitly added to `watchFolders`.
- Existing environment names are narrower than the ticket: Web commonly uses `development`; Mobile currently recognizes `development`, `qa`, and `production`. DL-001 will define the complete logical set (`local`, `development`, `qa`, `uat`, `production`) without changing unrelated application-environment behavior.

## 6. Existing Service Boundaries

- `apps/web` owns browser-facing URL generation and environment access for this ticket.
- `apps/mobile` may reference the shared route contract only; no listeners, parsing, or navigation will be added.
- Backend services remain independent. The dependency-light `Contracts` assembly can expose a typed reader for the shared registry without adding endpoints or service wiring.
- `shared/contracts/deep-links` will hold the language-neutral authoritative artifact. Runtime-specific readers remain thin and additive.

## 7. Existing Configuration and Validation Conventions

- Web uses `process.env.NEXT_PUBLIC_ENV` for public environment selection and validates URLs with the built-in `URL` class in `src/lib/env-validation.ts`.
- Mobile uses `EXPO_PUBLIC_APP_ENV` through `ConfigService`; no DL-001 domain value is currently configured.
- Exact approved deep-link domains are absent. The implementation will require `NEXT_PUBLIC_DEEP_LINK_BASE_URL` and will not supply an invented production default. Deployment environments must provide their approved value.
- Local development may use HTTP; production requires HTTPS. HTTP and HTTPS are the only accepted protocols.
- Route and parameter validation will use focused handwritten checks rather than add a large validation dependency.
- Web's Node test convention uses `node:test` and `node:assert/strict` for pure utilities. Mobile uses Jest. Shared .NET tests use xUnit.

## 8. Design Decision

Use `shared/contracts/deep-links/routes.json` as the single authoritative registry, accompanied by a JSON Schema and documentation. A pure shared TypeScript reader/generator will consume that JSON for Web and Mobile. Web will provide the environment adapter. The .NET `Contracts` assembly will embed the same JSON file and expose a typed read-only registry.

This avoids duplicating route templates, does not force TypeScript and .NET into one runtime, preserves existing service boundaries, and adds no package or persistence layer. Mobile requires only a Metro `watchFolders` entry and a thin export to prove contract consumption; it will not perform link handling.

## 9. Route Contract

Implemented a versioned JSON contract with the required fields: `key`, `pathTemplate`, `mobileDestination`, authentication/authorization metadata, required path parameters, approved optional query parameters, fallback metadata, analytics metadata, and enablement. The TypeScript reader freezes cloned definitions and validates registry version, key uniqueness, absolute paths, placeholder/parameter agreement, query-name uniqueness, and required metadata. The .NET reader maps the embedded JSON to immutable typed records and performs equivalent structural checks.

## 10. Initial Route Registry

The authoritative `shared/contracts/deep-links/routes.json` registers each initial route exactly once:

| Key                  | Template                       | Required parameters | Optional query parameters |
| -------------------- | ------------------------------ | ------------------- | ------------------------- |
| `dashboard`          | `/dashboard`                   | None                | None                      |
| `dealDetails`        | `/deals/:dealId`               | `dealId`            | None                      |
| `contactDetails`     | `/contacts/:contactId`         | `contactId`         | None                      |
| `applicationDetails` | `/applications/:applicationId` | `applicationId`     | None                      |
| `reportDetails`      | `/reports/:reportId`           | `reportId`          | None                      |

All initial routes are enabled. Authentication and authorization metadata are explicitly `true`. `dashboard` is the explicit future fallback metadata. Mobile destinations and analytics event names are symbolic metadata only and are not executed by this ticket.

## 11. Environment Configuration

Web reads `NEXT_PUBLIC_ENV` and `NEXT_PUBLIC_DEEP_LINK_BASE_URL` only when URL generation is requested. Supported logical environments are `local`, `development`, `qa`, `uat`, and `production`. No deployed hostname default is committed because approved values were not present. Production requires HTTPS; only HTTP/HTTPS origins without credentials, paths, queries, or fragments are accepted. A trailing slash is normalized.

## 12. URL-Generation Behavior

`apps/web/src/lib/deep-links.ts` is the Web adapter. It resolves environment configuration and delegates to the shared pure generator. The generator looks up a registered route, rejects disabled routes, substitutes strictly declared path values, applies strict percent encoding, validates approved query names, omits null/undefined/blank query values, sorts emitted query names, and joins the normalized origin and absolute route path without a double slash. Registry definitions and input objects are not mutated.

## 13. Validation Rules

- Unknown route key: rejected with `UNKNOWN_ROUTE`.
- Disabled route: rejected with `DISABLED_ROUTE`.
- Missing or blank required path value: rejected with `MISSING_PATH_PARAMETER`.
- Undeclared path name: rejected with `INVALID_PATH_PARAMETER`.
- Unsupported query name: rejected with `UNSUPPORTED_QUERY_PARAMETER`.
- Missing base domain: rejected with `MISSING_BASE_URL`.
- Invalid origin, protocol, or Production HTTP: rejected with `INVALID_BASE_URL`.
- Missing or unsupported environment: rejected with `INVALID_ENVIRONMENT`.
- Path and approved query values: percent encoded.
- Query output: deterministic lexical name ordering.

## 14. Files Inspected

- Ticket attachment: `/Users/aaronruanto/.codex/attachments/11e818c0-5abf-4fa7-b7b3-d7f7a9207b2a/pasted-text.txt`
- Root `AGENTS.md`
- `.codex/skills/delivery-modes/SKILL.md`
- `.codex/skills/delivery-modes/references/implementation-mode.md`
- `package.json`
- `apps/web/package.json`
- `apps/web/tsconfig.json`
- `apps/web/vitest.config.ts`
- `apps/web/next.config.mjs`
- `apps/web/src/lib/env-validation.ts`
- `apps/web/src/lib/product-deep-links.ts`
- `apps/web/src/lib/__tests__/careconnect-login-url.test.ts`
- `apps/mobile/package.json`
- `apps/mobile/tsconfig.json`
- `apps/mobile/jest.config.js`
- `apps/mobile/metro.config.js`
- `apps/mobile/babel.config.js`
- `apps/mobile/src/shared/services/Config/ConfigService.ts`
- `apps/mobile/src/shared/services/Config/ConfigService.test.ts`
- `apps/mobile/src/shared/services/DeepLinking/DeepLinkingService.ts`
- `apps/mobile/src/shared/types/common.ts`
- `apps/mobile/src/types/env.d.ts`
- `shared/README.md`
- `shared/contracts/Contracts/Contracts.csproj`
- `shared/contracts/Contracts/Notifications/NotificationTemplateRegistry.cs`
- `shared/contracts/Contracts/Commerce/CommerceMonetizationRegistry.cs`
- `shared/building-blocks/BuildingBlocks/BuildingBlocks.csproj`
- `shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/BuildingBlocks.Tests.csproj`
- Existing Markdown reports under `analysis/`

## 15. Files Added

- `analysis/DL-001-report.md` — incremental implementation and validation record required by the ticket.
- `shared/contracts/deep-links/routes.json` — authoritative route definitions.
- `shared/contracts/deep-links/route-registry.schema.json` — language-neutral contract schema.
- `shared/contracts/deep-links/route-contract.ts` — shared TypeScript types and errors.
- `shared/contracts/deep-links/route-registry.ts` — validated immutable TypeScript reader.
- `shared/contracts/deep-links/deep-link-url.ts` — shared URL-generation and configuration validation core.
- `shared/contracts/deep-links/index.ts` — TypeScript public exports.
- `shared/contracts/deep-links/README.md` — route, configuration, usage, error, extension, and scope documentation.
- `apps/web/src/lib/deep-links.ts` — Web environment adapter and centralized generator API.
- `apps/web/src/lib/__tests__/deep-links.test.ts` — generator, validation, encoding, and registry tests.
- `apps/mobile/src/shared/deepLinks/index.ts` — Mobile shared-contract export only.
- `apps/mobile/src/shared/deepLinks/index.test.ts` — Mobile contract-consumption test.
- `shared/contracts/Contracts/DeepLinks/DeepLinkRouteDefinition.cs` — typed .NET route contract.
- `shared/contracts/Contracts/DeepLinks/DeepLinkRouteRegistry.cs` — typed embedded-registry reader and validator.
- `shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/DeepLinkRouteRegistryTests.cs` — .NET contract tests.

## 16. Files Modified

- `shared/contracts/Contracts/Contracts.csproj` — embeds the authoritative registry in the Contracts assembly.
- `apps/mobile/metro.config.js` — allows Metro to watch the shared contract source directory.
- `apps/web/README.md` — documents the Web deep-link base-domain variable.
- `shared/README.md` — documents the shared deep-link contract location.

## 17. Files Deleted

None.

## 18. Implementation Progress

- Complete: Created the required report before application-code changes.
- Complete: Inspected repository and runtime boundaries, configuration, URL, validation, test, and documentation conventions.
- Complete: Selected a language-neutral authoritative JSON registry with thin TypeScript and .NET consumers.
- Complete: Added the authoritative registry, JSON Schema, reusable contract, strict shared generator, and Web environment adapter.
- Complete: Added non-navigating Mobile and Backend consumption paths.
- Complete: Added route/configuration/usage documentation.
- Complete: Added Web, Mobile, and .NET automated tests.
- Complete: Web and Mobile formatting, type checking, linting, targeted tests, and iOS bundle export.
- Complete: Searched for conflicting route construction and reviewed the scoped diff.
- Blocked validation: .NET compilation and xUnit execution because the environment has no .NET SDK executable.

## 19. Tests Added

- Web Node tests cover the authoritative route set, static/parameterized generation, encoding, missing/blank/extra parameters, unsupported query names, disabled and unknown routes, deterministic query generation, input immutability, missing/invalid base URLs, Production HTTPS, and environment validation.
- Mobile Jest test proves the application can consume and look up the authoritative registry without adding link handling.
- .NET xUnit tests prove the Contracts assembly loads the same embedded five-route registry and exposes typed metadata and explicit lookup behavior.

## 20. Validation Commands and Results

| Command                                                                                              | Working directory | Result                 | Summary                                                                                                                                                                                                                                                                                                                                           |
| ---------------------------------------------------------------------------------------------------- | ----------------- | ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `apps/mobile/node_modules/.bin/prettier --write ...`                                                 | Repository root   | Passed                 | Formatted all added TypeScript, JSON, Markdown, JavaScript, and report files.                                                                                                                                                                                                                                                                     |
| `apps/mobile/node_modules/.bin/prettier --check ...`                                                 | Repository root   | Passed                 | All scoped TypeScript, JSON, JavaScript, Markdown, and report files match the installed formatter.                                                                                                                                                                                                                                                |
| `node -e "JSON.parse(...)"`                                                                          | Repository root   | Passed                 | The route registry and JSON Schema both parse successfully as JSON.                                                                                                                                                                                                                                                                               |
| `node_modules/.bin/tsc --noEmit -p apps/web/tsconfig.json`                                           | Repository root   | Passed                 | Web and imported shared TypeScript contract type-check successfully.                                                                                                                                                                                                                                                                              |
| `../../node_modules/.bin/tsx --test src/lib/__tests__/deep-links.test.ts`                            | `apps/web`        | Passed outside sandbox | 11/11 targeted deep-link tests passed. `tsx` emitted its existing Node 26 `module.register()` deprecation warning.                                                                                                                                                                                                                                |
| `apps/mobile/node_modules/.bin/tsc --noEmit -p apps/mobile/tsconfig.json`                            | Repository root   | Passed                 | Mobile and imported shared TypeScript contract type-check successfully.                                                                                                                                                                                                                                                                           |
| `apps/mobile/node_modules/.bin/eslint apps/mobile/src --ext .ts,.tsx --max-warnings 0`               | Repository root   | Passed                 | Mobile lint completed with zero warnings.                                                                                                                                                                                                                                                                                                         |
| `node_modules/.bin/jest --runInBand src/shared/deepLinks/index.test.ts`                              | `apps/mobile`     | Passed                 | 1/1 Mobile contract-consumption test passed.                                                                                                                                                                                                                                                                                                      |
| `node_modules/.bin/expo export --platform ios --output-dir /private/tmp/dl001-mobile-export --clear` | `apps/mobile`     | Passed                 | Metro bundled 2,837 modules and exported the iOS bundle to a temporary directory, proving the external shared contract is bundle-consumable.                                                                                                                                                                                                      |
| `node -e "...require('./metro.config.js')..."`                                                       | `apps/mobile`     | Passed                 | Confirmed Metro resolves the watched shared contract directory.                                                                                                                                                                                                                                                                                   |
| Deep-link template and construction `grep` searches                                                  | Repository root   | Passed                 | No duplicate implementation of the four parameterized DL-001 templates was found. Existing `/dashboard` occurrences are normal internal navigation, tests, or documentation, not environment-aware deep-link URL construction. Existing `product-deep-links.ts` has distinct Support business-route semantics and was intentionally not replaced. |
| `git diff --check`                                                                                   | Repository root   | Passed                 | No whitespace errors in tracked diffs. Added source/document files were also formatter-checked.                                                                                                                                                                                                                                                   |
| `dotnet build shared/contracts/Contracts/Contracts.csproj --no-restore`                              | Repository root   | Could not run          | Failed immediately with `zsh: command not found: dotnet`; no .NET SDK/compiler exists in this environment.                                                                                                                                                                                                                                        |

Validation execution notes:

- The first `pnpm --dir apps/web type-check` attempt invoked an automatic dependency-status/install check using global pnpm 11, then encountered restricted-network `ENOTFOUND` retries. It was stopped without dependency or lockfile changes. The installed `tsc` binary was then used directly and passed.
- The first sandboxed `tsx --test` attempt failed because `tsx` could not create its IPC pipe (`EPERM`). The same test command was rerun outside the sandbox and passed.
- Two initially mis-scoped direct tool invocations (Mobile `tsc`/Jest from the repository root without the project config/working directory) displayed compiler help or a Jest transform error. Both were rerun with the correct project/working directory and passed. These were command-invocation errors, not implementation failures.

## 21. Acceptance-Criteria Status

| Criterion | Status             | Evidence                                                                                                                                                     |
| --------- | ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| AC-001    | Complete           | `shared/contracts/deep-links/routes.json` is the authoritative registry.                                                                                     |
| AC-002    | Complete           | Registry and passing Web test confirm exactly five unique initial entries.                                                                                   |
| AC-003    | Complete           | `generateDeepLinkUrl` delegates to the shared generator; static and parameterized URL tests pass.                                                            |
| AC-004    | Complete           | Missing and blank path-parameter tests pass with `MISSING_PATH_PARAMETER`.                                                                                   |
| AC-005    | Complete           | Unknown-key test passes with `UNKNOWN_ROUTE`.                                                                                                                |
| AC-006    | Complete           | Passing tests verify encoded path and query values.                                                                                                          |
| AC-007    | Complete           | Web adapter supports all five logical environments through `NEXT_PUBLIC_ENV` plus the deployment-provided base URL; validation tests pass.                   |
| AC-008    | Complete           | Repository search found no new manual construction outside the centralized foundation.                                                                       |
| AC-009    | Partially complete | Web type check/tests, Mobile type check/test/bundle, and .NET typed reader exist. Backend consumption could not be compiled because `dotnet` is unavailable. |
| AC-010    | Complete           | `shared/contracts/deep-links/README.md` documents contract fields, routes, parameters, configuration, usage, errors, extension, and boundaries.              |
| AC-011    | Complete           | Disabled-route fixture passes with `DISABLED_ROUTE`.                                                                                                         |
| AC-012    | Complete           | Unsupported-query test passes with `UNSUPPORTED_QUERY_PARAMETER`.                                                                                            |
| AC-013    | Partially complete | 11 Web and 1 Mobile tests pass; three .NET tests were added but could not execute without the SDK.                                                           |
| AC-014    | Complete           | Git diff contains no database, persistence, or migration changes.                                                                                            |
| AC-015    | Complete           | Diff review confirms no native link handling, auth routing, authorization, resource lookup, or business navigation.                                          |
| AC-016    | Complete           | This report records all executed commands, successes, failures, warnings, and the unresolved .NET validation gap.                                            |

## 22. Issues and Failures

- Blocking validation gap: `dotnet` is not installed or available on `PATH`, so the Contracts build and xUnit tests could not be executed.
- Tooling-only: global pnpm 11 attempted a dependency status/install operation and could not access npm from the restricted network. No dependency files changed; direct installed binaries were used successfully.
- Tooling-only: sandboxed `tsx` could not open its IPC pipe. The approved outside-sandbox run passed all tests.
- Non-blocking warning: `tsx` reports that Node 26 deprecates `module.register()` in favor of `module.registerHooks()`.

## 23. Architecture Risks and Concerns

- Web/Mobile use TypeScript while Backend uses .NET, so the JSON artifact must remain authoritative and runtime readers must not redefine route templates.
- Environment values must remain non-secret and must not invent a production domain.
- Metro normally scopes file watching to the mobile project; the shared contract directory must be explicitly watched and validated in a bundle/type check.
- The existing Support product mapper has different routes and semantics. Replacing it would be a business integration outside DL-001 and could cause regressions.
- The .NET reader received static review only in this environment; compilation remains required before the Backend consumption criterion can be marked complete.

## 24. Known Gaps

- Deployment owners must provide approved `NEXT_PUBLIC_DEEP_LINK_BASE_URL` values for each environment. No Production or non-local hostname was available or invented.
- All initial routes intentionally approve no optional query parameters. The shared generator supports approved query parameters and tests that behavior with an isolated fixture.
- Business-component adoption is deferred because DL-001 is foundation-only.
- .NET Contracts build and xUnit results are pending in an environment with the .NET 10 SDK.

## 25. Out-of-Scope Confirmation

Confirmed by diff review: no native iOS/Android association files, OS link listeners, cold/foreground/background processing, Mobile navigation, authentication, authorization, login redirects, pending-route persistence, resource lookup, business-screen integration, Open in App UI, workflow/campaign/notification integration, analytics delivery, signed/expiring URLs, app-store handling, deployment, database, or migration changes were introduced.

## 26. Follow-Up Recommendations

1. In an environment with .NET 10 installed, run `dotnet build shared/contracts/Contracts/Contracts.csproj` and `dotnet test shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/BuildingBlocks.Tests.csproj --filter FullyQualifiedName~DeepLinkRouteRegistryTests`.
2. Obtain and configure approved base domains for Local/Development, QA, UAT, and Production deployment environments. Production must be HTTPS.
3. In later tickets, adopt the centralized generator in explicitly scoped business producers rather than adding manual URL concatenation.
4. Handle native app links, authentication continuation, authorization, resource validation, navigation, and analytics only in their dedicated follow-up tickets.

## 27. Final Status

Partially complete. The shared foundation, documentation, Web/Mobile consumption, generator, validation, and TypeScript/Mobile automated checks are implemented and passing. Required Backend/.NET validation remains unsupported in this execution environment because the .NET SDK is unavailable; therefore AC-009 and AC-013 remain partially complete and the ticket is not reported as complete.
