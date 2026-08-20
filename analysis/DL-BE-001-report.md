# DL-BE-001 Report

## 1. Ticket Summary

DL-BE-001 configures Backend / Platform-owned infrastructure for verified HTTPS deep-link association hosting. This ticket is handoff-only for Mobile and Web.

## 2. Objective

Establish the Backend / Platform mechanism for Apple Universal Links and Android App Links association documents, validate it locally where possible, and produce honest handoff values and blockers for Mobile and Web.

## 3. Scope

In scope: repository discovery, host/signing evidence review, gateway association hosting, deterministic generation/validation tooling, deployment documentation, and handoff reporting.

Out of scope: Mobile implementation, Web implementation, auth changes, business APIs, database persistence, UAT, deferred deep linking, store fallback, campaigns, notifications, analytics, and shared route registry mutation.

## 4. Initial Implementation Plan

1. Create this report before implementation.
2. Record branch, HEAD, and working-tree status.
3. Inspect instructions, route registry, gateway, deployment docs, CI/CD, static hosting, `.well-known` handling, Mobile identity evidence, and signing evidence.
4. Do not invent hosts, Apple Team IDs, or Android fingerprints.
5. Implement the smallest Backend / Platform association hosting path.
6. Add generation and validation tooling that consumes the shared registry read-only and fails when required values are absent.
7. Update Backend / Platform docs.
8. Run targeted validation.
9. Confirm Mobile, Web, and shared route registry boundaries.

## 5. Current Branch and Working-Tree State

- Branch: `feature-deeplinking-be-setup`
- HEAD: `753d04ea37048b4cc6f581bb5ca7eb2c35378353`
- Initial working tree: clean (`git status --short` produced no output)
- Final working tree: changed Backend / Platform files and report only; `apps/mobile/**`, `apps/web/**`, and `shared/contracts/**` have no status output.

## 6. Repository / Platform Architecture Findings

- LegalSynq is a mixed .NET/Node monorepo.
- `apps/gateway` is the YARP Backend / Platform entry point on port `5010`.
- Gateway already has anonymous route conventions for health/info and selected public endpoints.
- Public AWS backend runbook routes Internet -> Route 53 -> Nginx/TLS -> Gateway -> services.
- No Terraform, Kubernetes, Helm, or CDN configuration was found in this checkout.
- `.github/workflows/e2e.yml` is frontend mocked e2e only; no deploy/signing workflow evidence for association files was found.

## 7. Shared Route Registry Review

The requested `shared/contracts/deep-links/routes.json` and `shared/contracts/deep-links/route-registry.schema.json` do not exist in this checkout. `find shared -maxdepth 5 -type f` found only the current `shared/contracts/Contracts` C# project and shared building-block/audit-client files.

Result: shared-route alignment against the requested read-only registry is blocked until the registry file is restored or the correct path is supplied. The implementation does not create or modify the shared registry.

## 8. Hosting Architecture Review

- Gateway is the smallest Backend / Platform-owned existing HTTP surface suitable for anonymous deterministic association delivery.
- Existing Web `.well-known` route at `apps/web/src/app/.well-known/tenant-verify/route.ts` is Web-owned and only returns `tenant-verify-ok`; it was inspected as evidence and not modified.
- No Backend static public asset directory or existing Backend `.well-known` directory was found.
- A dynamic business API is unnecessary; gateway static file responses are sufficient.

## 9. Environment Model

Supported environments remain Development, QA / Preview, and Production. No UAT was added.

## 10. Deep-Link Host Discovery

| Environment | Approved Deep-Link Host | Status | Evidence / Blocker |
|---|---|---|---|
| Development | BLOCKED | Blocked | No approved HTTPS deep-link hostname found. Local `localhost` ports are not approved verified HTTPS hosts. |
| QA / Preview | BLOCKED | Blocked | Mobile README references `https://core-qa.legalsynq.net` as API URL, but the ticket forbids inferring deep-link hosts from API domains. No approved deep-link host found. |
| Production | BLOCKED | Blocked | Deployment docs use placeholders such as `api.yourdomain.com` and `app.yourdomain.com`; no approved production deep-link host found. |

## 11. DNS and TLS Findings

DNS/TLS live verification was not run because no approved deep-link host was identified. Deployment docs describe Route 53 plus Nginx/TLS/Certbot or ALB/ACM, but only as a runbook pattern with placeholders.

## 12. iOS Bundle Identity Findings

Read-only Mobile evidence verifies:

