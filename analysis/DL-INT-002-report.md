# DL-INT-002 Integration Report

## 1. Ticket Summary

- Ticket: DL-INT-002 — Integrate Deep-Link Association Backend Work with Current Shared-Contract Branch
- Type: Backend/Platform source-provenance and Git integration
- Status: Partially complete — DL-BE-001 Defect Found.

## 2. Objective

Locate and verify the authoritative DL-BE-001 implementation, integrate only that existing association work onto the current shared-contract branch, and validate registry wiring and Gateway/tooling behavior without reconstructing missing work.

## 3. Scope

Source discovery, provenance verification, minimal Backend/Platform integration, conflict handling, and targeted validation. Production-input resolution, deployment, Mobile/Web work, business APIs, databases, and shared-route redesign are excluded.

## 4. Initial Integration Plan

1. Record repository state, applicable instructions, and DL-PLAT-001 findings.
2. Inspect the authoritative shared registry and current Backend/Mobile/Web boundaries.
3. Search only already-available local refs/history/worktrees for the exact DL-BE-001 source and report.
4. Verify provenance and source diff before choosing or executing a Git strategy.
5. If provenance is Confirmed or Strong repository evidence, integrate the minimum coherent Backend/Platform commit set; otherwise stop with only this report changed.
6. Run focused tooling, Gateway, registry-wiring, documentation, diff, and boundary validation if integration is possible.

## 5. Current Branch and Working-Tree State

- Branch: `feat(app)-LSV3-1055-Mobile-Configure-Native-iOS-Universal-Links`.
- Baseline HEAD: `0b8f01db213edf8568b5efdebb685f505eabd69e`.
- Baseline working tree after mandatory report creation: only `?? analysis/DL-INT-002-report.md`.
- `analysis/DL-PLAT-001-report.md` is now tracked in HEAD; no pre-existing user working-tree changes were reported.

## 6. DL-PLAT-001 Dependency Review

DL-PLAT-001 established that the current checkout has the authoritative registry and app ID mappings, while external hosts/signing inputs remain blocked. Its earlier finding that DL-BE-001 was absent was true at that earlier HEAD; current local refs and HEAD now include the Backend source commit added later on 2026-08-20.

## 7. Current Shared Registry Review

`shared/contracts/deep-links/routes.json` exists, is version 1, and contains five enabled routes whose canonical field is `pathTemplate`. Its README identifies it as the only authoritative registry. Baseline content is preserved; no semantic change is planned.

## 8. DL-BE-001 Source Discovery

