# LegalSynq Admin Portal Discovery Report

---

## 1. Repo Structure Summary

The repository is a monorepo containing both .NET 8 backend microservices and a single Next.js 14 frontend.

```
/
├── apps/
│   ├── gateway/               # YARP reverse-proxy gateway — port 5010
│   │   └── Gateway.Api/
│   ├── services/
│   │   ├── identity/          # Auth/identity microservice — port 5001
│   │   │   ├── Identity.Api/
│   │   │   ├── Identity.Application/
│   │   │   ├── Identity.Domain/
│   │   │   └── Identity.Infrastructure/
│   │   ├── fund/              # SynqFund microservice — port 5002
│   │   │   ├── Fund.Api/ ...
│   │   └── careconnect/       # CareConnect microservice — port 5003
│   │       ├── CareConnect.Api/ ...
│   └── web/                   # ← THE OPERATOR PORTAL (Next.js 14) — port 5000
│       └── src/
│           ├── app/           # App Router — all routes live here
│           │   ├── (platform)/  # Product routes (careconnect, fund, lien)
│           │   ├── (admin)/     # ← EXISTING admin route group (nascent)
│           │   ├── api/         # BFF proxy routes + auth endpoints
│           │   ├── dashboard/
│           │   ├── login/
│           │   └── no-org/
│           ├── components/
│           │   ├── shell/       # AppShell, Sidebar, TopBar, OrgBadge, ProductSwitcher
│           │   ├── careconnect/ # Product-specific components
│           │   ├── fund/
│           │   ├── lien/
│           │   └── ui/          # (currently empty — raw Tailwind used everywhere)
│           ├── hooks/           # useSession, useTenantBranding
│           ├── lib/             # api-client, server-api-client, auth-guards, session, nav
│           ├── providers/       # SessionProvider, TenantBrandingProvider
│           └── types/           # index.ts (PlatformSession, NavGroup...), product types
├── shared/
│   ├── building-blocks/         # .NET shared lib: AuthorizationService, CapabilityCodes, Roles
│   └── contracts/               # .NET shared DTOs: ServiceResponse, HealthResponse
├── scripts/
│   └── run-dev.sh               # Startup: Next.js first on :5000, then .NET services
├── exports/                     # Architecture design documents
└── LegalSynq.sln
```

**Where the operator portal lives:** `apps/web` — the single Next.js 14 frontend that serves all platform users. It already contains a nascent `(admin)` route group at `apps/web/src/app/(admin)/` with one page (`/admin/users`) and a layout that calls `requireAdmin()`.

There is no separate "operator portal" app. `apps/web` **is** the operator portal.

---

## 2. Operator Portal Stack

| Dimension | Detail |
|---|---|
| **Framework** | Next.js 14.2 (App Router) |
| **Language** | TypeScript (strict, `tsconfig.json` with path alias `@/*` → `./src/*`) |
| **Package manager** | npm (root `package.json`; no pnpm-workspace.yaml or Turborepo) |
| **Build tooling** | Next.js built-in (`next build`), no custom Webpack config |
| **Workspace structure** | Single-app monorepo — one `package.json` at root with all deps, `apps/web` is the only JS project |
| **Routing approach** | Next.js App Router with route groups (parentheses-prefixed folders). Route groups: `(platform)` and `(admin)`. No `pages/` directory. |
| **Module organization** | By concern under `src/`: `app/`, `components/<product>/`, `components/shell/`, `hooks/`, `lib/`, `providers/`, `types/` |
| **Styling approach** | Tailwind CSS v4 (`@import "tailwindcss"` in `globals.css`, `@tailwindcss/postcss` plugin). Zero component library — raw Tailwind classes everywhere. Primary color injected as CSS variable `--color-primary` by `TenantBrandingProvider`. |
| **UI / component libraries** | None (no shadcn, Radix, MUI, etc.) — all components are hand-built with Tailwind |
| **Form handling** | Raw React `useState` — no `react-hook-form`, `formik`, or schema validation library |
| **API service approach** | Dual-client pattern (see Section 4): `serverApi` for Server Components, `apiClient` (BFF proxy) for Client Components |
| **Auth / session handling** | HttpOnly cookie `platform_session` (raw JWT). Middleware: lightweight cookie-existence gate. Real validation: `getServerSession()` calls `GET /identity/api/auth/me`. Client state: `SessionProvider` + `useSession()` hook. |
| **State management** | React Context only — `SessionContext` (session) + `TenantBrandingContext` (branding). No Zustand, Redux, or global state library. |
| **Environment / config** | `.env.local` + `process.env.*`. Key vars: `GATEWAY_URL` (server-side), `NEXT_PUBLIC_TENANT_CODE` (client-side dev override). Config file: `next.config.mjs` (must be `.mjs`, not `.ts`). |

