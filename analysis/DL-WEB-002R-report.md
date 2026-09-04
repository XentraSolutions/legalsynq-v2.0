# DL-WEB-002R Implementation Report

## 1. Ticket Summary

Remove the explicit Contact Details Open-in-App presentation while preserving Contact behavior and the underlying deep-link infrastructure.

## 2. Objective

Restore the Contact Actions menu to its non-deep-link structure so browser/OS association—not an explicit Contact UI action—owns app handoff.

## 3. Scope

Contact-specific Open-in-App UI, wrapper/test cleanup, focused regression coverage, validation, and this report only.

## 4. Initial Rollback Plan

1. Verify the current Contact shell, wrapper, tests, and shared DL-WEB-007 dependency.
2. Inventory every Contact-specific deep-link presentation reference before deletion.
3. Remove only the Contact menu item, wrapper, and UI-only tests.
4. Preserve and update shell regression coverage for Edit, Send Email, separator/Delete, responsiveness, and absence of Open in App.
5. Run Contact regressions, DL-WEB-001 regression, TypeScript/build/broad tests, source/boundary/whitespace checks, documentation sync, and independent review.

## 5. Current Branch and Working-Tree State

- Branch: `feat(app)-LSV3-1193-Implement-Web-to-Mobile-Deep-Link`
- HEAD: `8f5f5de9d65c806eab1c7e396e95b209b0d4b771`
- Initial working tree: clean
- This report is the first DL-WEB-002R repository change.

## 6. Repository Instruction Review

Root `AGENTS.md` and the implementation delivery workflow were reviewed. This is a narrow Web rollback: preserve user work, current route/data/auth patterns, shared infrastructure, and unrelated product surfaces; validate the closest tests and production build.

## 7. DL-WEB-002 Current Implementation Review

DL-WEB-002 is committed at `cd821bb07`. It added `components/lien/contact-detail/open-in-app-link.tsx`, its dedicated test, a shell Actions-menu item, and shell assertions/config fixtures. The wrapper now delegates to the later shared component but remains entirely Contact-specific.

## 8. DL-WEB-007 Shared Presentation Review

DL-WEB-007 is committed at current HEAD ancestry (`8f5f5de9d`). `apps/web/src/components/open-in-app-link.tsx` is still used by Application and Dashboard wrappers, so this Contact rollback must not delete or modify it. Removing the Contact wrapper removes Contact's only shared-presentation dependency.

## 9. Canonical Contact Surface Review

Verified canonical route `/lien/contacts/[id]`, which redirects to `/overview`; the persistent `ContactDetailShell` loads through `contactsService.getContact(id)`, owns loading/not-found behavior, displays `contact.id`, and provides tabs/edit/delete context. None of this changes.

## 10. Current Contact Actions Review

The shell's responsive header uses a one-column-to-two-column grid and a nested three-column stats/action grid. The Actions dropdown currently contains conditional Edit, conditional mailto Send Email, Open in App, separator, and destructive Delete. Radix primitives own keyboard/menu semantics.

## 11. Target Contact Actions Structure

Conditional Edit → conditional mailto Send Email → separator → destructive Delete. Existing permission/email conditions and the responsive grid remain byte-for-byte unchanged.

## 12. Contact Open-in-App Dependency Inventory

- `shell.tsx`: Contact wrapper import and one `DropdownMenuItem asChild` usage.
- `open-in-app-link.tsx`: Contact-only ID guard/`contactDetails` intent/shared-component adapter; no unrelated behavior or other consumers.
- `open-in-app-link.test.tsx`: entirely dedicated to removed builder/UI behavior.
- `shell.test.tsx`: base-URL setup plus two tests expecting Open in App/config-dependent behavior; these will become Contact Actions/no-action regression coverage.
- No other Contact source references `OpenInAppLink`, `buildDeepLink`, `contactDetails`, `DeepLinkError`, or `NEXT_PUBLIC_DEEP_LINK_BASE_URL`.
- Shared presentation remains used by Application and Dashboard.

## 13. Implementation Changes

- Created this report before any other DL-WEB-002R change.
- Removed the Contact shell import and Actions-menu item.
- Deleted the Contact-specific wrapper and its dedicated UI/builder test file because they had no remaining purpose or consumers.
- Reworked the existing shell tests into no-Open-in-App regressions for exact action order, mailto behavior, separator/Delete section, conditional email visibility, and independence from deep-link configuration.

## 14. Contact Action Removal