- `apps/mobile/app.config.js`: production bundle ID `com.legalsynq`, non-production bundle ID `com.legalsynq.qa`.
- `apps/mobile/README.md`: documents production iOS bundle ID `com.legalsynq`.
- `apps/mobile/eas.json`: preview and production submit profiles have iOS App Store Connect app IDs, but no Apple Team ID.

## 13. Apple Team ID Findings

Status: Blocked. No Apple Team ID was found in repository docs, Mobile config, EAS config, CI/CD, deployment docs, or gateway/backend configuration. No Apple appID can be generated honestly without this value.

## 14. Apple Application Identity Mapping

| Environment | Bundle ID | Apple Team ID | Apple App ID | Status |
|---|---|---|---|---|
| Development | `com.legalsynq.qa` | BLOCKED | BLOCKED | Blocked |
| QA / Preview | `com.legalsynq.qa` | BLOCKED | BLOCKED | Blocked |
| Production | `com.legalsynq` | BLOCKED | BLOCKED | Blocked |

## 15. Android Package Identity Findings

Read-only Mobile evidence verifies:

- `apps/mobile/app.config.js`: production Android package `com.legalsynq`, non-production package `com.legalsynq.qa`.
- `apps/mobile/README.md`: documents production Android package `com.legalsynq`.

## 16. Android Signing Architecture

`apps/mobile/eas.json` defines `development`, `preview`, and `production` EAS build profiles, but does not contain signing certificate fingerprints. No keystores were found or committed by this ticket. Signing appears EAS-managed or externally managed, but the exact distribution signing certificate is not represented in repository evidence.

## 17. Android SHA-256 Fingerprint Findings

| Environment | Package ID | SHA-256 Fingerprint | Fingerprint Source | Status |
|---|---|---|---|---|
| Development | `com.legalsynq.qa` | BLOCKED | No public signing fingerprint found. | Blocked |
| QA / Preview | `com.legalsynq.qa` | BLOCKED | No public signing fingerprint found. | Blocked |
| Production | `com.legalsynq` | BLOCKED | No public signing fingerprint found. | Blocked |

## 18. Google Play App Signing Findings

Blocked. No repository evidence confirms whether Production uses Google Play App Signing or identifies the Play App Signing certificate fingerprint. Production `assetlinks.json` must use the fingerprint for the application actually installed from Google Play, not an upload-key fingerprint, if Play App Signing is enabled.

## 19. Association Hosting Design

Selected design: gateway-owned anonymous static file delivery.

- `/.well-known/apple-app-site-association` serves `apple-app-site-association`.
- `/.well-known/assetlinks.json` serves `assetlinks.json`.
- Files are read from `DeepLinks:AssociationDirectory`.
- Default tracked setting is `DeepLinks/Associations/production`; deployments should override it per environment.
- Missing files return JSON 404, not an HTML fallback.
- No database, auth, user context, tenant context, or business API is involved.

## 20. AASA Design

`scripts/deep-links/generate-association-files.py` builds modern `applinks.details[].appIDs` plus `components` entries from the read-only route registry. Route conversion maps `:param` segments to Apple path wildcards, for example `/deals/:dealId` -> `/deals/*`.

Deployable AASA generation is blocked until the shared route registry and Apple Team ID are available.

## 21. AASA Implementation

Implemented generation support and gateway hosting support, but no deployable AASA artifact was generated because required source inputs are missing or blocked.

## 22. AASA Route Alignment

Implemented validation support in `scripts/deep-links/validate-association-files.py`. Actual repository route alignment is blocked because `shared/contracts/deep-links/routes.json` is absent.

## 23. Asset Links Design

`scripts/deep-links/generate-association-files.py` builds Android Digital Asset Links statements using relation `delegate_permission/common.handle_all_urls`, namespace `android_app`, the verified package ID, and approved public SHA-256 fingerprints.

Deployable `assetlinks.json` generation is blocked until Android public signing fingerprints are provided.

## 24. Asset Links Implementation

Implemented generation support and gateway hosting support, but no deployable `assetlinks.json` artifact was generated because required fingerprints are blocked.

## 25. Environment Isolation

The example config isolates:

- Development: `com.legalsynq.qa`
- QA / Preview: `com.legalsynq.qa`
- Production: `com.legalsynq`

Generator output is per environment. Production generation cannot include QA identities unless they are explicitly placed in production config, which should be rejected in review. Full isolation validation is blocked until approved values exist.

## 26. Authentication / Public Access

Gateway association endpoints call `.AllowAnonymous()` and are mapped before the YARP proxy route requiring authorization. They do not require JWT, cookie, API key, tenant context, CSRF token, database access, or session state.

## 27. Files Inspected

