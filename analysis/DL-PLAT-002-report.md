# DL-PLAT-002 Report

## 1. Ticket Summary
Repository evidence verifies mechanisms and app IDs, but not production association inputs.
## 2. Objective
Define OS-managed portal-root handoff with normal Web fallback.
## 3. Simplified Phase-1 Scope
Exact generic portal entry only; no IDs, Web UI/JS, schemes, detection, or stores.
## 4. Initial Investigation Plan
Review prior reports, host/deployment, Mobile/signing, association tooling, navigation, owners, and tests.
## 5. Current Branch and Working-Tree State
Branch `feat(app)-LSV3-1193-Implement-Web-to-Mobile-Deep-Link`; HEAD `2f5b4c2a89b3656dea6e8f6547f2af2d6e63340e`; initially clean. This report was the first write.
## 6. Repository Instruction Review
Root `AGENTS.md` and implementation-mode instructions reviewed; no scoped analysis instructions.
## 7. Prior Association Architecture Review
DL-APP-008 restored environment-driven Mobile configuration; DL-BE-001/002 provide Gateway generation/serving; DL-WEB-001 retains URL generation; DL-WEB-007R removed Open-in-App UI. All prior platform reports leave release inputs external.
## 8. Current Web Rollback State
No production Open-in-App presentation or redirect logic remains.
## 9. Current Mobile Association State
`EXPO_PUBLIC_DEEP_LINK_HOST` drives iOS/Android claims. Missing non-production host omits claims; missing Production host fails closed.
## 10. Current Gateway Association State
Gateway anonymously serves direct JSON at both canonical `/.well-known` endpoints from a configured artifact directory; missing files return 404, not redirects.
## 11. Portal Host Inventory
Local portal is HTTP `localhost:5000`. Repository portal domains are environment-driven/tenant-specific; `portal.legalsynq.com` is an example. Historical `links-qa.legalsynq.net`/`links.legalsynq.net` are unapproved candidates. API hosts are not portal-host proof.
## 12. Associated Host Topology Decision
**Option C — Blocked: Portal Association Host Decision Required.** Option A (actual portal host) is preferred, but one shared host versus tenant subdomains is unresolved. Option B lacks approved fallback ownership.

| Property | Decision | Evidence |
|---|---|---|
| Associated/existing host; same host | Blocked; same host preferred | No approved committed deployment value |
| Development | Not Required by default | Local portal is HTTP; safe omission exists |
| QA / Production | Blocked | Candidates only |
| HTTPS / AASA / Asset Links | Required, unverified live | Implemented serving contract |
| DNS/TLS/hosting owners | Unresolved | Mechanisms/examples, no named owners |
## 13. Development Host Decision
Not Required unless release owners approve a physical-development association environment.
## 14. QA Host Decision
Blocked; `links-qa.legalsynq.net` is candidate-only.
## 15. Production Host Decision
Blocked; `links.legalsynq.net` and portal examples are not authoritative.
## 16. DNS Ownership
Identity has Route 53 provisioning mechanisms, but no named association-host owner/zone/record approval.
## 17. TLS Ownership
Deployment docs show Nginx/Certbot examples and UI claims automated TLS; named owner/lifecycle is unresolved.
## 18. Hosting / Deployment Ownership
Web owns fallback content and Gateway owns serving code; public edge and named deployer are unresolved.
## 19. iOS Bundle Identifier Review
Development/QA: `com.legalsynq.qa`; Production: `com.legalsynq` (tracked Expo/EAS config; strong evidence).
## 20. Apple Team ID Investigation
`X527XUP3B3` occurs only in ignored/generated Xcode output; ASC numeric IDs are not Team IDs.
## 21. Apple Team ID Decision
Candidate `X527XUP3B3` — not authoritative. QA/Production AASA IDs are blocked pending confirmed App Identifier Prefix.
## 22. AASA Existing Implementation Review
Generator emits modern `appIDs`/`components`; Gateway serves JSON directly. It currently derives every resource route.
## 23. AASA Generic Path Contract
iOS must claim exact `/` only. Current generator cannot do so without resource routes; no deployable AASA generated.
## 24. Android Package Identifier Review
Development/QA: `com.legalsynq.qa`; Production: `com.legalsynq` (tracked Expo config; strong evidence).
## 25. Android Signing Configuration Review
Development profile says internal; QA/Production installed distributions and signing sources are unproven. Local debug/generated data is non-authoritative.
## 26. Play App Signing Review
**External Android Release Input Required:** enrollment, installed-app certificate, upload-key distinction, and owner are unresolved.
## 27. QA Android SHA-256 Investigation
Blocked pending fingerprint/provenance for the exact QA-installed artifact.
## 28. Production Android SHA-256 Investigation
Blocked pending installed Play artifact fingerprint; upload key is not substitutable if Play signing applies.
## 29. assetlinks.json Existing Implementation Review
Generator emits `delegate_permission/common.handle_all_urls`, package, and public fingerprints. Path narrowing is not in Asset Links.
## 30. assetlinks Generic Contract
Use environment package + authoritative installed-build fingerprint(s); Android intent filter must separately match exact `/` only.
## 31. Mobile Associated Domains Review
Configured host emits `applinks:<host>`; non-production omission and Production fail-closed behavior are correct. Stale ignored entitlements are not authority.
## 32. Android Intent Filter Review
Correct VIEW/autoVerify/categories/HTTPS/host shape, but paths are `/dashboard` and resource prefixes, not `/`.
## 33. Resource-Specific Path Conflict Review
Conflict confirmed in Mobile helper and AASA generator. Do not mutate the established registry here; add a distinct generic association scope in follow-up.
## 34. Mobile Generic Portal Entry
OS launch on `/` should run normal bootstrap. Current resolver may classify `/` unsupported, which still permits default app startup; regression coverage is required.
## 35. Default Mobile Landing Destination
Authenticated: `Main → Tabs → Dashboard` (Home). Unauthenticated: `Auth → Login`.
## 36. Unauthenticated Mobile Flow
Launch → auth hydration/login → Main default Dashboard; no pending resource intent.
## 37. Normal Web Fallback
Absent/unverified app leaves the same portal URL in the browser, using existing Web auth/landing. No special Web code.
## 38. Environment Matrix