Complete. The single `DropdownMenuItem asChild` containing the Contact wrapper was removed. The surrounding conditional Edit/mailto blocks, separator, and destructive Delete block are unchanged.

## 15. Contact Wrapper Cleanup

Complete. `open-in-app-link.tsx` was Contact-specific and unused after menu removal, so it was deleted. Shared `components/open-in-app-link.tsx` remains unchanged and is still consumed by Application and Dashboard.

## 16. Contact Test Cleanup

Complete. The Contact-only wrapper test file was deleted. Shell tests no longer configure a deep-link base or expect an href/builder behavior; they now explicitly require no Open-in-App menu item while preserving business actions.

## 17. Accessibility / Responsive Review

The Radix Actions trigger/menu structure, existing semantic mailto anchor, button menu items, separator, responsive header grid, stats, and tabs are unchanged. Focused tests query actions by accessible menuitem names and the separator role.

## 18. Files Inspected

- User-provided DL-WEB-002R specification
- Root repository state
- Root instructions and implementation delivery workflow
- DL-WEB-002 and DL-WEB-007 reports/commits
- Canonical Contact layout/redirect/error pages, shell, data source, permission hooks, shared presentation, and tests
- Web package/test/type/build tooling

## 19. Files Added

- `analysis/DL-WEB-002R-report.md`

## 20. Files Modified

- `apps/web/src/components/lien/contact-detail/shell.tsx`
- `apps/web/src/components/lien/contact-detail/shell.test.tsx`

## 21. Files Deleted

- `apps/web/src/components/lien/contact-detail/open-in-app-link.tsx`
- `apps/web/src/components/lien/contact-detail/open-in-app-link.test.tsx`

## 22. Implementation Progress

| Area | Status | Completion |
|---|---|---:|
| Current Contact review | Done | 100% |
| UI removal | Done | 100% |
| Wrapper cleanup | Done | 100% |
| Actions-menu restoration | Done | 100% |
| Test cleanup | Done | 100% |
| Contact regression | Done | 100% |
| Source guards | Done | 100% |
| Type/build validation | Done | 100% |
| Boundary checks | Done | 100% |

## 23. Validation Commands and Results

- `../../node_modules/.bin/vitest run src/components/lien/contact-detail/shell.test.tsx` from `apps/web`: PASS, exit 0; Vitest 4.1.5, 1 file/2 tests. Existing Node `module.register()` deprecation warning only.
- `../../node_modules/.bin/tsx --test src/lib/__tests__/deep-links.test.ts` from `apps/web`: PASS, exit 0; 9/9 tests. Required approved local IPC access; existing Node deprecation warning only.
- `./node_modules/.bin/tsc --noEmit -p apps/web/tsconfig.json` from repository root: PASS, exit 0; no diagnostics.
- `pnpm --dir apps/web build` from repository root: PASS, exit 0; Next.js 16.2.6 compiled, type-checked, generated 29 static pages, and collected routes. Existing optional `CC_COMMON_PORTAL_HOSTNAME` and Node deprecation warnings only.
- `../../node_modules/.bin/vitest run` from `apps/web`: FAIL, exit 1; 72 files/337 tests passed and 3 files/23 unrelated existing tests failed in SynqLien funding notifications, CareConnect `PublicNetworkView` (`localStorage` unavailable), and referral-thread accessible-name expectations. No failing file imports or exercises the changed Contact shell.
- Contact source search, Application/Dashboard/shared consumer verification, scoped boundary diff, and `git diff --check`: PASS. Negative no-action assertions are the only remaining Contact `Open in App` test strings.

## 24. Contact Regression Validation

PASS: 2/2 shell regressions verify exact Edit → Send Email → Delete menuitem order, mailto target, separator/destructive action, conditional email visibility, and no Open in App.

## 25. DL-WEB-001 Regression Validation

PASS: unchanged builder/config/registry suite passed 9/9; `contactDetails` infrastructure remains supported.

## 26. Typecheck Validation

PASS: full Web TypeScript emitted no diagnostics.

## 27. Web Build Validation

PASS: production build completed successfully.

## 28. Documentation Validation

PASS: `python3 scripts/check-doc-sync.py` reported no doc-sensitive changes. This UI rollback does not change commands, ports, configuration contracts, service boundaries, or public runtime setup, so no durable README update is required.

## 29. Contact Source Guard

