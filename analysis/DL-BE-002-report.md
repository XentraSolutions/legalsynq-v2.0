# DL-BE-002 Report: Validate Integrated Deep-Link Gateway Build

## 1. Ticket Summary
- Ticket: DL-BE-002
- Title: Validate Integrated Deep-Link Gateway Build
- Final report status: Complete — DL-BE-001 Resume Ready, External Inputs Only
- Created before validation/build/test/correction/documentation changes: Yes

## 2. Objective
Validate the current integrated Backend/Platform deep-link association implementation on a repository-compatible .NET environment and determine whether DL-BE-001 can be classified as `Resume Ready — External Inputs Only`.

## 3. Scope
Validation-first scope includes repository state review, .NET SDK compatibility review, Gateway restore/build validation, association tooling validation, registry wiring validation, pathTemplate compatibility confirmation, Gateway canonical association route review, documentation sync validation, and boundary validation.

## 4. Initial Validation Plan
1. Record current branch, HEAD, and working-tree state.
2. Review prior reports and required source/config/documentation files.
3. Verify expected integrated artifacts and shared registry status.
4. Inspect Gateway target framework and repository SDK/build requirements.
5. Inspect installed .NET SDK/runtime availability.
6. Restore and build current Gateway when compatible SDK is available.
7. Run focused association tooling, Python syntax, registry wiring, doc-sync, and diff validation.
8. Classify any warnings, failures, defects, blockers, and external inputs.
9. Preserve Mobile/Web/shared route semantics and avoid unrelated changes.
10. Set DL-BE-001 resume classification and DL-BE-002 final status.

## 5. Current Branch and Working-Tree State
- Branch: `feature-deeplink-verification`
- HEAD: `cef0c65e757d6c0e61b358b093a807df92d4833c`
- HEAD summary: `cef0c65e7 add new api for lien management v2`
- Initial `git status --short`: `?? analysis/DL-BE-002-report.md`
- Conclusion: Initial working tree contained only the required newly-created DL-BE-002 report.

## 6. DL-BE-001 Dependency Review
- Source report available: `analysis/DL-BE-001-report.md`
- DL-BE-001 report states Gateway association hosting, generator, validator, tests, and backend docs were added.
- DL-BE-001 report also states the original branch was blocked by missing shared registry, approved hosts, Apple Team ID, Android fingerprints, Play App Signing evidence, deployment, and live HTTP/device verification.
- Historical Gateway build evidence from DL-BE-001 was reviewed but is not used as current DL-BE-002 build evidence.

## 7. DL-INT-002 Dependency Review
- Requested report `analysis/DL-INT-002-report.md` is not present in this checkout.
- `rg --files -g '*DL-INT-002*'` found no matching report file.
- Current integrated source is used as source of truth per ticket instruction.

## 8. DL-PLAT-001 External Blocker Review
- Requested report `analysis/DL-PLAT-001-report.md` is not present in this checkout.
- `rg --files -g '*DL-PLAT-001*'` found no matching report file.
- External blockers remain assessed from current repository evidence and DL-BE-001 report.

## 9. Current Integrated Artifact Review
- Present:
  - Gateway Apple association endpoint in `apps/gateway/Gateway.Api/Program.cs`.
  - Gateway Android association endpoint in `apps/gateway/Gateway.Api/Program.cs`.
  - `DeepLinks:AssociationDirectory` config in `apps/gateway/Gateway.Api/appsettings.json`.
  - Association generator at `scripts/deep-links/generate-association-files.py`.
  - Association validator at `scripts/deep-links/validate-association-files.py`.
  - Association tooling test at `scripts/tests/test-deep-link-association-tools.sh`.
  - Authoritative shared registry at `shared/contracts/deep-links/routes.json`.
  - Shared deep-link registry documentation and companion contract files under `shared/contracts/deep-links/`.
  - .NET shared contract reader under `shared/contracts/Contracts/DeepLinks/`.
  - Backend/Gateway documentation in `apps/gateway/README.md` and `AWS_EC2_BE_MICROSERVICES_DEPLOYMENT.md`.
- Missing: None for DL-BE-002 technical validation.
- Provenance: shared deep-link registry files restored from historical commit `771f620727759f75c1d6d0ad11d370d279d0cf38`.

## 10. Shared Registry Review
- `shared/contracts/deep-links/routes.json`: present.
- `shared/contracts/deep-links/README.md`: present.
- `routes.json` SHA-256: `745fb44451f954bdb85ca84c747ebdd6be7512c184354b0b3e6a0f554f835237`.
- Historical source `771f6207:shared/contracts/deep-links/routes.json` SHA-256: `745fb44451f954bdb85ca84c747ebdd6be7512c184354b0b3e6a0f554f835237`.
- Conclusion: restored registry content matches historical authoritative source exactly; no route semantic change was introduced.
- No duplicate deep-link route registry was found during targeted search.
- Shared contract additions are intentional restoration of missing authoritative registry artifacts.