---

## 3. Operator Portal Conventions That Must Be Reused

These are non-negotiable conventions that `/admin_portal` must follow exactly:

1. **Route group layout pattern** — Every section has its own `(group)/layout.tsx` that applies auth guards and renders `<AppShell>`. Ref: `app/(platform)/layout.tsx`, `app/(admin)/layout.tsx`.

2. **Auth guard chain** — Server Component layouts call `requireAdmin()` / `requireOrg()` / `requireSession()` from `lib/auth-guards.ts`. Never perform auth in client components.

3. **Dual API client** — Server Components call `serverApi.get(...)` (reads the `platform_session` cookie and calls the gateway directly). Client Components call `apiClient.get(...)` (calls `/api/<service>/...` → BFF proxy). Never call the gateway from a Client Component directly.

4. **BFF proxy route pattern** — Each backend service needs a catch-all proxy route at `app/api/<service>/[...path]/route.ts`. Ref: `app/api/careconnect/[...path]/route.ts`. The proxy reads the cookie and injects `Authorization: Bearer`.

5. **Product-specific API module** — Each service area gets a typed wrapper: `lib/<service>-api.ts` that exports both `<service>ServerApi` and `<service>Api`. Ref: `lib/careconnect-api.ts`, `lib/fund-api.ts`, `lib/lien-api.ts`.

6. **Product-specific types** — Each service area has its own type file in `types/<service>.ts`. Ref: `types/careconnect.ts`, `types/fund.ts`, `types/lien.ts`.

7. **Server Component pages** — All pages are `async` Server Components. Client Components are leaf components (forms, interactive tables) annotated with `'use client'`. Pages never import from `lib/api-client.ts` — only from `lib/server-api-client.ts` or the typed server wrappers.

8. **Component co-location by product** — Product-specific components live in `components/<product>/`. Shell/layout components live in `components/shell/`. Ref: `components/careconnect/`, `components/fund/`, `components/lien/`.

9. **List page pattern** — `<h1>` + action button row → optional filter strip → error banner → `<ProductListTable>` component. Ref: `(platform)/careconnect/referrals/page.tsx`.

10. **Table pattern** — Pure HTML `<table>` inside a white rounded-border container (`bg-white border border-gray-200 rounded-lg overflow-hidden`). Columns use `th` with `text-xs font-medium text-gray-500 uppercase tracking-wide`. Row hover: `hover:bg-gray-50`. "View →" link in last column. Pagination in the footer.

11. **Status badge pattern** — `<StatusBadge>` / `<UrgencyBadge>` components with color-coded pill styling. Ref: `components/careconnect/status-badge.tsx`.

12. **Navigation via `buildNavGroups`** — All sidebar navigation is computed from `lib/nav.ts:buildNavGroups(session)` based on product roles and system roles. Admin items are already included for `isPlatformAdmin | isTenantAdmin`.

13. **TypeScript path alias** — Always import with `@/` prefix (e.g. `@/lib/session`, `@/types`). Never use relative `../../../` imports.

14. **Middleware cookie gate only** — `middleware.ts` checks cookie existence only; it never decodes or inspects JWT payload. Real enforcement is in Server Components. Adding `/admin-portal/*` to the middleware matcher follows this same pattern.

15. **`clsx` for conditional classes** — `clsx(...)` is the approved utility for conditional classname construction (already installed). Ref: `components/shell/sidebar.tsx`.

---

## 4. Reusable Code and Shared Assets

