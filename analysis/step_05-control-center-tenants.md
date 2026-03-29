# Step 05 — Control Center Standalone App: Tenants List Page

## Summary

Created `apps/control-center` as a fully independent Next.js 14 App Router application. No imports from `apps/web`. All shared logic (session, types, server API client) is duplicated cleanly inside the new app. The Tenants List page is implemented with mock data and is production-ready for backend wiring.

---

## Directory Structure Created

```
apps/control-center/
├── next-env.d.ts
├── next.config.mjs
├── package.json
├── postcss.config.js
├── tsconfig.json
└── src/
    ├── middleware.ts
    ├── types/
    │   ├── css.d.ts
    │   ├── index.ts              (PlatformSession, SystemRole, NavGroup, NavItem)
    │   └── control-center.ts    (TenantSummary, TenantDetail, PagedResponse, etc.)
    ├── lib/
    │   ├── server-api-client.ts  (ServerApiError + serverApi.get/post/put/patch/delete)
    │   ├── session.ts            (getServerSession, requireSession)
    │   ├── auth-guards.ts        (requirePlatformAdmin)
    │   ├── control-center-api.ts (controlCenterServerApi with mock tenants.list())
    │   └── nav.ts               (buildCCNav — host-root paths, no prefix)
    └── app/
        ├── globals.css
        ├── layout.tsx
        ├── page.tsx              (redirect → /tenants)
        ├── api/auth/
        │   ├── login/route.ts
        │   └── logout/route.ts
        ├── login/
        │   ├── page.tsx
        │   └── login-form.tsx    (Client Component)
        └── tenants/
            └── page.tsx
    └── components/
        ├── shell/
        │   ├── cc-shell.tsx       (Server Component shell — takes userEmail prop)
        │   ├── cc-sidebar.tsx     (Client Component — active link highlighting)
        │   └── sign-out-button.tsx (Client Component)
        └── tenants/
            └── tenant-list-table.tsx
```

---

## Key Design Decisions

### 1. No cross-imports from `apps/web`
All logic duplicated cleanly. `types/index.ts`, `lib/session.ts`, `lib/server-api-client.ts`, and `app/api/auth/*/route.ts` are clean copies adapted for the CC. In a later step, common logic can be extracted to a shared package if the team decides to formalize the monorepo boundary.

### 2. Shell as Server Component
`CCShell` is a Server Component that receives `userEmail` as a prop from the page (which already called `requirePlatformAdmin()`). This avoids the need for a `SessionProvider` context or client-side session hook. The only Client Components in the shell are `CCSidebar` (for active-link highlighting via `usePathname()`) and `SignOutButton` (for the `onClick` handler).

### 3. Auth guard — one guard, one redirect
`requirePlatformAdmin()` redirects both missing sessions and non-admins to `/login`. In a standalone app on a dedicated host, `/login` is always reachable and correct — there's no operator portal dashboard to redirect to.

### 4. Port
`apps/control-center` runs on port **5004** (`next dev -p 5004`). The main operator portal remains on port 5000.

### 5. Mock data
`MOCK_TENANTS` in `control-center-api.ts` is identical to the prototype in `apps/web` — 8 tenants covering all `TenantType` variants (LawFirm, Provider, Corporate, Government) and all `TenantStatus` values (Active, Inactive, Suspended). In-memory search filtering on `displayName`, `code`, and `primaryContactName` makes the search bar functional even against mock data.

---

## Tenants Page: `/tenants`

**File:** `src/app/tenants/page.tsx`

- `requirePlatformAdmin()` — auth guard (redirects to `/login` if not authenticated or not PlatformAdmin)
- Reads `searchParams.page` and `searchParams.search`
- Calls `controlCenterServerApi.tenants.list({ page, pageSize: 20, search })`
- Renders inside `<CCShell userEmail={session.email}>`
- Header: "Tenants" + disabled "Create Tenant" button
- Search: native `<form method="GET">` — functional with mock stub
- Error banner (red) on catch
- `<TenantListTable>` with pagination footer

---

## Tenant Table: `TenantListTable`

**File:** `src/components/tenants/tenant-list-table.tsx`

| Column | Source | Notes |
|---|---|---|
| Name | `displayName` + `code` | Code shown as secondary gray line |
| Type | `type` | `LawFirm` → `Law Firm` via `formatType()` |
| Status | `status` | Color-coded badge (green/gray/red) |
| Primary Contact | `primaryContactName` | Plain text |
| Created | `createdAtUtc` | `Mon DD, YYYY` format |
| Actions | — | `View →` → `/tenants/:id` |

- Empty state: "No tenants found."
- Row hover: `hover:bg-gray-50 transition-colors`
- Pagination footer: "Showing X–Y of Z" + Previous/Next via `?page=N`

---

## Backend Wiring Instructions (when Identity.Api is ready)

In `src/lib/control-center-api.ts`, replace `tenants.list()`:

**Before (mock stub):**
```ts
list: (params) => {
  // in-memory filter + Promise.resolve(...)
},
```

**After (live endpoint):**
```ts
list: (params = {}) =>
  serverApi.get<PagedResponse<TenantSummary>>(
    `/identity/api/admin/tenants${toQs(params as Record<string, unknown>)}`,
  ),
```

No changes needed in the page, table, or any other file.

---

## TypeScript

Zero errors confirmed (`tsc --noEmit` clean).

---

## To Run the Control Center

Add to `scripts/run-dev.sh` (or run manually):

```bash
cd apps/control-center
GATEWAY_URL=http://localhost:5010 \
NEXT_PUBLIC_ENV=development \
NEXT_PUBLIC_TENANT_CODE=LEGALSYNQ \
node /path/to/next dev -p 5004
```

Or use the package script from the repo root:
```bash
cd apps/control-center && ../../node_modules/.bin/next dev -p 5004
```

Login with: `admin@legalsynq.com` / `Admin1234!` / tenant code `LEGALSYNQ`

---

## Next Step Recommendations

### Step 06 — Tenant Detail Page (`/tenants/[id]`)
- `src/app/tenants/[id]/page.tsx`
- `src/components/tenants/tenant-detail-card.tsx`
- Stub `controlCenterServerApi.tenants.getById(id)` with mock data
- Show: name, code, type, status, contact, dates, user/org counts
- Show: product entitlements table
- Action buttons: Activate / Deactivate (client-side POST to BFF)

### Step 07 — Add CC to `run-dev.sh`
Start control-center on port 5004 alongside the other services.

### Step 08 — Identity.Api admin endpoints
Backend work that unblocks all real data on all CC pages.