## 11. Gateway Project / Target Framework Review
- Gateway project: `apps/gateway/Gateway.Api/Gateway.Api.csproj`
- SDK: `Microsoft.NET.Sdk.Web`
- Target framework: `net10.0`
- Project references:
  - `shared/contracts/Contracts/Contracts.csproj`
  - `shared/building-blocks/BuildingBlocks/BuildingBlocks.csproj`
- Package references:
  - `Microsoft.AspNetCore.Authentication.JwtBearer` `8.0.8`
  - `Yarp.ReverseProxy` `2.3.0`
- Root `global.json`: not present.
- Root `Directory.Build.props`: not present.
- Relevant environment override inspected: `apps/gateway/Gateway.Api/appsettings.Development.json`; it does not override `DeepLinks:AssociationDirectory`.

## 12. .NET SDK Environment Review
- dotnet executable path: `/usr/local/share/dotnet/dotnet`
- `dotnet --version`: `10.0.203`
- Installed SDKs:
  - `8.0.420`
  - `10.0.203`
- Host runtime: `10.0.7`
- Installed runtimes include:
  - `Microsoft.AspNetCore.App 8.0.26`
  - `Microsoft.AspNetCore.App 10.0.7`
  - `Microsoft.NETCore.App 8.0.26`
  - `Microsoft.NETCore.App 10.0.7`
- `dotnet --info` reports `global.json file: Not found`.
- Compatibility conclusion: Compatible SDK/runtime available for Gateway `net10.0` validation.

## 13. Restore Strategy
- Gateway targets `net10.0` and references local shared projects plus NuGet packages.
- Repository docs support targeted restore/build using `dotnet restore` and `dotnet build`.
- Strategy: run `dotnet restore apps/gateway/Gateway.Api/Gateway.Api.csproj`, then `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore`.

## 14. Gateway Build Validation
- Current Gateway restore result: Passed with warning.
- Current Gateway build result: Passed with constrained command.
- Successful build command: `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore --verbosity minimal -maxcpucount:1 /p:UseSharedCompilation=false /nodeReuse:false`
- SDK: `10.0.203`
- Target framework: `net10.0`
- Result: Build succeeded, 1 warning, 0 errors.
- Note: an initial sandboxed restore hung until cancellation; escalated restore completed. An initial sandboxed build remained quiet and did not exit promptly; escalated constrained build completed successfully.

## 15. Build Warning Classification
- `NU1510` in `shared/building-blocks/BuildingBlocks/BuildingBlocks.csproj`: `PackageReference Microsoft.Extensions.Caching.Memory will not be pruned.`
  - Classification: pre-existing/unrelated repository warning.
  - Evidence: DL-BE-001 report recorded existing `NU1510` warnings for `BuildingBlocks` package pruning during historical Gateway build.
  - Introduced by DL-BE-001/DL-INT-002 association changes: No evidence.
  - Ticket impact: Warning only; current Gateway build has 0 errors.

## 16. Build Failure Analysis
- No Gateway build failure in the successful constrained build.
- Initial sandboxed restore/build attempts were inconclusive process-control/environment issues, not code defects:
  - Restore attempt: hung after `Determining projects to restore...`; escalated restore passed.
  - Build attempt: no output for approximately 90 seconds; constrained escalated build passed.

## 17. Defect Findings
- Defect 1: Generator/validator `pathTemplate` incompatibility.
  - Evidence: Direct generator fixture with `{"pathTemplate": "/deals/:dealId"}` failed with `ERROR: route entry does not contain a path string`.
  - Evidence: Direct validator fixture with `{"pathTemplate": "/deals/:dealId"}` failed with `ERROR: route registry contains invalid route paths`.
  - Root cause: route extraction accepted `path`, `route`, and `pattern`, but not the current `pathTemplate` registry field.
  - Classification: genuine deep-link association tooling defect.
  - Correction plan: add `pathTemplate` to generator and validator route extraction; update focused tooling test to use `pathTemplate`.
- Defect/drift 2: Missing authoritative shared route registry.
  - Initial state: `shared/contracts/deep-links/routes.json` and `shared/contracts/deep-links/README.md` were missing.
  - Classification: expected integrated artifact was missing in current branch.
  - Correction: after user approval to proceed, restored the historical authoritative shared deep-link contract set from commit `771f620727759f75c1d6d0ad11d370d279d0cf38` without changing route semantics.

## 18. Defect Corrections
Corrected Defect 1 with the smallest compatible tooling change:
- `scripts/deep-links/generate-association-files.py`: route extraction now accepts `pathTemplate` before legacy-compatible keys.
- `scripts/deep-links/validate-association-files.py`: route extraction now accepts `pathTemplate` before legacy-compatible keys.
- `scripts/tests/test-deep-link-association-tools.sh`: focused route fixture now uses `pathTemplate`.
- `apps/gateway/README.md`: documents that association tooling consumes registry `pathTemplate`.

Rerun evidence:
- Focused tooling test passed.
- Python syntax validation passed.
- Direct generator `pathTemplate` fixture passed.
- Direct validator `pathTemplate` fixture passed.
- Post-correction Gateway build passed.