### Auth helpers
- `lib/session.ts` → `getServerSession()`, `requireSession()` — usable as-is
- `lib/auth-guards.ts` → `requireAdmin()`, `requireOrg()`, `requireAuthenticated()`, `getOptionalSession()` — usable as-is; `requireAdmin()` is already the correct guard for admin-portal pages

### API layer
- `lib/server-api-client.ts` → `serverApi` — usable as-is for all server-side identity API calls
- `lib/api-client.ts` → `apiClient` + `ApiError` — usable as-is for client-side calls via BFF proxy
- `lib/careconnect-api.ts`, `lib/fund-api.ts`, `lib/lien-api.ts` — patterns to clone for `lib/admin-api.ts`

### Shared types
- `types/index.ts` → `PlatformSession`, `NavGroup`, `NavItem`, `ApiResponse`, `SystemRole`, `ProductRole`, `OrgType` — all usable as-is
- `types/careconnect.ts`, `types/fund.ts`, `types/lien.ts` — reference patterns for new `types/admin.ts`

### Shell / layout components
- `components/shell/app-shell.tsx` → `<AppShell>` — usable as-is; the admin layout already uses it
- `components/shell/sidebar.tsx` → `<Sidebar>` — usable as-is; already renders admin nav group
- `components/shell/top-bar.tsx` → `<TopBar>` — usable as-is
- `components/shell/org-badge.tsx`, `components/shell/product-switcher.tsx` — usable as-is

### Providers
- `providers/session-provider.tsx` → `<SessionProvider>` + `useSessionContext()` — usable as-is; already in root layout
- `providers/tenant-branding-provider.tsx` → `<TenantBrandingProvider>` — usable as-is; already in root layout

### Hooks
- `hooks/use-session.ts` → `useSession()`, `useRequiredSession()` — usable as-is
- `hooks/use-tenant-branding.ts` → `useTenantBranding()` — usable as-is

### Navigation
- `lib/nav.ts` → `buildNavGroups()` + `orgTypeLabel()` — the admin nav items are already defined here under the `'admin'` group; extend this for admin-portal-specific nav

### Utilities
- No dedicated utility helpers file exists yet. Date formatting is inlined in components (e.g. `formatDate()` in `referral-list-table.tsx`) — consider extracting to `lib/utils.ts` during admin portal build.

### Styling assets
- `globals.css` — `@import "tailwindcss"` with CSS variable `--color-primary`
- `tailwind.config.ts` — kept for IDE hints only; Tailwind v4 is configured via `postcss.config.js`
- Color palette: pure Tailwind gray scale + `primary` (CSS variable). Admin portal inherits both.

### Patterns to clone
- **Table pattern**: `components/careconnect/referral-list-table.tsx` — canonical table with header row, body rows, hover state, pagination footer
- **Status badge pattern**: `components/careconnect/status-badge.tsx`
- **List page pattern**: `(platform)/careconnect/referrals/page.tsx` — Server Component with filter strip, error banner, table
- **Detail page pattern**: `(platform)/careconnect/referrals/[id]/page.tsx`
- **Form pattern**: `login/login-form.tsx`, `components/careconnect/create-referral-form.tsx` — Client Component with `useState`
- **BFF proxy pattern**: `app/api/careconnect/[...path]/route.ts`

---

## 5. Recommended `/admin_portal` Placement

**Decision: Expand the existing `(admin)` route group inside `apps/web`.**

Concretely, all admin portal pages should live at `apps/web/src/app/(admin)/admin-portal/` (or under a dedicated new route group `apps/web/src/app/(admin-portal)/`), served at the URL prefix `/admin-portal/`.

**Why this is the best option:**

| Factor | In-app expansion | New separate app |
|---|---|---|
| Shared `lib/`, `types/`, `components/shell/` | Immediate, zero config | Must copy or package |
| Shared `SessionProvider` + `useSession()` | Automatic | Requires duplication |
| Shared `middleware.ts` | Add one path rule | Separate middleware |
| New workflow / port | Not needed | Needs new port + workflow |
| Shared `next.config.mjs` rewrites | Already proxies `/api/*` to gateway | Needs its own next.config |
| Auth guard reuse | `requireAdmin()` already exists | Must replicate |
| Styling / Tailwind v4 | Inherited | Must reconfigure |
| Existing `(admin)` route group | Already bootstrapped | Wasted work |