- Source branch: `origin/feature-deeplinking-be-setup` (already-available remote-tracking ref; no fetch performed).
- Source implementation commit: `624ef3583daeb95adeb330763b6f2a3ca518250c`.
- Source report: `analysis/DL-BE-001-report.md` in that commit.
- Source was merged by `db8affd03b06fd0fd9bb33ec77ad3d9ea20c68d5` (PR #195) and is already an ancestor of current HEAD.
- Discovery matched the exact expected filenames and association symbols from the ticket.

## 9. Source Provenance Table

| Field | Value |
|---|---|
| Source branch | `origin/feature-deeplinking-be-setup` |
| Source commit | `624ef3583daeb95adeb330763b6f2a3ca518250c` |
| Source report path | `analysis/DL-BE-001-report.md` |
| Source author | xentra-ralphlopez `<ralph.lopez@xentragroup.com>` |
| Source commit date | `2026-08-20T23:03:44+08:00` |
| Files changed | 10 files: Gateway Program/config/README; Backend EC2 runbook; report; example+QA configs; generator; validator; tooling test |
| Unrelated changes present? | No product/Mobile/Web/shared/service changes in the source commit; one QA fixture config is part of association tooling |
| Integration strategy | Verify existing ancestry; no duplicate Git operation. Source commit is already integrated. |
| Provenance confidence | Confirmed |

## 10. Source Commit Review

The commit subject and body explicitly identify DL-BE-001, Gateway-hosted association endpoints, deterministic generation/validation tooling, shared-registry consumption, and blocked external inputs. Its sole parent is `753d04ea37048b4cc6f581bb5ca7eb2c35378353`. Author and committer match, and the exact source report documents the implementation/validation.

## 11. Source Diff Review

Source diff: 940 insertions and one deletion across 10 Backend/Platform/report files. Added files are the DL-BE-001 report, example/QA association configs, generator, validator, and shell tooling test. Modified files are Gateway Program/config/README and the Backend EC2 deployment runbook.

## 12. Unrelated Source Changes Review

`git diff-tree` and scoped source-parent diff show no `apps/mobile/**`, `apps/web/**`, `shared/contracts/deep-links/**`, `apps/services/**`, auth, database, or product-domain changes. No unrelated source commit needs filtering.

## 13. Integration Strategy

Selected before any Git mutation: **verify existing ancestry; perform no merge/rebase/cherry-pick**. `git merge-base HEAD 624ef358...` equals the source commit, and `git branch -a --contains` includes the current branch. Reapplying it would duplicate work and risk conflicts. Validation will treat `624ef358...` as the integrated commit and inspect all changes between it and current HEAD for preservation/defects.

Integration correction strategy after validation exposed a contract mismatch: narrowly add `pathTemplate` support to the existing generator and validator and update the existing tooling test to use the real authoritative registry. Do not change the registry or create another one.

## 14. Integration Execution

No Git integration command was required because the source commit was already integrated. Existing ancestry preserves exact provenance. A narrow post-integration compatibility correction was applied to the existing generator, validator, and tooling test so they consume the current contract's `pathTemplate` field.

## 15. Conflict Inventory

| File | Conflict | Resolution | Why |
|---|---|---|---|
| None | No conflict encountered in this ticket | Not applicable | Source was already integrated before execution |

## 16. Conflict Resolutions

No textual Git conflicts occurred. The semantic contract conflict was resolved in tooling: preserve `routes.json` unchanged, add `pathTemplate` as the preferred recognized field, retain legacy aliases for compatibility, and replace the synthetic test registry with the authoritative file.

## 17. Shared Registry Preservation

Baseline verified. The registry uses `pathTemplate`; its content will remain unchanged. Integration tooling—not the contract—must adapt.

Final scoped diff confirms `shared/contracts/deep-links/routes.json` is unchanged.

## 18. Integrated Gateway Review

Present in current `apps/gateway/Gateway.Api/Program.cs`. Both mappings precede the authenticated YARP fallback and delegate deterministic file serving to `MapDeepLinkAssociationFile`. Static review confirms no tenant, database, session, or business-service dependency.

## 19. Apple Association Path Review

`/.well-known/apple-app-site-association` is mapped with `.AllowAnonymous()`. Existing file response uses `application/json`; absence returns a JSON 404 result and no redirect/HTML fallback is coded.

## 20. Android Association Path Review

`/.well-known/assetlinks.json` is mapped with `.AllowAnonymous()`. Existing file response uses `application/json`; absence returns a JSON 404 result and no redirect/HTML fallback is coded.

## 21. Association Directory Configuration

`DeepLinks:AssociationDirectory` defaults to `DeepLinks/Associations/production`. `ResolveAssociationDirectory` supports rooted deployment overrides and content-root-relative defaults. File resolution validates containment beneath the selected directory. Deployment docs use `DeepLinks__AssociationDirectory` and environment-specific output directories. No real values/artifacts were populated.

## 22. Generator Integration

Defect found and corrected: `route_path()` now prefers `pathTemplate` while retaining source aliases. A direct rerun against the real registry now parses routes and stops at the example config's intentionally missing host/Apple Team ID.

## 23. Validator Integration

Corrected identically: `extract_route_paths()` recognizes `pathTemplate`. Fixture validation against the authoritative registry passes, including app ID, package, fingerprint format, and exact route coverage.

## 24. Association Test Integration

The existing test now consumes `shared/contracts/deep-links/routes.json` directly. It passes generation/validation, expected unresolved-config failure, and unsafe environment-name rejection.

## 25. Backend Documentation Integration

Present: `apps/gateway/README.md` documents canonical paths/config/generation/validation; `AWS_EC2_BE_MICROSERVICES_DEPLOYMENT.md` documents environment directory deployment and post-deployment checks; source report remains available. Existing docs already name the correct registry path, so no additional Backend doc edit was required.

## 26. Files Inspected

- User-provided DL-INT-002 execution instructions only.
- Root repository instructions and delivery-mode implementation workflow.
- Current refs, reflogs, worktrees, commit/file history, source commit metadata/diff, and source DL-BE-001 report.
- Current shared registry/README; Gateway Program/config/README; Backend EC2 runbook; association configs; generator; validator; tooling test; relevant post-source commit history.

## 27. Files Added

- `analysis/DL-INT-002-report.md` — created first as required.

## 28. Files Modified

- `analysis/DL-INT-002-report.md` — execution/provenance/integration/validation record.
- `scripts/deep-links/generate-association-files.py` — recognizes authoritative `pathTemplate`.
- `scripts/deep-links/validate-association-files.py` — validates authoritative `pathTemplate` routes.
- `scripts/tests/test-deep-link-association-tools.sh` — uses the real shared registry.
- `apps/gateway/README.md` — documents direct `pathTemplate` consumption and real-registry test coverage.

## 29. Files Deleted

None.

## 30. Integrated Artifact Inventory

| Artifact | Current status |
|---|---|
| `analysis/DL-BE-001-report.md` | Present from confirmed source |
| Gateway canonical mappings | Present |
| `DeepLinks:AssociationDirectory` configuration | Present |
| `config/deep-links/association-config.example.json` | Present with blocked/null external values |
| `config/deep-links/association-config.qa.json` | Intentionally absent after later safety deletion; not required/restored |
| Association generator | Present; current-registry compatibility corrected |
| Association validator | Present; current-registry compatibility corrected |
| Tooling test | Present; runs against authoritative registry |
| Gateway README / Backend runbook | Present |
| Generated deployable artifacts | Absent by design; production inputs blocked |

## 31. Implementation Progress

- Mandatory report created first.
- Baseline branch/HEAD/status and instructions recorded.
- Authoritative source branch, commit, author/date, report, file set, and merge commit confirmed.
- Source diff contains only coherent Backend/Platform association work.
- Confirmed the source commit is already an ancestor of current HEAD; selected no-op ancestry verification rather than duplicate integration.
- Current-file and validation review completed within the available toolchain.
- Verified the later commit `3505e94b2` intentionally deleted `association-config.qa.json`, which contained unapproved/unsafe values (an API host inferred as QA deep-link host, a Team ID without approved provenance, and `localhost:5010`). It will not be restored.
- Reproduced the real registry-wiring defect: generator exited 1 because route entries expose `pathTemplate`, not the source fixture's `path`.
- Corrected generator/validator compatibility and converted the tooling test to the authoritative registry.
- Tooling tests, Python compile, registry-wiring expected-failure check, diff/boundary checks pass.
- Gateway build attempt failed because `dotnet` is unavailable in the environment; standard installation paths were checked with no result.

## 32. Validation Commands and Results

All commands ran from `/Users/aaronruanto/Documents/project/legalsynq-v2.0`.

| Exact command | Purpose | Result | Failure / integration relation | Rerun |
|---|---|---|---|---|
| `python3 scripts/deep-links/generate-association-files.py --config config/deep-links/association-config.example.json --routes shared/contracts/deep-links/routes.json --output /private/tmp/dl-int-002-real-registry` (before correction) | Prove current registry wiring | FAIL, exit 1 — rejected first `pathTemplate` route | DL-BE-001 integration defect | Yes |
| `bash scripts/tests/test-deep-link-association-tools.sh` | Generate/validate fixtures and safety failures using real registry | PASS — generation, validation, blocked example, unsafe name checks | No remaining failure | No |
| `PYTHONPYCACHEPREFIX=/private/tmp/dl-int-002-pycache python3 -m py_compile scripts/deep-links/generate-association-files.py scripts/deep-links/validate-association-files.py` | Python syntax validation with sandbox-safe cache | PASS | None | No |
| `python3 scripts/deep-links/generate-association-files.py --config config/deep-links/association-config.example.json --routes shared/contracts/deep-links/routes.json --output /private/tmp/dl-int-002-real-registry` (after correction) | Prove registry parses before external-input validation | Expected FAIL, exit 1 — `development: missing required values: host, appleTeamId` | External input blocker, not integration failure | No |
| `git diff --check; git diff -- shared/contracts/deep-links/routes.json; git status --short apps/mobile apps/web shared/contracts/deep-links apps/services` | Whitespace and boundary validation | PASS — no scoped boundary output | None | No |
| `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore` | Build integrated Gateway | NOT RUN successfully, exit 127 — `dotnet: command not found` | Environment/toolchain absence, not shown to be integration-related | No viable rerun |
| `command -v dotnet \|\| true; ls -l /usr/local/share/dotnet/dotnet /opt/homebrew/bin/dotnet /usr/local/bin/dotnet ...; find /Users/aaronruanto/.dotnet ...` | Check standard local SDK locations | Completed — no installation found | Confirms toolchain blocker | No |
| `bash -n scripts/tests/test-deep-link-association-tools.sh` | Shell syntax validation | PASS | None | No |
| `python3 scripts/check-doc-sync.py` (first run) | Documentation policy validation | FAIL — script test change required relevant doc review | Introduced documentation-sync requirement | Yes |
| `git diff --check; git diff --stat; git diff --name-status; git status --short` | Diff hygiene and inventory | PASS — three tooling files modified plus report before doc update | None | Yes, final rerun required |
| `git diff --exit-code -- shared/contracts/deep-links/routes.json; git status --short apps/mobile apps/web shared/contracts/deep-links apps/services; grep ...` | Registry/boundary/canonical-path check | PASS — no boundary diff; both Gateway paths present | None | No |
| `python3 scripts/check-doc-sync.py` (rerun) | Documentation policy validation after Gateway README update | PASS — doc-sensitive change has relevant documentation | None | No |
| Final `git diff --check; git diff --stat; git diff --name-status; git status --short` | Final whitespace, inventory, and status review | PASS — four tracked integration/doc files modified plus new report | None | No |
| `git status --short apps/mobile apps/web shared/contracts/deep-links apps/services; git diff --exit-code -- shared/contracts/deep-links/routes.json; grep -c '^## [0-9][0-9]*\\.' analysis/DL-INT-002-report.md` | Final boundaries and report structure | PASS — no boundary diff; registry unchanged; 50 sections | None | No |
| `git diff --no-index --check /dev/null analysis/DL-INT-002-report.md` | Check whitespace in the untracked report | PASS — expected exit 1 because the file differs from `/dev/null`; no whitespace-error output | None | No |

## 33. Tooling Test Validation

PASS — `bash scripts/tests/test-deep-link-association-tools.sh` generated QA fixture artifacts from the authoritative registry, validated them, confirmed unresolved example configuration fails, and rejected unsafe environment names.

## 34. Gateway Build Validation

BLOCKED — command attempted, but `dotnet` is unavailable (`zsh: command not found`, exit 127). No restore was attempted or required because execution could not start. Source DL-BE-001 report records a successful build at the source commit with two pre-existing NU1510 warnings, but that historical result is not substituted for current validation.

## 35. Python Syntax Validation

PASS — both scripts compiled with bytecode redirected to `/private/tmp/dl-int-002-pycache`.

## 36. Registry Wiring Validation

Initial direct run: FAIL — `pathTemplate` mismatch. After correction: PASS for registry discovery/parsing; generation proceeds to and stops at the expected missing external host/Apple Team ID validation. Tooling generation+validation against the same real registry passes. Classification: **Registry wiring PASS; production-input generation BLOCKED**.

## 37. Production Input Blockers

Still blocked and not guessed: approved Development/QA/Production hosts, Apple Team ID/App Identifier Prefix, Android installed-build public SHA-256 fingerprints, Play App Signing status, DNS/TLS ownership/configuration, deployment, live HTTP verification, and physical-device verification. The deleted QA config is not authoritative evidence and was not restored.

## 38. Boundary / Scope Validation

Scoped status/diff checks show no changes under `apps/mobile/**`, `apps/web/**`, `shared/contracts/deep-links/**`, or `apps/services/**`. No database, auth, business API, or generated deployment artifact was added. Source-history differences in Mobile/Web after the source commit are pre-existing current-branch commits, not DL-INT-002 changes.

## 39. Acceptance-Criteria Status

| Criterion | Status | Evidence |
|---|---|---|
| AC-001 | Complete | Source branch `origin/feature-deeplinking-be-setup`, commit `624ef358...`, and report located |
| AC-002 | Complete | Author/date/body/report/exact filenames/merge PR and ancestry confirm provenance |
| AC-003 | Complete | Source parent diff reviewed: 10 coherent Backend/Platform/report files |
| AC-004 | Complete | Existing-ancestry/no-duplicate strategy documented before correction changes |
| AC-005 | Complete | Source commit is ancestor of HEAD; Gateway/tooling/docs present |
| AC-006 | Complete | Final diff proves `routes.json` unchanged |
| AC-007 | Complete | Generator now reads canonical `pathTemplate`; real-registry tooling test passes |
| AC-008 | Complete | No duplicate route registry added; synthetic fixture removed |
| AC-009 | Complete | Apple canonical path present in current Gateway |
| AC-010 | Complete | Android canonical path present in current Gateway |
| AC-011 | Complete | Both current mappings call `.AllowAnonymous()` before authenticated YARP fallback |
| AC-012 | Complete | Current file responses specify `application/json` |
| AC-013 | Complete | Default and override directory behavior plus containment check present/documented |
| AC-014 | Complete | Generator present and focused real-registry generation passes |
| AC-015 | Complete | Validator present and real-registry fixture validation passes |
| AC-016 | Complete | `bash scripts/tests/test-deep-link-association-tools.sh` passed |
| AC-017 | Blocked | Current Gateway build could not execute because `dotnet` is unavailable |
| AC-018 | Complete | Direct run reaches expected external-input validation, not missing/invalid registry |
| AC-019 | Complete | Example values remain null/empty; no hosts/Team IDs/fingerprints/Play status guessed |
| AC-020 | Complete | Final scoped status has no `apps/mobile/**` changes |
| AC-021 | Complete | Final scoped status has no `apps/web/**` changes |
| AC-022 | Complete | `git diff --exit-code -- shared/contracts/deep-links/routes.json` passed |
| AC-023 | Complete | No `apps/services/**` or business Backend changes; only association tooling/docs/report |
| AC-024 | Complete | No textual conflict; semantic contract conflict and narrow resolution documented |
| AC-025 | Complete | Gateway README and Backend EC2 runbook are present; README synced to correction |
| AC-026 | Complete | Resume classification is explicitly `Defect Found` |
| AC-027 | Complete | No deployment/live HTTP/Apple/Android/device success is claimed |

## 40. Issues and Failures

- No source/provenance failure. Earlier DL-PLAT-001 evidence was stale relative to the current HEAD because the Backend source was merged/integrated afterward.
- DL-BE-001 generator/validator schema mismatch: source expects `path`/`route`/`pattern`; current authoritative registry uses `pathTemplate`.
- The source commit briefly included `association-config.qa.json` with values that were not authoritative under DL-PLAT-001 rules; it was deleted by later commit `3505e94b2` and will remain deleted.
- Gateway build command failed to start because `dotnet` is unavailable (exit 127). Current build success remains unverified.
- Initial documentation-sync run failed after the script-test change; Gateway README was updated and the rerun passed.

## 41. Blockers and External Dependencies

- Local validation blocker: install/provide a .NET 10 SDK, then run the Gateway build.
- External release blockers: approved hosts, Apple Team ID/App Identifier Prefix, Android installed-app fingerprints, and Play App Signing confirmation.
- Deployment blockers: named DNS/TLS/deployment owners, environment directory mapping, artifact generation using approved inputs, deployment, and live verification.

## 42. Security Review

No credentials, private keys, keystores, passwords, provisioning profiles, private certificates, or production signing values were added or recorded. Only public placeholder schema fields and application IDs remain. The unsafe deleted QA config was reviewed through Git history, explicitly rejected as authoritative, and not restored. Path-containment/environment-slug protections remain intact.

## 43. Architecture Risks and Concerns

- Recreating association infrastructure would destroy source provenance and is prohibited.
- A mixed source commit could import unrelated Backend, Mobile, Web, auth, or database changes.
- The current shared registry must remain authoritative and semantically unchanged.
- Future contract field changes could break Python tooling; using the real registry in the focused test now exposes that drift.
- Historical source-build success does not prove the current Gateway until a current .NET build runs.
- Default Production association directory must be overridden correctly for non-production deployments.

## 44. Known Gaps

- Current Gateway build not executed due missing SDK.
- No live HTTP endpoint behavior was exercised.
- External production inputs and deployable artifacts remain unavailable.
- No Apple, Android, Play Console, or physical-device validation occurred.

## 45. DL-BE-001 Resume Status

**Defect Found** — source provenance/integration and registry wiring are resolved, and the identified `pathTemplate` defect is corrected with passing focused tests. However, the ticket requires a current Gateway build before `Resume Ready — External Inputs Only`; that build is blocked by the missing .NET SDK.

## 46. DL-PLAT-001 Handoff

DL-PLAT-001 can now treat the source branch/commit/report and registry wiring as resolved. Its external host, DNS/TLS, Apple identity, Android signing, Play, deployment-owner, and live-verification blockers remain unchanged. The removed QA config supplies no approved values.

## 47. DL-QA-001 Handoff

Gateway association code and corrected tooling are present, but QA remains not ready: no approved host/signing inputs, generated deployable artifacts, deployment, current Gateway build validation, or live endpoint evidence exists.

## 48. Out-of-Scope Confirmation

No host approval, DNS, TLS, Apple/Android/Play identity resolution, deployment, live HTTP/device verification, Mobile/Web change, Deal/Report work, Backend business API, database, campaign, notification, analytics, or shared-route redesign has been performed.

## 49. Follow-Up Recommendations

1. On a workstation/CI runner with .NET 10, run `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore` (restore first if assets are unavailable).
2. Commit/review the narrow compatibility correction and report with source commit `624ef358...` preserved in ancestry.
3. Platform/Mobile release owners supply only authoritative public production inputs from DL-PLAT-001.
4. Generate and validate per-environment artifacts, configure `DeepLinks__AssociationDirectory`, deploy, and run direct HTTPS checks before DL-QA-001.

## 50. Final Status

**Partially complete — DL-BE-001 Defect Found**. Authoritative source is confirmed and already integrated. The current-registry defect is corrected and focused tests pass, but the required current Gateway build is blocked because the .NET SDK is unavailable; external production inputs also remain blocked.
