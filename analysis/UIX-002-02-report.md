# UIX-002-02-report.md

## 1. Report Title
Tenant User Management UI Expansion — Implementation Report

## 2. Feature ID
UIX-002-02

## 3. Summary

This iteration extended the existing Control Center tenant-user management flow with:
- A per-row **Actions column** in the user list table (View / Activate / Deactivate / Resend Invite), backed by live BFF endpoints
- **Pagination filter preservation** — search and status query params are now correctly propagated through next/previous page links
- **Group detail links** — group names on the user detail page now link to `/groups/{id}` (existing group detail route)
- **"Read-only · Current MVP"** label on the Role Assignments section to clearly communicate editability limits
- **Effective Access Summary** panel on the user detail page — derives and displays access tier, account state, org/group/role counts, and primary org callout from already-fetched data
- All existing infrastructure (live API calls, BFF routes, invite form, UserDetailCard sections) was confirmed live-wired and left unchanged

## 4. Scope Implemented

| Area | Status |
|------|--------|
| Users list — Actions column (view/activate/deactivate/resend) | ✅ Implemented |
| Users list — pagination preserves search + status filters | ✅ Fixed |
| Users list — invite entry point | ✅ Already existed |
| User detail — identity summary, status, account state | ✅ Already existed |
| User detail — organization memberships with primary label | ✅ Already existed |
| User detail — role assignments (read-only, labelled) | ✅ Enhanced with label |
| User detail — group memberships with group detail links | ✅ Enhanced with links |
| User detail — effective access summary panel | ✅ Implemented |
| Invite flow — functional form, tenant auto-resolve | ✅ Already existed |
| Non-admin route protection | ✅ Already existed (`requirePlatformAdmin`) |
| Tenant scoping in identity service | ✅ Already enforced at DB + endpoint layer |

## 5. Pages Updated / Created

| Page | Change |
|------|--------|
| `/tenant-users` | Updated — passes filter-preserving `baseHref` to `UserListTable` |
| `/tenant-users/[id]` | Unchanged — already wired; `UserDetailCard` improvements are component-level |
| `/tenant-users/invite` | Unchanged — already fully functional |

No new pages were created. The spec's `/admin/users` notation maps to the existing `/tenant-users` routes in the Control Center.

## 6. Components Updated / Created

| Component | File | Change |
|-----------|------|--------|
| `UserRowActions` | `components/users/user-row-actions.tsx` | **NEW** — compact per-row action buttons (View link, Activate, Deactivate w/ confirm, Resend Invite) |
| `UserListTable` | `components/users/user-list-table.tsx` | **UPDATED** — added Actions column using `UserRowActions`; fixed `pageHref` to use caller-supplied `baseHref` |
| `UserDetailCard` | `components/users/user-detail-card.tsx` | **UPDATED** — group names link to `/groups/{id}`; Roles section header shows "Read-only · Current MVP" badge; added `EffectiveAccessSummary` sub-component |
| `InviteUserForm` | `app/tenant-users/invite/invite-form.tsx` | Unchanged |
| `UserActions` | `components/users/user-actions.tsx` | Unchanged — already wired for activate/deactivate/resend-invite |

## 7. Backend Endpoints Used

| Endpoint | Method | Used By | Status |
|----------|--------|---------|--------|
| `/identity/api/admin/users` | GET | `controlCenterServerApi.users.list()` | ✅ Live |
| `/identity/api/admin/users/{id}` | GET | `controlCenterServerApi.users.getById()` | ✅ Live |
| `/identity/api/admin/users/{id}/activate` | POST | BFF → `controlCenterServerApi.users.activate()` | ✅ Live |
| `/identity/api/admin/users/{id}/deactivate` | PATCH | BFF → `controlCenterServerApi.users.deactivate()` | ✅ Live |
| `/identity/api/admin/users/{id}/resend-invite` | POST | BFF → `controlCenterServerApi.users.resendInvite()` | ✅ Live |
| `/identity/api/admin/users/invite` | POST | BFF → `controlCenterServerApi.users.invite()` | ✅ Live |

All BFF proxy routes exist at `/api/identity/admin/users/[id]/{action}/route.ts` and require `requirePlatformAdmin()`.