Update after user instruction to proceed:
- Restored `shared/contracts/deep-links/routes.json` and companion shared contract files from historical commit `771f620727759f75c1d6d0ad11d370d279d0cf38`.
- Restored `Contracts.csproj` embedded-resource wiring and .NET registry reader from the same commit.
- Verified restored `routes.json` checksum matches the historical source exactly.
- Reran restore, Gateway build, tooling tests, Python validation, registry wiring, documentation sync, and diff checks.

## 19. Gateway Canonical Path Review
- Source location: `apps/gateway/Gateway.Api/Program.cs`
- Apple route mapped at line evidence from source review: `/.well-known/apple-app-site-association`.
- Android route mapped at line evidence from source review: `/.well-known/assetlinks.json`.
- Both routes are mapped before `app.MapReverseProxy(...).RequireAuthorization()`.

## 20. Apple Association Endpoint Review
- Source route: `/.well-known/apple-app-site-association`
- File served: `apple-app-site-association`
- Serving helper: `MapDeepLinkAssociationFile(...)`
- Anonymous access: `.AllowAnonymous()`
- Content type on success: `application/json`
- Missing file behavior: JSON `NotFound` payload with `error = "deep_link_association_file_missing"`.

## 21. Android Association Endpoint Review
- Source route: `/.well-known/assetlinks.json`
- File served: `assetlinks.json`
- Serving helper: `MapDeepLinkAssociationFile(...)`
- Anonymous access: `.AllowAnonymous()`
- Content type on success: `application/json`
- Missing file behavior: JSON `NotFound` payload with `error = "deep_link_association_file_missing"`.

## 22. Public Access Review
- Gateway association endpoints call `.AllowAnonymous()`.
- They are mapped before the authenticated YARP fallback route.
- No tenant, session, database, or business API dependency was found in the endpoint code.

## 23. Content-Type Review
- Successful association file responses use `Results.File(fullPath, "application/json")`.

## 24. Missing-Artifact Behavior Review
- Missing association artifacts return `Results.NotFound(new { error = "deep_link_association_file_missing", route, fileName })`.
- No HTML fallback or redirect logic is present in the endpoint helper.

## 25. Association Directory Review
- Config key: `DeepLinks:AssociationDirectory`.
- Tracked default in `apps/gateway/Gateway.Api/appsettings.json`: `DeepLinks/Associations/production`.
- If unset, helper defaults to `DeepLinks/Associations/<EnvironmentName>` under the Gateway content root.
- Path containment check prevents resolved file path escape from the configured directory.

## 26. Generator Review
- File: `scripts/deep-links/generate-association-files.py`
- Generates per-environment `apple-app-site-association` and `assetlinks.json`.
- Validates environment slug, host, Apple Team ID, iOS bundle ID, Android package ID, and Android SHA-256 fingerprints.
- Corrected route extraction accepts `pathTemplate`, `path`, `route`, and `pattern`.

## 27. Validator Review
- File: `scripts/deep-links/validate-association-files.py`
- Validates generated AASA appIDs, component paths, broad wildcard absence, Asset Links relation/namespace/package/fingerprint format.
- Corrected route extraction accepts `pathTemplate`, `path`, `route`, and `pattern`.

## 28. pathTemplate Compatibility Review
- Initial state failed `pathTemplate` compatibility in both generator and validator.
- Correction added `pathTemplate` support to both tooling files.
- Focused test fixture now uses `pathTemplate`.
- Rerun result: Complete for tooling compatibility.
- Final result: verified against real restored `shared/contracts/deep-links/routes.json`.

## 29. Tooling Test Validation
- Initial result before correction: Passed, but fixture used `path` and did not prove `pathTemplate`.
- Rerun result after correction: Passed with fixture using `pathTemplate`.
- Output summary: generated QA fixture artifacts, validator passed, unresolved example config failed intentionally, unsafe environment name failed intentionally.

## 30. Python Syntax Validation
- Result: Passed after correction.
- Command used safe bytecode cache path via `PYTHONPYCACHEPREFIX=/tmp/dl-be-002-pycache-rerun`.

## 31. Registry Wiring Validation
- Initial result: failed because `shared/contracts/deep-links/routes.json` was absent.
- Final result after restoring registry: registry wiring passed.
- Evidence: generator found and parsed the restored registry and advanced to external-input validation.
- Expected final output: `ERROR: development: missing required values: host, appleTeamId`.
- Classification: Registry wiring PASS; production generation BLOCKED by expected external inputs.

## 32. Optional Local HTTP Validation
Not run.
- Reason: optional validation was not practical because sandboxed dotnet restore/build sessions showed unreliable process cancellation/control. Starting a long-lived Gateway process would risk another unmanaged session.
- Not claimed: production HTTPS verification, live public verification, Apple verification, Android verification, or device verification.

## 33. Documentation Validation
- Initial `python3 scripts/check-doc-sync.py`: failed because doc-sensitive test changes required documentation review.
- Correction: updated `apps/gateway/README.md` to document `pathTemplate` consumption.
- Rerun `python3 scripts/check-doc-sync.py`: passed.