PASS for production Contact source: no `OpenInApp`, `Open in App`, `buildDeepLink`, `contactDetails`, `NEXT_PUBLIC_DEEP_LINK_BASE_URL`, or `DeepLinkError` remains. Shell tests intentionally retain negative `Open in App` assertions.

## 30. Boundary / Scope Validation

PASS: diff is limited to this report and Contact shell/wrapper/test files. Application, Dashboard, shared presentation, DL-WEB-001, shared registry, Mobile, Gateway, backend services, routes, data loading, permission hooks, and database are unchanged.

## 31. Acceptance-Criteria Status

| AC | Status | Evidence |
|---|---|---|
| AC-001 | Complete | Canonical shell no longer renders Open in App; focused absence assertions pass. |
| AC-002 | Complete | Unused Contact wrapper and dedicated test were deleted. |
| AC-003 | Complete | Contact production source guard finds no `buildDeepLink`. |
| AC-004 | Complete | Contact source no longer imports shared `OpenInAppLink`. |
| AC-005 | Complete | `/lien/contacts/[id]` route files are unchanged. |
| AC-006 | Complete | `contactsService.getContact(id)` loading flow is unchanged. |
| AC-007 | Complete | `contact.id` display/context/business behavior is unchanged. |
| AC-008 | Complete | Conditional Edit block remains and focused test passes. |
| AC-009 | Complete | Conditional mailto Send Email remains and both visibility states pass. |
| AC-010 | Complete | Destructive Delete remains after the separator and is tested. |
| AC-011 | Complete | `useRoleAccess`/`canEdit` logic is unchanged. |
| AC-012 | Complete | Menu is restored to Edit, Send Email, separator, Delete. |
| AC-013 | Complete | Responsive shell classes/layout are unchanged. |
| AC-014 | Complete | Radix structure and accessible action names are preserved/tested. |
| AC-015 | Complete | No app-detection logic was added. |
| AC-016 | Complete | No custom scheme was added. |
| AC-017 | Complete | No store fallback was added. |
| AC-018 | Complete | No host/fallback/config logic was added. |
| AC-019 | Complete | DL-WEB-001 files unchanged; regression passes 9/9. |
| AC-020 | Complete | Shared registry unchanged. |
| AC-021 | Complete | Shell tests now enforce the no-action requirement; UI-only tests removed. |
| AC-022 | Complete | Focused Contact tests pass 2/2. |
| AC-023 | Complete | Builder regression passes 9/9. |
| AC-024 | Complete | Full TypeScript passes. |
| AC-025 | Complete | Production build passes. |
| AC-026 | Complete | Production Contact source guard has no deep-link presentation reference. |
| AC-027 | Complete | No Application file changed. |
| AC-028 | Complete | No Dashboard file changed. |
| AC-029 | Complete | No Mobile file changed. |
| AC-030 | Complete | No Gateway/backend service file changed. |
| AC-031 | Complete | Report distinguishes automated Web validation from unperformed physical association QA. |

## 32. Issues and Failures

- Broad Vitest retains 23 unrelated failures across three pre-existing test files; 337 tests pass, including the changed Contact suite.
- Independent review approved the rollback with no critical, high, or medium findings and independently passed the focused shell regression 2/2. The reviewer noted existing Edit/Delete click and `canEdit=false` behavior is not newly exercised, but confirmed those production blocks are byte-for-byte unchanged and considered the gap non-blocking.

## 33. Blockers and External Dependencies

Association configuration and physical-device QA remain outside this rollback.

## 34. Security Review

Removing a navigation action reduces exposed UI surface and adds no auth, token, host, URL, or native behavior. Existing permission and destructive-action handling are unchanged.

## 35. Architecture Risks and Concerns

No current architecture defect identified. Shared presentation remains required by unchanged Application and Dashboard consumers; its eventual cleanup belongs to DL-WEB-007R after those rollbacks.

## 36. Known Gaps

Physical association behavior will not be claimed.

## 37. DL-WEB-003R Handoff

Application UI removal is outside this ticket.

## 38. DL-WEB-004R Handoff

Dashboard UI removal is outside this ticket.

## 39. DL-WEB-007R Handoff

Shared presentation cleanup is outside this ticket while other consumers remain.

## 40. Out-of-Scope Confirmation

Confirmed: no DL-WEB-001/shared registry, Application, Dashboard, shared presentation, Mobile, Gateway/backend, database, DNS/TLS/association/release-host, native behavior, custom scheme, store fallback, or physical-device changes.

## 41. Final Status

Complete — Contact Open-in-App UI Removed.