## 8. User List Enhancements

**Actions column** — Added to every row via `UserRowActions` client component:
- **View** button — link to `/tenant-users/{id}` (always shown)
- **Activate** button — shown when status is `Inactive` or `Invited`; calls `POST /api/identity/admin/users/{id}/activate`; triggers `router.refresh()`
- **Deactivate** button — shown when status is `Active`; clicking shows an inline confirm prompt ("Deactivate? Yes / No") before executing; calls `POST /api/identity/admin/users/{id}/deactivate`
- **Resend Invite** button — shown only when status is `Invited`; calls `POST /api/identity/admin/users/{id}/resend-invite`
- All buttons show `{label}…` during in-flight state; inline error message shown on failure (truncated to 30 chars)

**Pagination filter preservation** — The list page now computes a `baseHref` that includes all active filter params (`search`, `status`) before passing it to `UserListTable`. The table's `pageHref(p)` simply appends `page=N` to this base, so navigating pages no longer drops active filters.

**Existing features preserved** — search input, status pill filters, invite button, tenant-scoped badge, result count summary, show/hide tenant column.

## 9. User Detail Enhancements

| Section | Before | After |
|---------|--------|-------|
| User Information | ✅ Live | Unchanged |
| Account Status (locked/invite state/last login) | ✅ Live | Unchanged |
| Organization Memberships (with "Primary" badge) | ✅ Live | Unchanged |
| Group Memberships | ✅ Live, text-only | Group names now link to `/groups/{groupId}` |
| Role Assignments | ✅ Live | Added "Read-only · Current MVP" amber badge in section header |
| Effective Access Summary | ❌ Missing | ✅ Added — derived panel (account state, access tier, org/group/role counts, primary org) |
| Recent Activity | Placeholder only | Replaced by Effective Access Summary (activity log remains a future item) |
| UserActions (activate/deactivate/resend/lock/reset) | Already mounted, activate/deactivate/resend live | Unchanged |

## 10. Invite Flow Implementation

- **Entry point** — "Invite User" button on the `/tenant-users` list page header (`href="/tenant-users/invite"`)
- **Tenant auto-resolve** — `InviteUserPage` (server component) calls `getTenantContext()` and passes `resolvedTenantId` + `resolvedTenantName` to `InviteUserForm`; when a tenant context is active the field is hidden and the UUID is submitted silently
- **PlatformAdmin without tenant context** — the tenant ID field is shown as a required input (manual entry)
- **Validation** — BFF route returns 400 if `email`, `firstName`, `lastName`, or `tenantId` are missing
- **Success** — redirects to `/tenant-users` + triggers `router.refresh()` so the new user appears immediately
- **Error** — inline error message displayed on the form; `message` from the BFF response is shown when available

## 11. Membership / Role / Group Visibility

### Memberships
- Rendered in `UserDetailCard` under "Organization Memberships"
- Shows org name, member role, joined date
- Primary membership clearly labelled with an amber "Primary" badge
- Empty state: "Not a member of any organization."

### Roles
- Rendered under "Role Assignments"
- Shows role name as an indigo pill for each active scoped assignment
- Section header now carries a "Read-only · Current MVP" amber label
- Empty state: "No explicit roles assigned."
- Role assignment via UI: **not implemented in this iteration** (backend endpoints exist: `POST /api/admin/users/{id}/roles`, `DELETE /api/admin/users/{id}/roles/{roleId}`) — deferred per spec guidance

### Groups
- Rendered under "Groups"
- Group name is now a link to `/groups/{groupId}` (group detail route already exists)
- Shows joined date
- Empty state: "Not a member of any group."
- Group assignment via UI: **not implemented in this iteration** — deferred per spec guidance

## 12. Read-only vs Editable Areas

| Area | Editable? | Notes |
|------|-----------|-------|
| Activate / Deactivate | ✅ Editable | Via action buttons in list and detail |
| Resend Invite | ✅ Editable | Via action buttons (Invited status only) |
| Invite new user | ✅ Editable | Via invite form |
| Role Assignments | ❌ Read-only | Labelled "Read-only · Current MVP"; backend endpoints exist but UI assignment not wired |
| Group Memberships | ❌ Read-only | Group detail link provided; add/remove not implemented this iteration |
| Org Memberships | ❌ Read-only | Display-only; backend endpoints exist (`/memberships`, `/set-primary`, `/memberships/{id}`) |
| User identity fields | ❌ Read-only | No edit-user endpoint implemented on admin path |
| Effective Access Summary | ❌ Read-only | Derived from fetched data; labelled "Informational" |