## 34. Files Inspected
- `analysis/DL-BE-001-report.md`
- `analysis/DL-INT-002-report.md` (attempted; missing)
- `analysis/DL-PLAT-001-report.md` (attempted; missing)
- `shared/contracts/deep-links/routes.json` (initially missing; later restored and inspected)
- `shared/contracts/deep-links/README.md` (initially missing; later restored and inspected)
- `shared/contracts/**`
- `apps/gateway/Gateway.Api/Gateway.Api.csproj`
- `apps/gateway/Gateway.Api/Program.cs`
- `apps/gateway/Gateway.Api/appsettings.json`
- `apps/gateway/Gateway.Api/appsettings.Development.json`
- `config/deep-links/association-config.example.json`
- `scripts/deep-links/generate-association-files.py`
- `scripts/deep-links/validate-association-files.py`
- `scripts/tests/test-deep-link-association-tools.sh`
- `apps/gateway/README.md`
- `README.md`
- `scripts/build-prod.sh`
- `scripts/run-dev.sh`
- `.github/workflows/e2e.yml`
- Historical source commit `771f620727759f75c1d6d0ad11d370d279d0cf38`

## 35. Files Added
- `analysis/DL-BE-002-report.md`
- `shared/contracts/Contracts/DeepLinks/DeepLinkRouteDefinition.cs`
- `shared/contracts/Contracts/DeepLinks/DeepLinkRouteRegistry.cs`
- `shared/contracts/deep-links/routes.json`
- `shared/contracts/deep-links/route-registry.schema.json`
- `shared/contracts/deep-links/route-contract.ts`
- `shared/contracts/deep-links/route-registry.ts`
- `shared/contracts/deep-links/deep-link-url.ts`
- `shared/contracts/deep-links/index.ts`
- `shared/contracts/deep-links/README.md`

## 36. Files Modified
- `apps/gateway/README.md`
- `scripts/deep-links/generate-association-files.py`
- `scripts/deep-links/validate-association-files.py`
- `scripts/tests/test-deep-link-association-tools.sh`
- `shared/contracts/Contracts/Contracts.csproj`
- `analysis/DL-BE-002-report.md`

## 37. Files Deleted
None.