| Configuration | Development | QA | Production |
|---|---|---|---|
| Host / same Web host | Not Required | Blocked/preferred same | Blocked/preferred same |
| iOS / Android ID | `com.legalsynq.qa` | `com.legalsynq.qa` | `com.legalsynq` |
| Apple prefix / Android SHA | N/A unless enabled | Blocked | Blocked |
| Play signing | N/A | Unresolved if Play | Unresolved |
| Scope | `/` if enabled | `/` follow-up | `/` follow-up |
| DNS/TLS/deployer | N/A | Unresolved | Unresolved |
| Status | Not Required | Blocked | Blocked |
## 39. Ownership Matrix
DNS, TLS, public hosting, association deployer, Apple/Android signing, Mobile release approval, and physical QA named owners are unresolved. Repository mechanisms belong respectively to Identity DNS, Web, Gateway/scripts, and Mobile EAS config, but mechanism is not accountable ownership.
## 40. QA Scenario Matrix
QA-001/003 installed iOS/Android open Mobile; QA-002/004 uninstalled load Web; QA-005 unauthenticated logs in then Dashboard; QA-006 invalid association stays Web; QA-007 Web uses existing auth. All physical execution is blocked by host/artifact/deployment inputs; none claimed.
## 41. Files Inspected
Required prior reports; Mobile Expo/EAS/native classification, navigation and deep-link code/tests; shared registry; Gateway source/config/docs/tooling/tests; portal/DNS/TLS/deployment references.
## 42. Files Added
None (the required report path already existed at HEAD).
## 43. Files Modified
`analysis/DL-PLAT-002-report.md` only.
## 44. Files Deleted
None.
## 45. Validation Commands and Results
- `bash scripts/tests/test-deep-link-association-tools.sh` — PASS.
- `cd apps/mobile && ./node_modules/.bin/jest --runInBand app.config.test.js` — PASS, 1 suite/16 tests.
- `python3 scripts/check-doc-sync.py` — PASS.
- `git diff --check` — PASS.
- Scoped `git grep` hard-coded-host guard — PASS; no tracked implementation match outside analysis/generated native paths.
- `git status --short` — only this report is modified.
- `rg` was unavailable; scoped `find`/`grep`/`git grep` fallbacks succeeded.
## 46. Hard-Coded Host Guard
No candidate host promoted in tracked implementation configuration; historical reports/ignored output are provenance only.
## 47. Resource-Specific Scope Guard
No new resource path introduced; existing conflicts are documented, not promoted.
## 48. Security Review
No secret/key/certificate/credential added. Public identity candidates remain blocked. Static endpoints do not change authorization.
## 49. Acceptance-Criteria Status
Complete: AC-003–006, 010, 015–021, 026–030. Partially complete: AC-008–009, 013–014. Blocked: AC-001–002, 007, 011–012, 022–025.
## 50. Issues and Failures
`rg` unavailable; fallback succeeded. Generic-root scope conflicts with current registry-derived association paths.
## 51. Blockers and External Inputs
Approve QA/Production normal portal topology (including tenant subdomains), named DNS/TLS/edge/deployment/release/QA owners, Apple prefix, QA artifact/fingerprint, Play status, and Production installed-app fingerprint.
## 52. Required Mobile Follow-Up
Emit one exact HTTPS `/` intent-filter claim, preserve fail-closed behavior/registry, and test generic launch/default auth landing without resource continuation.
## 53. Required Web Follow-Up
None for same-host Option A. A separate host would require explicitly owned edge/Web fallback, never JS/UI handoff.
## 54. Required Platform / Release Follow-Up
Approve hosts/topology/owners/identities; add generic AASA scope support; generate/deploy artifacts; verify HTTPS, content type, and no redirects.
## 55. QA Handoff
QA needs approved live hosts, matching signed builds/artifacts, devices, auth states, install states, and invalid-association control.
## 56. Out-of-Scope Confirmation
No Web UI/JS, schemes, stores, detection, resource routing, registry mutation, DNS/TLS mutation, generated native edit, or device-success claim.
## 57. Final Status
**Blocked — Production Association Inputs Required.** Host/topology and signing inputs are unresolved; generic path implementation also needs follow-up.

| Area | Status |
|---|---|
| Host, signing, owners | Blocked |
| Generic Mobile entry/Web fallback/matrices/QA plan | Done |
| AASA/Asset Links contract | Partially complete; inputs and path follow-up blocked |
| Validation | Done |