**Why a separate app is worse:** The only upside of a separate app would be independent deployability and process isolation — neither of which is a current requirement. The overhead (duplicating all shared code, a new workflow, a new port, a new build pipeline) outweighs any benefit at this stage.

**Alternative rejected: expanding `/admin` directly** — The existing `(admin)` route group currently maps to URLs like `/admin/users`. Using `/admin-portal/` as the new prefix cleanly separates platform admin management from the nascent `(admin)` tenant-admin section, avoids URL conflicts, and signals intent. The middleware already notes the admin section; adding `/admin-portal` is a minimal change.

---

## 6. Recommended `/admin_portal` Architecture

### High-level folder structure

```
apps/web/src/
├── app/
│   ├── (admin)/                         # existing — tenant admin (keep)
│   │   ├── layout.tsx                   # requireAdmin() + AppShell
│   │   └── admin/
│   │       └── users/page.tsx
│   │
│   ├── (admin-portal)/                  # NEW — LegalSynq platform admin
│   │   ├── layout.tsx                   # requirePlatformAdmin() + AdminPortalShell
│   │   └── admin-portal/
│   │       ├── page.tsx                 # Dashboard
│   │       ├── tenants/
│   │       │   ├── page.tsx             # Tenant list
│   │       │   └── [id]/
│   │       │       ├── page.tsx         # Tenant detail
│   │       │       └── users/page.tsx   # Tenant user management
│   │       ├── roles/
│   │       │   └── page.tsx             # Role / permission management
│   │       ├── products/
│   │       │   └── page.tsx             # Product entitlements
│   │       ├── audit-logs/
│   │       │   └── page.tsx
│   │       ├── support/
│   │       │   └── page.tsx
│   │       ├── settings/
│   │       │   └── page.tsx
│   │       └── monitoring/
│   │           └── page.tsx
│   │
│   └── api/
│       └── identity/                    # NEW — BFF proxy for identity service
│           └── [...path]/route.ts       # Proxies /api/identity/* → gateway → identity:5001
│
├── components/
│   ├── shell/                           # existing — reuse as-is
│   └── admin-portal/                    # NEW — admin portal specific components
│       ├── tenant-list-table.tsx
│       ├── tenant-detail-panel.tsx
│       ├── user-list-table.tsx
│       ├── role-capability-matrix.tsx
│       ├── product-entitlement-card.tsx
│       ├── audit-log-table.tsx
│       └── system-health-card.tsx
│
├── lib/
│   ├── admin-api.ts                     # NEW — typed wrapper for identity admin endpoints
│   └── admin-nav.ts                     # NEW — admin portal sidebar nav builder
│
└── types/
    └── admin.ts                         # NEW — Tenant, User, Role, Audit types
```

### Route structure

| URL | Purpose |
|---|---|
| `/admin-portal` | Dashboard — platform health summary |
| `/admin-portal/tenants` | Tenant list + search |
| `/admin-portal/tenants/[id]` | Tenant detail: products, config, status |
| `/admin-portal/tenants/[id]/users` | Users within a tenant |
| `/admin-portal/roles` | Role and capability matrix viewer |
| `/admin-portal/products` | Product entitlement management |
| `/admin-portal/audit-logs` | Platform audit log viewer |
| `/admin-portal/support` | Support tools: impersonation tokens, session reset |
| `/admin-portal/settings` | Platform-wide configuration |
| `/admin-portal/monitoring` | Service health, gateway status |

### Layout approach

A new `(admin-portal)/layout.tsx` should call `requirePlatformAdmin()` (a new guard that is stricter than `requireAdmin()` — platform admin only, not tenant admin) and render a dedicated `<AdminPortalShell>`.

`<AdminPortalShell>` should mirror `<AppShell>` in structure but use `buildAdminPortalNav(session)` (from `lib/admin-nav.ts`) instead of the product-aware `buildNavGroups()`. This prevents platform-admin-only users from seeing irrelevant product navigation.

### Shared module strategy