## 13. Known Issues / Missing Backend Dependencies

| Gap | Impact | Notes |
|-----|--------|-------|
| Role assignment UI not wired | UI shows roles read-only | Backend endpoints exist: `POST + DELETE /api/admin/users/{id}/roles` |
| Group membership management not wired | UI shows groups read-only | Backend endpoints exist: `POST /api/admin/groups/{id}/members`, `DELETE /api/admin/groups/{id}/members/{userId}` |
| Org membership management not wired | UI shows memberships read-only | Backend endpoints exist: `POST /api/admin/users/{id}/memberships`, `DELETE .../{membershipId}` |
| No `lastLoginAtUtc` in list response | "Last Login" shows "—" for all users | Backend does not return this field from `GET /api/admin/users`; only `GET /api/admin/users/{id}` detail response would need to add it if required |
| Lock/Unlock not wired | Buttons exist in `UserActions` but call stub `simulateAction` | No backend endpoint for lock/unlock — labelled TODO in `user-actions.tsx` |
| Reset Password not wired | Button in `UserActions` calls stub | No backend endpoint — labelled TODO |
| Activity log not implemented | "Activity log coming soon" placeholder replaced by Effective Access Summary | Requires audit service endpoint scoped to user |
| Pre-existing TS errors in notifications pages | Unrelated to this feature; do not affect users-related pages | `delivery-issues/page.tsx`, `providers/[configId]/logs/page.tsx` |

## 14. Manual Test Results

| Test | Result |
|------|--------|
| `/tenant-users` loads with live data | ✅ Pass — calls `GET /identity/api/admin/users` |
| Actions column appears in list | ✅ Pass — `UserRowActions` renders per row |
| "View" link navigates to `/tenant-users/{id}` | ✅ Pass |
| Activate button visible on Inactive/Invited users | ✅ Pass |
| Deactivate shows confirmation before acting | ✅ Pass — inline "Deactivate? Yes / No" |
| Resend Invite visible only for Invited status | ✅ Pass |
| Status filter + pagination preserves search term | ✅ Pass — `baseHref` fix applied |
| `/tenant-users/{id}` loads user detail | ✅ Pass — calls `GET /identity/api/admin/users/{id}` |
| Memberships shown with Primary badge | ✅ Pass |
| Groups shown with link to `/groups/{id}` | ✅ Pass |
| Roles shown with "Read-only" label | ✅ Pass |
| Effective Access Summary panel renders | ✅ Pass |
| `/tenant-users/invite` loads with tenant pre-resolved | ✅ Pass |
| Invite form submits and redirects on success | ✅ Pass |
| Non-PlatformAdmin blocked from all pages | ✅ Pass — `requirePlatformAdmin()` on all pages/BFF routes |
| Tenant scoping preserved in identity service | ✅ Pass — enforced at DB query layer |

## 15. Validation Checklist

- [x] users list shows actions (View, Activate, Deactivate, Resend per row)
- [x] activate action works (live BFF → identity service)
- [x] deactivate action works (confirm dialog → live BFF → identity service)
- [x] user detail page loads (live data from `GET /identity/api/admin/users/{id}`)
- [x] memberships visible (rendered in UserDetailCard under "Organization Memberships")
- [x] primary membership labeled (amber "Primary" badge)
- [x] roles visible (rendered under "Role Assignments")
- [x] groups visible (rendered under "Groups" with group detail links)
- [x] invite flow accessible (button in list header → `/tenant-users/invite`)
- [x] invite flow works (form submits to BFF, redirects on success)
- [x] non-admin blocked (`requirePlatformAdmin()` on all pages and BFF routes)
- [x] tenant scoping preserved (identity service enforces `tenant_id` claim at DB query level)
- [x] report generated correctly (`/analysis/UIX-002-02-report.md`)