- Root instructions supplied in prompt
- `AGENTS.md`
- `README.md`
- `.codex/skills/delivery-modes/SKILL.md`
- `.codex/skills/delivery-modes/references/implementation-mode.md`
- `apps/gateway/README.md`
- `apps/gateway/Gateway.Api/Program.cs`
- `apps/gateway/Gateway.Api/Gateway.Api.csproj`
- `apps/gateway/Gateway.Api/appsettings.json`
- `apps/gateway/Gateway.Api/appsettings.Development.json`
- `AWS_EC2_BE_MICROSERVICES_DEPLOYMENT.md`
- `AWS_EC2_FE_NEXTJS_DEPLOYMENT.md`
- `.github/workflows/e2e.yml`
- `scripts/run-prod.sh`
- `scripts/build-prod.sh`
- `apps/web/src/app/.well-known/tenant-verify/route.ts` (read-only evidence)
- `apps/mobile/app.config.js` (read-only identity evidence)
- `apps/mobile/eas.json` (read-only identity/build evidence)
- `apps/mobile/README.md` (read-only identity/build evidence)
- Shared files under `shared/` via `find shared -maxdepth 5 -type f`

## 28. Files Added

- `analysis/DL-BE-001-report.md`
- `config/deep-links/association-config.example.json`
- `scripts/deep-links/generate-association-files.py`
- `scripts/deep-links/validate-association-files.py`
- `scripts/tests/test-deep-link-association-tools.sh`

## 29. Files Modified

- `apps/gateway/Gateway.Api/Program.cs`
- `apps/gateway/Gateway.Api/appsettings.json`
- `apps/gateway/README.md`
- `AWS_EC2_BE_MICROSERVICES_DEPLOYMENT.md`

## 30. Files Deleted

None.

## 31. Implementation Progress

- Created the report first.
- Inspected required Backend / Platform, deployment, route, `.well-known`, Mobile identity, and signing evidence.
- Added gateway anonymous static association endpoints.
- Added association config example with blocked/null public values rather than invented values.
- Added deterministic generation and validation tooling.
- Added script test coverage for generation/validation behavior.
- Addressed reviewer findings by validating environment slugs, enforcing generated output path containment, and aligning default production directory casing.
- Updated gateway and backend deployment docs.
- Ran targeted validation.

## 32. Tests Added

- `scripts/tests/test-deep-link-association-tools.sh`

## 33. Validation Commands and Results

| Command | Working Directory | Environment | Result | Output Summary | Introduced by DL-BE-001? | Infrastructure Access Required? | Credentials Required? | Deployment Occurred? |
|---|---|---|---|---|---|---|---|---|
| `git branch --show-current` | repo root | local | Passed | `feature-deeplinking-be-setup` | No | No | No | No |
| `git rev-parse HEAD` | repo root | local | Passed | `753d04ea37048b4cc6f581bb5ca7eb2c35378353` | No | No | No | No |
| `git status --short` | repo root | local | Passed | Initial status clean | No | No | No | No |
| `bash scripts/tests/test-deep-link-association-tools.sh` | repo root | local | Passed | Generated fixture QA artifacts, validated them, confirmed unresolved example config fails, and confirmed unsafe environment names are rejected. | Yes | No | No | No |
| `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj` | repo root | local with approved network-capable restore/build | Passed | Gateway built successfully; two existing `NU1510` warnings for `BuildingBlocks` package pruning. | Yes | NuGet restore required | No | No |
| `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore` | repo root | local sandbox | Failed | `project.assets.json` missing because restore had not completed. Not introduced by DL-BE-001. | No | No | No | No |
| `python3 scripts/deep-links/generate-association-files.py --config config/deep-links/association-config.example.json --routes shared/contracts/deep-links/routes.json --output /tmp/dl-be-001-out` | repo root | local | Failed as expected | `required file not found: shared/contracts/deep-links/routes.json`. This is a blocker, not a code regression. | No | No | No | No |
| `rg -n "BEGIN (RSA\|OPENSSH\|DSA\|EC\|PRIVATE) KEY\|PRIVATE KEY\|BEGIN CERTIFICATE\|\\.jks\|keystore\|storePassword\|keyPassword\|password\\s*=\|access_token\|secret\|provisioning profile\|TEAM_ID\|APPLE_TEAM\|sha256_cert_fingerprints" ...` | repo root | local | Passed with reviewed findings | Matches were placeholders/existing docs/code references and public schema keys; no private signing material introduced. Existing docs already contain secret placeholders. | Yes | No | No | No |
| `python3 scripts/check-doc-sync.py` | repo root | local | Passed | Documentation sync check passed; docs touched include `apps/gateway/README.md`. | Yes | No | No | No |
| `PYTHONPYCACHEPREFIX=/tmp/dl-be-001-pycache python3 -m py_compile scripts/deep-links/generate-association-files.py scripts/deep-links/validate-association-files.py` | repo root | local | Passed | Python syntax check passed with bytecode cache redirected to `/tmp`. | Yes | No | No | No |
| `python3 -m py_compile scripts/deep-links/generate-association-files.py scripts/deep-links/validate-association-files.py` | repo root | local sandbox | Failed | Python attempted to write bytecode under `/Users/ralphlopez/Library/Caches/...`, which sandbox denied. Not introduced by DL-BE-001. | No | No | No | No |
| `git status --short apps/mobile apps/web shared/contracts/deep-links shared/contracts` | repo root | local | Passed | No output; Mobile, Web, and shared contracts not modified. | Yes | No | No | No |