- Reuse: `lib/session.ts`, `lib/server-api-client.ts`, `lib/api-client.ts`, `lib/auth-guards.ts`, all `providers/`, all `hooks/`, all `components/shell/`, all `types/index.ts` — zero changes needed.
- Add `requirePlatformAdmin()` to `lib/auth-guards.ts` (stricter than `requireAdmin()` — only `isPlatformAdmin`).
- Add `lib/admin-api.ts` — typed wrapper for Identity service admin endpoints.
- Add `lib/admin-nav.ts` — sidebar nav builder for admin portal (no product groups, only admin sections).
- Add `types/admin.ts` — Tenant, TenantUser, Role, ProductEntitlement, AuditEntry types.

### Service / API strategy

The Identity service (`apps/services/identity`) already owns:
- Tenant management
- User management (within tenant)
- Role and product role management
- Capability assignments
- Organization management

All admin portal reads/writes flow through the gateway at `GET|POST|PUT|DELETE /identity/api/...`.

A new BFF proxy at `apps/web/src/app/api/identity/[...path]/route.ts` is needed for Client Component calls (the same catch-all pattern used by `api/careconnect`, `api/fund`, `api/lien`). Server Components call `serverApi` directly.

Monitoring / health data: each service exposes `GET /health` and `GET /info` — the admin portal can call these via `serverApi`.

Audit logs: no backend audit service exists yet (see Risks section).

### Auth / permission strategy

- Middleware: add `/admin-portal/` to `middleware.ts` — check for `platform_session` cookie only (same cookie as the rest of the platform).
- Layout guard: `requirePlatformAdmin()` in `(admin-portal)/layout.tsx` — new guard that redirects to `/dashboard` if the user is not a `PlatformAdmin`.
- Note: Tenant admins should NOT have access to `/admin-portal/` — they use the existing `(admin)` section at `/admin/*`.

### Feature module strategy

Each admin portal feature is an independent folder under `app/(admin-portal)/admin-portal/<feature>/`. Features are not coupled to each other. Each feature gets its own `page.tsx` (Server Component), optional `[id]/page.tsx`, and components under `components/admin-portal/`.

---

## 7. Feature Mapping for `/admin_portal`

| Feature | Admin Portal Module | Operator Portal Pattern to Mirror |
|---|---|---|
| **Dashboard** | `admin-portal/page.tsx` | `app/dashboard/page.tsx` — summary cards with counts and links to modules; redirect to first available section |
| **Tenants** | `admin-portal/tenants/` | `(platform)/careconnect/referrals/` — list page with search/filter + `TenantListTable`; detail page with multiple panels |
| **Tenant Users** | `admin-portal/tenants/[id]/users/` | `(platform)/fund/applications/` — scoped list with paged table; actions (invite, deactivate) as Client Component forms |
| **Roles & Permissions** | `admin-portal/roles/` | No existing deep parallel — closest is `components/lien/lien-status-badge.tsx` for status representation; table with capability matrix grid |
| **Product Entitlements** | `admin-portal/products/` | `(platform)/fund/applications/[id]/page.tsx` — detail panel with action buttons (enable/disable product per tenant/org) |
| **Support Tools** | `admin-portal/support/` | `(admin)/admin/users/page.tsx` — simple action page with forms; add impersonation token generator, session lookup |
| **Audit Logs** | `admin-portal/audit-logs/` | `(platform)/careconnect/referrals/page.tsx` — filterable table with date range, actor, entity type filters |
| **Platform Settings** | `admin-portal/settings/` | `login/login-form.tsx` + `fund/create-funding-application-form.tsx` — form with save button, inline validation |
| **Monitoring** | `admin-portal/monitoring/` | `app/dashboard/page.tsx` — summary card grid showing service health status fetched server-side from `/health` endpoints |

---

## 8. Risks and Unknowns

### Technical risks

1. **No identity BFF proxy exists yet.** The admin portal will need `app/api/identity/[...path]/route.ts` to proxy Client Component calls through to the identity service. Without this, Client Component forms (create user, disable tenant, etc.) have no route to reach the backend.

2. **No audit log backend.** There is no audit service, no audit event publisher, and no audit DB schema. Admin portal audit log pages will have nothing to read until an audit trail is built into the identity service (and ideally all services).

3. **No monitoring/metrics backend.** Service health is available via `/health` and `/info` endpoints, but detailed metrics (error rates, latency, queue depth) are not exposed. The monitoring page will be limited to basic up/down status until observability tooling is added.