## 38. Validation Commands and Results
- Command: `git branch --show-current`
  - Working directory: repository root
  - Purpose: Record current branch before validation.
  - Result: `feature-deeplink-verification`
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git rev-parse HEAD`
  - Working directory: repository root
  - Purpose: Record current HEAD before validation.
  - Result: `cef0c65e757d6c0e61b358b093a807df92d4833c`
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git status --short`
  - Working directory: repository root
  - Purpose: Record initial working-tree state.
  - Result: `?? analysis/DL-BE-002-report.md`
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git log -1 --oneline`
  - Working directory: repository root
  - Purpose: Record HEAD summary.
  - Result: `cef0c65e7 add new api for lien management v2`
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `ls analysis`
  - Working directory: repository root
  - Purpose: Check available analysis reports.
  - Result: `DL-BE-001-report.md` and `DL-BE-002-report.md` present; DL-INT-002 and DL-PLAT-001 reports absent.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `sed -n '1,220p' analysis/DL-BE-001-report.md` and `sed -n '221,520p' analysis/DL-BE-001-report.md`
  - Working directory: repository root
  - Purpose: Review DL-BE-001 dependency report.
  - Result: Reviewed.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `sed -n '1,260p' analysis/DL-INT-002-report.md`
  - Working directory: repository root
  - Purpose: Review DL-INT-002 dependency report.
  - Result: File missing.
  - Exit code: 1
  - Warnings/errors: `sed: analysis/DL-INT-002-report.md: No such file or directory`
  - Deep-link introduced failure: No; report unavailable in checkout.
- Command: `sed -n '1,220p' analysis/DL-PLAT-001-report.md`
  - Working directory: repository root
  - Purpose: Review DL-PLAT-001 external blocker report.
  - Result: File missing.
  - Exit code: 1
  - Warnings/errors: `sed: analysis/DL-PLAT-001-report.md: No such file or directory`
  - Deep-link introduced failure: No; report unavailable in checkout.
- Command: `sed -n '1,240p' shared/contracts/deep-links/routes.json`
  - Working directory: repository root
  - Purpose: Inspect authoritative shared route registry.
  - Result: File missing.
  - Exit code: 1
  - Warnings/errors: `No such file or directory`
  - Deep-link introduced failure: Expected integrated artifact absent in current branch; later restored after user instruction to proceed.
- Command: `sed -n '1,240p' shared/contracts/deep-links/README.md`
  - Working directory: repository root
  - Purpose: Inspect shared deep-link registry documentation.
  - Result: File missing.
  - Exit code: 1
  - Warnings/errors: `No such file or directory`
  - Deep-link introduced failure: Expected integrated artifact absent in current branch; later restored after user instruction to proceed.
- Command: `command -v dotnet`
  - Working directory: repository root
  - Purpose: Locate dotnet executable.
  - Result: `/usr/local/share/dotnet/dotnet`
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `dotnet --version`
  - Working directory: repository root
  - SDK/tool version: `10.0.203`
  - Purpose: Identify selected .NET SDK.
  - Result: `10.0.203`
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `dotnet --list-sdks`
  - Working directory: repository root
  - Purpose: Identify installed .NET SDKs.
  - Result: `8.0.420`, `10.0.203`
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `dotnet --info`
  - Working directory: repository root
  - SDK/tool version: `10.0.203`
  - Purpose: Identify SDK/runtime/global.json environment.
  - Result: SDK `10.0.203`, host `10.0.7`, ASP.NET runtime `10.0.7`, `global.json file: Not found`.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `dotnet restore apps/gateway/Gateway.Api/Gateway.Api.csproj --verbosity minimal`
  - Working directory: repository root
  - SDK/tool version: `10.0.203`
  - Purpose: Restore Gateway dependencies.
  - Result: Initial sandboxed attempt hung after `Determining projects to restore...`; cancellation later exited with code 1.
  - Exit code: 1 after cancellation.
  - Warnings/errors: No package/code error emitted before cancellation.
  - Deep-link introduced failure: No evidence; classified as sandbox/process-control issue because escalated restore passed.
- Command: `dotnet restore apps/gateway/Gateway.Api/Gateway.Api.csproj --verbosity minimal`
  - Working directory: repository root
  - SDK/tool version: `10.0.203`
  - Purpose: Restore Gateway dependencies with network-capable execution after sandboxed restore did not complete.
  - Result: Restore succeeded.
  - Exit code: 0
  - Warnings: `NU1510` for `Microsoft.Extensions.Caching.Memory` in `BuildingBlocks`.
  - Failure details: None.
  - Deep-link introduced failure: No.
- Command: `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore --verbosity minimal`
  - Working directory: repository root
  - SDK/tool version: `10.0.203`
  - Purpose: Build Gateway after restore.
  - Result: Initial sandboxed attempt produced no output and did not exit promptly after cancellation.
  - Exit code: 1 after cancellation.
  - Warnings/errors: Ended with `Build FAILED. 0 Warning(s) 0 Error(s). Time Elapsed 00:05:01.63`
  - Deep-link introduced failure: No evidence; constrained build passed.
- Command: `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore --verbosity minimal -maxcpucount:1 /p:UseSharedCompilation=false /nodeReuse:false`
  - Working directory: repository root
  - SDK/tool version: `10.0.203`
  - Purpose: Current Gateway build validation with constrained MSBuild/compiler settings.
  - Result: Build succeeded.
  - Exit code: 0
  - Relevant output summary: `Gateway.Api -> .../bin/Debug/net10.0/Gateway.Api.dll`; `Build succeeded. 1 Warning(s) 0 Error(s).`
  - Warnings: `NU1510` in `BuildingBlocks`.
  - Failure details: None.
  - Deep-link introduced failure: No.
- Command: `bash scripts/tests/test-deep-link-association-tools.sh`
  - Working directory: repository root
  - Purpose: Run focused association tooling test before correction.
  - Result: Passed.
  - Exit code: 0
  - Relevant output summary: generated QA fixture, validation passed, test passed.
  - Warnings/errors: None.
  - Deep-link introduced failure: No, but fixture did not cover `pathTemplate`.
- Command: `env PYTHONPYCACHEPREFIX=/tmp/dl-be-002-pycache python3 -m py_compile scripts/deep-links/generate-association-files.py scripts/deep-links/validate-association-files.py`
  - Working directory: repository root
  - Purpose: Python syntax validation before correction.
  - Result: Passed.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `python3 scripts/deep-links/generate-association-files.py --config config/deep-links/association-config.example.json --routes shared/contracts/deep-links/routes.json --output /tmp/dl-be-002-registry-wiring`
  - Working directory: repository root
  - Purpose: Registry wiring validation before correction.
  - Result: Failed because registry file is missing.
  - Exit code: 1
  - Failure details: `ERROR: required file not found: shared/contracts/deep-links/routes.json`
  - Deep-link introduced failure: Expected integrated artifact missing at that point; later restored after user instruction to proceed.
- Command: direct generator `pathTemplate` temp fixture
  - Working directory: repository root
  - Purpose: Prove generator pathTemplate support before correction.
  - Result: Failed.
  - Exit code: 1
  - Failure details: `ERROR: route entry does not contain a path string: {'pathTemplate': '/deals/:dealId'}`
  - Deep-link introduced failure: Yes; tooling defect corrected in DL-BE-002.
- Command: direct validator `pathTemplate` temp fixture
  - Working directory: repository root
  - Purpose: Prove validator pathTemplate support before correction.
  - Result: Failed.
  - Exit code: 1
  - Failure details: `ERROR: route registry contains invalid route paths`
  - Deep-link introduced failure: Yes; tooling defect corrected in DL-BE-002.
- Command: `bash scripts/tests/test-deep-link-association-tools.sh`
  - Working directory: repository root
  - Purpose: Rerun focused association tooling after correction.
  - Result: Passed.
  - Exit code: 0
  - Relevant output summary: generated QA fixture artifacts, validator passed, expected negative cases passed, test passed.
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `env PYTHONPYCACHEPREFIX=/tmp/dl-be-002-pycache-rerun python3 -m py_compile scripts/deep-links/generate-association-files.py scripts/deep-links/validate-association-files.py`
  - Working directory: repository root
  - Purpose: Rerun Python syntax validation after correction.
  - Result: Passed.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: direct generator `pathTemplate` temp fixture after correction
  - Working directory: repository root
  - Purpose: Prove corrected generator pathTemplate support.
  - Result: Passed.
  - Exit code: 0
  - Relevant output summary: generated temp QA fixture artifacts.
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: direct validator `pathTemplate` temp fixture after correction
  - Working directory: repository root
  - Purpose: Prove corrected validator pathTemplate support.
  - Result: Passed.
  - Exit code: 0
  - Relevant output summary: `deep-link association validation passed`.
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `python3 scripts/deep-links/generate-association-files.py --config config/deep-links/association-config.example.json --routes shared/contracts/deep-links/routes.json --output /tmp/dl-be-002-registry-wiring-rerun`
  - Working directory: repository root
  - Purpose: Rerun registry wiring validation after correction.
  - Result: Failed because registry file is missing.
  - Exit code: 1
  - Failure details: `ERROR: required file not found: shared/contracts/deep-links/routes.json`
  - Deep-link introduced failure: Expected integrated artifact missing at that point; later restored after user instruction to proceed.
- Command: `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore --verbosity minimal -maxcpucount:1 /p:UseSharedCompilation=false /nodeReuse:false`
  - Working directory: repository root
  - SDK/tool version: `10.0.203`
  - Purpose: Post-correction Gateway build validation.
  - Result: Build succeeded.
  - Exit code: 0
  - Relevant output summary: `Build succeeded. 1 Warning(s) 0 Error(s).`
  - Warnings: `NU1510` in `BuildingBlocks`.
  - Deep-link introduced failure: No.
- Command: `python3 scripts/check-doc-sync.py`
  - Working directory: repository root
  - Purpose: Documentation sync validation before documentation correction.
  - Result: Failed.
  - Exit code: 1
  - Failure details: doc-sensitive change in `scripts/tests/test-deep-link-association-tools.sh` required docs review.
  - Deep-link introduced failure: No; documentation update required and completed.
- Command: `python3 scripts/check-doc-sync.py`
  - Working directory: repository root
  - Purpose: Documentation sync validation after Gateway README update.
  - Result: Passed.
  - Exit code: 0
  - Relevant output summary: documentation sync check passed; docs touched include `apps/gateway/README.md`.
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git diff --check`
  - Working directory: repository root
  - Purpose: Whitespace/diff validation.
  - Result: Passed.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git status --short`
  - Working directory: repository root
  - Purpose: Final worktree status.
  - Result: Modified Gateway README and association tooling/test; added DL-BE-002 report.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git status --short apps/mobile apps/web shared/contracts/deep-links shared/contracts apps/services`
  - Working directory: repository root
  - Purpose: Boundary validation for Mobile/Web/shared/services.
  - Result: No output.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git log --all -- shared/contracts/deep-links/routes.json shared/contracts/deep-links/README.md --oneline`
  - Working directory: repository root
  - Purpose: Locate authoritative shared registry history after user asked to proceed.
  - Result: Found commits `771f620727759f75c1d6d0ad11d370d279d0cf38` and `4febc7801c9639f788c1c7a6b5ea90ce9d4dd54f`.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git show 771f620727759f75c1d6d0ad11d370d279d0cf38:shared/contracts/deep-links/routes.json`
  - Working directory: repository root
  - Purpose: Inspect authoritative historical registry contents.
  - Result: Verified five routes using `pathTemplate`.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `dotnet restore apps/gateway/Gateway.Api/Gateway.Api.csproj --verbosity minimal`
  - Working directory: repository root
  - SDK/tool version: `10.0.203`
  - Purpose: Restore after shared registry contract restoration.
  - Result: Passed.
  - Exit code: 0
  - Warnings: `NU1510` in `BuildingBlocks`.
  - Deep-link introduced failure: No.