Note: an initial sandboxed `dotnet build` session became stuck in restore and could not be killed from the sandbox because process listing/kill lookup is unavailable. The subsequent approved build completed successfully.

## 34. AASA Static Validation

Fixture validation passed in `scripts/tests/test-deep-link-association-tools.sh`. Repository AASA validation is blocked because deployable Apple app IDs cannot be built without Apple Team ID and the shared route registry path is absent.

## 35. Asset Links Static Validation

Fixture validation passed in `scripts/tests/test-deep-link-association-tools.sh`, including fingerprint format validation. Repository `assetlinks.json` validation is blocked because public Android SHA-256 fingerprints are not available.

## 36. HTTP Validation

Not run against live HTTPS hosts. No approved deep-link host was identified and no deployment was performed. Gateway code is configured to serve direct canonical paths when generated files are present in `DeepLinks:AssociationDirectory`.

## 37. Apple Verification

Not run. Apple Team ID is unresolved, no deployable AASA file was generated, no live host was available, and no signed Mobile build/device verification was available.

## 38. Android Verification

Not run. Android public SHA-256 fingerprints and Play App Signing status are unresolved, no deployable `assetlinks.json` was generated, no live host was available, and no signed Mobile build/device verification was available.

## 39. Security Review

- No private keys, keystores, private certificates, provisioning profiles, credentials, or secret values were added.
- The config example uses `null` or empty arrays for blocked values and only public package/bundle identifiers.
- Security scan findings were placeholders or existing code/doc references.
- Gateway endpoints do not use user context, tenant context, auth, database, or secret material.

## 40. Acceptance-Criteria Status

| AC | Status | Evidence |
|---|---|---|
| AC-001 Approved Host Discovery | Blocked | No approved Development, QA / Preview, or Production deep-link host found. |
| AC-002 HTTPS Hosting | Blocked | No approved live HTTPS host or deployment access available. |
| AC-003 AASA Artifact | Partially complete | Generator and gateway hosting implemented; deployable artifact blocked by missing route registry and Apple Team ID. |
| AC-004 Correct Apple Identity | Blocked | Apple Team ID unresolved. |
| AC-005 Apple Route Restriction | Partially complete | Tooling restricts to registry routes; actual registry missing. |
| AC-006 Shared Route Alignment | Blocked | Required route registry file absent. |
| AC-007 Asset Links Artifact | Partially complete | Generator and gateway hosting implemented; deployable artifact blocked by missing fingerprints. |
| AC-008 Correct Android Package | Complete | Read-only Mobile evidence verifies `com.legalsynq.qa` and `com.legalsynq`. |
| AC-009 Correct Android Fingerprint | Blocked | Public fingerprints unresolved. |
| AC-010 Production Signing Correctness | Blocked | Play App Signing status and production signing fingerprint unresolved. |
| AC-011 Public Access | Partially complete | Gateway endpoints are `.AllowAnonymous()`; live public access not deployed. |
| AC-012 Canonical Paths | Complete | Gateway maps `/.well-known/apple-app-site-association` and `/.well-known/assetlinks.json`. |
| AC-013 No Redirect | Partially complete | Gateway static endpoints do not redirect; live host behavior unverified. |
| AC-014 Content Type | Complete | Gateway serves files with `application/json`. |
| AC-015 Environment Isolation | Partially complete | Example config and output directories are environment-specific; final values blocked. |
| AC-016 No Private Signing Material | Complete | No private signing material added; security scan reviewed. |
| AC-017 No Database | Complete | No database changes. |
| AC-018 No Business API | Complete | No business API introduced; gateway serves deterministic files. |
| AC-019 Mobile Boundary Preserved | Complete | `git status --short apps/mobile` produced no output. |
| AC-020 Web Boundary Preserved | Complete | `git status --short apps/web` produced no output. |
| AC-021 Shared Registry Read-Only | Complete | `git status --short shared/contracts` produced no output; registry path absent and not created. |
| AC-022 Mobile Handoff | Partially complete | Handoff table produced with verified package/bundle IDs and blockers. |
| AC-023 Web Handoff | Partially complete | Host handoff produced as blocked; no Web integration performed. |
| AC-024 Backend/Platform Documentation | Complete | Gateway README, backend EC2 runbook, and this report updated. |
| AC-025 Live Validation | Blocked | No approved host/deployment access. |
| AC-026 Honest Platform Verification | Complete | No unexecuted Apple, Android, HTTP, deployment, or device verification is claimed. |