4. **`requirePlatformAdmin()` guard does not yet exist.** `requireAdmin()` accepts both `TenantAdmin` and `PlatformAdmin`. A stricter guard is needed to ensure tenant admins cannot reach admin portal pages.

5. **Identity API admin endpoints may not be implemented.** The identity service has user CRUD (`POST /api/users`, `GET /api/users`), tenant read, and auth endpoints — but full admin endpoints (list all tenants, update product entitlements per tenant, manage role-capability assignments) may be incomplete. Each admin portal feature depends on corresponding backend endpoints existing.

6. **AppShell shows product nav for platform admins with product roles.** The existing `AppShell` + `buildNavGroups()` will show all product nav groups if the platform admin user also has product roles. The admin portal needs its own shell/nav builder to avoid confusion.

7. **No `types/admin.ts` exists.** All admin-specific types (Tenant summary/detail, Role assignment matrix, AuditEntry) need to be defined before components can be built. These types must match actual identity service API response shapes.

8. **Tailwind `ui/` component folder is empty.** There are no reusable primitive components (Button, Input, Badge, Modal, Toast). Every feature currently inlines Tailwind. As the admin portal grows (more forms, modals, notifications), this becomes a maintenance risk. Consider establishing a minimal set of shared primitives before building admin forms.

### Architectural risks

9. **Single Next.js app scaling risk.** As the admin portal grows, the single `apps/web` bundle will grow with it. This is acceptable now but should be monitored. Code-splitting via route-based lazy loading (Next.js default behavior) mitigates most of this.

10. **Cookie namespace collision.** The admin portal reuses the `platform_session` cookie. If a future requirement calls for completely separate admin sessions (different JWT audience, shorter expiry), the shared cookie model will need to be revisited.

11. **No toast/notification system.** Form submissions currently have no global feedback mechanism (no toast, no notification bus). Admin operations (tenant activation, role changes) will need a reliable notification pattern before the portal is usable.

12. **No modal pattern.** No reusable modal component exists. Admin workflows (confirm deactivate, create user inline) will need modals. These must be built from scratch.

---

## 9. Phased Build Plan

### Phase 1 — Foundation (Zero new pages, pure scaffolding)

1. Add `requirePlatformAdmin()` to `lib/auth-guards.ts`.
2. Create `app/(admin-portal)/layout.tsx` — calls `requirePlatformAdmin()`, renders `<AdminPortalShell>` (new component).
3. Create `components/shell/admin-portal-shell.tsx` — mirrors `AppShell` but uses `buildAdminPortalNav()`.
4. Create `lib/admin-nav.ts` — `buildAdminPortalNav(session)` returning admin-portal-specific `NavGroup[]`.
5. Update `middleware.ts` — add `/admin-portal/` to the `platform_session` cookie gate.
6. Create `app/api/identity/[...path]/route.ts` — BFF proxy for identity service (clone of `api/careconnect` proxy).
7. Create `types/admin.ts` — stub interfaces: `TenantSummary`, `TenantDetail`, `AdminUser`, `RoleSummary`, `AuditEntry`.
8. Create `lib/admin-api.ts` — stub typed wrappers for `identityServerApi` and `identityApi`.
9. Create `app/(admin-portal)/admin-portal/page.tsx` — minimal dashboard with placeholder cards.

_Deliverable: `/admin-portal` is navigable, auth-guarded, has its own shell and nav, all code compiles, zero TypeScript errors._

### Phase 2 — Tenant Administration

10. Implement `GET /identity/api/tenants` backend endpoint (if missing) and add type to `types/admin.ts`.
11. Build `TenantListTable` component in `components/admin-portal/`.
12. Build `app/(admin-portal)/admin-portal/tenants/page.tsx` — filterable tenant list.
13. Build `app/(admin-portal)/admin-portal/tenants/[id]/page.tsx` — tenant detail panel (products enabled, user count, status).
14. Build tenant activation/deactivation form (Client Component).

### Phase 3 — Tenant User Administration