- Command: `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore --verbosity minimal -maxcpucount:1 /p:UseSharedCompilation=false /nodeReuse:false`
  - Working directory: repository root
  - SDK/tool version: `10.0.203`
  - Purpose: Gateway build after shared registry contract restoration.
  - Result: Passed.
  - Exit code: 0
  - Relevant output summary: `Build succeeded. 1 Warning(s) 0 Error(s).`
  - Warnings: `NU1510` in `BuildingBlocks`.
  - Deep-link introduced failure: No.
- Command: `bash scripts/tests/test-deep-link-association-tools.sh`
  - Working directory: repository root
  - Purpose: Final focused association tooling validation.
  - Result: Passed.
  - Exit code: 0
  - Relevant output summary: generated fixture, validation passed, expected negative cases passed.
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `env PYTHONPYCACHEPREFIX=/tmp/dl-be-002-pycache-complete python3 -m py_compile scripts/deep-links/generate-association-files.py scripts/deep-links/validate-association-files.py`
  - Working directory: repository root
  - Purpose: Final Python syntax validation.
  - Result: Passed.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `python3 scripts/deep-links/generate-association-files.py --config config/deep-links/association-config.example.json --routes shared/contracts/deep-links/routes.json --output /tmp/dl-be-002-registry-wiring-complete`
  - Working directory: repository root
  - Purpose: Final registry wiring validation against restored real registry.
  - Result: Passed registry wiring; failed on expected external inputs.
  - Exit code: 1
  - Relevant output summary: `ERROR: development: missing required values: host, appleTeamId`.
  - Failure classification: expected external-input blocker, not route-contract failure.
  - Deep-link introduced failure: No.