## 41. Issues and Failures

- Required route registry path is absent.
- Approved deep-link hosts are absent.
- Apple Team ID is absent.
- Android public SHA-256 fingerprints are absent.
- Production Play App Signing status is absent.
- Live deployment and HTTP validation were not possible.
- Initial sandboxed `dotnet build` restore did not complete; approved build passed.

## 42. Blockers and External Dependencies

1. Restore or identify `shared/contracts/deep-links/routes.json` and schema.
2. Platform/domain owner must approve Development, QA / Preview, and Production deep-link hosts.
3. DNS/TLS owner must provision HTTPS and route canonical paths to the gateway or edge serving layer.
4. Apple account owner must provide Apple Team ID.
5. Mobile release/signing owner must provide public Android SHA-256 signing fingerprints per environment.
6. Production release owner must confirm Google Play App Signing status and provide the installed app signing certificate fingerprint if applicable.
7. Deployment operator must run generation, validation, deploy, and live HTTP checks.

## 43. Architecture Risks and Concerns

- Gateway default `DeepLinks:AssociationDirectory` points to the generated lowercase production directory, but deployers must override it per non-production environment.
- Missing files return JSON 404; until generated artifacts exist, live app association verification will fail.
- Without the shared route registry in the expected path, route alignment cannot be enforced against the ticket's claimed source of truth.

## 44. Known Gaps

- No approved hosts.
- No Apple Team ID.
- No Android fingerprints.
- No Play App Signing confirmation.
- No generated deployable association files.
- No live deployment.
- No Apple/Android/device verification.

## 45. Mobile / Web Developer Handoff

| Environment | Approved Deep-Link Host | Apple App ID | Android Package | Android SHA-256 Fingerprint | Association Hosting Status |
|---|---|---|---|---|---|
| Development | BLOCKED | BLOCKED | `com.legalsynq.qa` | BLOCKED | Gateway support implemented; artifact/deploy blocked. |
| QA / Preview | BLOCKED | BLOCKED | `com.legalsynq.qa` | BLOCKED | Gateway support implemented; artifact/deploy blocked. |
| Production | BLOCKED | BLOCKED | `com.legalsynq` | BLOCKED | Gateway support implemented; artifact/deploy blocked. |

### Mobile developer should consume

`EXPO_PUBLIC_DEEP_LINK_HOST` with the approved host for the target environment after Platform approves hosts.

Do not make that Mobile change in DL-BE-001.

### Web developer should consume

The approved environment-specific deep-link host through the Web team's existing configuration mechanism after Platform approves hosts.

Do not make that Web change in DL-BE-001.

## 46. Out-of-Scope Confirmation

No implementation was performed for Mobile application, Web application, Mobile host integration, Web host integration, Mobile routing, navigation, authentication, authorization, resource validation, workflow continuation, campaigns, notifications, analytics, UAT, deferred deep linking, store fallback, database persistence, or unrelated security work.

## 47. Follow-Up Recommendations

1. Add or restore the shared route registry and schema in the expected path, or amend the ticket with the correct source-of-truth path.
2. Create a Platform domain decision record naming approved Development, QA / Preview, and Production deep-link hosts.
3. Export Apple Team ID from Apple Developer/EAS credentials into release documentation.
4. Export Android public SHA-256 fingerprints from EAS credentials or Google Play Console into release documentation.
5. Confirm Production Play App Signing and use the installed app signing certificate fingerprint.
6. Generate and validate artifacts, then deploy with `DeepLinks__AssociationDirectory` pointing at the environment directory.
7. Run live `curl -i` checks and platform/device verification in the Mobile-owned tickets.

## 48. Final Status

Partially complete / blocked. Backend / Platform gateway hosting, generation tooling, validation tooling, tests, and documentation were implemented. DL-BE-001 cannot be called complete because required approved hosts, shared route registry file, Apple Team ID, Android public signing fingerprints, Play App Signing evidence, deployment, live HTTP validation, and platform/device verification remain blocked.