15. Implement/verify `GET /identity/api/users?tenantId=` and `POST /identity/api/users` identity endpoints.
16. Build `UserListTable` component.
17. Build `app/(admin-portal)/admin-portal/tenants/[id]/users/page.tsx`.
18. Build invite/deactivate user forms (Client Components).

### Phase 4 — Roles & Product Entitlements

19. Build `RoleCapabilityMatrix` component (read-only grid of roles × capabilities).
20. Build `app/(admin-portal)/admin-portal/roles/page.tsx`.
21. Build `ProductEntitlementCard` component.
22. Build `app/(admin-portal)/admin-portal/products/page.tsx`.

### Phase 5 — Support Tools & Monitoring

23. Build `app/(admin-portal)/admin-portal/monitoring/page.tsx` — calls `/health` + `/info` on all four services server-side.
24. Build `app/(admin-portal)/admin-portal/support/page.tsx` — admin utilities (user lookup, token inspection).

### Phase 6 — Audit Logs & Settings

25. Design and implement audit event schema in identity service (requires backend work).
26. Build `AuditLogTable` component.
27. Build `app/(admin-portal)/admin-portal/audit-logs/page.tsx`.
28. Build `app/(admin-portal)/admin-portal/settings/page.tsx`.

### Phase 7 — Polish

29. Extract shared UI primitives: `Button`, `Input`, `Badge`, `Modal`, `Toast` to `components/ui/`.
30. Refactor admin portal components to use shared primitives.
31. Add `lucide-react` icons to admin nav (already installed).
32. Type-check pass: `tsc --noEmit` — zero errors.

---

## 10. Actionable Guidance for Step 2

**The next implementation step should be Phase 1 — Foundation scaffolding.**

Specifically, in this exact order:

1. **Add `requirePlatformAdmin()` to `lib/auth-guards.ts`** — 3 lines. Guards the entire admin portal at the layout level.
2. **Create `lib/admin-nav.ts`** — `buildAdminPortalNav()` returning the 8 admin sections as `NavGroup[]`.
3. **Create `components/shell/admin-portal-shell.tsx`** — mirrors `AppShell` but calls `buildAdminPortalNav()` instead of `buildNavGroups()`.
4. **Create `app/(admin-portal)/layout.tsx`** — `requirePlatformAdmin()` + `<AdminPortalShell>`.
5. **Create `app/api/identity/[...path]/route.ts`** — copy the careconnect proxy verbatim, change `careconnect` → `identity`.
6. **Create `types/admin.ts`** and **`lib/admin-api.ts`** stubs.
7. **Update `middleware.ts`** — add `/admin-portal` to the `platform_session` gate (it already falls through to the default case, but make it explicit).
8. **Create `app/(admin-portal)/admin-portal/page.tsx`** — minimal dashboard that renders a 3×3 grid of module cards (Tenants, Users, Roles, Products, Audit, Support, Settings, Monitoring) — all linking to their eventual routes. Each card is a `<Link>` styled as a white rounded panel.

This gives you a fully navigable, correctly auth-guarded admin portal skeleton in one focused session, before any backend API work is needed. Once Phase 1 is done, every subsequent phase can be built and tested independently.

---

## Executive Summary

The LegalSynq platform has one JavaScript application: `apps/web`, a Next.js 14 App Router project using TypeScript, Tailwind CSS v4, and a clean dual-client API pattern (server-side `serverApi` + client-side `apiClient` BFF proxy). There is no UI component library — all components are hand-built with Tailwind.

The admin portal should be built **inside `apps/web`** as a new `(admin-portal)` App Router route group, served at `/admin-portal/*`. This approach allows immediate reuse of the session, auth, API, shell, and branding infrastructure — all of which are already production-quality. A new `requirePlatformAdmin()` guard, a dedicated `AdminPortalShell` with its own nav builder, and an identity-service BFF proxy are the only structural additions required before feature development can begin.

An `(admin)` route group already exists in the repo (nascent, one page). This should be kept as the tenant-admin section (`/admin/*`). The new `(admin-portal)` route group is strictly for LegalSynq platform administrators and should be guarded by `isPlatformAdmin` only.

The largest near-term risks are backend gaps: no audit service, incomplete identity admin endpoints, and no monitoring data beyond raw `/health` pings. Backend and frontend work for Phases 5–6 must be coordinated in parallel.