- Command: `shasum -a 256 shared/contracts/deep-links/routes.json`
  - Working directory: repository root
  - Purpose: Record restored registry checksum.
  - Result: `745fb44451f954bdb85ca84c747ebdd6be7512c184354b0b3e6a0f554f835237`.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git show 771f620727759f75c1d6d0ad11d370d279d0cf38:shared/contracts/deep-links/routes.json | shasum -a 256`
  - Working directory: repository root
  - Purpose: Compare restored registry with historical authoritative source.
  - Result: `745fb44451f954bdb85ca84c747ebdd6be7512c184354b0b3e6a0f554f835237`.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No; checksums match.
- Command: `python3 scripts/check-doc-sync.py`
  - Working directory: repository root
  - Purpose: Final documentation sync validation.
  - Result: Passed.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git diff --check`
  - Working directory: repository root
  - Purpose: Final diff whitespace validation.
  - Result: Passed.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.
- Command: `git status --short apps/mobile apps/web apps/services`
  - Working directory: repository root
  - Purpose: Final Mobile/Web/services boundary validation.
  - Result: No output.
  - Exit code: 0
  - Warnings/errors: None.
  - Deep-link introduced failure: No.

## 39. Boundary / Scope Validation
- No `apps/mobile/**` files modified.
- No `apps/web/**` files modified.
- `shared/contracts/**` files modified only to restore the missing authoritative registry and its companion contract reader/docs from historical commit `771f6207`.
- No unrelated `apps/services/**` files modified.
- Backend/Gateway production code was not modified by DL-BE-002.
- DL-BE-002 modifications were limited to association tooling, focused association test, Gateway documentation, and this report.

## 40. External Production Input Blockers
- Development host: unresolved.
- QA host: unresolved.
- Production host: unresolved.
- Apple Team ID/App Identifier Prefix: unresolved.
- Android public SHA-256 fingerprints: unresolved.
- Play App Signing status: unresolved.
- DNS owner: unresolved.
- TLS owner: unresolved.
- Deployment owner/live deployment: unresolved.
- No values were guessed or injected.

## 41. Acceptance-Criteria Status
- AC-001 — Integrated State Confirmed: Complete — Gateway endpoints/config/tooling/docs and restored authoritative shared registry are present.
- AC-002 — Required SDK Identified: Complete — Gateway csproj targets `net10.0`; no root `global.json`; SDK requirement is .NET 10.
- AC-003 — Compatible SDK Available: Complete — `dotnet --version` is `10.0.203`; ASP.NET runtime `10.0.7` installed.
- AC-004 — Gateway Restore Passes: Complete — escalated `dotnet restore apps/gateway/Gateway.Api/Gateway.Api.csproj --verbosity minimal` exited 0.
- AC-005 — Current Gateway Build Passes: Complete — constrained current build exited 0 with 1 warning and 0 errors.
- AC-006 — Build Warnings Classified: Complete — `NU1510` in `BuildingBlocks` classified pre-existing/unrelated.
- AC-007 — Association Tooling Tests Pass: Complete — focused test passed after pathTemplate fixture update.
- AC-008 — Python Validation Passes: Complete — `py_compile` passed after correction.
- AC-009 — Shared Registry Found: Complete — `shared/contracts/deep-links/routes.json` is present and parsed by generator.
- AC-010 — pathTemplate Supported: Complete — generator/validator direct fixtures and focused test pass with `pathTemplate`.
- AC-011 — Registry Wiring Passes: Complete — generator resolves real registry and advances to expected external-input validation.
- AC-012 — No Duplicate Registry: Complete — no duplicate deep-link route registry found or introduced.
- AC-013 — Apple Canonical Path Present: Complete — `Program.cs` maps `/.well-known/apple-app-site-association`.
- AC-014 — Android Canonical Path Present: Complete — `Program.cs` maps `/.well-known/assetlinks.json`.
- AC-015 — Anonymous Access Preserved: Complete — both mapped endpoint builders call `.AllowAnonymous()`.
- AC-016 — JSON Content Type Preserved: Complete — success path uses `Results.File(fullPath, "application/json")`.
- AC-017 — Missing Artifact Safety: Complete — missing files return JSON `Results.NotFound(...)`, not app fallback.
- AC-018 — No Tenant / Database Dependency: Complete — endpoint helper only reads config/environment/filesystem.
- AC-019 — Documentation Sync Passes: Complete — `python3 scripts/check-doc-sync.py` passed after README update.
- AC-020 — No Mobile Changes: Complete — scoped git status for `apps/mobile` produced no output.
- AC-021 — No Web Changes: Complete — scoped git status for `apps/web` produced no output.
- AC-022 — Shared Registry Unchanged: Complete — restored `routes.json` checksum matches historical source commit exactly; no route semantic change introduced.
- AC-023 — No Unrelated Backend Changes: Complete — no `apps/services/**` files modified; Gateway production code unchanged.
- AC-024 — External Inputs Remain Honest: Complete — host/signing/Play/DNS/TLS/deployment values remain unresolved and unguessed.
- AC-025 — DL-BE-001 Resume Classification Defined: Complete — classification set to `Resume Ready — External Inputs Only`.
- AC-026 — Historical Results Not Substituted: Complete — current restore/build/test evidence recorded in DL-BE-002.
- AC-027 — No Deployment Fabrication: Complete — no live deployment, HTTPS, Apple, Android, or device verification claimed.

## 42. Issues and Failures
- Missing requested DL-INT-002 report file.
- Missing requested DL-PLAT-001 report file.
- Missing requested shared deep-link registry path: resolved by restoring historical authoritative contract files.
- Missing requested shared deep-link registry README path: resolved by restoring historical authoritative contract files.
- Generator/validator pathTemplate support was not present initially; corrected in DL-BE-002.
- Initial sandboxed restore/build attempts did not complete cleanly, but escalated restore and constrained build passed.
- Registry wiring now reaches expected external-input validation.

## 43. Blockers and External Dependencies
- External production inputs:
  - Approved Development/QA/Production hosts.
  - Apple Team ID/App Identifier Prefix.
  - Android public SHA-256 fingerprints.
  - Google Play App Signing status.
  - DNS/TLS/deployment ownership and live deployment.

## 44. Security Review
- No secrets, signing material, host guesses, fingerprints, or production values were added.
- Association endpoints remain anonymous by design and serve only configured static JSON artifacts.
- Path containment guard remains in Gateway source.
- Tooling validates public identity/fingerprint formats only.

## 45. Architecture Risks and Concerns
- Integrated route alignment is now proven against the restored authoritative shared registry path.
- Tooling now supports `pathTemplate` and real registry wiring validates up to expected external-input checks.
- Optional local HTTP validation was not run due process-control risk; static endpoint review plus build validation covered Gateway behavior.

## 46. Known Gaps
- No approved hosts.
- No Apple Team ID/App Identifier Prefix.
- No Android fingerprints.
- No Play App Signing confirmation.
- No generated production association artifacts.
- No live deployment/public HTTPS verification.
- No physical-device verification.

## 47. DL-BE-001 Resume Classification
Resume Ready — External Inputs Only

Rationale: Current Gateway restore/build passes, focused association tooling passes, Python validation passes, `pathTemplate` support is confirmed, the authoritative shared registry is restored and parsed, registry wiring advances to expected external-input validation, Gateway canonical routes are confirmed, and Mobile/Web/service boundaries are preserved. The remaining unresolved items are external Platform/release/deployment values.

## 48. DL-PLAT-001 Handoff
- External values remain unresolved and must be supplied outside DL-BE-002:
  - Development/QA/Production deep-link hosts.
  - DNS/TLS/deployment ownership.
  - Apple Team ID/App Identifier Prefix.
  - Android signing fingerprints and Play App Signing status.
- DL-BE-002 did not implement host approval, DNS, TLS, deployment, or production artifact publication.

## 49. DL-QA-001 Handoff
- QA can rely on current Gateway build success and static endpoint source review.
- QA cannot start production/deployment verification until the authoritative registry file and external host/signing values are supplied.
- Optional local HTTP validation was not run in DL-BE-002.

## 50. Out-of-Scope Confirmation
- No implementation was performed for host approval, DNS, TLS, Apple Team ID, Android signing metadata discovery, Play Console, production deployment, public HTTP verification, physical-device verification, Mobile, Web, Deal, Report, Backend business APIs, database, campaigns, notifications, analytics, or shared-route redesign.

## 51. Follow-Up Recommendations
1. Continue DL-PLAT-001 external-input collection for host, Apple, Android, DNS, TLS, and deployment values.
2. Generate real environment association artifacts once approved values exist.
3. Run live HTTPS and Apple/Android/device verification in the appropriate deployment/Mobile/QA tickets.

## 52. Final Status
Complete — DL-BE-001 Resume Ready, External Inputs Only

Gateway restore/build validation succeeded on a compatible .NET 10 environment. One genuine `pathTemplate` tooling defect was found and corrected. The missing authoritative shared registry was restored from historical source without route semantic changes, and registry wiring now reaches expected external-input blockers. Remaining work is limited to external Platform/release/deployment inputs and live verification.
