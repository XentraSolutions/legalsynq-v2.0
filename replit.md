# LegalSynq — .NET 8 Microservices + Next.js Monorepo

## Overview
Bash-based monorepo for a .NET 8 microservices platform + Next.js 14 App Router frontend, plus a standalone TypeScript Docs Service. Clean layered architecture (Api / Application / Domain / Infrastructure) per bounded context. Gateway validates JWT; downstream services also validate independently.

## Environment
- **Runtime:** .NET SDK 8.0.412 (via Nix `dotnet-sdk_8`) + Node.js 22 (via Nix module)
- **System packages:** `dotnet-sdk_8`, `git`, `nodejs-22` (replit.nix)
- **Nix channel:** stable-25_05
- **Backend entry point:** `bash scripts/run-dev.sh`
- **Frontend entry point:** `cd apps/web && node /home/runner/workspace/node_modules/.bin/next dev -p 3000`

## Frontend (apps/web)
- **Framework:** Next.js 15.2.9 App Router + TypeScript + Tailwind CSS (React 18.3.1)
- **Port:** 5000 (dev)
- **Dev proxy:** `scripts/dev-proxy.js` — lightweight HTTP proxy on port 5000 that (1) gates browser requests until Next.js (on internal port 3050) returns HTTP 200 for `/login`, and (2) intercepts 5xx responses for page requests during a 30-second post-warmup window and serves an auto-refreshing loading page. Page detection uses URL pattern matching (excludes `/_next/`, `/api/`, file extensions) rather than browser headers (which Replit's proxy strips). Non-page requests (API calls, assets) get proper 503/502 during warmup. After the 30s cold-compile guard window, real 500s pass through for debugging. Auto re-gates if Next.js becomes unreachable (3+ consecutive connection errors). WebSocket upgrade passthrough for HMR. **Host header handling:** The proxy sets `Host: 127.0.0.1:PORT` when forwarding to Next.js (required because Next.js 15 production mode rejects requests with non-localhost Host headers for static assets — returns 400 Bad Request). The original host is preserved in `X-Forwarded-Host` so application code can still resolve subdomains/tenants. All BFF route handlers already read `x-forwarded-host` before `host`.
- **Error boundary:** `global-error.tsx` at app root catches any rendering errors gracefully
- **Session:** HttpOnly cookie (`platform_session`) set by BFF login route; validated via BFF `/api/auth/me` — frontend never decodes raw JWT
- **BFF Routes:** `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/logout`, `POST /api/auth/forgot-password`, `POST /api/auth/reset-password` — Next.js API routes that proxy to Identity service with Bearer auth
- **API:** All requests proxy through gateway via Next.js rewrites `/api/*` → `http://127.0.0.1:5000/*`
- **Environment:** `apps/web/.env.local` (gitignored) — `NEXT_PUBLIC_ENV=development`, `NEXT_PUBLIC_TENANT_CODE=LEGALSYNQ`, `GATEWAY_URL=http://127.0.0.1:5000`
- **IPv6 note:** All server-side `localhost` fallbacks use `127.0.0.1` because Node.js resolves `localhost` to `::1` (IPv6) first, but .NET services bind to `0.0.0.0` (IPv4 only). Using `127.0.0.1` avoids connection failures.
- **node_modules:** Installed at monorepo root (`/home/runner/workspace/node_modules`) — `apps/web` inherits via Node.js module resolution traversal

## Control Center (apps/control-center)
- **Framework:** Next.js 15.2.9 App Router + TypeScript + Tailwind CSS v4 (React 18.3.1)
- **Port:** 5004 (dev) — started by `scripts/run-dev.sh`
- **Purpose:** Internal platform administration portal for LegalSynq operators. Tenant management, cross-tenant user management, RBAC, audit logs, monitoring, notifications, CareConnect integrity, SynqAudit investigation.
- **Auth:** Requires `PlatformAdmin` system role. Cookie-based session (`platform_session`) validated via Identity service `/auth/me`.
- **API:** BFF pattern — `/api/auth/login`, `/api/auth/logout`, and `/api/identity/admin/users/[id]/set-password` are local route handlers; unmatched `/api/*` requests fall through to a `fallback` rewrite to the gateway (`CONTROL_CENTER_API_BASE` or `GATEWAY_URL`, default `http://127.0.0.1:5010`). The rewrite uses the `fallback` strategy (not a plain array) so filesystem route handlers (including dynamic `[id]` segments) are always checked first.
- **Environment:** `apps/control-center/.env.local` — `CONTROL_CENTER_API_BASE=http://127.0.0.1:5010`
- **node_modules:** Uses root monorepo `node_modules` (no local `node_modules`). Must NOT have its own `node_modules` — a local copy causes duplicate React, which triggers the `useReducer` null error on every render.
- **Key files:** `src/lib/env.ts` (centralised env access), `src/lib/session.ts` (server session), `src/lib/auth-guards.ts` (requirePlatformAdmin), `src/lib/control-center-api.ts` (API client with stubbed data), `src/middleware.ts` (route protection)
- **Dashboard (`/`):** Statistics dashboard (Server Component). Fetches tenants, users (with status-filtered count queries), monitoring health, canonical audit events, and support cases via `Promise.allSettled` for graceful degradation. Displays: system health status card, 4 KPI stat cards (tenants, users, support cases, alerts), tenant type distribution, recent support cases, recent audit events, and quick-link cards to Platform Readiness, SynqAudit, and CareConnect Integrity. Components in `src/components/dashboard/`.
- **Reports (`/reports`):** Reports service health, readiness probes, template management, and template assignment/tenant catalog. Server Component under Operations nav. API route `/api/reports/summary` probes Reports service (`/api/v1/health`, `/api/v1/ready`, `/api/v1/templates`). Components: `ReportsServiceCard` (online/degraded/offline status), `ReadinessChecksPanel` (adapter probe results), `TemplatesTable` (template list with code/name/product/version/status). Reports service also added to Monitoring health probes. Port: 5029 (Reports service). Badge: IN PROGRESS.
  - **Template Assignments (LS-REPORTS-02-001):** Templates can be assigned globally or to specific tenants. Assignment endpoints under `/api/v1/templates/{templateId}/assignments` (CRUD). Tenant catalog resolution via `GET /api/v1/tenant-templates?tenantId=&productCode=&organizationType=`. Entities: `ReportTemplateAssignment` (scope, product, org, feature/tier gates) + `ReportTemplateAssignmentTenant` (tenant targeting). Tables: `rpt_ReportTemplateAssignments`, `rpt_ReportTemplateAssignmentTenants`. Business rules: scope validity, duplicate prevention (409), published-version filtering, product/org alignment.

### Frontend Structure
```
apps/web/
  src/
    types/index.ts              ← PlatformSession, TenantBranding, OrgType, ProductRole, NavGroup
    lib/
      api-client.ts             ← apiClient + ApiError (correlationId-aware)
      session.ts                ← getServerSession() — calls /auth/me (server-side)
      auth-guards.ts            ← requireAuthenticated/Org/ProductRole/Admin (server components)
      tenant-auth-guard.ts      ← requireTenantAdmin() — redirects non-admins to /tenant/access-denied
      tenant-api.ts             ← BFF layer for tenant authorization APIs (server + client methods)
      nav.ts                    ← buildNavGroups(session) — role-driven nav derivation; PRODUCT_NAV lien section has MY TASKS, MARKETPLACE (role-gated: SynqLienSeller/Buyer/Holder), MY TOOLS, SETTINGS; filterNavByRoles() gates items by ProductRole; filterNavByAccess() combines role + mode filtering (LS-LIENS-UI-012)
      role-access/              ← Centralized role-access service (LS-LIENS-UI-012): buildRoleAccess() maps productRoles + mode → RoleAccessInfo with can(LienAction) and canViewModule(LienModule). Replaces legacy AppRole/canPerformAction from lien-store.
      bulk-operations/          ← Bulk operations framework (LS-LIENS-UI-013): executeBulk(ids, handler) processes items sequentially, returns BulkOperationResult with succeeded/failed counts. Types: BulkActionConfig, BulkOperationResult, BulkItemResult, BulkExecutor.
    providers/
      session-provider.tsx      ← SessionProvider — fetches BFF /api/auth/me client-side on mount; idle timer uses showWarningRef to avoid callback cascade; context value memoized
      tenant-branding-provider.tsx ← TenantBrandingProvider — anonymous branding fetch + CSS vars + X-Tenant-Code header
    hooks/
      use-session.ts            ← useSession() / useRequiredSession()
      use-provider-mode.ts      ← useProviderMode() — returns ProviderModeInfo & { isReady } from ProviderModeContext (org config API sourced)
      use-role-access.ts        ← useRoleAccess() — returns RoleAccessInfo (can(), canViewModule(), isSeller/isBuyer/isHolder flags); combines session productRoles + provider mode into granular action-level checks (LS-LIENS-UI-012)
      use-selection-state.ts    ← useSelectionState<T>() — generic multi-select state hook (select/deselect/toggle-all/clear); used by bulk operations (LS-LIENS-UI-013)
      use-tenant-branding.ts    ← re-exports useTenantBranding()
      use-nav-badges.ts         ← useNavBadges() — polls new referral count for Provider/CareConnectReceiver users (30s interval)
    contexts/
      settings-context.tsx        ← SettingsProvider + useSettings() — resolves AppSettings (appearance, careConnect)
      product-context.tsx         ← ProductProvider + useProduct() — infers activeProductId from pathname; context value memoized with useMemo
    config/
      app-settings.ts             ← AppSettings interface, GLOBAL_DEFAULTS, TENANT_OVERRIDES, resolveSettings()
                                     Includes CareConnectSettings.requireAvailabilityCheck (default: false)
    lib/
      product-config.ts           ← PRODUCT_DEFS array + inferProductIdFromPath() (single source of truth for product→route mapping)
    components/
      shell/
        app-shell.tsx             ← ProductProvider wrapper + TopBar + Sidebar + main content
        top-bar.tsx               ← dark top bar (bg-slate-900): branding | org context | product tabs | user menu (Jira-style)
        sidebar.tsx               ← product-filtered sidebar: shows only activeGroup nav items, product icon header
        org-badge.tsx             ← orgType label + orgName display
        product-switcher.tsx      ← SUPERSEDED — logic now inline in top-bar.tsx (kept for safety, can be deleted)
      careconnect/
        status-badge.tsx              ← StatusBadge + UrgencyBadge (colour-coded by value)
        provider-card.tsx             ← clickable provider list card
        provider-search-filters.tsx   ← filter bar (client; writes to URL params)
        provider-detail-card.tsx      ← full provider detail layout
        referral-list-table.tsx       ← paginated referral table
        referral-detail-panel.tsx     ← referral detail with sections: referral / client / notes
        create-referral-form.tsx      ← modal form; validates + POSTs via BFF proxy
        slot-picker.tsx               ← individual availability slot button (selected/unavailable states)
        availability-list.tsx         ← groups slots by calendar date; calls SlotPicker
        booking-panel.tsx             ← modal; pre-populated from referral; POST /appointments; 409 handled
        appointment-list-table.tsx    ← paginated appointment table with status badges
        appointment-timeline.tsx      ← chronological status-history timeline
        appointment-detail-panel.tsx  ← full appointment detail: slot, client, orgs, notes, timeline
      fund/
        funding-status-badge.tsx      ← colour-coded status pill (Draft/Submitted/InReview/Approved/Rejected)
        applicant-summary-card.tsx    ← inline applicant fields card
        funding-status-timeline.tsx   ← derived status history timeline (Phase 1: from updatedAtUtc)
        funding-application-list-table.tsx ← sortable table with status filter chips
        funding-application-detail-panel.tsx ← full detail layout with all funding fields
        submit-application-panel.tsx  ← SYNQFUND_REFERRER: Draft→Submitted transition form
        review-decision-panel.tsx     ← SYNQFUND_FUNDER: BeginReview / Approve / Deny actions
        create-funding-application-form.tsx ← full create form (client); saves as Draft
      lien/
        lien-status-badge.tsx         ← colour-coded pill (Draft/Offered/Sold/Withdrawn)
        lien-list-table.tsx           ← seller's lien inventory table (reusable basePath prop)
        lien-status-timeline.tsx      ← Phase 1 derived status history timeline
        lien-detail-panel.tsx         ← full detail: amounts, orgs, subject party, offers, timeline
        create-lien-form.tsx          ← SYNQLIEN_SELLER create form; confidentiality toggle + subject party
        offer-lien-panel.tsx          ← SYNQLIEN_SELLER: Draft→Offered (set ask price) + Withdraw
        marketplace-filters.tsx       ← client component; updates URL params (type/jurisdiction/min/max)
        marketplace-card.tsx          ← grid card for marketplace browse; hides confidential subject
        lien-offer-panel.tsx          ← SYNQLIEN_BUYER: submit negotiated offer
        purchase-lien-panel.tsx       ← SYNQLIEN_BUYER: two-step direct purchase at asking price
        portfolio-table.tsx           ← SYNQLIEN_BUYER/HOLDER portfolio with acquisition cost
    lib/
      server-api-client.ts       ← server-side helper: reads cookie → calls gateway as Bearer
      careconnect-api.ts         ← typed wrappers: careConnectServerApi (server) + careConnectApi (client)
      fund-api.ts                ← typed wrappers: fundServerApi (server) + fundApi (client)
      lien-api.ts                ← typed wrappers: lienServerApi (server) + lienApi (client); my-liens/marketplace/portfolio/offer/purchase/submit-offer
      lien-mock-data.ts          ← V2 prototype mock data: cases, liens, BOS, servicing, contacts, documents, users + formatCurrency/formatDate/timeAgo helpers
      cases/                     ← LS-LIENS-UI-002: layered API service pattern for Cases
        cases.types.ts           ← DTOs (CaseResponseDto, CreateCaseRequestDto, UpdateCaseRequestDto), UI models (CaseListItem, CaseDetail, CaseLienItem), PaginationMeta
        cases.api.ts             ← raw HTTP client: list, getById, getByNumber, create, update, listLiensByCase → uses apiClient
        cases.mapper.ts          ← DTO→UI model mappers: mapCaseToListItem, mapCaseToDetail, mapDtoToUpdateRequest, mapLienToListItem, mapPagination
        cases.service.ts         ← business service: getCases, getCase, createCase, updateCase, updateCaseStatus (non-destructive: re-fetches DTO), getCaseLiens
        index.ts                 ← barrel exports
      liens/                     ← LS-LIENS-UI-003: layered API service pattern for Liens (same 5-file pattern as Cases)
        liens.types.ts           ← DTOs (LienResponseDto, LienOfferResponseDto, CreateLienRequestDto, UpdateLienRequestDto, CreateLienOfferRequestDto, SaleFinalizationResultDto), UI models (LienListItem, LienDetail, LienOfferItem), PaginationMeta, LiensQuery
        liens.api.ts             ← raw HTTP client: list, getById, getByNumber, create, update, getOffers, createOffer, acceptOffer → uses apiClient
        liens.mapper.ts          ← DTO→UI model mappers: mapLienToListItem, mapLienToDetail, mapOfferToItem, mapDtoToUpdateRequest, mapPagination; inline LIEN_TYPE_LABELS
        liens.service.ts         ← business service: getLiens, getLien, createLien, updateLien, getLienOffers, createOffer, acceptOffer
        index.ts                 ← barrel exports
      servicing/                 ← LS-LIENS-UI-004: layered API service pattern for Servicing (same 5-file pattern)
        servicing.types.ts       ← DTOs (ServicingItemResponseDto, CreateServicingItemRequestDto, UpdateServicingItemRequestDto, UpdateServicingStatusRequestDto), UI models (ServicingListItem, ServicingDetail), PaginationMeta, ServicingQuery
        servicing.api.ts         ← raw HTTP client: list, getById, create, update, updateStatus → uses apiClient
        servicing.mapper.ts      ← DTO→UI model mappers: mapServicingToListItem, mapServicingToDetail, mapServicingPagination
        servicing.service.ts     ← business service: getItems, getItem, createItem, updateItem, updateStatus
        index.ts                 ← barrel exports
      documents/                 ← LS-LIENS-UI-005: layered API service for Documents (v2 shared Documents service integration)
        documents.types.ts       ← DTOs (DocumentResponseDto, DocumentListResponseDto, DocumentVersionResponseDto, IssuedTokenResponseDto, UpdateDocumentRequestDto), UI models (DocumentListItem, DocumentDetail, DocumentVersion), PaginationMeta, DocumentsQuery, UploadDocumentParams
        documents.api.ts         ← raw HTTP client: list, getById, upload (FormData/multipart), update, delete, requestViewUrl, requestDownloadUrl, listVersions → uses apiClient + raw fetch for uploads
        documents.mapper.ts      ← DTO→UI model mappers: mapDocumentToListItem, mapDocumentToDetail, mapDocumentVersion, mapDocumentPagination; formatFileSize helper
        documents.service.ts     ← business service: list, getById, upload, update, delete, getViewUrl, getDownloadUrl, listVersions
        index.ts                 ← barrel exports
      notifications/               ← LS-LIENS-UI-009: notification service layer for SynqLiens shell (bell icon + dashboard activity)
        notifications.types.ts     ← DTOs (NotifSummaryDto, NotifStatsDto, NotifListResponseDto, NotifStatsResponseDto), UI models (NotificationItem, NotificationStats, NotificationListResult), NotificationQuery
        notifications.api.ts       ← HTTP client calling BFF routes at /api/notifications/{list,stats} with credentials
        notifications.mapper.ts    ← DTO→UI model mappers: mapNotificationItem (parses recipientJson, metadataJson), mapNotificationStats
        notifications.service.ts   ← business service: getNotifications, getRecentNotifications, getStats, getFailedCount
        index.ts                   ← barrel exports
      provider-mode/               ← LS-LIENS-UI-011/011-01: sell vs manage mode from org config API (backed by DB + JWT claim)
        provider-mode.types.ts     ← ProviderMode, OrgConfigResponseDto, ProviderModeInfo
        provider-mode.api.ts       ← fetchOrgConfig() — calls BFF /api/org-config
        provider-mode.service.ts   ← resolveProviderMode(), getDefaultModeInfo(), isSellMode(), isManageMode()
        index.ts                   ← barrel export
      unified-activity/            ← LS-LIENS-UI-010: unified activity feed merging audit + notification events
        unified-activity.types.ts  ← UnifiedActivityItem, AuditSourceDetail, NotificationSourceDetail, ActivitySource, ActivityEntityRef, ActivityActorRef, UnifiedActivityQuery/Result
        unified-activity.api.ts    ← delegates to auditApi.getEvents() + notificationsApi.list() — no direct HTTP
        unified-activity.mapper.ts ← mapAuditToUnified, mapNotificationToUnified, getEntityHref (entity→route), getNotificationHref
        unified-activity.service.ts ← getUnifiedActivity (merge+sort), getRecentUnifiedActivity, getUnifiedActivityBySource — resilient (partial results if one source fails)
        index.ts                   ← barrel exports
    stores/
      lien-store.ts              ← Zustand store: full CRUD for all 7 entities, role simulation, toast state, activity log, case notes, canPerformAction() helper
    app/api/
      careconnect/[...path]/route.ts ← BFF catch-all proxy for CareConnect client calls
      fund/[...path]/route.ts        ← BFF catch-all proxy for Fund client calls
      lien/[...path]/route.ts        ← BFF catch-all proxy for SynqLien client calls (fixed: /liens/ prefix matches gateway YARP route)
      notifications/list/route.ts    ← BFF proxy: resolves tenantId from session → injects X-Tenant-Id → proxies GET /v1/notifications
      notifications/stats/route.ts   ← BFF proxy: resolves tenantId from session → injects X-Tenant-Id → proxies GET /v1/notifications/stats
    types/
      careconnect.ts             ← ProviderSummary/Detail, ReferralSummary/Detail, CreateReferralRequest, PagedResponse
      fund.ts                    ← FundingApplicationSummary/Detail, Create/Submit/Approve/DenyRequest, ApplicationStatus
      lien.ts                    ← LienSummary/Detail, CreateLienRequest, OfferLienRequest, SubmitLienOfferRequest, PurchaseLienRequest, LienStatus, LIEN_TYPE_LABELS
    app/
      layout.tsx                ← root layout: TenantBrandingProvider → SessionProvider
      page.tsx                  ← redirect → /dashboard
      login/page.tsx            ← branded login; tenantCode input in dev only
      login/login-form.tsx      ← login form; POSTs to BFF /api/auth/login
      dashboard/page.tsx        ← redirects to first available product route
      no-org/page.tsx           ← shown when user has no org membership
      api/
        auth/{login,logout,me}/route.ts  ← BFF auth routes
        careconnect/[...path]/route.ts   ← catch-all BFF proxy for CareConnect client-side calls
      (platform)/               ← route group: requireOrg() guard + AppShell
        layout.tsx
        careconnect/
          providers/page.tsx                        ← provider search (CARECONNECT_REFERRER only)
          providers/[id]/page.tsx                   ← provider detail + Create Referral modal (Client Component)
          providers/[id]/availability/page.tsx      ← availability calendar; date-range picker; BookingPanel modal; ?referralId= context (Client Component)
          referrals/page.tsx                        ← referral list (both roles; UX label adapts)
          referrals/[id]/page.tsx                   ← referral detail + "Book Appointment" link for referrers
          appointments/page.tsx                     ← appointment list (both roles; UX label adapts; status filter chips)
          appointments/[id]/page.tsx                ← appointment detail; back-links to referral; Phase-2 status actions placeholder
        fund/applications/page.tsx
        lien/marketplace/page.tsx
        lien/layout.tsx                           ← LienProviders wrapper (ToastContainer + RoleSwitcher)
        lien/dashboard/page.tsx                   ← V2 UX: store-wired dashboard, KPI cards, task queue, activity feed, donut charts, Create Case modal
        lien/task-manager/page.tsx                ← V2 UX: Kanban board + list view, KPI cards (pending/in-progress/escalated/overdue), board/list toggle, filter by priority/assignee, quick status actions
        lien/cases/page.tsx                       ← LS-LIENS-UI-002: API-backed list via casesService, loading/error/pagination, Create Case modal, ActionMenu (advance status via API), SideDrawer preview
        lien/cases/[id]/page.tsx                  ← LS-LIENS-UI-002: API-backed detail via casesService, StatusProgress workflow, related liens from API, advance status via API, NotesPanel (read-only until backend supports)
        lien/liens/page.tsx                       ← V2 UX: store-backed list, Create Lien modal, ActionMenu (list/withdraw), SideDrawer, multi-filter
        lien/liens/[id]/page.tsx                  ← V2 UX: lien lifecycle StatusProgress, Submit/Accept/Reject Offer workflow, FormModal, ConfirmDialog
        lien/bill-of-sales/page.tsx               ← V2 UX: store-backed KPI cards, ActionMenu (submit/execute/cancel), ConfirmDialog
        lien/bill-of-sales/[id]/page.tsx          ← V2 UX: BOS workflow StatusProgress, submit/execute/cancel with confirm
        lien/servicing/page.tsx                   ← V2 UX: AssignTaskForm, ActionMenu (start/complete/escalate/reassign), ConfirmDialog
        lien/servicing/[id]/page.tsx              ← V2 UX: task progress StatusProgress, start/complete/escalate/reassign actions
        lien/contacts/page.tsx                    ← V2 UX: AddContactForm, SideDrawer preview, ActionMenu with email
        lien/contacts/[id]/page.tsx               ← V2 UX: store-backed detail, related cases from store, edit/email actions
        lien/batch-entry/page.tsx                 ← V2 prototype: 4-step bulk import wizard
        lien/document-handling/page.tsx            ← LS-LIENS-UI-005: real API-backed document list, FilterToolbar (status), async list via documentsService, download via opaque tokens, archive via PATCH
        lien/document-handling/[id]/page.tsx       ← LS-LIENS-UI-005: real API-backed document detail, preview/download via opaque tokens, version history table, scan threat display
        lien/user-management/page.tsx              ← V2 UX: AddUserForm, ActionMenu (activate/deactivate/unlock), admin-only, ConfirmDialog
        lien/user-management/[id]/page.tsx         ← V2 UX: store-backed detail, activate/deactivate/unlock actions
      tenant/
        access-denied/page.tsx                    ← access denied page for non-admin users
        authorization/
          layout.tsx                              ← requireTenantAdmin() guard + header + AuthorizationNav tabs
          users/page.tsx                          ← LS-TENANT-002: user list with search/filter/pagination
          users/AuthUserTable.tsx                 ← client table: search, status filter, row click → detail
          users/[userId]/page.tsx                 ← LS-TENANT-002: user detail (identity, products, roles, groups, effective access)
          users/[userId]/UserDetailClient.tsx     ← client detail: assign/revoke products/roles/groups, access-debug, simulator link
          groups/page.tsx                         ← LS-TENANT-003: group list with search/filter/pagination/create
          groups/GroupTable.tsx                    ← client table: search, status filter, create modal, row click → detail
          groups/[groupId]/page.tsx               ← LS-TENANT-003: group detail (summary, members, products, roles, access preview)
          groups/[groupId]/GroupDetailClient.tsx   ← client detail: edit/archive, member picker, product/role assign/revoke, effective access
          access/page.tsx                         ← LS-TENANT-004: Access & Explainability (overview, user explorer, permissions, search)
          access/AccessExplainabilityClient.tsx     ← client: 4-tab access dashboard (overview widgets, user explorer w/ lazy access-debug, permission drilldown, global search)
          simulator/page.tsx                      ← LS-TENANT-005: Authorization Simulator (server prefetch users + permissions)
          simulator/SimulatorClient.tsx             ← client: split-panel simulator (user/perm select, context editors, policy result UI)
      (admin)/                  ← route group: requireAdmin() guard + AppShell
        layout.tsx
        admin/users/page.tsx
      portal/                   ← injured party portal (separate session shape — Phase 2)
        login/page.tsx
        my-application/page.tsx
    forgot-password/page.tsx    ← forgot password page (email input → reset link)
    forgot-password/forgot-password-form.tsx ← forgot password form component
    reset-password/page.tsx     ← set new password page (token from URL)
    reset-password/reset-password-form.tsx   ← reset password form component
    middleware.ts               ← global cookie gate (platform_session / portal_session)
```

### Navigation Rules
- `CARECONNECT_REFERRER` → CareConnect group (Referrals, Appointments, Find Providers)
- `CARECONNECT_RECEIVER` → CareConnect group (Referrals, Appointments)
- `SYNQFUND_REFERRER`    → SynqFund group (Applications, New Application)
- `SYNQFUND_FUNDER`      → SynqFund group (Applications)
- `SYNQLIEN_SELLER`      → SynqLien group (My Liens)
- `SYNQLIEN_BUYER`       → SynqLien group (Marketplace, Portfolio)
- `SYNQLIEN_HOLDER`      → SynqLien group (Portfolio)
- `TenantAdmin`          → + Administration group (Users, Organizations, Products)
- `PlatformAdmin`        → + Administration group (+ All Tenants)

## Project Structure

```
LegalSynq.sln
scripts/
  run-dev.sh                              ← build + start all services in parallel
apps/
  gateway/
    Gateway.Api/                          → YARP reverse proxy (port 5000)
      Program.cs                          ← JWT validation + YARP routing
      appsettings.json                    ← YARP routes/clusters + JWT config
  services/
    identity/
      Identity.Api/                       → ASP.NET Core Web API (port 5001)
        Endpoints/
          UserEndpoints.cs                ← POST/GET /api/users
          AuthEndpoints.cs                ← POST /api/auth/login (anon), GET /api/auth/me (Bearer), POST /api/auth/logout (anon)
          TenantBrandingEndpoints.cs      ← GET /api/tenants/current/branding (anon; X-Tenant-Code > Host header)
        DesignTimeDbContextFactory.cs
        appsettings.json                  ← port 5001 + ConnectionStrings:IdentityDb
        appsettings.Development.json      ← dev JWT signing key + debug logging
      Identity.Application/
        Services/UserService.cs
        Services/AuthService.cs
      Identity.Domain/                    → Tenant, User, Role, UserRole, Product, TenantProduct
                                            Organization, OrganizationDomain, OrganizationProduct
                                            ProductRole, Permission, RolePermissionMapping, RolePermissionAssignment (LS-COR-AUT-010A)
                                            UserOrganizationMembership, ScopedRoleAssignment
                                            TenantProductEntitlement, UserProductAccess, UserRoleAssignment (LS-COR-AUT-002)
                                            AccessGroup, AccessGroupMembership, GroupProductAccess, GroupRoleAssignment (LS-COR-AUT-004)
                                            EntitlementStatus, AccessStatus, AssignmentStatus, GroupStatus, GroupScopeType, MembershipStatus enums
      Identity.Application/
        Interfaces/IAuditPublisher.cs     ← audit event wrapper interface
        Interfaces/ITenantProductEntitlementService.cs
        Interfaces/IUserProductAccessService.cs
        Interfaces/IUserRoleAssignmentService.cs
        Interfaces/IAccessSourceQueryService.cs  ← combined snapshot query
        Interfaces/IEffectiveAccessService.cs    ← LS-COR-AUT-003 effective access computation (+ source attribution)
        Interfaces/IGroupService.cs              ← LS-COR-AUT-004 group CRUD
        Interfaces/IGroupMembershipService.cs    ← LS-COR-AUT-004 membership management
        Interfaces/IGroupProductAccessService.cs ← LS-COR-AUT-004 group product access
        Interfaces/IGroupRoleAssignmentService.cs ← LS-COR-AUT-004 group role assignments
      Identity.Infrastructure/
        Data/IdentityDbContext.cs         ← 21 DbSets (+3 LS-COR-AUT-002 + 4 LS-COR-AUT-004)
        Data/Configurations/              ← IEntityTypeConfiguration<T> per entity (22 configs)
        Auth/PermissionService.cs         ← IPermissionService impl, 5-min IMemoryCache TTL
        Persistence/Migrations/           ← InitialIdentitySchema
                                            AddMultiOrgProductRoleModel (8 tables + seed)
                                            SeedAdminOrgMembership
                                            AddTenantDomains (TenantDomains table)
                                            SeedTenantDomains (legalsynq.legalsynq.com)
                                            CorrectSynqLienRoleMappings (SELLER→PROVIDER)
                                            DropStaleApplicationsTable (identity_db cleanup)
                                            AddAccessVersion (LS-COR-AUT-003: Users.AccessVersion + unique index fix)
                                            AddAccessGroups (LS-COR-AUT-004: 4 group tables + indexes)
        Services/JwtTokenService.cs       ← emits org_id, org_type, product_roles, product_codes, access_version JWT claims
        Services/ProductProvisioningService.cs ← centralized product provisioning engine
        Services/CareConnectProvisioningHandler.cs ← CareConnect-specific provisioning hook
        Services/AuditPublisher.cs        ← IAuditPublisher impl (wraps IAuditEventClient)
        Services/EffectiveAccessService.cs       ← LS-COR-AUT-003/004: computes effective access from direct + inherited (group) sources
        Services/TenantProductEntitlementService.cs  ← LS-COR-AUT-002 service (+ AccessVersion bump)
        Services/UserProductAccessService.cs         ← LS-COR-AUT-002 service (+ AccessVersion bump)
        Services/UserRoleAssignmentService.cs        ← LS-COR-AUT-002 service (+ AccessVersion bump)
        Services/AccessSourceQueryService.cs         ← LS-COR-AUT-002 snapshot query
        Services/GroupService.cs                     ← LS-COR-AUT-004: group CRUD + archive w/ AccessVersion bump
        Services/GroupMembershipService.cs            ← LS-COR-AUT-004: member add/remove w/ AccessVersion bump
        Services/GroupProductAccessService.cs         ← LS-COR-AUT-004: group product grant/revoke w/ AccessVersion bump
        Services/GroupRoleAssignmentService.cs        ← LS-COR-AUT-004: group role assign/remove w/ AccessVersion bump
        DependencyInjection.cs
    fund/
      Fund.Api/                           → ASP.NET Core Web API (port 5002)
        Endpoints/
          ApplicationEndpoints.cs         ← POST/GET /api/applications
        DesignTimeDbContextFactory.cs
        appsettings.json                  ← port 5002 + ConnectionStrings:FundDb
        appsettings.Development.json      ← dev JWT signing key + debug logging
      Fund.Application/
        DTOs/CreateApplicationRequest.cs
        DTOs/ApplicationResponse.cs
        Interfaces/IApplicationService.cs
        Services/ApplicationService.cs
        IApplicationRepository.cs
      Fund.Domain/
        Application.cs                    ← Application entity (factory method)
      Fund.Infrastructure/
        Data/FundDbContext.cs
        Data/Configurations/ApplicationConfiguration.cs
        Data/Migrations/                  ← InitialFundSchema
        Repositories/ApplicationRepository.cs
        DependencyInjection.cs
    liens/
      Liens.Api/                          → ASP.NET Core Web API (port 5009)
        Endpoints/
          LienEndpoints.cs               ← real database-backed Lien CRUD endpoints (GET list/by-id/by-number, POST, PUT)
          CaseEndpoints.cs               ← real database-backed Case CRUD endpoints (GET list/by-id/by-number, POST, PUT)
        Middleware/ExceptionHandlingMiddleware.cs ← handles ValidationException→400, NotFoundException→404, ConflictException→409, InvalidOperationException→409, UnauthorizedAccessException→401
        appsettings.json                  ← port 5009 + ConnectionStrings:LiensDb (placeholder)
        appsettings.Development.json      ← dev JWT signing key + debug logging
      Liens.Application/
        DTOs/                             ← LienResponse, CreateLienRequest, UpdateLienRequest, CaseResponse, CreateCaseRequest, UpdateCaseRequest, PaginatedResult<T>
        Interfaces/                       ← ILienService, ICaseService, ILienSaleService, IUnitOfWork, ITransactionScope
        Services/                         ← LienService, CaseService, LienSaleService
      Liens.Domain/
        Entities/                         ← Lien, LienOffer, Case, Contact, Facility, LookupValue, BillOfSale
        LiensPermissions.cs              ← static permission code constants (LienRead/Create/Update/Offer/ReadOwn/Browse/Purchase/ReadHeld/Service/Settle + CaseRead/Create/Update)
      Liens.Infrastructure/
        DependencyInjection.cs            ← AddLiensServices() extension (repos, services, UnitOfWork, ICurrentRequestContext)
        Persistence/                      ← LiensDbContext, UnitOfWork, migrations
        Repositories/                     ← LienRepository, CaseRepository, FacilityRepository, ContactRepository, etc.
    careconnect/
      CareConnect.Api/                    → ASP.NET Core Web API (port 5003)
        Endpoints/
          ProviderEndpoints.cs            ← GET/POST/PUT /api/providers
          ReferralEndpoints.cs            ← GET/POST/PUT /api/referrals
          CategoryEndpoints.cs            ← GET /api/categories
        Middleware/ExceptionHandlingMiddleware.cs
        DesignTimeDbContextFactory.cs
        appsettings.json                  ← port 5003 + ConnectionStrings:CareConnectDb
        appsettings.Development.json      ← dev JWT signing key + debug logging
      CareConnect.Application/
        DTOs/                             ← CreateProviderRequest, UpdateProviderRequest, ProviderResponse
                                             CreateReferralRequest, UpdateReferralRequest, ReferralResponse
                                             CategoryResponse
        Interfaces/IProviderService.cs, IReferralService.cs, ICategoryService.cs
        Repositories/IProviderRepository.cs, IReferralRepository.cs, ICategoryRepository.cs
        Services/ProviderService.cs, ReferralService.cs, CategoryService.cs
      CareConnect.Domain/
        Provider.cs                       ← Provider entity (AuditableEntity)
        Category.cs                       ← Category entity (seeded)
        ProviderCategory.cs               ← join table entity
        Referral.cs                       ← Referral entity (ValidStatuses, ValidUrgencies)
        ReferralStatusHistory.cs          ← Referral lifecycle history
        AppointmentSlot.cs                ← Slot with Reserve/Release/Block methods
        SlotStatus.cs                     ← Open, Blocked, Closed constants
        Appointment.cs                    ← Appointment with UpdateStatus/Reschedule/Cancel
        AppointmentStatus.cs              ← Scheduled, Confirmed, Completed, Cancelled, NoShow
        AppointmentStatusHistory.cs       ← Appointment lifecycle history
        AppointmentWorkflowRules.cs       ← Transition table + terminal/reschedulable guards
        ProviderAvailabilityTemplate.cs   ← Recurring schedule template
        ProviderAvailabilityException.cs  ← Blackout/exception entity (AuditableEntity)
        ExceptionType.cs                  ← Unavailable, Holiday, Vacation, Blocked constants
      CareConnect.Infrastructure/
        Data/CareConnectDbContext.cs
        Data/Configurations/              ← ProviderConfiguration, CategoryConfiguration,
                                             ProviderCategoryConfiguration, ReferralConfiguration
        Data/Migrations/                  ← InitialCareConnectSchema
        Repositories/ProviderRepository.cs, ReferralRepository.cs, CategoryRepository.cs
        DependencyInjection.cs
    notifications/
      Notifications.Api/                     → ASP.NET Core Web API (port 5006)
        Program.cs                           ← Minimal API; no auth (multi-tenant via X-Tenant-Id header)
        Middleware/
          TenantMiddleware.cs                ← extracts X-Tenant-Id header → HttpContext.Items
          InternalTokenMiddleware.cs          ← validates X-Internal-Service-Token for /internal routes
          RawBodyMiddleware.cs               ← captures raw body for /v1/webhooks signature verification
        Endpoints/
          NotificationEndpoints.cs           ← POST/GET /v1/notifications
          TemplateEndpoints.cs               ← CRUD /v1/templates + /v1/templates/global
          ProviderEndpoints.cs               ← CRUD /v1/providers/configs + channel-settings
          WebhookEndpoints.cs                ← POST /v1/webhooks/sendgrid, /v1/webhooks/twilio
          BillingEndpoints.cs                ← GET /v1/billing/plan, /plans, /rates, /rate-limits
          ContactEndpoints.cs                ← CRUD /v1/contacts/suppressions + health
          BrandingEndpoints.cs               ← CRUD /v1/branding + resolved
          InternalEndpoints.cs               ← POST /internal/send-email
          HealthEndpoints.cs                 ← GET /health, /info
        appsettings.json
      Notifications.Application/
        DTOs/                                ← NotificationDtos, TemplateDtos, ProviderDtos, BillingDtos, ContactDtos, InternalDtos
        Interfaces/                          ← 15+ repository + 10+ service interfaces
      Notifications.Domain/                  → 18 entities + comprehensive Enums.cs
      Notifications.Infrastructure/
        Data/NotificationsDbContext.cs        ← 18 DbSets, all entity configurations
        Data/SchemaRenamer.cs                ← Startup migration: renames tables (ntf_snake_case → ntf_PascalCase), columns (snake_case → PascalCase), indexes (idx_/uq_ → IX_/UX_)
        Data/Configurations/                 ← 18 IEntityTypeConfiguration per entity (ntf_PascalCase tables, no HasColumnName, IX_/UX_ indexes)
        Repositories/                        ← All repository implementations
        Providers/Adapters/
          SendGridAdapter.cs                 ← HTTP-based SendGrid v3 mail/send
          TwilioAdapter.cs                   ← HTTP-based Twilio Messages API
          SmtpAdapter.cs                     ← MailKit-based SMTP adapter
        Webhooks/
          Verifiers/SendGridVerifier.cs      ← ECDSA P-256+SHA256 verification
          Verifiers/TwilioVerifier.cs        ← HMAC-SHA1 verification
          Normalizers/SendGridNormalizer.cs   ← Raw event → normalized event type
          Normalizers/TwilioNormalizer.cs     ← Form params → normalized event type
        Services/                            ← NotificationService, TemplateService, DeliveryStatusService,
                                                ContactEnforcementService, UsageEvaluationService,
                                                UsageMeteringService, ProviderRoutingService,
                                                WebhookIngestionService, BrandingResolutionService, etc.
        Workers/
          NotificationWorker.cs              ← BackgroundService (queue processing placeholder)
          ProviderHealthWorker.cs            ← BackgroundService (platform provider health checks, 2min interval)
          StatusSyncWorker.cs                ← BackgroundService (delivery status sync, 5min interval)
        DependencyInjection.cs               ← AddInfrastructure() extension method
shared/
  contracts/
    Contracts/                            → HealthResponse, InfoResponse, ServiceResponse<T>
  building-blocks/
    BuildingBlocks/
      Authorization/
        Roles.cs                          ← PlatformAdmin, TenantAdmin, StandardUser constants
        Policies.cs                       ← AuthenticatedUser, AdminOnly, PlatformOrTenantAdmin constants
      Context/
        ICurrentRequestContext.cs         ← interface: UserId, TenantId, TenantCode, Email, Roles, IsAuthenticated
        CurrentRequestContext.cs          ← reads claims from IHttpContextAccessor
      Domain/
        AuditableEntity.cs               ← base class: CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId
      ServiceBase.cs
```

## Key Packages

| Project | Package | Version |
|---|---|---|
| Identity.Infrastructure | Pomelo.EntityFrameworkCore.MySql | 8.0.0 |
| Identity.Infrastructure | Microsoft.EntityFrameworkCore.Design | 8.0.0 |
| Identity.Api | Microsoft.EntityFrameworkCore.Design | 8.0.0 |
| Fund.Infrastructure | Pomelo.EntityFrameworkCore.MySql | 8.0.2 |
| Fund.Infrastructure | Microsoft.EntityFrameworkCore.Design | 8.0.8 |
| Fund.Api | Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.8 |
| Fund.Api | Microsoft.EntityFrameworkCore.Design | 8.0.8 |
| Gateway.Api | Yarp.ReverseProxy | 2.2.0 |
| Gateway.Api | Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.x |

## Secrets

| Secret | Used by | Notes |
|---|---|---|
| `ConnectionStrings__IdentityDb` | Identity.Api | MySQL, identity_db |
| `ConnectionStrings__FundDb` | Fund.Api | MySQL, fund_db |
| `ConnectionStrings__CareConnectDb` | CareConnect.Api | MySQL, careconnect_db |
| `SENDGRID_API_KEY` | Notifications service | SendGrid API key for transactional email |
| `SENDGRID_DEFAULT_FROM_EMAIL` | Notifications service | Verified sender email address |
| `Route53__HostedZoneId` | Identity.Api | AWS Route53 hosted zone ID for tenant subdomains |
| `Route53__BaseDomain` | Identity.Api | Base domain for subdomains (default: legalsynq.com) |
| `Route53__RecordValue` | Identity.Api | CNAME target for tenant subdomains |
| `Route53__AccessKeyId` | Identity.Api | AWS access key (optional; falls back to instance role) |
| `Route53__SecretAccessKey` | Identity.Api | AWS secret key (optional; falls back to instance role) |
| `ConnectionStrings__AuditEventDb` | Audit Service | MySQL connection string for audit_db on RDS. `MigrateOnStartup=true` in both dev and prod. 3 migrations (InitialSchema, LegalHolds/Outbox, AddTablePrefixes). |
| `ConnectionStrings__DocsDb` | Documents.Api | MySQL, documents_db on RDS |
| `ConnectionStrings__LiensDb` | Liens.Api | MySQL, liens_db on RDS |
| `NOTIF_DB_PASSWORD` | Notifications.Api | MySQL password for notifications_db (host/port/name/user via shared env vars) |
| `ConnectionStrings__ReportsDb` | Reports.Api | MySQL, reports_db on RDS |

## Database (AWS RDS MySQL)
- **Host:** `legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com` (MySQL 8.0, us-east-2)
- **All services connected:** Identity, Fund, CareConnect, Liens, Audit, Notifications, Documents, Reports
- **Auto-migration:** All services run `Database.Migrate()` or `MigrateOnStartup` on startup — no manual migration steps needed
- **Databases:** `identity_db`, `fund_db`, `careconnect_db`, `documents_db`, `liens_db`, `notifications_db`, `reports_db`, `audit_db`, `audit_event_db`
- **Notifications uses individual env vars:** `NOTIF_DB_HOST`, `NOTIF_DB_PORT`, `NOTIF_DB_NAME`, `NOTIF_DB_USER` (shared), `NOTIF_DB_PASSWORD` (secret)
- **Audit service config:** `Database:Provider=MySQL` in both `appsettings.json` and `appsettings.Development.json`, reads connection via `ConnectionStrings:AuditEventDb`
- **Setup utility:** `scripts/DbSetup/` — `dotnet run` checks/creates databases; `dotnet run -- reset-audit` drops and recreates audit schema

## JWT

- **Issuer:** `legalsynq-identity`
- **Audience:** `legalsynq-platform`
- **Dev signing key:** `dev-only-signing-key-minimum-32-chars-long!` (in both Identity and Fund `appsettings.Development.json`)
- **Claims:** `sub` (userId), `email`, `jti`, `tenant_id`, `tenant_code`, `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` (ClaimTypes.Role)
- **`MapInboundClaims = false`** in Fund.Api and Gateway so claim names are literal
- **`RoleClaimType = ClaimTypes.Role`** set in Fund.Api `TokenValidationParameters` so `RequireRole()` policies resolve correctly

## Gateway Routes (YARP)

| Route | Auth | Upstream |
|---|---|---|
| `/identity/api/auth/**` | Anonymous | Identity :5001 |
| `/identity/health` | Anonymous | Identity :5001 |
| `/identity/info` | Anonymous | Identity :5001 |
| `/identity/**` | Bearer JWT required | Identity :5001 |
| `/fund/health` | Anonymous | Fund :5002 |
| `/fund/info` | Anonymous | Fund :5002 |
| `/fund/**` | Bearer JWT required | Fund :5002 |
| `/careconnect/health` | Anonymous | CareConnect :5003 |
| `/careconnect/info` | Anonymous | CareConnect :5003 |
| `/careconnect/**` | Bearer JWT required | CareConnect :5003 |
| `/audit-service/health` | Anonymous | Audit :5007 |
| `/audit-service/info` | Anonymous | Audit :5007 |
| `/audit-service/audit/**` | Anonymous (audit service handles own auth via QueryAuth__Mode) | Audit :5007 |
| `/audit-service/export/**` | Anonymous (audit service handles own auth via QueryAuth__Mode) | Audit :5007 |
| `/notifications/health` | Anonymous | Notifications :5008 |
| `/notifications/**` | Bearer JWT required | Notifications :5008 |
| `/documents/health` | Anonymous | Documents :5006 |
| `/documents/access/**` | Anonymous | Documents :5006 |
| `/documents/internal/**` | Anonymous | Documents :5006 |
| `/documents/public/logo/{id}` | Anonymous | Documents :5006 |
| `/documents/**` | Bearer JWT required | Documents :5006 |
| `/liens/health` | Anonymous | Liens :5009 |
| `/liens/info` | Anonymous | Liens :5009 |
| `/liens/**` | Bearer JWT required | Liens :5009 |

## Identity Domain Model

| Entity | Table | PK | Key constraints |
|---|---|---|---|
| Tenant | Tenants | Id (Guid) | Code unique; Subdomain unique (filtered, nullable) |
| User | Users | Id (Guid) | (TenantId, Email) unique |
| Role | Roles | Id (Guid) | (TenantId, Name) unique |
| UserRole | UserRoles | (UserId, RoleId) | FK→Users Cascade, FK→Roles Cascade |
| Product | Products | Id (Guid) | Code unique |
| TenantProduct | TenantProducts | (TenantId, ProductId) | FK→Tenants Cascade |

### Product Role Resolution Engine (LS-COR-ROL-001)
- **Engine:** `IProductRoleResolutionService` → `ProductRoleResolutionService` (Identity.Infrastructure)
- **Flow:** `ResolveAsync(userId, tenantId)` → load tenant-enabled products → load all active org memberships with product/role graph → for each org+product: eligibility gate → dispatch to product-specific mapper or default mapper → return `EffectiveAccessContext`
- **EffectiveAccessContext:** `ProductAccess` (per-org, per-product entries with grant/deny, effective roles, access source tracing), `DeniedReasons`, helper methods (`GetEffectiveProductRoles()`, `HasProductAccess()`, `GetRolesForProduct()`, `GetAccessForOrganization()`)
- **Mapper interface:** `IProductRoleMapper` — `ProductCode` property + `ResolveRoles(ProductRoleMapperContext)`. Registered via DI; engine dispatches by product code.
- **CareConnectRoleMapper:** 3-tier resolution: (1) ScopedRoleAssignment (PRODUCT scope), (2) ProductOrganizationTypeRule DB rules, (3) OrgType fallback (PROVIDER→CARECONNECT_RECEIVER, LAW_FIRM→CARECONNECT_REFERRER, INTERNAL→CARECONNECT_ADMIN)
- **Default mapper:** Handles any product without a registered IProductRoleMapper — uses scoped assignments + DB OrgType rules
- **AuthService integration:** `LoginAsync` calls `_roleResolutionService.ResolveAsync()` → `accessContext.GetEffectiveProductRoles()` — replaces previous inline loop
- **Repository:** `UserRepository.GetActiveMembershipsWithProductsAsync()` — eager loads Organization → OrganizationProducts → Product → ProductRoles → OrgTypeRules
- **DI:** `IProductRoleMapper → CareConnectRoleMapper` (scoped), `IProductRoleResolutionService → ProductRoleResolutionService` (scoped)
- **Report:** `analysis/LS-COR-ROL-001-report.md`

### Product Provisioning Engine (LS-COR-PRD-001)
- **Engine:** `IProductProvisioningService` → `ProductProvisioningService` (Identity.Infrastructure)
- **Flow:** `ProvisionAsync(tenantId, productCode, enabled)` → TenantProduct creation → OrganizationProduct cascading (eligibility-filtered) → product-specific handler execution
- **Eligibility:** `ProductEligibilityConfig` (Identity.Domain) — centralized OrgType → Product mapping. LAW_FIRM→[CC,FUND,LIENS], PROVIDER→[CC], FUNDER→[FUND], LIEN_OWNER→[LIENS], INTERNAL→[ALL]
- **Permission code format:** `PRODUCT_CODE.domain:action` (e.g., `SYNQ_LIENS.lien:create`). Validated by regex in `Permission.cs`. Migration `20260414000001_UpdatePermissionCodesToNamespaced` updated all old-format codes to the namespaced format.
- **ICurrentRequestContext.Permissions:** Exposes JWT `permissions` claims. Added alongside existing `ProductRoles` for fine-grained permission checks in downstream services.
- **LiensPermissions (Liens.Domain):** Static constants for all 8 SYNQ_LIENS permission codes. Used with `RequirePermission` endpoint filter in Liens.Api.
- **Handler abstraction:** `IProductProvisioningHandler` — resolved by ProductCode, executed after org products are created
- **CareConnect handler:** `CareConnectProvisioningHandler` — calls CareConnect `/internal/provision-provider` to create/link/activate Provider records for PROVIDER orgs
- **Internal endpoint:** CareConnect `POST /internal/provision-provider` (AllowAnonymous) — idempotent provider creation/activation by OrganizationId
- **Integration points:** `UpdateEntitlement`, `ProvisionForCareConnect`, and `CreateTenant` all delegate to the engine
- **CreateTenant extension:** Accepts optional `products` array in request body for onboarding-time provisioning

### Tenant Provisioning & Verification (LSCC-01-006 + LSCC-01-006-01)
- **Lifecycle:** `Pending → InProgress → Provisioned → Verifying → Active` (with `Failed` branch at each stage)
- **Fields:** `Subdomain` (varchar 63, unique filtered), `ProvisioningStatus` (enum: Pending/InProgress/Provisioned/Verifying/Active/Failed), `ProvisioningFailureStage` (enum: None/DnsProvisioning/DnsVerification/HttpVerification), `LastProvisioningAttemptUtc`, `ProvisioningFailureReason`
- **TenantDomain:** Added `VerifiedAtUtc` (nullable datetime) and `MarkVerified()` method
- **Slug:** `SlugGenerator` (static class in Tenant.cs) — `Generate()`, `Normalize()`, `Validate()`, `AppendSuffix()`. Reserved: www, api, app, admin, mail, ftp, login, status. Rules: 3-63 chars, lowercase a-z0-9 + hyphens, no leading/trailing hyphens.
- **`PreferredSubdomain`:** `[NotMapped]` property on Tenant — set during `Create()`, consumed by provisioning service. Subdomain is NOT persisted until provisioning resolves uniqueness (prevents unique-index conflicts).
- **Verification Service:** `ITenantVerificationService` (Scoped) — two-phase: DNS resolution + HTTP check against `/.well-known/tenant-verify`
- **Verification Config:** `TenantVerification` section in appsettings.json — `Enabled`, `DevBypass` (true in dev), `DnsTimeoutSeconds`, `HttpTimeoutSeconds`, `VerificationEndpointPath`
- **Web Endpoint:** `GET /.well-known/tenant-verify` returns `tenant-verify-ok` (anonymous, used by verification service)
- **Retry Provisioning:** `POST /api/admin/tenants/{id}/provisioning/retry` — re-runs full flow
- **Retry Verification:** `POST /api/admin/tenants/{id}/verification/retry` — re-runs verification only via `IVerificationRetryService` with smart backoff
- **Login Hardening:** `AuthService.LoginAsync` rejects tenants with `ProvisioningStatus == Verifying` (DNS verifying message) or `!= Active` (not provisioned); BFF returns 503 with user-friendly messages
- **DI:** `ITenantProvisioningService` (Scoped), `ITenantVerificationService` (Scoped), `IVerificationRetryService` (Scoped), `IDnsService` (Singleton)
- **Secrets:** `Route53__HostedZoneId`, `Route53__BaseDomain`, `Route53__RecordValue`
- **Tenant Code = Subdomain:** The tenant code and subdomain slug are unified — the same lowercase slug (e.g. `acme-law`) is used as both the `Code` column and the `Subdomain`. This eliminates mapping issues between codes and subdomains. The `Tenant.Create` factory normalizes via `SlugGenerator.Normalize()`. `AuthService.LoginAsync` tries lowercase first, then uppercase fallback (for legacy tenants), then subdomain lookup. Create-tenant modal has a single "Tenant Code" field that shows the resulting subdomain URL inline.
- **Login:** `extractRawSubdomain()` in BFF route resolves tenant code from Host header in production (raw subdomain = tenant code); explicit `tenantCode` only accepted when `NEXT_PUBLIC_ENV=development`
- **Migration:** `20260407100001_AddVerificationRetryFields` — adds `VerificationAttemptCount`, `LastVerificationAttemptUtc`, `NextVerificationRetryAtUtc`, `IsVerificationRetryExhausted`, `ProvisioningFailureStage` to Tenants; `VerifiedAtUtc` to TenantDomains

### Smart Verification Retry (LSCC-01-006-02)
- **Purpose:** DNS propagation can take time after subdomain creation. Instead of immediately marking as Failed, the system auto-retries verification with exponential backoff.
- **Retry Options:** `VerificationRetry` config section — `MaxAttempts` (5), `InitialDelaySeconds` (30), `MaxDelaySeconds` (300), `BackoffMultiplier` (2.0), `MaxRetryWindowMinutes` (30)
- **Domain Fields:** `VerificationAttemptCount` (int), `LastVerificationAttemptUtc` (nullable), `NextVerificationRetryAtUtc` (nullable), `IsVerificationRetryExhausted` (bool)
- **Domain Methods:** `RecordVerificationAttempt()`, `ScheduleVerificationRetry()`, `MarkVerificationRetryExhausted()`, `ResetVerificationRetryState()`
- **Service:** `IVerificationRetryService` / `VerificationRetryService` — `ExecuteRetryAsync()` (single retry attempt with backoff scheduling), `ProcessPendingRetriesAsync()` (batch process all tenants with due retries)
- **Integration:** `TenantProvisioningService` delegates first verification attempt through retry service. `AdminEndpoints.RetryVerification` resets retry state and delegates to retry service.
- **API Response:** `GET /tenants/{id}` now includes `verificationAttemptCount`, `lastVerificationAttemptUtc`, `nextVerificationRetryAtUtc`, `isVerificationRetryExhausted`
- **Control Center UI:** Tenant detail card shows retry attempt count, last verification time, next retry time (amber), "Auto-retrying" pulse badge, "Retries exhausted" badge. Tenant list table pulses the "Verifying…" badge.
- **Login Gating:** `AuthService` returns specific "verifying DNS configuration" message for `Verifying` status; BFF routes (web + control-center) detect this and return 503 with "typically completes within a few minutes" message
- **Audit Events:** Retry success/failure emitted with attempt number, stage, exhaustion state

## Exception Handling (Fund.Api)

`ExceptionHandlingMiddleware` registered first in the pipeline (before auth). Maps:

| Exception | HTTP | Response `error.code` |
|---|---|---|
| `BuildingBlocks.Exceptions.ValidationException` | 400 | `validation_error` + `details` map |
| `BuildingBlocks.Exceptions.NotFoundException` | 404 | `not_found` |
| Any other `Exception` | 500 | `server_error` (safe message only) |

## Authorization Policies (Fund.Api)

| Policy | Requirement | Applied to |
|---|---|---|
| `AuthenticatedUser` | Any valid JWT | GET /api/applications, GET /api/applications/{id} |
| `AdminOnly` | Role = PlatformAdmin | (reserved, not yet applied) |
| `PlatformOrTenantAdmin` | Role = PlatformAdmin OR TenantAdmin | POST /api/applications |

Role claim read from `ClaimTypes.Role` = `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`.  
No token → **401**, authenticated but wrong role → **403**.

## Tenant / User Context (BuildingBlocks)

`ICurrentRequestContext` is registered as `Scoped` via `AddInfrastructure`. Reads claims from `IHttpContextAccessor`:
- `sub` → `UserId`
- `tenant_id` → `TenantId`
- `tenant_code` → `TenantCode`
- `email` → `Email`
- `ClaimTypes.Role` → `Roles`

Endpoints inject `ICurrentRequestContext` instead of parsing `ClaimsPrincipal` directly.

## Fund Domain Model

| Entity | Table | Audit fields | Indexes |
|---|---|---|---|
| Application | Applications | CreatedAtUtc, UpdatedAtUtc, CreatedByUserId (required), UpdatedByUserId (nullable) | unique (TenantId, ApplicationNumber); idx (TenantId, Status); idx (TenantId, CreatedAtUtc) |

`Application` inherits `BuildingBlocks.Domain.AuditableEntity`.  
`FundDbContext.SaveChangesAsync` auto-stamps `CreatedAtUtc` / `UpdatedAtUtc` for all `AuditableEntity` instances.  
Migration `AddUpdatedByUserId` added nullable `UpdatedByUserId char(36)` column.

## Seed Data

**Products:** SYNQ_FUND, SYNQ_LIENS, SYNQ_CARECONNECT, SYNQ_PAY, SYNQ_AI  
**Tenant:** LegalSynq Internal (`LEGALSYNQ`, id `20000000-…-0001`)  
**Roles:** PlatformAdmin (`30000000-…-0001`), TenantAdmin (`…-0002`), StandardUser (`…-0003`)  
**Seeded user:** `admin@legalsynq.com` / `ChangeMe123!` — PlatformAdmin

## Endpoints

| Via Gateway | Method | Auth | Description |
|---|---|---|---|
| `GET /health` | GET | Public | Gateway health |
| `GET /identity/health` | GET | Public | Identity health |
| `GET /identity/info` | GET | Public | Identity info |
| `POST /identity/api/auth/login` | POST | Public | Login → JWT |
| `GET /identity/api/auth/me` | GET | Bearer JWT | Current user session (called by Next.js BFF only) |
| `POST /identity/api/auth/logout` | POST | Public | Backend logout (no-op; cookie deletion is BFF's job) |
| `GET /identity/api/tenants/current/branding` | GET | Public | Tenant branding (X-Tenant-Code > Host) |
| `POST /identity/api/users` | POST | Bearer | Create user |
| `GET /identity/api/users` | GET | Bearer | List users (tenant-scoped) |
| `GET /identity/api/users/{id}` | GET | Bearer | Get user by ID |
| `GET /fund/health` | GET | Public | Fund health |
| `GET /fund/info` | GET | Public | Fund info |
| `POST /fund/api/applications` | POST | Bearer + PlatformOrTenantAdmin | Create application |
| `PUT /fund/api/applications/{id}` | PUT | Bearer + PlatformOrTenantAdmin | Update application |
| `GET /fund/api/applications` | GET | Bearer (AuthenticatedUser) | List applications (tenant-scoped) |
| `GET /fund/api/applications/{id}` | GET | Bearer (AuthenticatedUser) | Get application by ID |
| `GET /careconnect/health` | GET | Public | CareConnect health |
| `GET /careconnect/info` | GET | Public | CareConnect info |
| `GET /careconnect/api/categories` | GET | Bearer (AuthenticatedUser) | List active categories |
| `GET /careconnect/api/providers` | GET | Bearer + `provider:search` capability | List providers (tenant-scoped) |
| `GET /careconnect/api/providers/map` | GET | Bearer + `provider:map` capability | Provider map markers |
| `GET /careconnect/api/providers/{id}` | GET | Bearer + `provider:search` capability | Get provider by ID |
| `GET /careconnect/api/providers/{id}/availability` | GET | Bearer + `provider:search` capability | Provider open slots summary (from/to, up to 90 days) |
| `POST /careconnect/api/providers` | POST | Bearer + `provider:manage` capability | Create provider |
| `PUT /careconnect/api/providers/{id}` | PUT | Bearer + `provider:manage` capability | Update provider |
| `GET /careconnect/api/referrals` | GET | Bearer (AuthenticatedUser, org-scoped) | List referrals (org-participant scoped) |
| `GET /careconnect/api/referrals/{id}` | GET | Bearer (AuthenticatedUser) | Get referral by ID |
| `POST /careconnect/api/referrals` | POST | Bearer + `referral:create` capability | Create referral |
| `PUT /careconnect/api/referrals/{id}` | PUT | Bearer + status-driven capability | Update referral (accept→`referral:accept`, decline→`referral:decline`, cancel→`referral:cancel`) |
| `GET /careconnect/api/slots` | GET | Bearer + `appointment:create` capability | List slots (tenant-scoped, filterable) |
| `POST /careconnect/api/providers/{id}/slots/generate` | POST | Bearer + `schedule:manage` capability | Generate slots from templates |
| `POST /careconnect/api/appointments` | POST | Bearer + `appointment:create` capability | Book appointment |
| `GET /careconnect/api/appointments` | GET | Bearer (AuthenticatedUser) | List appointments (org-scoped by participant role) |
| `GET /careconnect/api/appointments/{id}` | GET | Bearer (AuthenticatedUser, participant only) | Get appointment — 404 for non-participants |
| `PUT /careconnect/api/appointments/{id}` | PUT | Bearer + `appointment:update` capability | Update status/notes |
| `POST /careconnect/api/appointments/{id}/cancel` | POST | Bearer + `appointment:manage` capability | Cancel appointment |
| `POST /careconnect/api/appointments/{id}/reschedule` | POST | Bearer + `appointment:manage` capability | Reschedule appointment |
| `GET /careconnect/api/appointments/{id}/history` | GET | Bearer (AuthenticatedUser) | Appointment status history |
| `GET /careconnect/api/providers/{id}/availability-templates` | GET | Bearer + `schedule:manage` capability | List availability templates |
| `POST /careconnect/api/providers/{id}/availability-templates` | POST | Bearer + `schedule:manage` capability | Create availability template |
| `PUT /careconnect/api/availability-templates/{id}` | PUT | Bearer + `schedule:manage` capability | Update availability template |
| `GET /careconnect/api/providers/{id}/availability-exceptions` | GET | Bearer (AuthenticatedUser) | List provider exceptions |
| `POST /careconnect/api/providers/{id}/availability-exceptions` | POST | Bearer + `schedule:manage` capability | Create exception |
| `PUT /careconnect/api/availability-exceptions/{id}` | PUT | Bearer + `schedule:manage` capability | Update exception |
| `POST /careconnect/api/providers/{id}/slots/apply-exceptions` | POST | Bearer + `schedule:manage` capability | Block slots overlapping active exceptions |
| `GET /careconnect/api/referrals/{id}/notes` | GET | Bearer (AuthenticatedUser) | List referral notes (newest first) |
| `POST /careconnect/api/referrals/{id}/notes` | POST | Bearer + `referral:create` capability | Create referral note |
| `PUT /careconnect/api/referral-notes/{id}` | PUT | Bearer + `referral:update_status` capability | Update referral note |
| `GET /careconnect/api/appointments/{id}/notes` | GET | Bearer (AuthenticatedUser) | List appointment notes (newest first) |
| `POST /careconnect/api/appointments/{id}/notes` | POST | Bearer + `appointment:create` capability | Create appointment note |
| `PUT /careconnect/api/appointment-notes/{id}` | PUT | Bearer + `appointment:update` capability | Update appointment note |
| `GET /careconnect/api/referrals/{id}/attachments` | GET | Bearer (AuthenticatedUser) | List referral attachment metadata (newest first) |
| `POST /careconnect/api/referrals/{id}/attachments` | POST | Bearer + `referral:create` capability | Create referral attachment metadata |
| `GET /careconnect/api/appointments/{id}/attachments` | GET | Bearer (AuthenticatedUser) | List appointment attachment metadata (newest first) |
| `POST /careconnect/api/appointments/{id}/attachments` | POST | Bearer + `appointment:create` capability | Create appointment attachment metadata |
| `GET /careconnect/api/notifications` | GET | Bearer (AuthenticatedUser) | List notifications (filterable: status, notificationType, relatedEntityType, relatedEntityId, scheduledFrom, scheduledTo, page, pageSize) |
| `GET /careconnect/api/notifications/{id}` | GET | Bearer (AuthenticatedUser) | Get notification by id |

## Running

```bash
bash scripts/run-dev.sh
```

Starts Identity (5001), Fund (5002), CareConnect (5003), and Gateway (5000) in parallel after build.  
Identity, Fund, and CareConnect auto-migrate on startup in Development.

## Migration Commands

```bash
# Identity
dotnet tool run dotnet-ef migrations add <Name> \
  --project apps/services/identity/Identity.Infrastructure \
  --startup-project apps/services/identity/Identity.Api \
  --output-dir Persistence/Migrations

# Fund
dotnet tool run dotnet-ef migrations add <Name> \
  --project apps/services/fund/Fund.Infrastructure \
  --startup-project apps/services/fund/Fund.Api \
  --output-dir Data/Migrations

# CareConnect
dotnet tool run dotnet-ef migrations add <Name> \
  --project apps/services/careconnect/CareConnect.Infrastructure \
  --startup-project apps/services/careconnect/CareConnect.Api \
  --output-dir Data/Migrations
```

## Important Notes

- **EF tool:** Use `dotnet tool run dotnet-ef` (local manifest at `.config/dotnet-tools.json`)
- **MySqlServerVersion:** Hardcoded `new MySqlServerVersion(new Version(8, 0, 0))` — do NOT use `ServerVersion.AutoDetect`
- **ApplicationNumber format:** `FUND-{year}-{8 hex chars}` e.g. `FUND-2026-D0D8784A`
- **ApplicationService.cs** uses `Domain.Application` (resolves to `Fund.Domain.Application` via C# parent-namespace lookup)
- **EF migrations via RDS:** EF tools hang due to RDS latency. Write migrations manually (`.cs` + `.Designer.cs` + Snapshot update) and rely on `db.Database.Migrate()` on startup.
- **double? geo columns:** Entity `double?` fields mapped to `decimal(10,7)` — migrations must use `AddColumn<double>`, snapshot must use `b.Property<double?>()`

## CareConnect Product Authorization Enforcement (LS-COR-AUT-001)

Declarative endpoint filters enforce product-level access control on all CareConnect routes using JWT `product_roles` claims. Applied as `IEndpointFilter` in the minimal API pipeline, running after authentication but before endpoint handlers.

**Shared building blocks** (`BuildingBlocks/Authorization/Filters/`):
- `RequireProductAccessFilter` — coarse product check via `HasProductAccess(productCode)`
- `RequireProductRoleFilter` — product-scoped role check via `HasProductRole(productCode, roles)`
- `RequireOrgProductAccessFilter` — org-scoped check, stores `org_id` in `HttpContext.Items["ProductAuth:OrgId"]`
- `RequirePermissionFilter` — capability/permission check via `HasPermission(permissionCode)` (LS-COR-AUT-009)
- `ProductAuthorizationExtensions` — fluent `.RequireProductAccess()`, `.RequireProductRole()`, `.RequireOrgProductAccess()`, `.RequirePermission()` on `RouteHandlerBuilder`/`RouteGroupBuilder`

**Claim extensions** (`ProductRoleClaimExtensions`): `HasProductAccess(productCode)` checks if any `product_roles` claim starts with `{productCode}:`. `HasProductRole(productCode, roles)` checks for exact `{productCode}:{role}` match. `HasPermission(permissionCode)` checks `permissions` claim (case-insensitive). `GetPermissions()` returns all permission claims. `IsTenantAdminOrAbove()` bypasses all product/permission checks. LS-COR-AUT-006: removed static `ProductToRolesMap` — product prefix is now parsed dynamically from `PRODUCT:Role` format claims.

**Bypass rules**: PlatformAdmin and TenantAdmin always bypass product filters. Product-level enforcement applies to Member role users.

**Structured 403 response**: `{"error":{"code":"PRODUCT_ACCESS_DENIED","message":"...","productCode":"SYNQ_CARECONNECT","requiredRoles":null,"organizationId":null}}`. Handled by `ExceptionHandlingMiddleware` catching `ProductAccessDeniedException`.

**Coverage**: All authenticated CareConnect endpoints have `.RequireProductAccess(ProductCodes.SynqCareConnect)`. Write endpoints additionally have `RequireOrgProductAccess` or `RequireProductRole`. Excluded: `InternalProvisionEndpoints` (service-to-service), `CareConnectIntegrityEndpoints` (anonymous), 5 public referral routes (token-gated). Admin endpoints (`PlatformOrTenantAdmin`) implicitly covered by bypass.

## CareConnect Capability-Based Authorization

Authorization uses a two-level check: PlatformAdmin/TenantAdmin always bypass capability checks; all other users are evaluated against a static role→capability map.

**Key classes:**
- `CareConnectCapabilityService` (Infrastructure/Services) — singleton, static `Dictionary<string,HashSet<string>>` keyed by `ProductRoleCodes`
- `CareConnectAuthHelper.RequireAsync()` (Application/Authorization) — PlatformAdmin bypass → TenantAdmin bypass → capability check
- `CapabilityCodes` (BuildingBlocks) — all capability string constants

**Role → Capability mapping:**

| Product Role | Capabilities |
|---|---|
| `CARECONNECT_REFERRER` | `referral:create`, `referral:read:own`, `referral:cancel`, `provider:search`, `provider:map`, `appointment:create`, `appointment:read:own`, `dashboard:read` |
| `CARECONNECT_RECEIVER` | `referral:read:addressed`, `referral:accept`, `referral:decline`, `appointment:create`, `appointment:update`, `appointment:manage`, `appointment:read:own`, `schedule:manage`, `provider:search`, `provider:map`, `dashboard:read` |

**Status models (canonical):**
- Referral: `New (Unopened) → NewOpened → Accepted → Scheduled → Completed/Cancelled`; `New → Declined`; `NewOpened → Declined/Cancelled`. Auto-transition: when a receiving provider org views referral detail, `New` auto-transitions to `NewOpened` (inline await in GET endpoint). Nav badge counts only `New` (unopened). Queue toolbar exposes separate "Unopened" / "Opened" filters. Legacy: `Received`/`Contacted` normalize to `Accepted` via `Referral.ValidStatuses.Legacy.Normalize()`.
- Appointment: `Pending → Confirmed → Completed/Cancelled`; `Rescheduled` as real status. `Scheduled` retained as backward-compat alias.

**Org-scoped referral list:** `GET /api/referrals` applies `ReferringOrgId`/`ReceivingOrgId` filters from JWT `org_id` claim based on user's product roles. Admins see all.

**xUnit test suite:** `CareConnect.Tests` — 158 tests covering `CareConnectCapabilityService`, `ReferralWorkflowRules`, `AppointmentWorkflowRules`, `OrgScopingTests`, `ProviderAvailabilityServiceTests`, `CareConnectParticipantHelperTests`, `AppointmentOrgScopingTests`, `AccessControlValidationTests`. All passing.

**LSCC-002 — Access hardening (complete):**
- `GET /api/referrals/{id}` — row-level participant check: non-participant callers receive 404 (not 403).
- `GET /api/appointments` — org-scoped: mirrors referral list scoping (receiver sees receiving-org appointments, referrer sees referring-org appointments, admins see all).
- `GET /api/appointments/{id}` — row-level participant check: non-participant callers receive 404.
- `PUT /api/admin/providers/{id}/link-organization` — explicit admin backfill for providers with null `OrganizationId`.
- `Appointment.Create` now denormalizes `ReferringOrganizationId` and `ReceivingOrganizationId` from the source Referral at booking time.
- `CareConnectParticipantHelper` — shared static helper: `IsAdmin`, `IsReferralParticipant`, `IsAppointmentParticipant`, `GetReferralOrgScope`, `GetAppointmentOrgScope`.

**LSCC-002-01 — Provider bulk tooling + appointment backfill (complete):**
- `GET /api/admin/providers/unlinked` — list all active providers with no Identity `OrganizationId` set. Returns `{ providers, count }`.
- `POST /api/admin/providers/bulk-link-organization` — bulk-link providers to organizations from an explicit `{ items: [{providerId, organizationId}] }` admin mapping. Returns `BulkLinkReport { total, updated, skipped, unresolved }`. Idempotent per item.
- `POST /api/admin/appointments/backfill-org-ids` — finds legacy appointments with null org IDs, copies `ReferringOrganizationId`/`ReceivingOrganizationId` from parent Referral. Returns `AppointmentBackfillReport { updated, skipped, alreadySet, candidates }`. Never guesses mappings; only derives from parent Referral.
- `Appointment.BackfillOrgIds(Guid, Guid)` — new idempotent domain method for legacy org-ID population.
- **EF Core alignment:** `Microsoft.EntityFrameworkCore.Design` downgraded from `8.0.8` → `8.0.2` in all four affected projects (CareConnect.Api, CareConnect.Infrastructure, Fund.Api, Fund.Infrastructure) to eliminate MSB3277 version conflict with Pomelo 8.0.2.

**LSCC-003-01 — Workflow Completion UX Polish (complete):**
- **Toast system:** `toast-context.tsx` (ToastProvider, useToast, useToastState) + `toast-container.tsx`; wired into platform layout; used on every mutation action (referral status, appointment confirm/noshow/reschedule/cancel).
- **ReferralTimeline:** `components/careconnect/referral-timeline.tsx` — renders `GET /api/referrals/{id}/history` status history with timestamped entries.
- **AppointmentActions:** `components/careconnect/appointment-actions.tsx` — Confirm / No-Show buttons + Reschedule modal with slot picker; calls `PUT /api/appointments/{id}` and `POST /api/appointments/{id}/reschedule`.
- **Dashboard stat counts:** Live counts via parallel API calls; referral counts by status; today's appointment count.
- **15 new backend tests** added across `AppointmentActionTests.cs` and `WorkflowIntegrationTests.cs`.
- **Report:** `analysis/LSCC-003-01.md`.

**LSCC-004 — Analytics & Operational Visibility (complete):**
- **`apps/web/src/lib/daterange.ts`** — date range utilities: presets (7d / 30d / custom), ISO formatting, URL param parsing, validation.
- **`apps/web/src/lib/careconnect-metrics.ts`** — pure metric functions: `safeRate`, `computeReferralFunnel`, `computeAppointmentMetrics`, `computeProviderPerformance`, `formatRate`.
- **Analytics components** (`src/components/careconnect/analytics/`):
  - `date-range-picker.tsx` — Client Component; preset + custom date inputs; pushes `analyticsFrom`/`analyticsTo` URL params.
  - `referral-funnel.tsx` — bar funnel with Total / Accepted / Scheduled / Completed + rates + drilldown links.
  - `appointment-metrics.tsx` — 4-card panel (Total / Completed / Cancelled / No-Show + rates).
  - `provider-performance.tsx` — top-10 provider table sorted by referrals received; colored acceptance rate; drilldown links.
- **Dashboard** extended with **Performance Overview** section: 11 parallel `Promise.allSettled` API calls for accurate counts; referral funnel + appointment metrics + provider table; date range picker.
- **Referral + appointment list pages** extended with `createdFrom`/`createdTo`/`providerId` (referrals) and `from`/`to`/`providerId` (appointments) filter params; active filter banner with clear link.
- **25 new backend tests** in `AnalyticsMetricsTests.cs` (metric contracts, rate computation, date range logic, drilldown URL contracts, graceful empty-data handling).
- **Report:** `analysis/LSCC-004-report.md`.

**LSCC-005 — Minimal Referral Flow + Basic Dashboard Analytics (complete):**
- **Domain layer:** `Referral.cs` — `ReferrerEmail`/`ReferrerName` fields + `Accept(Guid?)` method. `NotificationType` — 3 new values (`ReferralCreated`, `ReferralAcceptedProvider`, `ReferralAcceptedReferrer`). `CareConnectNotification` — `MarkSent()`/`MarkFailed()` domain methods.
- **Migration:** `20260401100000_AddReferrerFieldsToReferral` — adds `ReferrerEmail`/`ReferrerName` columns to `Referrals`.
- **`ReferralEmailService`** — HMAC-SHA256 token (format: `{referralId}:{expiryUnixSeconds}:{hmacHex}`, Base64url, 30-day TTL); HTML email templates for new-referral and acceptance confirmations; notification record queuing with SMTP best-effort delivery. Config keys: `ReferralToken:Secret`, `AppBaseUrl`, `Smtp:Host/Port/EnableSsl/Username/Password/FromAddress/FromName`.
- **`SmtpEmailSender`** — `ISmtpEmailSender` implementation; explicit failure logging at Warning level; throws `InvalidOperationException` if `Smtp:Host` absent.
- **Public API endpoints** (no `[Authorize]`):
  - `GET /api/referrals/resolve-view-token?token=X` — returns `{ routeType: "pending"|"active"|"invalid"|"notfound", referralId?, tenantCode? }`.
  - `POST /api/referrals/{id}/accept-by-token` — validates HMAC token, accepts referral, fires confirmation emails (fire-and-observe).
- **`IReferralRepository.GetByIdGlobalAsync`** — cross-tenant lookup for public token flows.
- **Frontend (`apps/web/src/`):**
  - `middleware.ts` — `/referrals/view` and `/referrals/accept` added to `PUBLIC_PATHS`.
  - `app/referrals/view/page.tsx` — Server Component; validates token via gateway; redirects pending providers to accept page, active-tenant providers to login with `returnTo` deep link.
  - `app/referrals/accept/[referralId]/page.tsx` — public Client Component; Accept button POSTs `accept-by-token`; shows success/error states; `/invalid` sub-path for bad/expired links.
  - `login-form.tsx` — `returnTo` query param support with open-redirect guard (`/` prefix check).
  - `provider-card.tsx` — converted to Client Component; `isReferrer` + referrer identity props; "Refer Patient" button (outside the `<Link>`) that opens `CreateReferralForm` modal via `useState`.
  - `provider-map-shell.tsx` — pulls referrer identity from `useSession()` and passes to `ProviderCard`.
  - `create-referral-form.tsx` — `referrerEmail?`/`referrerName?` props forwarded in `CreateReferralRequest` payload.
  - `types/careconnect.ts` — `referrerEmail?`/`referrerName?` added to `CreateReferralRequest`.
  - `careconnect-api.ts` — `referrals.acceptByToken(id, token)` method.
  - `dashboard/page.tsx` — fixed 30-day **Referral Activity** section (4 cards: Total, Pending, Accepted, Acceptance Rate); only visible for referrer role.
- **14 new tests** in `ReferralEmailServiceTests.cs`: token round-trip, URL-safe encoding, expiry, HMAC tampering, wrong-secret, malformed inputs, dev-fallback.
- **Bug fix (post-completion):** `ReferralService.CreateAsync` was using `_providers.GetByIdAsync(tenantId, ...)` which filters by `TenantId`. Since providers are a platform-wide marketplace (`BuildBaseQuery` deliberately ignores TenantId), cross-tenant provider lookups returned null → `NotFoundException` → 404. Fixed by switching to `_providers.GetByIdCrossAsync(id)` — consistent with `ProviderService`, `SearchAsync`, and the marketplace design intent.

**LSCC-005-01 — Referral Flow Hardening & Operational Visibility (complete):**
- **Domain:** `CareConnectNotification` gains `AttemptCount int` + `LastAttemptAtUtc DateTime?`; `MarkSent()`/`MarkFailed()` now increment `AttemptCount`. `Referral` gains `TokenVersion int` (default 1) + `IncrementTokenVersion()`. `NotificationType.ReferralEmailResent` added.
- **Token strategy:** 4-part HMAC token format: `{referralId}:{tokenVersion}:{expiry}:{hmacHex}` (Base64url). Version is cryptographically bound in the HMAC payload. `ValidateViewToken` now returns `ViewTokenValidationResult?(ReferralId, TokenVersion)`. Old 3-part tokens auto-rejected.
- **Revocation:** `RevokeTokenAsync` increments `TokenVersion` via `IncrementTokenVersion()`; all previously issued tokens are instantly invalidated. Emits `careconnect.referral.token.revoked` audit event (Security category).
- **Resend:** `ResendEmailAsync` creates a new `ReferralEmailResent` notification record using the current `TokenVersion`. Only available while referral is in `New` status.
- **Replay/duplicate hardening:** `AcceptByTokenAsync` checks `status != New` and emits `careconnect.referral.accept.replay` security audit event; returns 409 Conflict on double-accept.
- **Migration:** `20260401110000_ReferralHardening` — adds `AttemptCount`, `LastAttemptAtUtc` to `CareConnectNotifications`; adds `TokenVersion` to `Referrals`.
- **New endpoints:** `POST /{id}/resend-email`, `POST /{id}/revoke-token`, `GET /{id}/notifications` — all authenticated, `ReferralCreate` capability for mutations.
- **`ReferralResponse` DTO:** Extended with `TokenVersion`, `ProviderEmailStatus`, `ProviderEmailAttempts`, `ProviderEmailFailureReason`.
- **Frontend:** `ReferralNotification` type; `careconnect-api.ts` +3 methods (`resendEmail`, `revokeToken`, `getNotifications`). New `ReferralDeliveryCard` component (email status badge, attempt count, resend/revoke buttons, lazy notification history drawer) — referrer-only on referral detail page. Invalid token page redesigned with reason-aware messaging (missing/revoked/expired).
- **Tests:** `ReferralEmailServiceTests` updated for 4-part token API. 21 new tests in `ReferralHardeningTests.cs` covering token versioning, domain transitions, `AttemptCount` accumulation, format validation. **278 tests pass** (5 pre-existing `ProviderAvailabilityServiceTests` failures unchanged).
- **Report:** `/analysis/LSCC-005-01.md`

**LSCC-005-02 — Operational Automation & Email Reliability (complete):**
- **Retry model:** Automatic retries update the same notification record in-place (no new records). MaxAttempts=3, delays: 5 min after attempt 1, 30 min after attempt 2. Retry stops on success or exhaustion.
- **Domain:** `CareConnectNotification` gains `TriggerSource string` (Initial/AutoRetry/ManualResend) and `NextRetryAfterUtc DateTime?`. `MarkFailed(reason, nextRetryAfterUtc?)` schedules next retry. `ClearRetrySchedule()` nulls the schedule. `MarkSent()` always clears schedule. `NotificationType.ReferralEmailAutoRetry` added. `NotificationSource.cs` constants.
- **Retry policy:** `ReferralRetryPolicy` (static) — `IsEligibleForRetry`, `IsExhausted`, `GetDerivedStatus`, `GetNextRetryAfter`. Derived display states (not persisted): Pending, Sent, Failed, Retrying, RetryExhausted.
- **BackgroundService:** `ReferralEmailRetryWorker` — polls every 60 s via `IServiceScopeFactory`; skips retries if referral is not in `"New"` status; calls `RetryNotificationAsync` on `IReferralEmailService`.
- **Manual resend distinction:** `ResendEmailAsync` creates a new `ManualResend` notification record; on success calls `ClearRetrySchedule()` on the original failed record to suppress auto-retry double-send.
- **Audit timeline:** `GET /api/referrals/{id}/audit` — merges `ReferralStatusHistory` + `CareConnectNotifications` chronologically into `ReferralAuditEventResponse[]` (EventType, Label, OccurredAt, Detail, Category).
- **DTO updates:** `ReferralNotificationResponse` gains `TriggerSource`, `NextRetryAfterUtc`, `DerivedStatus`. New `ReferralAuditEventResponse`.
- **Migration:** `20260401120000_NotificationRetry` — adds `TriggerSource`, `NextRetryAfterUtc` to `CareConnectNotifications`.
- **Frontend:** `ReferralNotification` TS type updated (triggerSource, nextRetryAfterUtc, derivedStatus). `ReferralAuditEvent` type added. `careconnect-api.ts` +1 method (`getAuditTimeline`). New `ReferralAuditTimeline` component (collapsible, colour-coded by category). `ReferralDeliveryCard` updated for retry-aware badges (Retrying…, Retry Exhausted), next-retry hint, exhausted callout, source context pill. Detail page: `ReferralAuditTimeline` added for referrers.
- **Tests:** 35 new tests in `ReferralRetryTests.cs` covering policy eligibility, delay schedule, derived-state derivation, domain methods, retry/resend distinction, constants. **292 tests pass** (5 pre-existing `ProviderAvailabilityServiceTests` failures unchanged).
- **Report:** `/analysis/LSCC-005-02.md`

**CCX-002 — CareConnect Referral Notifications & Delivery Wiring (complete):**
- **Scope:** Wired all four referral lifecycle events (submitted, accepted, rejected, cancelled) to notification creation and email delivery.
- **New notification types:** `ReferralRejectedProvider`, `ReferralRejectedReferrer`, `ReferralCancelledProvider`, `ReferralCancelledReferrer` added to `NotificationType.cs`.
- **Idempotency:** `DedupeKey` field added to `CareConnectNotification` model (varchar 500, nullable, unique index). Format: `referral:{referralId}:{event}:{recipientRole}`. All referral notification creation paths check `ExistsByDedupeKeyAsync` before creating. Applied to new AND existing paths (created, accepted, rejected, cancelled).
- **Rejection notifications:** `SendRejectionNotificationsAsync` on `IReferralEmailService` — notifies provider and referrer when status → Declined.
- **Cancellation notifications:** `SendCancellationNotificationsAsync` on `IReferralEmailService` — notifies provider and referrer when status → Cancelled.
- **Wiring:** `ReferralService.UpdateAsync` dispatches email notifications via fire-and-observe `Task.Run` for Accepted/Declined/Cancelled status transitions. Uses `GetByIdCrossAsync` for cross-tenant provider lookup.
- **Retry support:** All 4 new notification types added to `RetryNotificationAsync` switch cases in `ReferralEmailService`.
- **Email templates:** 4 new HTML templates (BuildProviderRejectionHtml, BuildReferrerRejectionHtml, BuildProviderCancellationHtml, BuildReferrerCancellationHtml).
- **Migration:** `20260404000000_AddNotificationDedupeKey` — adds `DedupeKey` column + unique index.
- **No frontend changes:** Backend-only feature, fire-and-observe pattern.
- **No appointment notifications added.**
- **Report:** `/analysis/CCX-002-report.md`

## CareConnect Provider Geo / Map-Ready Discovery

- **Radius search:** `latitude` + `longitude` + `radiusMiles` (max 100 mi). Bounding-box filter in `ProviderGeoHelper.BoundingBox`.
- **Viewport search:** `southLat` + `northLat` + `westLng` + `eastLng`. northLat must be >= southLat.
- **Conflict rule:** Radius + viewport together → 400 validation error on `search` key.
- **`GET /api/providers/map`:** Returns `ProviderMarkerResponse[]`, capped at 500 markers, only geo-located providers. Shares all filter params with the list endpoint.
- **`GET /api/providers/{id}/availability`:** Returns `ProviderAvailabilityResponse` with open slot summaries for a date range (max 90 days). Optional `facilityId`/`serviceOfferingId` filters. Requires `provider:search` capability.
- **Display fields (both endpoints):** `DisplayLabel = OrganizationName ?? Name`; `MarkerSubtitle = "City, State[ · PrimaryCategory]"`; `PrimaryCategory` = first category alphabetically.
- **`BuildBaseQuery`:** Shared LINQ filter builder in `ProviderRepository` used by both `SearchAsync` and `GetMarkersAsync` to avoid duplication.

## Docs Service (_archived/documents-nodejs) — ARCHIVED (replaced by documents)

**258 tests across 14 suites, all passing.**

### Unit Tests (161 tests, 7 suites)
`npm run test:unit` — mocked DB/storage/auth.

| Suite | Tests | Coverage |
|-------|-------|----------|
| errors.test.ts | 28 | Error hierarchy, HTTP status codes, error codes |
| rbac.test.ts | 22 | ROLE_PERMISSIONS matrix, assertPermission, assertTenantScope |
| malware-scanning.test.ts | 27 | NullScanner, MockScanner, ClamAV, scan gate, ScanService lifecycle |
| access-mediation.test.ts | 20 | AccessTokenService issue/redeem/one-time-use, scan gate |
| redis-backing.test.ts | 23 | RedisRateLimiter, RedisAccessTokenStore, fallback |
| tenant-isolation.test.ts | 22 | assertDocumentTenantScope, resolveEffectiveTenantId, Layer2 ABAC |
| rate-limiting.test.ts | 19 | generalLimiter, uploadLimiter, signedUrlLimiter, IP+user+tenant dims |

### Integration Tests (97 tests, 7 suites)
`npm run test:int` — real PostgreSQL (heliumdb), local storage, HS256 JWT.

| Suite | Tests | Coverage |
|-------|-------|----------|
| auth.test.ts | 25 | Missing/invalid/expired tokens, auth bypass attempts |
| rbac.test.ts | 22 | Full RBAC matrix against live API |
| tenant-isolation.test.ts | 21 | Three-layer isolation; cross-tenant 404 not 403; admin audit |
| upload-validation.test.ts | 14 | MIME whitelist, size limit, magic-byte mismatch |
| access-control.test.ts | 19 | Soft delete, legal hold, scan status gating, access token round-trip |
| rate-limiting.test.ts | 9 | 429 after limit, Retry-After header, per-user buckets |
| audit.test.ts | 28 | DOCUMENT_CREATED/UPDATED/DELETED, SCAN events, ADMIN_CROSS_TENANT_ACCESS, immutability |

### Key architectural fixes discovered during integration testing
1. `DocumentRepository.create` was generating its own UUID independently from `docId` pre-generated in `document-service.ts`, causing scan audit events to be stored under a mismatched `document_id`. Fixed by accepting optional `id` in create input.
2. `auth.ts` was using `'unknown'` and `'n/a'` as UUID placeholders in audit events — replaced with nil UUID `00000000-0000-0000-0000-000000000000`.
3. `file-type` downgraded from v19 (ESM-only) to v16.5.4 (last CJS release) for Jest compatibility.

### Analysis documents
- `analysis/step14_integration_tests.md` — coverage summary, infrastructure, gaps, how to run

---

## .NET Documents Service (apps/services/documents)

**Port**: 5006  
**Framework**: .NET 8 Minimal APIs + EF Core 8 + Npgsql (PostgreSQL)  
**Architecture**: 4-project layered monorepo (Domain → Application → Infrastructure → Api)  
**Status**: Fully implemented, builds cleanly (0 errors, 0 warnings)
**EF Core alignment**: `Microsoft.EntityFrameworkCore.Design` downgraded from `9.0.0` → `8.0.4` to eliminate NU1605 package downgrade error (Design 9.0 pulled EF 9.0 transitive dep, conflicting with EF 8.0.4 direct ref).

### Project Layout

| Project | Purpose |
|---------|---------|
| `Documents.Domain` | Entities, enums, interfaces, value objects. Zero external NuGet deps. |
| `Documents.Application` | Services, DTOs, exceptions, RequestContext. Depends only on Domain + FluentValidation. |
| `Documents.Infrastructure` | EF Core DbContext, repositories, storage providers (Local/S3), scanners, token stores, JWT extractor, DI wiring. |
| `Documents.Api` | Minimal API endpoints, middleware, Program.cs, appsettings. |

### Key Characteristics
- **PostgreSQL** — uses Npgsql/EF Core 8 (NOT MySQL, unlike other .NET services; matches TypeScript Docs service schema)
- **Full API parity**: 13/13 TypeScript endpoints implemented
- **Three-layer tenant isolation**: L1 pre-query guard + L2 LINQ WHERE predicate + L3 ABAC in DocumentService
- **RBAC**: 5 roles (DocReader/DocUploader/DocManager/TenantAdmin/PlatformAdmin)
- **Storage**: `s3` (both dev and prod), selected via `Storage:Provider` config. Dev and prod share the same RDS database, so both must use S3 to avoid storage-provider mismatch (local storage can't serve files uploaded via S3). Database provider also available as fallback.
- **File scanning**: `none` / `mock` / `clamav` (TCP to clamd) — `Scanner:Provider` config; fully async background worker model
- **Async scanning**: Uploads immediately return `scanStatus: "PENDING"`; `DocumentScanWorker` (BackgroundService) scans asynchronously via `IScanJobQueue` (lease/ack pattern)
- **Durable scan queue**: `ScanWorker:QueueProvider=memory` (dev) or `redis` (prod via Redis Streams XADD/XREADGROUP/XAUTOCLAIM); configurable via `ScanWorker:*`
- **Retry + backoff**: Exponential backoff retry up to `MaxRetryAttempts` (default 3); configurable `InitialRetryDelaySeconds` / `MaxRetryDelaySeconds`; all retries audited
- **Concurrent workers**: Configurable `ScanWorker:WorkerCount` parallel scan tasks; safe concurrent dequeue via lease isolation
- **Backpressure**: Queue saturation returns HTTP 503 `QUEUE_SATURATED` + `Retry-After: 30` header — no blocking hangs
- **Quarantine model**: All uploads stored under `quarantine/{tenantId}/{docTypeId}/` prefix; access gated by `ScanStatus` enforcement (fail-closed by default)
- **RequireCleanScanForAccess**: Defaults to `true` — Pending/Failed/Infected files blocked; `SCAN_ACCESS_DENIED` audit event emitted on every blocked access
- **Prometheus metrics**: 11 custom metrics at `GET /metrics` (prometheus-net.AspNetCore): queue depth, scan lifecycle, duration histogram, ClamAV health gauge
- **Health checks**: `GET /health` (liveness, DB only) and `GET /health/ready` (DB + ClamAV); JSON response with per-check detail
- **Access tokens**: Opaque 64-hex (256-bit), one-time-use, configurable TTL; backed by in-memory or Redis
- **JWT auth**: HS256 symmetric key (dev) or JWKS/RS256 (prod), configured via `Jwt:SigningKey` or `Jwt:JwksUri`
- **Structured logging**: Serilog with console sink
- **Swagger**: Available at `/docs` in Development environment

### Build Command
```bash
dotnet build apps/services/documents/Documents.Api/Documents.Api.csproj
```

### Database Setup
At startup, `Program.cs` handles schema automatically:
- **Fresh database**: `EnsureCreated()` creates all tables from the EF model
- **Existing database**: Runs idempotent `ALTER TABLE` patches for any missing columns/constraints:
  - `ALTER TABLE document_audits ADD COLUMN IF NOT EXISTS actor_email VARCHAR(500);`
  - `ALTER TABLE document_audits ALTER COLUMN actor_id DROP NOT NULL;` (scan worker audits have no actor)
- **No EF Core migrations**: The migration snapshot is a placeholder; schema is managed via `EnsureCreated` + startup patches
- **Dev vs Prod Postgres**: Dev uses `helium:5432` (Replit built-in); Prod uses `DATABASE_URL` (Replit deployment Postgres). Document IDs are NOT portable between environments.
- **Storage**: Both dev and prod use S3 (`Storage:Provider=s3`) with credentials from env vars (`AWS_S3_BUCKET_NAME`, `AWS_S3_REGION`, `AWS_S3_ACCESS_KEY_ID`, `AWS_S3_SECRET_ACCESS_KEY`). Dev and prod share the same RDS instance, so storage providers must match to avoid 404s on files uploaded from the other environment. Database provider (`docs_file_blobs` table) available as fallback. Local filesystem option retained for offline dev (`/home/runner/data/docs-local`).

Reference schema: `apps/services/documents/Documents.Infrastructure/Database/schema.sql`

### Analysis Documents (7 + 6 phases)
Architecture phases in `_archived/documents-nodejs/analysis/`:
- `dotnet_phase1_discovery_and_mapping.md` — TS→.NET translation decisions
- `dotnet_phase2_scaffolding.md` — project structure and dependency graph
- `dotnet_phase3_domain_and_contracts.md` — entities, enums, interfaces, invariants
- `dotnet_phase4_api_and_application.md` — services, RBAC, endpoints, configuration
- `dotnet_phase5_infrastructure.md` — EF Core, repositories, storage, scanner, token stores
- `dotnet_phase6_security_and_tenancy.md` — threat model, three-layer isolation, HIPAA notes
- `dotnet_phase7_parity_review.md` — 13/13 endpoint parity, A- grade, gaps, next steps

ClamAV phases in `apps/services/documents/analysis/`:
- `dotnet_clamav_phase1_design.md` — async scan architecture, quarantine model, ADRs
- `dotnet_clamav_phase2_provider.md` — ClamAV TCP implementation, provider selection
- `dotnet_clamav_phase3_worker.md` — BackgroundService, Channel queue, scan lifecycle
- `dotnet_clamav_phase4_quarantine_and_access.md` — quarantine prefix, access enforcement, API changes
- `dotnet_clamav_phase5_review.md` — audit events, config reference, parity gaps, production notes
- `dotnet_clamav_final_summary.md` — complete summary, security posture, schema changes

Enterprise hardening phases in `apps/services/documents/analysis/`:
- `dotnet_enterprise_phase1_durable_queue.md` — Redis Streams durable queue, IScanJobQueue lease/ack redesign
- `dotnet_enterprise_phase2_retries_and_scaling.md` — exponential backoff retry, WorkerCount concurrency, duplicate prevention
- `dotnet_enterprise_phase3_backpressure.md` — QueueSaturationException (503), fail-fast upload, Retry-After header
- `dotnet_enterprise_phase4_audit_and_observability.md` — SCAN_ACCESS_DENIED event, 11 Prometheus metrics, health checks
- `dotnet_enterprise_phase5_clamav_hardening.md` — ClamAV PING/PONG health, timeout isolation, fail-closed review
- `dotnet_enterprise_final_summary.md` — complete architecture, production deployment guidance, remaining risks

Phase 4 Final Hardening in `apps/services/documents/analysis/`:
- `dotnet_phase4_final_hardening.md` — Redis circuit breaker, durable Redis Streams publisher, correlation propagation, production runbook, alert rules

### Phase 4 Final Hardening Summary (COMPLETE — 0 errors, 0 regressions)

| Capability | Implementation |
|---|---|
| Redis circuit breaker | `RedisResiliencePipeline` (Polly `AdvancedCircuitBreaker`) shared by queue + publishers; state 0/1/2 exposed via `docs_redis_circuit_state` gauge |
| Durable event delivery | `RedisStreamScanCompletionPublisher` — XADD to `documents:scan:completed` stream; configurable `StreamKey` + `StreamMaxLength`; set `Provider=redis-stream` |
| Correlation propagation | `ScanJob.CorrelationId` carries HTTP `X-Correlation-Id` from upload → Redis queue fields → worker logs → `DocumentScanCompletedEvent.CorrelationId` |
| Health check enhancement | `RedisHealthCheck` injects `RedisResiliencePipeline`; reports `circuit=<state>` in description; returns `Degraded` when circuit open |
| New Prometheus metrics | `docs_redis_circuit_state`, `docs_redis_circuit_open_total`, `docs_redis_circuit_short_circuit_total`, `docs_scan_completion_stream_publish_total`, `docs_scan_completion_stream_publish_failures_total` |
| Config additions | `Redis:CircuitBreaker` (FailureThreshold/BreakDuration/SamplingDuration/MinThroughput); `Notifications:ScanCompletion:Redis:StreamKey` + `StreamMaxLength` |

**Notification provider options (choose in `Notifications:ScanCompletion:Provider`):**
- `"log"` — structured log only (default, zero dependencies)
- `"redis"` — Redis Pub/Sub at-most-once
- `"redis-stream"` — **RECOMMENDED for production** — Redis Streams XADD, durable + replayable
- `"none"` — disabled

---

## Platform Foundation Upgrade (6-Phase — COMPLETE)

Analysis report: `analysis/step1_platform-foundation-upgrade.md`

### New Identity Domain Entities
| Entity | Table | Phase |
|--------|-------|-------|
| `OrganizationType` | `OrganizationTypes` | 1 |
| `RelationshipType` | `RelationshipTypes` | 2 |
| `OrganizationRelationship` | `OrganizationRelationships` | 2 |
| `ProductRelationshipTypeRule` | `ProductRelationshipTypeRules` | 2 |
| `ProductOrganizationTypeRule` | `ProductOrganizationTypeRules` | 3 |
| `ScopedRoleAssignment` | `ScopedRoleAssignments` | 4 |

### Identity Migrations
```
20260330110001_AddOrganizationTypeCatalog.cs       — OrganizationTypes table + Organization.OrganizationTypeId FK + backfill
20260330110002_AddRelationshipGraph.cs             — RelationshipTypes + OrganizationRelationships + ProductRelationshipTypeRules + seeds
20260330110003_AddProductOrgTypeRules.cs           — ProductOrganizationTypeRules + 7 backfilled seeds
20260330110004_AddScopedRoleAssignment.cs          — ScopedRoleAssignments + INSERT SELECT from UserRoleAssignments
```

### CareConnect Migrations
EF Core migrations run at startup in **all environments** (not dev-only). The `__EFMigrationsHistory` table tracks which have been applied. If migration fails, the service crashes immediately (fail-fast) to prevent serving traffic on an incompatible schema.
```
20260330110001_AlignCareConnectToPlatformIdentity.cs   — Provider.OrganizationId, Facility.OrganizationId,
                                                          Referral.OrganizationRelationshipId, Appointment.OrganizationRelationshipId
20260331200000_NormalizeStatusValues.cs                — Referral: Received/Contacted→Accepted; Appointment: Scheduled→Pending;
                                                          applies to main tables + history tables
```

### Phase 3 Activation Note
`UserRepository.GetPrimaryOrgMembershipAsync` now eager-loads
`ProductRole → OrgTypeRules → OrganizationType` via chained `.ThenInclude`.
`AuthService.IsEligible` checks the rule table first; falls back to `EligibleOrgType` string (legacy compat).

### New Admin Endpoints (Phase 6)
| Method | Path |
|--------|------|
| GET/GET | `/api/admin/organization-types`, `/api/admin/organization-types/{id}` |
| GET/GET | `/api/admin/relationship-types`, `/api/admin/relationship-types/{id}` |
| GET/GET/POST/DELETE | `/api/admin/organization-relationships[/{id}]` |
| GET | `/api/admin/product-org-type-rules` |
| GET | `/api/admin/product-relationship-type-rules` |

### Build status after all 6 phases
- Identity.Api: ✅ 0 errors, 0 warnings
- CareConnect.Api: ✅ 0 errors, 0 regressions (1 pre-existing CS0168)

---

## Platform Foundation — Continuation Phases A–F (COMPLETE)

### Phase A — Organization.Create overload ✅
- `Organization.Create(string name, string orgType, Guid? organizationTypeId)` overload added
- `Organization.AssignOrganizationType(Guid, string)` instance method for post-create / backfill assignment

### Phase B — DB-backed eligibility activation ✅
- `User.ScopedRoleAssignments` nav collection + EF `WithMany` config
- `UserRepository.GetByIdWithRolesAsync` includes ScopedRoleAssignments→Role
- `UserRepository.GetPrimaryOrgMembershipAsync` includes OrganizationTypeRef
- `AuthService.LoginAsync` merges GLOBAL-scoped assignments into roleNames
- `AuthService.IsEligible` uses `OrganizationTypeId` comparison with legacy string fallback
- `JwtTokenService` emits `org_type_id` claim when `OrganizationTypeId` is set

### Phase C — CareConnect relationship persistence ✅
- `IOrganizationRelationshipResolver` interface in `CareConnect.Application.Interfaces`
- `OrganizationRelationshipNullResolver` stub in `CareConnect.Infrastructure.Services` (safe default; replace with HTTP resolver when Identity endpoint is stable)
- `Referral.Create` extended with optional `organizationRelationshipId` param
- `Referral.SetOrganizationRelationshipId(Guid)` instance method for post-create / backfill
- `Appointment.Create` extended with optional `organizationRelationshipId` param (denormalized from Referral)
- `Appointment.SetOrganizationRelationshipId(Guid)` instance method
- `CreateReferralRequest` extended with `ReferringOrganizationId?` and `ReceivingOrganizationId?`
- `ReferralService.CreateAsync` resolves org relationship via resolver, passes IDs through to `Referral.Create`
- `AppointmentService.CreateAppointmentAsync` denormalizes `OrganizationRelationshipId` from loaded Referral
- `OrganizationRelationshipNullResolver` registered in `CareConnect.Infrastructure.DependencyInjection`

### Phase D — Provider/Facility identity alignment ✅
- `Provider.LinkOrganization(Guid)` instance method
- `Facility.LinkOrganization(Guid)` instance method
- `CreateProviderRequest.OrganizationId?` optional field
- `UpdateProviderRequest.OrganizationId?` optional field
- `ProviderService.CreateAsync` calls `LinkOrganization` when `OrganizationId` is supplied
- `ProviderService.UpdateAsync` calls `LinkOrganization` when `OrganizationId` is supplied

### Phase E — Control-center frontend compatibility ✅
**Types added to `src/types/control-center.ts`:**
- `OrganizationTypeItem` — catalog entry
- `RelationshipTypeItem` — catalog entry
- `OrgRelationshipStatus` — `Active | Inactive | Pending`
- `OrgRelationship` — directed org→org relationship
- `ProductOrgTypeRule` — product access rule by org type
- `ProductRelTypeRule` — product access rule by relationship type

**Mappers added to `src/lib/api-mappers.ts`:**
- `mapOrganizationTypeItem`, `mapRelationshipTypeItem`
- `mapOrgRelationship`, `mapProductOrgTypeRule`, `mapProductRelTypeRule`

**API namespaces added to `src/lib/control-center-api.ts`:**
- `organizationTypes.list()`, `organizationTypes.getById(id)`
- `relationshipTypes.list()`, `relationshipTypes.getById(id)`
- `organizationRelationships.list(params?)`, `organizationRelationships.getById(id)`
- `productOrgTypeRules.list(params?)`
- `productRelTypeRules.list(params?)`

**Cache tags added to `src/lib/api-client.ts` CACHE_TAGS:**
- `orgTypes`, `relTypes`, `orgRelationships`, `productOrgTypeRules`, `productRelTypeRules`

### Phase F — Legacy deprecation notices ✅
- `ProductRole.EligibleOrgType` — XML `/// TODO [LEGACY — Phase F]` doc comment added
- `UserRoleAssignment` class — XML `/// TODO [LEGACY — Phase F]` doc comment added

### Build status after all Phases A–F
- Identity.Api: ✅ 0 errors, 0 warnings
- CareConnect.Api: ✅ 0 errors, 1 pre-existing CS0168 warning (unrelated)
- control-center TypeScript: ✅ 0 errors (`npx tsc --noEmit` clean)

---

## Step 4 — Platform Hardening ✅

**Report:** `analysis/step4_platform-hardening.md`

### 4.1 Resolver auth header support
- `IdentityServiceOptions` — `AuthHeaderName?` + `AuthHeaderValue?` fields added
- `HttpOrganizationRelationshipResolver` — auth header applied per-request when both fields configured; `_isEnabled` computed once at construction; "disabled" case emits `LogWarning` once at startup (not per-call)
- `appsettings.json` / `appsettings.Development.json` — new keys documented

### 4.2 AuthService eligibility observability
- `ILogger<AuthService>` injected
- `IsEligible` → `IsEligibleWithPath` returns `(bool, EligibilityPath)` enum (`DbRule | LegacyString | Unrestricted`)
- `LoginAsync` logs per-path counts; `LogInformation` fires only when legacy fallback is used

### 4.3 ProviderService / FacilityService — LinkOrganization logging
- Both services gain `ILogger<T>` (auto-injected via DI)
- `LogDebug` emitted on `LinkOrganization()` for create and update paths
- `ProviderResponse.OrganizationId` — `Guid?` field added to DTO and wired in `ToResponse()`

### 4.4 UserRepository — dual-write ScopedRoleAssignment
- `AddAsync` now creates a `ScopedRoleAssignment` (scope=GLOBAL) for every role assigned at user creation
- Legacy `UserRole` rows preserved — both tables kept in sync from first write

### 4.5 Identity startup diagnostic
- `Program.cs` — on every startup, queries for ProductRoles with `EligibleOrgType` set but no active `OrgTypeRules`
- Logs `LogInformation` when coverage is complete (current state: all 7 seeded roles covered)
- Logs `LogWarning` per uncovered role when gaps are detected

### 4.6 Control-center ORGANIZATION GRAPH pages

**Routes:** `lib/routes.ts` — `orgTypes`, `relationshipTypes`, `orgRelationships`, `productRules`

**Nav section:** `lib/nav.ts` — ORGANIZATION GRAPH section with 4 entries

**Pages created:**
- `app/org-types/page.tsx` — Org Type catalog list
- `app/relationship-types/page.tsx` — Relationship Type catalog list
- `app/org-relationships/page.tsx` — Live relationship graph with activeOnly filter + pagination
- `app/product-rules/page.tsx` — Combined ProductOrgTypeRules + ProductRelTypeRules (parallel fetch)

**Components created:**
- `components/platform/org-type-table.tsx` — `OrgTypeTable`
- `components/platform/relationship-type-table.tsx` — `RelationshipTypeTable`
- `components/platform/org-relationship-table.tsx` — `OrgRelationshipTable` (with pagination)
- `components/platform/product-rules-panel.tsx` — `ProductOrgTypeRuleTable`, `ProductRelTypeRuleTable`

### Build status after Step 4
- Identity.Api: ✅ 0 errors, 0 warnings
- CareConnect.Api: ✅ 0 errors, 1 pre-existing CS0168 warning (unrelated)
- control-center TypeScript: ✅ 0 errors (`npx tsc --noEmit` clean)

## Step 5 — Phase F Retirement + ScopedRoleAssignment Coverage ✅

### 5.1 Phase F — EligibleOrgType column retirement (COMPLETE)

**Gate conditions (both verified before proceeding):**
- `legacyStringOnly = 0` — confirmed prior to Step 5 (all restricted roles had OrgTypeRules)
- All 7 restricted ProductRoles had confirmed active `ProductOrganizationTypeRule` rows (Phase E)

**Three migrations applied in sequence:**
1. `20260330200001_NullifyEligibleOrgType.cs` — nulls `EligibleOrgType` for all 7 restricted ProductRoles; moves state from `withBothPaths=7` to `withDbRuleOnly=7`
2. `20260330200002_BackfillScopedRoleAssignmentsFromUserRoles.cs` — closes the coverage gap: backfills `ScopedRoleAssignments` (GLOBAL scope) from `UserRoles` for any user not already covered by the previous backfill (migration 20260330110004 only sourced from `UserRoleAssignments`)
3. `20260330200003_PhaseFRetirement_DropEligibleOrgTypeColumn.cs` — drops the `EligibleOrgType` column from `ProductRoles` table + its composite index

**C# code changes:**
- `ProductRole.cs` — `EligibleOrgType` property removed; `Create()` factory signature simplified (no `eligibleOrgType` param)
- `ProductRoleConfiguration.cs` — removed `HasMaxLength(50)` + `HasIndex(ProductId, EligibleOrgType)`; all `HasData` entries updated to omit the field
- `AuthService.cs` — Path 2 (legacy EligibleOrgType check) removed from `IsEligibleWithPath`; `EligibilityPath.LegacyString` enum value removed; legacy login logging removed
- `ProductOrganizationTypeRule.cs` — doc comment updated to reflect Phase F complete
- `Program.cs` — startup diagnostic replaced: now verifies OrgTypeRule coverage + ScopedRoleAssignment dual-write gap
- `IdentityDbContextModelSnapshot.cs` — `EligibleOrgType` property, index, and seed data references removed

### 5.2 Role assignment admin endpoints (NEW)

**`POST /api/admin/users/{id}/roles`** — assigns a role (dual-write: `UserRole` + `ScopedRoleAssignment` GLOBAL); returns 201 Created with roleId, roleName, assignedAtUtc
**`DELETE /api/admin/users/{id}/roles/{roleId}`** — revokes a role (deactivates `ScopedRoleAssignment`, removes `UserRole`); returns 204 No Content
- Both endpoints registered in `MapAdminEndpoints`
- `AssignRoleRequest` DTO added (private, scoped to `AdminEndpoints`)

### 5.3 Coverage endpoint improvements

**`GET /api/admin/legacy-coverage` updated:**
- Eligibility section: `withBothPaths = 0` and `legacyStringOnly = 0` are now hardcoded constants (Phase F complete); `dbCoveragePct` recalculated from OrgTypeRule coverage
- Role assignments section: new `usersWithGapCount` field — count of users with `UserRole` but no matching GLOBAL `ScopedRoleAssignment` (should reach 0 after migration 20260330200002)
- Both sections use `ToHashSetAsync()` for O(1) set lookups

### 5.4 TypeScript + UI updates

- `types/control-center.ts` — `RoleAssignmentsCoverage` gains `usersWithGapCount: number`; `EligibilityRulesCoverage` comments updated to reflect Phase F state
- `lib/api-mappers.ts` — `mapLegacyCoverageReport` maps `usersWithGapCount`
- `components/platform/legacy-coverage-card.tsx` — Phase F badge on eligibility card; `withBothPaths`/`legacyStringOnly` show "retired" pill at 0; new "Coverage gap" stat row in role assignments section
- `app/legacy-coverage/page.tsx` — info banner updated to emerald "Phase F complete" status; doc comment updated

### Build status after Step 5
- Identity.Api: ✅ 0 errors, 0 warnings
- control-center TypeScript: ✅ 0 errors (`npx tsc --noEmit` clean)

---

## Step 6 — Final Convergence and Relationship Activation

Analysis: `analysis/step6_final-convergence-and-relationship-activation.md`

### Phase A — OrganizationType as authoritative write model
- `Organization.Update()` now accepts optional `organizationTypeId` + `orgTypeCode`; delegates to `AssignOrganizationType()` keeping string and FK in sync
- **New:** `Identity.Domain/OrgTypeMapper.cs` — centralized `OrgType code ↔ OrganizationTypeId` mapping helper (`TryResolve`, `TryResolveCode`, `AllCodes`)

### Phase B — UserRoles eliminated from all read paths
- `AuthService.LoginAsync` — ScopedRoleAssignments (GLOBAL) is now primary role source; UserRoles is fallback-with-warning only
- `UserRepository.GetByIdWithRolesAsync` — ScopedRoleAssignments listed first; UserRoles retained with `TODO [Phase G]` marker
- `UserRepository.GetAllWithRolesAsync` — ScopedRoleAssignments Include added (was missing entirely)
- `AdminEndpoints.ListUsers` — role name from correlated ScopedRoleAssignment subquery (no UserRoles Include)
- `AdminEndpoints.GetUser` — filtered ScopedRoleAssignments Include replaces UserRoles Include
- `AdminEndpoints.ListRoles` — `userCount` from ScopedRoleAssignment count subquery
- `AdminEndpoints.GetRole` — `userCount` from async ScopedRoleAssignment count
- `AdminEndpoints.AssignRole` — existence check migrated to ScopedRoleAssignment

### Phase C — OrganizationRelationship in CareConnect workflows
- Confirmed **already complete**: `ReferralService` calls `HttpOrganizationRelationshipResolver` and sets `OrganizationRelationshipId`; `AppointmentService` denormalizes it from parent Referral. No code changes required.

### Phase D — Provider and Facility identity linkage
- `ProviderService.CreateAsync` — `LinkOrganization()` moved **before** `AddAsync`; eliminates the redundant second `UpdateAsync` call (aligns with FacilityService pattern)

### Phase E — Control Center minimal UI
- Confirmed **already complete**: all list pages (org types, relationship types, org relationships, product rules), API client methods, types, and routes already wired. No code changes required.

### Phase F — UserRoles retirement preparation
- All UserRoles write paths were marked `// TODO [Phase G — UserRoles Retirement]`: `UserRepository.AddAsync`, `AdminEndpoints.AssignRole`, `AdminEndpoints.RevokeRole`
- Full removal plan documented in analysis report (checklist of 14 items)
- All TODO markers resolved in Phase G (Step 7)

### Build status after Step 6
- Identity.Api: ✅ 0 errors, 0 warnings
- CareConnect.Api: ✅ 0 errors (1 pre-existing warning unrelated to Step 6)
- control-center TypeScript: ✅ 0 errors

---

## Step 7 — Phase G: UserRoles & UserRoleAssignment Table Retirement ✅

**Migration:** `20260330200004_PhaseG_DropUserRolesAndUserRoleAssignments`

### Completed actions
- **Deleted domain entities:** `UserRole.cs`, `UserRoleAssignment.cs`
- **Deleted EF configs:** `UserRoleConfiguration.cs`, `UserRoleAssignmentConfiguration.cs`
- **`User.cs` / `Role.cs` / `Organization.cs`:** Removed all `UserRoles` and `RoleAssignments` navigation collections
- **`IdentityDbContext.cs`:** Removed `UserRoles` + `UserRoleAssignments` DbSets and `OnModelCreating` registrations
- **`UserRepository.cs`:** Single `ScopedRoleAssignment` write in `AddAsync` (dual-write removed)
- **`AuthService.cs`:** Removed `UserRoles` fallback; sole role source is `ScopedRoleAssignments`
- **`UserService.ToResponse`:** Roles from `ScopedRoleAssignments` (GLOBAL, IsActive) — not `UserRoles`
- **`AdminEndpoints.AssignRole`:** Single SRA write only
- **`AdminEndpoints.RevokeRole`:** SRA deactivate only — no `UserRoles` teardown
- **`AdminEndpoints.GetLegacyCoverage`:** Phase G response shape; `userRolesRetired: true`, `dualWriteCoveragePct: 100.0`
- **`Program.cs`:** Startup diagnostic queries SRA counts; no `UserRoles` gap check
- **Model snapshot:** Entity, relationship, and navigation blocks for `UserRole` + `UserRoleAssignment` removed
- **New migration `200004`:** `DROP TABLE UserRoleAssignments; DROP TABLE UserRoles;`

### Build status after Step 7
- Identity.Api: ✅ 0 errors (verified with `dotnet build`)

---

## Step 8 — Phase H: Hardening Pass ✅

**Analysis doc:** `analysis/step8_hardening-pass.md`

### Completed actions

#### Identity backend
- **`Organization.Create()`:** Auto-resolves `OrganizationTypeId` via `OrgTypeMapper.TryResolve(orgType)` when not explicitly supplied
- **`JwtTokenService.cs`:** `org_type` JWT claim now derived from `OrgTypeMapper.TryResolveCode(org.OrganizationTypeId) ?? org.OrgType` (ID-first, string fallback)
- **`AuthService.LoginAsync`:** `orgTypeForResponse` derived from `OrgTypeMapper` (ID-first, string fallback)
- **`Identity.Api/Program.cs`:** Added check 3 — OrgType consistency diagnostic (warns on orgs with missing `OrganizationTypeId` or FK/string code mismatch)
- **`AdminEndpoints.cs`:** Added `GET /api/admin/platform-readiness` — cross-domain readiness summary (Phase G completion, OrgType consistency, ProductRole eligibility, org relationship stats)

#### CareConnect backend
- **`ProviderService.CreateAsync`:** Logs `Information` when `OrganizationId` not supplied (unlinked provider warning)
- **`FacilityService.CreateAsync`:** Logs `Information` when `OrganizationId` not supplied (unlinked facility warning)
- **`ReferralService`:** Added `ILogger<ReferralService>`; logs `Warning` when both org IDs supplied but no active `OrganizationRelationship` resolved
- **`CareConnect.Api/Program.cs`:** Added Phase H startup diagnostic — counts providers/facilities without Identity org link

#### Control Center (TypeScript)
- **`types/control-center.ts`:** `RoleAssignmentsCoverage` updated to Phase G shape (`userRolesRetired`, `usersWithScopedRoles`, `totalActiveScopedAssignments`); added `PlatformReadinessSummary` and sub-types
- **`lib/api-mappers.ts`:** `mapLegacyCoverageReport` roleAssignments updated to Phase G shape; added `mapPlatformReadiness`
- **`lib/api-client.ts`:** Added `platformReadiness: 'cc:platform-readiness'` to `CACHE_TAGS`
- **`lib/control-center-api.ts`:** Added `platformReadiness.get()` method
- **`components/platform/legacy-coverage-card.tsx`:** Renders Phase G SRA-only stats instead of deprecated dual-write fields

### Build status after Step 8
- Identity.Api: ✅ 0 errors, 0 warnings
- CareConnect.Api: ✅ 0 errors, 1 pre-existing warning (CS0168 in ExceptionHandlingMiddleware)
- control-center (tsc --noEmit): ✅ 0 errors

### Remaining Phase H / Phase I candidates
- Drop `Organization.OrgType` string column (all OrgType string fallback paths marked `// TODO [Phase H — remove OrgType string]`)
- Write backfill migration to populate `OrganizationTypeId` for any existing orgs with only an `OrgType` string

---

## Step 9 — Phase I: Identity Finalization and Relationship Enforcement ✅

**Analysis doc:** `analysis/step9_phase-i_identity-finalization-and-relationship-enforcement.md`

### Completed actions

#### Identity — OrganizationType as sole authoritative source
- **Migration `20260330200005_PhaseI_BackfillOrganizationTypeId`:** Data-only migration; backfills `OrganizationTypeId` from `OrgType` string for any existing org rows where FK was null. All five catalog GUIDs match OrgTypeMapper/SeedIds.
- **`Organization.Create(tenantId, name, Guid organizationTypeId, ...)`:** New overload accepting OrganizationTypeId as primary argument; OrgType derived from OrgTypeMapper (ID is the write authority for new callers).
- **`Organization.AssignOrganizationType`:** Strengthened catalog-consistency guard — when OrgTypeMapper resolves a code for the supplied ID, that catalog code always wins over caller-supplied string (prevents drift).
- **`AuthService.LoginAsync`:** Logs a `Warning` when `org.OrganizationTypeId` is null before product-role eligibility; after migration 200005 this path should never trigger.

#### Identity — Scoped Authorization Service (Phase I activation)
- **`IScopedAuthorizationService`** (`Identity.Application/Interfaces`): `HasOrganizationRoleAsync`, `HasProductRoleAsync`, `GetScopedRoleSummaryAsync`
- **`ScopedAuthorizationService`** (`Identity.Infrastructure/Services`): EF-backed implementation; GLOBAL scope always satisfies narrower scope checks
- **`ScopedRoleSummaryResponse` + `ScopedRoleEntry`** DTOs in `Identity.Application/DTOs`
- **DI registration** in `Identity.Infrastructure/DependencyInjection.cs`

#### Identity — Admin API extended for non-global scopes
- **`POST /api/admin/users/{id}/roles`:** Extended `AssignRoleRequest` to accept `ScopeType`, `OrganizationId`, `ProductId`, `OrganizationRelationshipId`. Scope-aware conflict check. Validates referenced entities exist. Backward compatible (scopeType defaults to GLOBAL).
- **`GET /api/admin/users/{id}/scoped-roles`:** New endpoint; returns all active SRAs per user grouped by scope type via `IScopedAuthorizationService`.
- **`GET /api/admin/platform-readiness`:** Extended with `scopedAssignmentsByScope: {global, organization, product, relationship, tenant}` section.

#### CareConnect — Admin Endpoints
- **`GET /api/admin/integrity`** (`CareConnectIntegrityEndpoints.cs`): Returns four integrity counters (referrals with org-pair but null relationship; appointments missing relationship where referral has one; providers/facilities without OrganizationId). Always returns 200; `-1` on individual query failure. `clean: true` when all counters are zero.
- **`PUT /api/admin/providers/{id}/link-organization`** (`ProviderAdminEndpoints.cs`): LSCC-002 backfill — sets `Provider.OrganizationId` to the supplied `organizationId`. Auth: `PlatformOrTenantAdmin`. Idempotent. Returns updated `ProviderResponse`.

#### Control Center (TypeScript)
- **`types/control-center.ts`:** Added `ScopedAssignmentsByScope` interface; extended `PlatformReadinessSummary` with `scopedAssignmentsByScope` field
- **`lib/api-mappers.ts`:** Extended `mapPlatformReadiness` to map `scopedAssignmentsByScope` section

### Build status after Step 9
- Identity.Domain: ✅ 0 errors
- Identity.Api: ✅ 0 errors, 0 warnings
- CareConnect.Api: ✅ 0 errors, 1 pre-existing warning (CS0168 in ExceptionHandlingMiddleware — unrelated)
- control-center (tsc --noEmit): ✅ 0 errors
- Runtime health: Identity `:5001/health` ✅, CareConnect `:5003/health` ✅

### Remaining optional future work
- Drop `Organization.OrgType` string column (add `NOT NULL` constraint to `OrganizationTypeId` first, then drop column, then remove JWT fallback path)
- CareConnect integrity counter repair tools (backfill referral/appointment relationship IDs; link providers/facilities to Identity orgs)
- JWT org-scoped roles claim for stateless org-scope enforcement
- RELATIONSHIP-scoped referral enforcement (configurable via `IdentityService:EnforceRelationshipOnReferral` appsetting)

---

## Step 10 — ClamAV Circuit Breaker (Documents Service) ✅

**Analysis doc:** `analysis/dotnet_circuit_breaker.md`

### Completed actions

#### New files
- **`Documents.Infrastructure/Scanner/CircuitBreakerScannerProvider.cs`** — Polly advanced circuit breaker decorator around `IFileScannerProvider`. State machine: CLOSED → OPEN → HALF-OPEN. Returns `ScanStatus.Failed` when circuit is open (fail-closed; never marks documents CLEAN without a real scan). Exposes `CircuitState` property for health check integration.

#### Modified files
- **`Documents.Infrastructure/Scanner/ClamAvFileScannerProvider.cs`** — Added `ClamAvCircuitBreakerOptions` class; added `CircuitBreaker` property to `ClamAvOptions`. Binds from `Scanner:ClamAv:CircuitBreaker` in appsettings.
- **`Documents.Infrastructure/Observability/ScanMetrics.cs`** — Added `ClamAvCircuitState` (Gauge, 0/1/2), `ClamAvCircuitOpenTotal` (Counter), `ClamAvCircuitShortCircuitTotal` (Counter).
- **`Documents.Infrastructure/Health/ClamAvHealthCheck.cs`** — Injected `IFileScannerProvider`; casts to `CircuitBreakerScannerProvider` to surface circuit state. OPEN circuit → immediate Degraded without TCP probe; HALF-OPEN → probe runs with `[circuit=half-open]` prefix in response description.
- **`Documents.Infrastructure/DependencyInjection.cs`** — Replaced scanner switch expression with a factory lambda that wraps `ClamAvFileScannerProvider` in `CircuitBreakerScannerProvider` when `Scanner:Provider=clamav`.
- **`Documents.Infrastructure/Documents.Infrastructure.csproj`** — Added `Polly` v7.2.4 package reference.
- **`Documents.Api/appsettings.json`** — Added `Scanner:ClamAv:CircuitBreaker` section with production-safe defaults (FailureThreshold=5, BreakDurationSeconds=30, SamplingDurationSeconds=60, MinimumThroughput=5).

### Design highlights
- Decorator pattern — circuit logic is 100% in the infrastructure layer; controllers, application services, and the scan worker are unchanged
- INFECTED result is never counted as a failure (it is a valid ScanResult, not an exception)
- Failure ratio = FailureThreshold / MinimumThroughput (5/5=1.0 = 100% failure rate across ≥5 calls → open)
- Worker's existing retry/backoff (`MaxRetryAttempts`, `InitialRetryDelaySeconds`, exponential cap) continues working unchanged

### Build status after Step 10
- Documents.Infrastructure: ✅ 0 errors, 0 warnings
- Documents.Api: ✅ 0 errors, 1 pre-existing warning (CS1998 in Program.cs — unrelated)

---

## Step 11 — Signature Freshness Monitoring + Large-File Policy (Documents Service) ✅

**Analysis doc:** `analysis/dotnet_phase2_signature_and_filesize.md`

### Completed actions

#### New files
- **`Documents.Infrastructure/Scanner/ClamAvSignatureFreshnessMonitor.cs`** — Singleton service that sends the `VERSION\n` TCP command to `clamd`, parses the response (`ClamAV <engine>/<db-version>/<db-date>`), and caches the result for 5 minutes. Exposes `GetSignatureInfoAsync()` returning a `ClamAvSignatureInfo` snapshot with `Success`, `RawVersion`, `EngineVersion`, `DbVersion`, `DbDate`, and `AgeHours`.
- **`Documents.Infrastructure/Health/ClamAvSignatureHealthCheck.cs`** — `IHealthCheck` that calls `ClamAvSignatureFreshnessMonitor`. Returns `Healthy` when age ≤ `SignatureMaxAgeHours`, `Degraded` when stale or unreachable. Observability-only — never blocks scans.
- **`Documents.Domain/Exceptions/FileTooLargeException.cs`** — Thrown when file exceeds upload limit (HTTP 413).
- **`Documents.Domain/Exceptions/FileSizeExceedsScanLimitException.cs`** — Thrown from `DocumentService` when file exceeds scan limit (HTTP 422).

#### Modified files
- **`Documents.Infrastructure/Scanner/ClamAvOptions.cs`** — Added `SignatureMaxAgeHours` (default 24) and `MaxScannableFileSizeMb` (default 25).
- **`Documents.Application/Options/DocumentServiceOptions.cs`** — Added `MaxUploadSizeMb` (default 25) and `MaxScannableFileSizeMb` (default 25).
- **`Documents.Infrastructure/Observability/ScanMetrics.cs`** — Added `UploadFileTooLargeTotal` (Counter) and `ScanSizeRejectedTotal` (Counter).
- **`Documents.Application/Services/DocumentService.cs`** — Added file-size guards in `CreateAsync` and `CreateVersionAsync`; throws `FileSizeExceedsScanLimitException` when file content exceeds `MaxScannableFileSizeMb`.
- **`Documents.Api/Endpoints/DocumentEndpoints.cs`** — Added early upload-size check at both upload endpoints (before body read). Returns HTTP 413 and increments `UploadFileTooLargeTotal`.
- **`Documents.Api/Middleware/ExceptionHandlingMiddleware.cs`** — Added catch handlers for `FileTooLargeException` (413) and `FileSizeExceedsScanLimitException` (422) with metric increments and structured JSON responses.
- **`Documents.Infrastructure/DependencyInjection.cs`** — Registered `ClamAvSignatureFreshnessMonitor` as singleton; added `ClamAvSignatureHealthCheck` to health checks (tag `"ready"`, `Degraded` failure status); added `ValidateFileSizeConfiguration()` startup validation (hard-fails if `MaxUploadSizeMb > MaxScannableFileSizeMb`; warns if app scan limit exceeds ClamAV's own limit).
- **`Documents.Api/appsettings.json`** — Added `Scanner:ClamAv:SignatureMaxAgeHours=24`, `Scanner:ClamAv:MaxScannableFileSizeMb=25`, `Documents:MaxUploadSizeMb=25`, `Documents:MaxScannableFileSizeMb=25`.

### Design highlights
- Three-layer file-size enforcement: HTTP endpoint (413) → `DocumentService` scan-limit guard (422) → `ExceptionHandlingMiddleware` (metric + JSON)
- Startup validation hard-fails if upload limit > scan limit (files would be accepted but never scannable — compliance gap)
- Freshness monitor is observability-only; stale signatures degrade health endpoint but never block uploads
- Logger for static endpoint class uses `ILoggerFactory.CreateLogger("DocumentEndpoints")` (static classes cannot be type arguments for `ILogger<T>`)

### Build status after Step 11
- Documents.Infrastructure: ✅ 0 errors, 0 warnings
- Documents.Api: ✅ 0 errors, 1 pre-existing warning (CS1998 in Program.cs — unrelated)

---

## Step 12 — Redis HA Readiness + Scan Completion Notifications (Documents Service) ✅

**Analysis doc:** `analysis/dotnet_phase3_redis_and_notifications.md`

### Completed actions

#### New files
- **`Documents.Domain/Events/DocumentScanCompletedEvent.cs`** — Immutable event record emitted on terminal scan outcomes. Carries: EventId, ServiceName, DocumentId, TenantId, VersionId?, ScanStatus, OccurredAt, CorrelationId?, AttemptCount, EngineVersion?, FileName. No file contents — identifiers only.
- **`Documents.Domain/Interfaces/IScanCompletionPublisher.cs`** — Publisher abstraction in Domain layer. `ValueTask PublishAsync(DocumentScanCompletedEvent, CancellationToken)`. Non-throwing contract.
- **`Documents.Infrastructure/Health/RedisHealthCheck.cs`** — `IHealthCheck` performing `db.PingAsync()`. Updates `docs_redis_healthy` gauge, increments `docs_redis_connection_failures_total` on failure. Tagged `"ready"` — registered only when `IConnectionMultiplexer` is in DI.
- **`Documents.Infrastructure/Observability/RedisMetrics.cs`** — New metrics file: `docs_redis_healthy` (Gauge), `docs_redis_connection_failures_total` (Counter), `docs_redis_stream_reclaims_total` (Counter), `docs_scan_completion_events_emitted_total` (Counter, label=status), `docs_scan_completion_delivery_success_total` (Counter), `docs_scan_completion_delivery_failures_total` (Counter).
- **`Documents.Infrastructure/Notifications/NotificationOptions.cs`** — Config POCOs: `NotificationOptions` → `ScanCompletionNotificationOptions` (Provider, Redis) → `RedisNotificationOptions` (Channel).
- **`Documents.Infrastructure/Notifications/NullScanCompletionPublisher.cs`** — No-op; used when `Provider=none`.
- **`Documents.Infrastructure/Notifications/LogScanCompletionPublisher.cs`** — Structured `ILogger.Information` message; default for dev/test. Zero external dependencies.
- **`Documents.Infrastructure/Notifications/RedisScanCompletionPublisher.cs`** — Publishes camelCase JSON payload to Redis Pub/Sub channel. Best-effort at-most-once. All exceptions caught internally.

#### Modified files
- **`Documents.Infrastructure/Scanner/RedisScanJobQueue.cs`** — `RedisStreamReclaims.Inc()` on XAUTOCLAIM hits (stale job recovery); `RedisConnectionFailures.Inc()` on XADD + XREADGROUP errors.
- **`Documents.Infrastructure/DependencyInjection.cs`** — Conditional `RedisHealthCheck` registration (only when `IConnectionMultiplexer` present); `NotificationOptions` config binding; `IScanCompletionPublisher` factory (none → Null, redis+active → Redis, else → Log); startup warning when `Provider=redis` but no active Redis connection.
- **`Documents.Api/Background/DocumentScanWorker.cs`** — Added `IScanCompletionPublisher _publisher` constructor param; `PublishCompletionEventAsync` private helper (non-throwing, belt-and-suspenders outer catch); event emission at all 3 terminal outcome paths: (1) max-retry-exceeded fast path, (2) normal scan result after ACK, (3) `RetryOrFailAsync` permanent-fail path.
- **`Documents.Api/appsettings.json`** — Added `Notifications:ScanCompletion:Provider=log` + `Redis:Channel=documents.scan.completed`.

### Design highlights
- Publisher lives in Domain layer → Application services can reference it in future without Infrastructure dependency
- Redis health check only activates when Redis is actually in use — does not pollute dev/memory-queue setups
- Notification delivery failures are logged + metered but never break scan pipeline (ACK precedes publish)
- Three-level non-throwing: publisher catches its own errors + worker wrapper catches any escaping exceptions
- Pub/Sub delivery guarantee: at-most-once (ephemeral — subscribers must be connected at publish time); extension to Redis Streams at-least-once documented in analysis
- `docs_scan_completion_events_emitted_total{status}` enables per-outcome delivery rate calculation

### Build status after Step 12
- Documents.Domain: ✅ 0 errors, 0 warnings
- Documents.Infrastructure: ✅ 0 errors, 0 warnings
- Documents.Api: ✅ 0 errors, 1 pre-existing warning (CS1998 in Program.cs — unrelated)

---

## DB Schema Repair — Platform Foundation Migrations (2026-03-30)

### Root cause
Migrations `20260330110001`–`20260330200005` (Identity) and `20260330110001` (CareConnect) had
their IDs absent from `__EFMigrationsHistory` on the live RDS instance, so EF had never executed
their DDL. As a result, 9 tables/columns were missing, breaking login and CareConnect startup.

### Fix applied
A one-shot C# repair program connected directly to both RDS databases and executed all migration
SQL idempotently (CREATE TABLE IF NOT EXISTS, INFORMATION_SCHEMA-conditional ALTER/INDEX,
INSERT IGNORE, DROP TABLE IF EXISTS). After the DDL was confirmed correct, all 9 identity migration
IDs and 1 CareConnect migration ID were inserted into `__EFMigrationsHistory` to keep EF in sync.

### Objects created / corrected
**Identity DB:**
- `OrganizationTypes` table + seed (5 rows)
- `Organizations.OrganizationTypeId` column + index + backfill
- `RelationshipTypes` table + seed (6 rows)
- `OrganizationRelationships` table
- `ProductRelationshipTypeRules` table + seed (4 rows)
- `ProductOrganizationTypeRules` table + seed (7 rows)
- `ScopedRoleAssignments` table — 8 GLOBAL assignments backfilled from legacy tables
- `ProductRoles.EligibleOrgType` column dropped (Phase F retirement)
- `UserRoleAssignments` + `UserRoles` tables dropped (Phase G)

**CareConnect DB:**
- `Providers.OrganizationId` column + index
- `Facilities.OrganizationId` column + index
- `Referrals.OrganizationRelationshipId` column + index
- `Appointments.OrganizationRelationshipId` column + index

### Post-repair service health
- Gateway (5010) ✅ — Fund (5002) ✅ — Identity (5001) ✅ — CareConnect (5003) ✅
- Phase G diagnostics: 8 active GLOBAL ScopedRoleAssignments across 8 users ✅
- OrgType consistency: 3 active orgs, all consistent OrganizationTypeId ✅
- Login flow: no more `Table 'identity_db.ScopedRoleAssignments' doesn't exist` errors ✅

### Key file modified
- `apps/services/identity/Identity.Api/DesignTimeDbContextFactory.cs` — reads
  `ConnectionStrings__IdentityDb` env var instead of hardcoded localhost fallback

---

## Platform Audit/Event Service — Step 1 Scaffold (2026-03-30)

### Location
`apps/services/audit/`

### Purpose
Standalone, independently deployable, portable audit/event service. Ingests business, security,
access, administrative, and system activity from distributed systems, normalizes into a canonical
event model, and persists immutable tamper-evident records. Not tied to any product, tenant model,
UI, or identity provider.

### Port
`5007` (planned — not yet wired into gateway)

### Project structure
```
PlatformAuditEventService.csproj    .NET 8 Web API, single-project
Controllers/    HealthController (GET /HealthCheck), AuditEventsController (POST/GET)
Services/       IAuditEventService + AuditEventService
Repositories/   IAuditEventRepository + InMemoryAuditEventRepository (dev adapter)
Models/         AuditEvent (record), EventCategory, EventSeverity, EventOutcome
DTOs/           IngestAuditEventRequest, AuditEventResponse, ApiResponse<T>, PagedResult<T>
Validators/     IngestAuditEventRequestValidator (FluentValidation)
Middleware/     ExceptionMiddleware, CorrelationIdMiddleware
Utilities/      IntegrityHasher (HMAC-SHA256), AuditEventMapper, TraceIdAccessor
Data/           AuditEventDbContext (EF Core, InMemory placeholder)
Configuration/  AuditServiceOptions (IntegrityHmacKeyBase64, PersistenceProvider, MaxPageSize)
Jobs/           RetentionPolicyJob (placeholder)
Docs/           architecture_overview.md
Examples/       Sample ingestion payloads (minimal, full, security-failure)
analysis/       step1_scaffold.md
```

### Key design decisions
- `AuditEvent` is a `sealed record` — immutable, supports `with` expressions
- Append-only repository interface — no update or delete methods
- HMAC-SHA256 integrity hash over canonical pipe-delimited fields per record
- `ApiResponse<T>` envelope on all endpoints (success, data, message, traceId, errors)
- `ExceptionMiddleware` first in pipeline — catches all unhandled exceptions → structured JSON
- `CorrelationIdMiddleware` — reads/writes `X-Correlation-ID` header
- Serilog with bootstrap logger to capture startup errors
- InMemory persistence for scaffold; `AuditEventDbContext` ready for durable migration

### NuGet packages
Swashbuckle.AspNetCore 6.5.0 · FluentValidation.AspNetCore 11.3.0 · Serilog.AspNetCore 8.0.1 ·
Serilog.Sinks.Console 5.0.1 · Serilog.Enrichers.Environment 2.3.0 · Serilog.Enrichers.Thread 3.1.0 ·
Microsoft.EntityFrameworkCore 8.0.0 · Microsoft.EntityFrameworkCore.InMemory 8.0.0

### Build status — Step 1
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit/Event Service — Step 2 Configuration & DB Bootstrap (2026-03-30)

### New configuration classes (`Configuration/`)
| Class | Section key | Purpose |
|---|---|---|
| `AuditServiceOptions` (updated) | `AuditService` | ServiceName, Version, ExposeSwagger, AllowedCorsOrigins |
| `DatabaseOptions` (new) | `Database` | Provider (InMemory\|MySQL), ConnectionString, pool, timeouts, startup probe/migration flags |
| `IntegrityOptions` (new) | `Integrity` | HmacKeyBase64 (moved from AuditServiceOptions), Algorithm, VerifyOnRead |
| `IngestAuthOptions` (new) | `IngestAuth` | Mode (None\|ApiKey\|Bearer), ApiKey, AllowedSources |
| `QueryAuthOptions` (new) | `QueryAuth` | Mode, PlatformAdminRoles, TenantAdminRoles, EnforceTenantScope |
| `RetentionOptions` (new) | `Retention` | DefaultRetentionDays, CategoryOverrides, JobEnabled, cron |
| `ExportOptions` (new) | `Export` | Provider (None\|Local\|S3\|AzureBlob), formats, file settings |

### New data / repository files
- `Data/DesignTimeDbContextFactory.cs` — reads `ConnectionStrings__AuditEventDb` for `dotnet ef` CLI
- `Repositories/EfAuditEventRepository.cs` — Pomelo/MySQL `IDbContextFactory`-backed append-only repository

### Key `AuditEventDbContext` additions
- `UserAgent` varchar(500), `Metadata` text columns added
- 7 named indexes: tenant+time, source+eventType, category+severity+outcome, actorId, targetType+targetId, correlationId, ingestedAt

### Provider switching in Program.cs
```
Database:Provider=InMemory  →  UseInMemoryDatabase + InMemoryAuditEventRepository (Singleton)
Database:Provider=MySQL     →  UseMySql (Pomelo 8.0.0) + EfAuditEventRepository (Scoped)
                               + IDbContextFactory<AuditEventDbContext>
```

### Startup DB probe (non-fatal)
When `Database:VerifyConnectionOnStartup=true` (default): runs `CanConnectAsync()` with
`StartupProbeTimeoutSeconds` timeout; logs Warning on failure but does NOT crash the process.

### NuGet packages added
- `Pomelo.EntityFrameworkCore.MySql` 8.0.0
- `Microsoft.EntityFrameworkCore.Design` 8.0.0 (PrivateAssets=all)

### Build status — Step 2
- PlatformAuditEventService: ✅ 0 errors, 0 warnings (Release build)

---

## Platform Audit/Event Service — Step 3 Core Data Model (2026-03-30)

### Namespaces
- Entities: `PlatformAuditEventService.Entities` (files in `Models/Entities/`)
- Enums: `PlatformAuditEventService.Enums` (files in `Models/Enums/`)
- Existing static constant classes: `PlatformAuditEventService.Models` (preserved, no conflict)

### Entities
| Entity | Fields | Mutability | Purpose |
|---|---|---|---|
| `AuditEventRecord` | 38 | All `init` (append-only) | Canonical audit event persistence model |
| `AuditExportJob` | 12 | Identity fields `init`, lifecycle fields `set` | Async export job tracking |
| `IntegrityCheckpoint` | 7 | All `init` | Aggregate hash snapshot over a time window |
| `IngestSourceRegistration` | 6 | Identity fields `init`, IsActive/Notes `set` | Advisory source registry |

### Enums
| Enum | Values | Notes |
|---|---|---|
| `EventCategory` | 9 | Security, Access, Business, Administrative, System, Compliance, DataChange, Integration, Performance |
| `SeverityLevel` | 7 | Debug → Info → Notice → Warn → Error → Critical → Alert (numeric ordering) |
| `VisibilityScope` | 5 | Platform, Tenant, Organization, User, Internal |
| `ScopeType` | 6 | Global, Platform, Tenant, Organization, User, Service |
| `ActorType` | 7 | User, ServiceAccount, System, Api, Scheduler, Anonymous, Support |
| `ExportStatus` | 6 | Pending, Processing, Completed, Failed, Cancelled, Expired |

### Key design points
- `long Id` + `Guid AuditId/ExportId` pattern: DB-efficient surrogate PK + stable public identifier
- `DateTimeOffset` throughout (not `DateTime`) — preserves UTC offset, avoids `DateTimeKind` ambiguity
- All `AuditEventRecord` fields are `init`-only — append-only contract enforced at compiler level
- `PreviousHash` forms a scoped chain per (TenantId, SourceSystem) — avoids global write serialization
- JSON columns (BeforeJson, AfterJson, MetadataJson, TagsJson, FilterJson) stored as raw text — schema-agnostic
- `IntegrityCheckpoint.CheckpointType` is an open string — custom cadences without schema migrations
- `IngestSourceRegistration` is advisory only — does not gate ingestion; hooks for future per-source config

### Build status — Step 3
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit/Event Service — Step 4 DTOs (2026-03-30)

### Namespace layout
| Sub-namespace | Directory | Purpose |
|---|---|---|
| `DTOs.Ingest` | `DTOs/Ingest/` | Ingest request, batch, per-item result |
| `DTOs.Query` | `DTOs/Query/` | Filter request, record response, paginated response |
| `DTOs.Export` | `DTOs/Export/` | Export job creation and status |
| `DTOs.Integrity` | `DTOs/Integrity/` | Checkpoint read model |
| `DTOs` (existing) | `DTOs/` | ApiResponse&lt;T&gt;, PagedResult&lt;T&gt; — unchanged |

### New files (14)
**Ingest:** AuditEventScopeDto, AuditEventActorDto, AuditEventEntityDto, IngestAuditEventRequest, BatchIngestRequest, IngestItemResult, BatchIngestResponse  
**Query:** AuditEventQueryRequest, AuditEventActorResponseDto, AuditEventEntityResponseDto, AuditEventScopeResponseDto, AuditEventRecordResponse, AuditEventQueryResponse  
**Export:** ExportRequest, ExportStatusResponse  
**Integrity:** IntegrityCheckpointResponse

### Key design notes
- Existing root DTOs preserved — still used by old AuditEvent service layer
- IngestAuditEventRequest uses nested Scope/Actor/Entity objects (vs. flat old version)
- All categorical fields use typed enums from `PlatformAuditEventService.Enums` — requires `JsonStringEnumConverter` in Program.cs
- `BatchIngestResponse.HasErrors` + `ExportStatusResponse.IsTerminal`/`IsAvailable` are computed convenience properties
- `AuditEventQueryResponse` includes `EarliestOccurredAtUtc`/`LatestOccurredAtUtc` for UI time-range rendering
- `IntegrityCheckpointResponse.IsValid` is nullable (null=never verified, true=clean, false=tamper detected)
- Field naming conventions: DTO uses `Before`/`After`/`Metadata`/`Visibility`; entity uses `BeforeJson`/`AfterJson`/`MetadataJson`/`VisibilityScope`

### Pending (Step 5)
- Register `JsonStringEnumConverter` globally in `Program.cs`
- FluentValidation for `DTOs.Ingest.IngestAuditEventRequest`, `BatchIngestRequest`, `ExportRequest`
- Mapper: `IngestAuditEventRequest` → `AuditEventRecord` (flatten nested objects, handle Guid parse, Tags serialization)
- Controller wiring to new DTOs

### Build status — Step 4
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit/Event Service — Step 5 EF Core Mappings (2026-03-30)

### Architecture change
DbContext now uses `ApplyConfigurationsFromAssembly` — new entity configurations are auto-discovered from `Data/Configurations/`. The inline `OnModelCreating` block is eliminated; all config lives in separate `IEntityTypeConfiguration<T>` classes.

### Configuration files (new)
| File | Entity | Table |
|---|---|---|
| `AuditEventRecordConfiguration.cs` | AuditEventRecord | `AuditEventRecords` |
| `AuditExportJobConfiguration.cs` | AuditExportJob | `AuditExportJobs` |
| `IntegrityCheckpointConfiguration.cs` | IntegrityCheckpoint | `IntegrityCheckpoints` |
| `IngestSourceRegistrationConfiguration.cs` | IngestSourceRegistration | `IngestSourceRegistrations` |
| `LegacyAuditEventConfiguration.cs` | AuditEvent (legacy) | `AuditEvents` (unchanged) |

### New DbSet properties on AuditEventDbContext
`AuditEventRecords`, `AuditExportJobs`, `IntegrityCheckpoints`, `IngestSourceRegistrations`

### Column type conventions
- Surrogate PK: `bigint` AUTO_INCREMENT
- Public Guid identifiers: `char(36)`, UNIQUE constraint
- Enums: `tinyint` with `HasConversion<int>()` — stable int backing values, compact, range-comparable
- DateTimeOffset: `datetime(6)` UTC — microsecond precision; Pomelo strips offset on write
- JSON fields: `mediumtext` for BeforeJson/AfterJson (up to 16 MB); `text` for others
- Bool: `tinyint(1)` (Pomelo default)

### Index counts
- AuditEventRecords: 16 indexes (13 required + 3 composite high-traffic patterns)
- AuditExportJobs: 6 indexes
- IntegrityCheckpoints: 4 indexes
- IngestSourceRegistrations: 2 indexes

### Key constraints
- IdempotencyKey UNIQUE with NULLs allowed — MySQL 8 treats each NULL as distinct in UNIQUE index
- (SourceSystem, SourceService) UNIQUE — NULLs allowed (NULL SourceService = "all services")
- No HasDefaultValueSql on required audit fields — values must come from ingest pipeline

### Build status — Step 5
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

## Platform Audit/Event Service — Step 6 EF Core Migrations (2026-03-30)

### DesignTimeDbContextFactory fix
Replaced `ServerVersion.AutoDetect(connectionString)` (requires live MySQL) with `new MySqlServerVersion(new Version(8, 0, 0))` — migration generation now works fully offline without a database connection.

### Migration generated
- `Data/Migrations/20260330140138_InitialSchema.cs` — creates 4 new tables, all indexes
- `AuditEventDbContextModelSnapshot.cs` — EF model snapshot tracking all 5 entities
- `analysis/deploy_InitialSchema_idempotent.sql` — idempotent SQL script for production deployment

### AuditEvents exclusion strategy
The legacy `AuditEvents` table is tracked in the EF model snapshot (so the ORM knows about it) but is intentionally **excluded from the migration `Up()`/`Down()` methods** — it pre-exists in production databases and was not created by this service. For fresh databases, the table must be created separately before this migration is applied.

### Tables created by InitialSchema
| Table | PK | Public ID | Notes |
|---|---|---|---|
| `AuditEventRecords` | bigint AI | `AuditId` char(36) UNIQUE | 16 indexes; mediumtext for JSON fields |
| `AuditExportJobs` | bigint AI | `ExportId` char(36) UNIQUE | 6 indexes |
| `IntegrityCheckpoints` | bigint AI | — | 4 indexes |
| `IngestSourceRegistrations` | bigint AI | — | 2 indexes; (SourceSystem, SourceService) UNIQUE |

### Production deployment
- **Build:** `scripts/build-prod.sh` — cleans `.next` directories before building (prevents stale dev cache from causing hydration/hook errors), builds both Next.js apps and all .NET services (including Liens) in Release mode. Post-build cleanup removes `.git` (~3.3GB), pnpm store (~2.1GB), NuGet cache (~232MB), Replit agent state (~638MB), `_archived`, `.NET obj/Debug` dirs, test artifacts, analysis/exports/downloads to keep the deployment image under 8GB.
- **Run:** `scripts/run-prod.sh` — starts web (port 3050 internal → 5000 proxy), control center (port 5004), gateway (port 5010), all .NET services (including notifications on port 5008, liens on port 5009), artifacts server (port 5020). Includes fallback build block for all services including Liens.
- **CareConnect internal provisioning:** Identity service calls CareConnect on port 5003 (fallback in `DependencyInjection.cs`; override via `CareConnect:InternalUrl` config)
- **Documents `appsettings.Production.json`:** Sets `Storage:Provider` to `s3` (AWS S3, persistent). S3 credentials read from env vars (`AWS_S3_BUCKET_NAME`, `AWS_S3_REGION`, `AWS_S3_ACCESS_KEY_ID`, `AWS_S3_SECRET_ACCESS_KEY`). Database and local filesystem providers available as fallback options.
```bash
# Idempotent SQL (safe to run multiple times):
dotnet ef migrations script --idempotent -o migration.sql
# Apply:
ConnectionStrings__AuditEventDb="..." dotnet ef database update
```

### Build status — Step 6
- PlatformAuditEventService: ✅ 0 errors, 0 warnings (migration compiles cleanly)

## Platform Audit/Event Service — Step 7 Repositories + Mapper (2026-03-30)

### JsonStringEnumConverter (Program.cs)
`AddControllers().AddJsonOptions(...)` now globally registers `JsonStringEnumConverter` — all typed enums (`EventCategory`, `SeverityLevel`, `ActorType`, `ScopeType`, `VisibilityScope`, `ExportStatus`) serialize as strings in both requests and responses.

### AuditEventRecordMapper
`Mappers/AuditEventRecordMapper.cs` — static class, no DI needed. Maps `IngestAuditEventRequest` → `AuditEventRecord`:
- `AuditId` = `Guid.NewGuid()` (TODO: upgrade to UUIDv7)
- `PlatformId` parsed from `Scope.PlatformId` string → `Guid?`
- `TagsJson` serialized from `Tags` list → compact JSON array string
- `Hash`/`PreviousHash` left `null` — populated by ingest service after idempotency check

### New repository interfaces (4)
| Interface | Methods |
|---|---|
| `IAuditEventRecordRepository` | AppendAsync, GetByAuditIdAsync, ExistsIdempotencyKeyAsync, QueryAsync, CountAsync, GetLatestInChainAsync |
| `IAuditExportJobRepository` | CreateAsync, GetByExportIdAsync, UpdateAsync, ListByRequesterAsync, ListActiveAsync |
| `IIntegrityCheckpointRepository` | AppendAsync, GetByIdAsync, GetLatestAsync, GetByWindowAsync, ListByTypeAsync |
| `IIngestSourceRegistrationRepository` | UpsertAsync, GetBySourceAsync, ListActiveAsync, ListAllAsync, SetActiveAsync |

### New EF implementations (4)
All use `IDbContextFactory<AuditEventDbContext>` (short-lived contexts per operation). Registered in DI as `AddScoped` — work for both MySQL and InMemory providers.

### Namespace disambiguation
Both `PlatformAuditEventService.DTOs.AuditEventQueryRequest` (legacy) and `PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest` (new) exist. The record repository files use a `using AuditRecordQueryRequest = ...` alias to avoid CS0104 ambiguous reference.

### Build status — Step 7
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit Service — Step 10: Tamper-Evident Hashing ✅

**Analysis doc:** `analysis/step10_hashing.md`
**Integrity spec:** `apps/services/audit/Docs/integrity-model.md`

### Key design gap fixed

`PreviousHash` was stored on each record (linked-list pointer) but was NOT included in the canonical hash payload. `Hash(N)` did not depend on `Hash(N-1)`. The chain was a linked list, not a cryptographic chain.

After Step 10: `PreviousHash` is position 10 in the canonical field set, so `Hash(N) = f(canonical_fields(N) + Hash(N-1))`. Modifying any record now invalidates all subsequent hashes.

### `AuditRecordHasher.cs` — full rewrite

**Two-stage pipeline (payload builder separated from hash function):**

```
Stage 1 — BuildPayload()       public, deterministic, no crypto
Stage 2 — ComputeSha256()      public, keyless SHA-256
          ComputeHmacSha256()  public, HMAC-SHA256 with secret
```

**Canonical field order (fixed, breaking to change):**
```
AuditId | EventType | SourceSystem | TenantId | ActorId |
EntityType | EntityId | Action | OccurredAtUtc | RecordedAtUtc | PreviousHash
```

**`BuildPayload(AuditEventRecord record)` overload** — rebuilds payload from persisted record including `record.PreviousHash`; used by `Verify()` on read.

**`Verify(record, algorithm, hmacSecret?)`** — constant-time `FixedTimeEquals` comparison; supports both `SHA-256` and `HMAC-SHA256`; returns false for null Hash, unknown algorithm, or missing HMAC secret.

### `AuditEventIngestionService.cs` — pipeline update

New fields: `_algorithm`, `_signingEnabled`.

Signing enabled when:
- `Algorithm = "SHA-256"` → always (keyless, portable)
- `Algorithm = "HMAC-SHA256"` → only when `HmacKeyBase64` is set (silent skip in dev)

**Step 3 guard:** now uses `_signingEnabled` (not `_hmacSecret is not null`)

**Step 4 — new call sequence:**
```csharp
payload = AuditRecordHasher.BuildPayload(..., previousHash: previousHash)
hash    = algorithm == "SHA-256"
          ? ComputeSha256(payload)
          : ComputeHmacSha256(payload, _hmacSecret!)
```

Constructor logs `"Audit integrity signing ENABLED — Algorithm=..."` or a `Warning` when disabled.

### `IntegrityOptions.cs`

- `Algorithm` property now documents `"SHA-256"` and `"HMAC-SHA256"` with activation rules.

### `appsettings.Development.json`

- Added explicit `Algorithm: HMAC-SHA256` for clarity.

### Algorithm support matrix

| Algorithm     | Key required | Integrity | Authentication |
|---------------|-------------|-----------|----------------|
| `SHA-256`     | No          | ✓         | ✗              |
| `HMAC-SHA256` | Yes         | ✓         | ✓              |

### Build status after Step 10
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit Service — Step 11: Ingestion API Layer ✅

**Analysis doc:** `analysis/step11_ingest_api.md`

### New file: `Controllers/AuditEventIngestController.cs`

Route prefix: `/internal/audit` — machine-to-machine, internal source systems only.

**Endpoints:**

| Method | Path | Action |
|--------|------|--------|
| POST | `/internal/audit/events` | `IngestSingle` — single event ingest |
| POST | `/internal/audit/events/batch` | `IngestBatch` — batch event ingest (1–500 events) |

**Dependencies injected:**
- `IAuditEventIngestionService` — full ingest pipeline (idempotency, hashing, chain, persist)
- `IValidator<IngestAuditEventRequest>` — structural validation for single endpoint
- `IValidator<BatchIngestRequest>` — structural + per-item validation for batch endpoint
- `ILogger<AuditEventIngestController>` — debug logging on validation failure

### Status code matrix

**Single endpoint (`POST /internal/audit/events`):**

| Code | Trigger |
|------|---------|
| 201 Created | `IngestItemResult.Accepted = true` — AuditId in body, Location header set |
| 400 Bad Request | FluentValidation failed before service call |
| 409 Conflict | `RejectionReason = "DuplicateIdempotencyKey"` |
| 503 Service Unavailable | `RejectionReason = "PersistenceError"` — retry with backoff |
| 422 Unprocessable Entity | Unknown rejection reason |

**Batch endpoint (`POST /internal/audit/events/batch`):**

| Code | Trigger |
|------|---------|
| 200 OK | All events accepted |
| 207 Multi-Status | Some accepted, some rejected — inspect per-item `Results` |
| 400 Bad Request | Outer validator failed (batch shape or per-item structural errors with `Events[n].Field` prefix) |
| 422 Unprocessable Entity | Zero events accepted |

Body shape is `ApiResponse<BatchIngestResponse>` for 200/207/422 — always inspect `Results`.

### Swagger updates

- `PlatformAuditEventService.csproj`: `GenerateDocumentationFile=true` + `NoWarn 1591`
- `Program.cs`: `IncludeXmlComments()` wired; Swagger description updated with endpoint group index
- XML doc comments (`<summary>`, `<response>`) on both actions surface in Swagger UI
- Pre-existing malformed XML cref warnings fixed: `ExportStatus.cs`, `LegacyAuditEventConfiguration.cs`, `AuditEventIngestionService.IngestOneAsync`

### Build status after Step 11
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit Service — Step 12: Service-to-Service Ingest Auth ✅

**Analysis doc:** `analysis/step12_ingest_auth.md`
**Operator reference:** `apps/services/audit/Docs/ingest-auth.md`

### Design

- **`IIngestAuthenticator`** — pluggable auth interface. One implementation per mode.
- **`ServiceTokenAuthenticator`** — ServiceToken mode; constant-time registry scan; per-service named tokens.
- **`NullIngestAuthenticator`** — None mode; dev pass-through; always accepted.
- **`IngestAuthMiddleware`** — path-scoped to `/internal/audit/*`; delegates to authenticator; short-circuits with 401/403; stores `ServiceAuthContext` in `HttpContext.Items`.
- **`ServiceAuthContext`** — read-only identity carrier available to controllers post-auth.
- **`IngestAuthHeaders`** — centralized header name constants (`x-service-token`, `x-source-system`, `x-source-service`).

### Headers

| Header | Mode | Purpose |
|--------|------|---------|
| `x-service-token` | ServiceToken — required | Shared secret credential |
| `x-source-system` | Optional | Logging + allowlist enforcement |
| `x-source-service` | Optional | Logging only |

### Modes

| Mode | Implementation | When |
|------|---------------|------|
| `"None"` | `NullIngestAuthenticator` | Development/test only |
| `"ServiceToken"` | `ServiceTokenAuthenticator` | Staging + production |
| `"Bearer"` | (planned) | JWT / OIDC |
| `"MtlsHeader"` | (planned) | Proxy-forwarded client cert |
| `"MeshInternal"` | (planned) | Istio/Linkerd SPIFFE |

### Security properties

- Constant-time comparison via `CryptographicOperations.FixedTimeEquals`
- Full-registry scan (no early exit) — response time independent of match position
- Length normalization before comparison — prevents token length timing leak
- Per-service revocation (`Enabled: false` on individual entries)
- Per-service token rotation (add new → deploy → remove old)
- Startup WARNING when Mode=None or registry is empty

### Extension path (adding JWT)

1. Implement `IIngestAuthenticator` in `JwtIngestAuthenticator`
2. Register singleton + add `"Bearer"` case to the factory switch in `Program.cs`
3. No middleware, controller, or validator changes needed

### `appsettings.json` additions

- `ServiceTokens: []` (named token registry)
- `RequireSourceSystemHeader: false`
- `AllowedSources: []`

### `appsettings.Development.json`

- Three dev token entries (identity-service, fund-service, care-connect-api) — Mode remains `"None"` so tokens are unused in development but wired for testing

### Files created

`Configuration/ServiceTokenEntry.cs`, `Services/IIngestAuthenticator.cs`, `Services/AuthResult` (inside interface file), `Services/ServiceAuthContext.cs`, `Services/IngestAuthHeaders.cs`, `Services/NullIngestAuthenticator.cs`, `Services/ServiceTokenAuthenticator.cs`, `Middleware/IngestAuthMiddleware.cs`

### Files updated

`Configuration/IngestAuthOptions.cs` (new fields + mode docs), `Program.cs` (DI + middleware), `appsettings.json`, `appsettings.Development.json`, `Docs/ingest-auth.md` (new), `README.md` (rewritten)

### Build status after Step 12
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit Service — Step 13: Query Services and Retrieval APIs ✅

**Analysis doc:** `analysis/step13_query_api.md`

### Endpoints (controller: `/audit`)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/audit/events` | Full filtered, paginated query |
| `GET` | `/audit/events/{auditId}` | Single record by stable AuditId |
| `GET` | `/audit/entity/{entityType}/{entityId}` | Events for a specific resource |
| `GET` | `/audit/actor/{actorId}` | Events by a specific actor |
| `GET` | `/audit/user/{userId}` | User events (actorType=User enforced) |
| `GET` | `/audit/tenant/{tenantId}` | Events for a tenant |
| `GET` | `/audit/organization/{organizationId}` | Events for an organization |

### Scoped endpoint pattern
Path segment takes precedence over matching query-string param. All scoped endpoints accept additional `[FromQuery] AuditEventQueryRequest` parameters.

### Filters added in Step 13 (to `AuditEventQueryRequest`)
- `SourceEnvironment` (string?) — exact match
- `RequestId` (string?) — exact match
- `Visibility` (VisibilityScope?) — exact match; takes precedence over `MaxVisibility`

### Pagination
- `page` (1-based), `pageSize` (default 50, capped by `QueryAuth:MaxPageSize`), `sortBy`, `sortDescending`
- Response includes `totalCount`, `totalPages`, `hasNext`, `hasPrev`, `earliestOccurredAtUtc`, `latestOccurredAtUtc`

### Time-range metadata
`AuditEventQueryService` issues the paginated query and a `GROUP BY 1` aggregate (min/max `OccurredAtUtc`) in parallel, giving accurate time-range metadata without extra sequential round-trips.

### Key types

- **`AuditEventRecordMapper`** — `Mapping/` — static mapper: `AuditEventRecord` → `AuditEventRecordResponse`. Hash exposed conditionally. Tags deserialized from `TagsJson`. Network identifiers redactable.
- **`IAuditEventQueryService`** / **`AuditEventQueryService`** — `Services/` — read-only pipeline. Enforces `QueryAuth:MaxPageSize`, maps entities → DTOs.
- **`AuditEventQueryController`** — `Controllers/` — 7 GET endpoints.

### Files created
`Mapping/AuditEventRecordMapper.cs`, `Services/IAuditEventQueryService.cs`, `Services/AuditEventQueryService.cs`, `Controllers/AuditEventQueryController.cs`, `analysis/step13_query_api.md`

### Files modified
`DTOs/Query/AuditEventQueryRequest.cs` (3 new fields), `Repositories/IAuditEventRecordRepository.cs` (`GetOccurredAtRangeAsync`), `Repositories/EfAuditEventRecordRepository.cs` (new filter predicates + aggregate method), `Program.cs` (service registration + Swagger description)

### Build status after Step 13
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit Service — Step 14: Query Authorization Foundations ✅

**10 new files, 5 files updated. 0 errors, 0 warnings.**

### CallerScope enum (6 values, ordered by privilege)
`Unknown(0)` → `UserSelf(1)` → `TenantUser(2)` → `Restricted(3)` → `OrganizationAdmin(4)` → `TenantAdmin(5)` → `PlatformAdmin(6)`

### Authorization pipeline
- **`QueryAuthMiddleware`** — path-scoped to `/audit/*`; resolves caller context; issues 401 when Mode≠None and caller is unresolved
- **`IQueryCallerResolver`** — contract; `AnonymousCallerResolver` (Mode=None, dev only) and `ClaimsCallerResolver` (Mode=Bearer, reads `HttpContext.User.Claims`)
- **`IQueryAuthorizer` / `QueryAuthorizer`** — Phase 1: access check (cross-tenant, unknown scope, self-scope without UserId); Phase 2: constraint application (overrides TenantId, OrgId, ActorId, MaxVisibility)
- **`QueryCallerContext`** — immutable record stored in `HttpContext.Items`; factory helpers `Anonymous()`, `Authenticated()`, `Failed()`
- **`QueryAuthorizationResult`** — carries IsAuthorized, DenialReason, StatusCode

### Configuration additions to `QueryAuthOptions`
`OrganizationAdminRoles`, `RestrictedRoles`, `TenantUserRoles`, `UserSelfRoles`, `TenantIdClaimType`, `OrganizationIdClaimType`, `UserIdClaimType`, `RoleClaimType`

### Provider-neutral design
All claim type names are config-driven. Switching from Auth0 → Entra ID → Keycloak requires only appsettings changes, not code changes.

### Build status after Step 14
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit Service — Step 15: Integrity Checkpoint Support ✅

**7 new files, 5 files updated. 0 errors, 0 warnings.**

### Checkpoint generation algorithm
1. Stream `Hash` values from `AuditEventRecord` rows where `RecordedAtUtc ∈ [from, to)`, ordered by `Id` ASC.
2. Concatenate hashes in order; null hashes → empty string (preserves positional count accuracy).
3. Apply configured algorithm (HMAC-SHA256 or SHA-256 fallback) to concatenated string.
4. Persist as `IntegrityCheckpoint` (append-only, never updated).

### New endpoints
- `GET  /audit/integrity/checkpoints` — paginated list; optional `type`, `from`, `to` filters; requires TenantAdmin+ scope
- `POST /audit/integrity/checkpoints/generate` — on-demand generation; requires PlatformAdmin scope; returns HTTP 201

### New services / jobs
- **`IIntegrityCheckpointService` / `IntegrityCheckpointService`** — streaming hash aggregation + persistence
- **`IntegrityCheckpointJob`** — placeholder for scheduled generation (Quartz.NET / BackgroundService pattern documented)

### New repository methods
- `IAuditEventRecordRepository.StreamHashesForWindowAsync(from, to)` — projects only `Hash` field for efficiency
- `IIntegrityCheckpointRepository.ListAsync(type?, from?, to?, page, pageSize)` — multi-filter paginated list

### Build status after Step 15
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit Service — Step 16: Export Capability ✅

**8 new files, 5 files updated. 0 errors, 0 warnings.**

### Endpoints
- `POST /audit/exports` — Submit export job; processes synchronously in v1; returns 202 with terminal status
- `GET  /audit/exports/{exportId}` — Poll job status (immediate in v1; designed for async in future)

### Output formats
- **JSON** — Full envelope `{ exportId, exportedAtUtc, format, records:[...] }`; camelCase, null fields omitted
- **NDJSON** — One JSON object per line, no envelope; best for streaming data pipelines
- **CSV** — RFC 4180 header + flat rows; nested JSON fields inlined as strings

### Conditional field groups (per-request flags)
| Flag | Fields controlled |
|---|---|
| `includeStateSnapshots` | `beforeJson`, `afterJson` |
| `includeHashes` | `hash`, `previousHash` (also requires `QueryAuth:ExposeIntegrityHash=true`) |
| `includeTags` | `tags` |

### Job lifecycle
`Pending → Processing → Completed | Failed` — all transitions happen within the POST request in v1. Terminal state is returned in the response. GET endpoint is ready for async polling in future releases.

### Storage abstraction
`IExportStorageProvider` → `LocalExportStorageProvider` (v1). Swap to `S3ExportStorageProvider` / `AzureBlobExportStorageProvider` by registering a different implementation in Program.cs — no other changes needed.

### Authorization
Delegates to `IQueryAuthorizer` — same scope constraints as query endpoints. TenantAdmin can export their tenant; PlatformAdmin can export any scope; cross-tenant requests denied.

### Entity change: `AuditExportJob.RecordCount`
Added nullable `long? RecordCount` to track the number of records written. EF configuration and `UpdateAsync` selective-update pattern both updated.

### Build status after Step 16
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Platform Audit Service — Step 17: Retention and Archival Foundations ✅

**11 new files, 7 files updated. 0 errors, 0 warnings.**

### What was built

**Storage tier model** (`StorageTier` enum): Hot / Warm / Cold / Indefinite / LegalHold — five tiers classify where a record sits in its lifecycle.

**Retention policy engine** (`IRetentionService` / `RetentionService`): all methods read-only.
- `ResolveRetentionDays(record)` — applies priority chain: per-tenant > per-category > default
- `ComputeExpirationDate(record)` — `RecordedAtUtc + days`, or null for indefinite
- `ClassifyTier(record)` — returns StorageTier based on record age vs. configured windows
- `EvaluateAsync(request, ct)` — samples up to `SampleLimit` oldest records; returns tier counts, expired-by-category breakdown, oldest record timestamp, policy summary. Always dry-run in v1.
- `BuildPolicySummary()` — human-readable policy string for logs and evaluation results

**Archival provider abstraction** (`IArchivalProvider` → `NoOpArchivalProvider`): mirrors export provider pattern. Streams records to count them, logs what would be archived, writes nothing.
- `ArchivalContext` — carries job metadata (jobId, window, tenantId, initiator)
- `ArchivalResult` — structured result (recordsProcessed, archived, destination, success/error)
- `ArchivalStrategy` enum — None / NoOp / LocalCopy / S3 / AzureBlob
- `ArchivalOptions` config — all provider-specific keys pre-defined

**Evaluation DTOs**: `RetentionEvaluationRequest` (tenantId, category, sampleLimit) + `RetentionEvaluationResult` (tier counts, expired-by-category, oldest record, policy summary, isDryRun)

**Retention policy job** (`RetentionPolicyJob`): replaced placeholder with structured evaluation + Warning logs for Cold-tier records + forward guidance to activate archival.

**Config changes**: `RetentionOptions` gains `HotRetentionDays` (365), `DryRun` (true), `LegalHoldEnabled` (false). New `ArchivalOptions` section with all provider keys. Both appsettings files updated.

### Key design decisions

**Evaluation-only (DryRun=true default)** — Audit record deletion cannot be undone. The safe default lets operators observe tier distributions in production before enabling deletion.

**NoOpArchivalProvider** — Wires the full DI graph and validates tier classification without any storage risk. First step to validating the pipeline before activating a real backend.

**Sample-based evaluation** — Queries the N oldest records (oldest-first, capped at `SampleLimit`). Focuses on the records most likely to be expired. `CountAsync` gives the live total without a full-table scan.

**Legal hold as a documented future extension** — `LegalHold` tier and `LegalHoldEnabled` config key defined; no per-record hold tracking in v1. Implementation spec documented in Docs/retention-and-archival.md and analysis/step17_retention.md.

### New files

| File | Role |
|---|---|
| `Models/Enums/StorageTier.cs` | 5-tier storage classification enum |
| `Models/Enums/ArchivalStrategy.cs` | Archival backend enum |
| `Configuration/ArchivalOptions.cs` | `Archival:*` config class |
| `Services/Archival/IArchivalProvider.cs` | Storage abstraction interface |
| `Services/Archival/ArchivalContext.cs` | Job metadata carrier |
| `Services/Archival/ArchivalResult.cs` | Archival operation result |
| `Services/Archival/NoOpArchivalProvider.cs` | v1 no-op provider |
| `Services/IRetentionService.cs` | Retention service contract |
| `Services/RetentionService.cs` | Full evaluation logic |
| `DTOs/Retention/RetentionEvaluationRequest.cs` | Evaluation input DTO |
| `DTOs/Retention/RetentionEvaluationResult.cs` | Evaluation output DTO |
| `Docs/retention-and-archival.md` | Operator reference |
| `analysis/step17_retention.md` | Implementation analysis + production hardening backlog |

### Startup log

```
[WRN] Retention:JobEnabled = false — retention policy job is inactive.
      Set Retention:JobEnabled=true and configure a scheduler to activate.
```

### Build status after Step 17
- PlatformAuditEventService: ✅ 0 errors, 0 warnings

---

## Control Center Admin Refresh ✅

**Scope:** Full admin dashboard overhaul — infrastructure layer + new pages + sidebar badges.

### Infrastructure layer (all additive)

#### `types/control-center.ts`
- Added `CareConnectIntegrityReport` interface (generatedAtUtc, clean, referrals/appointments/providers/facilities counters; -1 = query failure)
- Added `ScopedRoleAssignment` interface (per-user Phase G SRA record)

#### `types/index.ts`
- Added `badge?: 'LIVE' | 'MOCKUP' | 'IN PROGRESS'` to `NavItem`

#### `lib/api-client.ts`
- Added `ccIntegrity: 'cc:careconnect-integrity'` to `CACHE_TAGS`

#### `lib/api-mappers.ts`
- Added `mapCareConnectIntegrity(raw)` — preserves -1 values for failed queries
- Added `mapScopedRoleAssignment(raw)` — snake_case and camelCase both handled

#### `lib/control-center-api.ts`
- Added `careConnectIntegrity.get()` — GET `/careconnect/api/admin/integrity`, 10 s cache, `cc:careconnect-integrity` tag
- Added `scopedRoles.getByUser(userId)` — GET `/identity/api/admin/users/{id}/scoped-roles`, 30 s cache

### Navigation layer

#### `lib/routes.ts`
- Added `dashboard`, `platformReadiness`, `scopedRoles`, `careConnectIntegrity`, `domains` routes
- Ordered: overview → platform → identity → relationships → product rules → careconnect → operations → catalog → system

#### `lib/nav.ts`
- Full rewrite: 10 nav sections; badge annotations: `Scoped Roles` (MOCKUP), `Tenant Domains` (MOCKUP), `Products` (MOCKUP), `Monitoring` (IN PROGRESS), all others unlabelled (LIVE by implication)

#### `components/shell/cc-sidebar.tsx`
- Added `NavBadge` pill sub-component (LIVE=emerald, IN PROGRESS=amber, MOCKUP=gray)
- Nav items now render badge pill in expanded mode only (`item.badge && <NavBadge />`)

### New components
- **`components/platform/platform-readiness-card.tsx`** — full breakdown: Phase G, OrgType coverage bar, ProductRole eligibility bar, org relationship counts, SRA by scope type. Coverage bars colour: ≥90% green, ≥60% amber, else red.
- **`components/careconnect/integrity-report-card.tsx`** — four counters with LIVE status labels. -1 renders "query failed" pill. Remediation callout when issues exist.

### New pages
- **`/platform-readiness`** (LIVE) — pulls `controlCenterServerApi.platformReadiness.get()`, renders `PlatformReadinessCard`
- **`/careconnect-integrity`** (LIVE) — pulls `controlCenterServerApi.careConnectIntegrity.get()`, renders `IntegrityReportCard`
- **`/scoped-roles`** (MOCKUP) — explains Phase G completion; links to per-user user detail; illustrative mockup table with disabled controls + footnote
- **`/domains`** (MOCKUP) — tenant domain management placeholder; disabled form controls; illustrative data with row-level opacity

### Updated pages
- **`/` (root)** — full admin dashboard grid: seven `SectionCard` sections (Platform, Identity, Relationships, Product Rules, CareConnect, Operations, Mockup/Not-yet-wired) each with `NavLink` rows that carry LIVE/IN PROGRESS/MOCKUP status badges; sign-in CTA at bottom
- **`/products`** — added MOCKUP badge, amber info callout linking to Tenant detail

### Build status after Control Center Admin Refresh
- control-center (tsc --noEmit): ✅ 0 errors, 0 warnings
- Workflow: ✅ running (fast refresh 727 ms)

---

## Control Center Admin Refresh — Step 11 ✅

**Scope:** Functional completion pass — nav reorganisation, status badges aligned to backend capabilities.
**Constraint:** No visual redesign; existing layout, shell, branding, and theme preserved.

### Navigation (`apps/control-center/src/lib/nav.ts`)
- Moved **Tenants** out of the IDENTITY section into its own TENANTS section (alongside Tenant Domains)
- Added `IN PROGRESS` badge to **Support Tools**, **Audit Logs**, and **Platform Settings** (previously unlabelled)
- **Monitoring** already carried `IN PROGRESS`; no change needed

### Page header badges added
| Page | Badge | File |
|------|-------|------|
| Legacy Migration Coverage | LIVE | `app/legacy-coverage/page.tsx` |
| Organization Types | LIVE | `app/org-types/page.tsx` |
| Relationship Types | LIVE | `app/relationship-types/page.tsx` |
| Organization Relationships | LIVE | `app/org-relationships/page.tsx` |
| Product Access Rules | LIVE | `app/product-rules/page.tsx` |
| Audit Logs | IN PROGRESS | `app/audit-logs/page.tsx` |
| Support Tools | IN PROGRESS | `app/support/page.tsx` |
| System Health | LIVE | `app/monitoring/page.tsx` — BFF at `app/api/monitoring/summary/route.ts` probes 6 services (Gateway, Identity, Notifications, Audit, SynqFund, CareConnect) via `/health` endpoints; grouped into Platform Services + Products; auth-protected via cookie forwarding |
| Platform Settings | IN PROGRESS | `app/settings/page.tsx` |

### Verification
- `tsc --noEmit` (control-center): ✅ 0 errors
- All mappers and types confirmed aligned with Phase G backend shapes
- Analysis report: `analysis/step11_control-center-admin-refresh.md`

---

## Platform Audit Service — Step 21: Production Hardening Pass ✅

**Build:** 0 errors, 0 warnings  
**Files changed:** 8 modified, 2 new config/docs, 2 new analysis docs

### Security fixes
- **`ExceptionMiddleware`** — internal `ex.Message` is no longer forwarded to API clients; all error response bodies use static, caller-safe strings. Exception detail remains in server logs only.
- **`ExceptionMiddleware`** — `UnauthorizedAccessException` now correctly maps to HTTP 403 (access denied), not 401 (unauthenticated).
- **`ExceptionMiddleware`** — added `JsonStringEnumConverter` to the middleware JSON options so exception-path responses serialize enums as strings, consistent with the controller pipeline.
- **`CorrelationIdMiddleware`** — incoming `X-Correlation-ID` header is now sanitized: max 100 chars, alphanumeric / hyphen / underscore only. Out-of-spec values are discarded and a fresh GUID is generated.
- **`Program.cs`** — security response headers added to every response: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-XSS-Protection: 0`.
- **`Program.cs`** — `IngestAuth:Mode = "None"` and `QueryAuth:Mode = "None"` now emit `Log.Error` (not Warning) in Production so they surface in alerting pipelines.

### Observability fixes
- **`CorrelationIdMiddleware`** — correlation ID pushed into `Serilog.Context.LogContext` so every log entry in the request scope automatically carries `CorrelationId` as a structured property.
- **`appsettings.json`** — Serilog console output template updated to `[{Timestamp} {Level}] [{CorrelationId}] {SourceContext}: {Message}`.
- **`ExceptionMiddleware`** — client errors (4xx) now logged at Warning; server faults (5xx) at Error.

### API contract consistency
- **`AuditExportController`** — all 5 error paths previously returning `new { error = "..." }` anonymous objects now return `ApiResponse<T>` envelope. Success paths (202 and 200) also wrapped in `ApiResponse<T>.Ok`.

### Configuration / hardening
- **`HealthController`** — `Service` and `Version` now sourced from `IOptions<AuditServiceOptions>` instead of hardcoded literals.
- **`HealthController`** — route changed from `/health` to `/health/detail` to resolve ambiguous endpoint match with `app.MapHealthChecks("/health")`. `/health` is the lightweight k8s probe; `/health/detail` is the rich diagnostic endpoint.
- **`AuditEventQueryController`** — `IValidator<AuditEventQueryRequest>` now injected and called in all 6 query actions (after path params are merged, before authorization). Returns 400 `ApiResponse.ValidationFail` on invalid input.
- **`appsettings.Production.json`** (new) — hardened production baseline: MySQL provider, HMAC-SHA256 signing, ServiceToken ingest auth, Bearer query auth, Serilog ISO-8601 timestamps. Secrets documented as env-var only.

### New files
- `appsettings.Production.json` — production configuration baseline
- `Docs/production-readiness-checklist.md` — 40-item deployment checklist covering auth, DB, integrity, retention, export, observability, network, and HIPAA compliance
- `analysis/step21_hardening.md` — full issue catalogue: 14 findings, fixes, and build verification

## Step 24 — Audit Cutover, Producer Integration & UI Activation (2026-03-30)

### T001 — Gateway: Audit Service Routes
Added 4 routes to `apps/services/gateway/appsettings.json`:
- `GET /audit-service/audit/events` → query canonical events
- `GET /audit-service/audit/export` → export
- `GET /audit-service/health` → health probe
- `GET /audit-service/audit/info` → service info
New `audit-cluster` upstream → `http://localhost:5007`. Purely additive.

### T002 — Shared Audit Client Library (`shared/audit-client/LegalSynq.AuditClient/`)
- `IAuditEventClient` — `IngestAsync` / `BatchIngestAsync` contract
- `HttpAuditEventClient` — fire-and-observe HTTP implementation (never throws on delivery failure)
- `AuditClientOptions` — `BaseUrl`, `ServiceToken`, `TimeoutSeconds`
- `AuditClientServiceCollectionExtensions` — `AddAuditEventClient(IConfiguration)`
- `IdempotencyKey` — deterministic key generation (`For` / `ForWithTimestamp`)
- DTOs: `IngestAuditEventRequest`, `BatchIngestRequest`, `IngestResult`, `BatchIngestResult`, scope/actor/entity DTOs
- Enums: `EventCategory`, `SeverityLevel`, `ScopeType`, `ActorType`, `VisibilityScope`
- Added to `LegalSynq.sln` under `shared` solution folder (properly registered via `dotnet sln add`)

### T003 — Identity & CareConnect Producers
- **Identity `AuthService`** — emits `user.login.succeeded` on successful authentication
- **Identity `AdminEndpoints`** — emits `user.role.assigned` / `user.role.revoked` on admin role changes
- **CareConnect `DependencyInjection`** — wired with `AddAuditEventClient` (ready for event emission)
- Both services have `AuditClient` config block in `appsettings.json` (BaseUrl → `:5007`, empty ServiceToken, 5 s timeout)

### T004 — Control Center UI: Canonical + Legacy Hybrid
- **`types/control-center.ts`** — added `CanonicalAuditEvent`, `AuditReadMode` (`legacy` | `canonical` | `hybrid`)
- **`lib/api-client.ts`** — added `auditCanonical` cache tag
- **`lib/api-mappers.ts`** — added `mapCanonicalAuditEvent(raw)` normaliser
- **`lib/control-center-api.ts`** — added `auditCanonical.list(params)` → `GET /audit-service/audit/events` (13 query params, 10 s cache)
- **`app/audit-logs/page.tsx`** — AUDIT_READ_MODE-driven hybrid page: `legacy` (default) / `canonical` / `hybrid` (canonical-first with silent legacy fallback); adaptive filter UI per mode; source badge in header
- **`components/audit-logs/canonical-audit-table.tsx`** — NEW: read-only table for canonical events with severity/category/outcome badge components

### T005 — Tenant Portal: Activity Page
- **`apps/web/src/app/(platform)/activity/page.tsx`** — Phase 1 placeholder with `requireOrg()` guard + `BlankPage`. Phase 2 (pending): canonical events scoped to tenantId.

### T006 — Technical Report
- **`docs/step-24-audit-cutover-report.md`** — full technical report: architecture diagram, change-by-task breakdown, AUDIT_READ_MODE deployment guide (4-stage cutover), HIPAA alignment table, limitations & next steps

### Build Status
- Identity API: 0 errors, 0 warnings (LegalSynq.AuditClient compiled transitively)
- CareConnect API: 0 errors, 1 pre-existing warning
- Control Center TypeScript: 0 errors
- Solution file: fixed bogus placeholder GUIDs; audit client correctly registered with `dotnet sln add`

---

## Step 28 — SynqAudit UI (Control Center)

Full dedicated audit section added to the Control Center (Next.js 14, port 5004). Six pages + six client components + four API route handlers.

### Pages (`apps/control-center/src/app/synqaudit/`)
| Route | File | Description |
|---|---|---|
| `/synqaudit` | `page.tsx` | Overview: stat cards, quick-nav, recent events table |
| `/synqaudit/investigation` | `investigation/page.tsx` | Full filter bar + paged event stream (server fetch → InvestigationWorkspace) |
| `/synqaudit/trace` | `trace/page.tsx` | Correlation ID trace viewer (chronological timeline) |
| `/synqaudit/exports` | `exports/page.tsx` | Async export job submission (JSON/CSV/NDJSON) |
| `/synqaudit/integrity` | `integrity/page.tsx` | HMAC-SHA256 checkpoint list + generate form |
| `/synqaudit/legal-holds` | `legal-holds/page.tsx` | Legal hold management per audit record ID |

### Client Components (`apps/control-center/src/components/synqaudit/`)
- **`synqaudit-badges.tsx`** — `SeverityBadge`, `CategoryBadge`, `OutcomeBadge`, `formatUtc`, `formatUtcFull` (no `use client` — server-safe)
- **`investigation-workspace.tsx`** — filter bar (URL-driven), event stream table, full event detail side panel, pagination
- **`trace-timeline.tsx`** — searchable correlation ID trace timeline with expandable event cards
- **`export-request-form.tsx`** — export job form; calls `POST /api/synqaudit/exports`
- **`integrity-panel.tsx`** — checkpoint list + generate form; calls `POST /api/synqaudit/integrity/generate`
- **`legal-hold-manager.tsx`** — active/released hold list, place new hold, release hold; calls `/api/synqaudit/legal-holds/[id]` and `/api/synqaudit/legal-holds/[id]/release`

### API Route Handlers (`apps/control-center/src/app/api/synqaudit/`)
| Route | Purpose |
|---|---|
| `POST /api/synqaudit/exports` | Proxy → `auditExports.create()` |
| `POST /api/synqaudit/integrity/generate` | Proxy → `auditIntegrity.generate()` |
| `POST /api/synqaudit/legal-holds/[id]` | Proxy → `auditLegalHolds.create(auditId)` |
| `POST /api/synqaudit/legal-holds/[id]/release` | Proxy → `auditLegalHolds.release(holdId)` |

All routes guarded with `requirePlatformAdmin()`. Dynamic segments use same `[id]` name to satisfy Next.js router uniqueness constraint.

### Extended Types & API Client
- **`types/control-center.ts`** — `CanonicalAuditEvent` extended (action/before/after/tags/sourceService/actorType/requestId/sessionId/hash); new types: `AuditExport`, `AuditExportFormat`, `IntegrityCheckpoint`, `LegalHold`
- **`lib/api-mappers.ts`** — `mapCanonicalAuditEvent` rewritten; `mapAuditExport`, `mapIntegrityCheckpoint`, `mapLegalHold` added; `unwrapApiResponse`/`unwrapApiResponseList` helpers for `ApiResponse<T>` envelope
- **`lib/control-center-api.ts`** — `auditCanonical.getById`, `auditExports.{create,getById}`, `auditIntegrity.{list,generate}`, `auditLegalHolds.{listForRecord,create,release}`
- **`lib/nav.ts`** — SYNQAUDIT section with 6 live nav items

### Build Status
- Next.js control-center: ✅ `✓ Ready` (0 compile errors, routing conflict resolved)
- No TypeScript errors (both `✓ Ready in <4s`)

---

## Step 29 — Missing Audit Events + User Access Logs & Activity Reports

**16 canonical audit events now fully emitting** across 4 source systems. 5 new events wired in this step.

### New Canonical Events

| Event Type | Source | Visibility | Severity |
|---|---|---|---|
| `platform.admin.tenant.entitlement.updated` | `AdminEndpoints.UpdateEntitlement` | Platform | Warn |
| `platform.admin.org.relationship.created` | `AdminEndpoints.CreateOrganizationRelationship` | Platform | Info |
| `platform.admin.org.relationship.deactivated` | `AdminEndpoints.DeactivateOrganizationRelationship` | Platform | Warn |
| `platform.admin.impersonation.started` | CC `startImpersonationAction` | Platform | Warn |
| `platform.admin.impersonation.stopped` | CC `stopImpersonationAction` | Platform | Info |

All follow fire-and-observe: `_ = auditClient.IngestAsync(...)` (C#) / `.catch(() => {})` (TypeScript).

### Impersonation Audit Upgrade
- **`apps/control-center/src/app/actions/impersonation.ts`** — now dual-emits: (1) local NDJSON log (existing) + (2) canonical event via `controlCenterServerApi.auditIngest.emit()`. The `.catch()` on the canonical emit ensures impersonation never fails due to audit pipeline unavailability.
- All `TODO: persist to AuditLog table` comments removed — now fulfilled.

### New CC API Method
- **`auditIngest.emit(payload: AuditIngestPayload)`** added to `controlCenterServerApi` — calls `POST /audit-service/audit/ingest` via the API gateway. Used by server actions that live outside the Identity service DI container.
- **`AuditIngestPayload`** interface added to `types/control-center.ts`.

### Control Center — User Activity Page
- **`apps/control-center/src/app/synqaudit/user-activity/page.tsx`** — new `requirePlatformAdmin()`-guarded page
  - Category tabs: All Events | Access (Security) | Admin Actions (Administrative) | Clinical (Business)
  - Actor filter: narrows stream to a specific user; clicking any actor ID in the table pre-fills the filter
  - Date range filter
  - Trace link per row → `/synqaudit/investigation?search={auditId}`
  - Tenant context aware (narrows scope when a tenant context is active)
- **`apps/control-center/src/lib/nav.ts`** — "User Activity" added to SYNQAUDIT section (`ri-user-heart-line`, badge: LIVE)

### Tenant Portal — Activity Page Enhancements
- **`apps/web/src/app/(platform)/activity/page.tsx`** — enhanced with:
  - **Category tabs**: All | Access (Security) | Admin (Administrative) | Clinical (Business)
  - **Actor filter field**: adds `actorId` to the query, narrowing to a specific user
  - **"My Activity" toggle**: header button; sets `actorId=me` → resolves to `session.userId` server-side
  - **Clickable actor IDs**: each actor cell links to `?actorId={id}` for drill-down
  - All filter state preserved across pagination and tab changes via unified `hrefFor()` helper

### Analysis
- `analysis/step29_user_activity_audit.md` — full event taxonomy table, change log, architecture notes

## Step 31 — Audit Service SQLite Dev Fixes (2026-03-31)

### Root Cause Chain (resolved)

Three layered bugs were each silently masking the next:

1. **`HasColumnType("bigint")` on PKs** — `EnsureCreated` was generating `"Id" bigint NOT NULL PRIMARY KEY AUTOINCREMENT` which SQLite rejects (`AUTOINCREMENT` only allowed on `INTEGER`). Fixed by removing explicit column type on PK `Id` properties in all 4 entity configurations (`AuditEventRecordConfiguration`, `AuditExportJobConfiguration`, `IngestSourceRegistrationConfiguration`, `IntegrityCheckpointConfiguration`).

2. **Empty connection string (`ConnectionString=`)** — `DatabaseOptions.ConnectionString` defaults to `""` (empty string), so `dbOpts.ConnectionString ?? $"Data Source={dbOpts.SqliteFilePath}"` never fell through to the file path (null-coalescing ignores empty string). An empty connection string creates a per-connection in-memory SQLite database — `EnsureCreated` succeeded on the first connection, but every subsequent connection got a brand-new empty DB. Fixed by replacing `??` with `string.IsNullOrEmpty()` checks in both the shared `connectionString` and the `sqliteCs` variables in `Program.cs`.

3. **`DateTimeOffset` ORDER BY and `Min`/`Max` aggregates** — SQLite EF Core provider cannot translate `OrderBy(r => r.OccurredAtUtc)` or `GroupBy.Select(g.Min(DateTimeOffset))`. Fixed across 6 repository files:
   - `EfAuditEventRecordRepository` — `ApplySorting`, `GetOccurredAtRangeAsync`, and `GetBatchForRetentionAsync`
   - `EfOutboxMessageRepository` — `ListPendingAsync`
   - `EfAuditExportJobRepository` — `ListByStatusAsync`
   - `EfIntegrityCheckpointRepository` — `ListAsync`
   - `EfLegalHoldRepository` — `ListByAuditIdAsync`, `ListActiveByAuthorityAsync`

### Files Changed
- `apps/services/audit/Program.cs` — fixed `string.IsNullOrEmpty()` for `connectionString` and `sqliteCs`
- `apps/services/audit/Data/Configurations/AuditEventRecordConfiguration.cs` — removed `HasColumnType("bigint")` from PK
- `apps/services/audit/Data/Configurations/AuditExportJobConfiguration.cs` — same
- `apps/services/audit/Data/Configurations/IngestSourceRegistrationConfiguration.cs` — same
- `apps/services/audit/Data/Configurations/IntegrityCheckpointConfiguration.cs` — same
- `apps/services/audit/Repositories/EfAuditEventRecordRepository.cs` — replaced all `DateTimeOffset` ORDER BY + `Min`/`Max` aggregates with `OrderBy(r => r.Id)` equivalents
- `apps/services/audit/Repositories/EfOutboxMessageRepository.cs` — `OrderBy(m => m.Id)`
- `apps/services/audit/Repositories/EfAuditExportJobRepository.cs` — `OrderBy(j => j.Id)`
- `apps/services/audit/Repositories/EfIntegrityCheckpointRepository.cs` — `OrderBy(c => c.Id)`
- `apps/services/audit/Repositories/EfLegalHoldRepository.cs` — `OrderBy(h => h.Id)` (two methods)

### Result
- Audit service starts cleanly on port 5007 with `Data Source=audit_dev.db`
- `EnsureCreated` succeeds on every startup; all tables present
- `POST /internal/audit/events` → `{"success":true, "accepted":true}` ✅
- `GET /audit/events?tenantId=...` → `{"success":true, "data":{"items":[...],"totalCount":1,...}}` ✅
- `earliestOccurredAtUtc` / `latestOccurredAtUtc` computed correctly ✅
- Background jobs (`OutboxRelayHostedService`, `ExportProcessingJob`) start without errors ✅

---

## Step 36 — LSCC-010 Auto Provisioning — Provider Instant Activation (2026-03-31)

Eliminates the manual admin step from the provider activation happy path. When a provider
submits the LSCC-008 form, `auto-provision` fires: validates the HMAC token, creates/resolves
an Identity Organization (idempotent), links the provider, auto-approves the activation request,
and returns a login URL for immediate portal access. Any failure falls back to the LSCC-009 queue.

### New Files — Backend
- `CareConnect.Application/Interfaces/IIdentityOrganizationService.cs` — cross-service interface
- `CareConnect.Application/Interfaces/IAutoProvisionService.cs` — orchestration interface
- `CareConnect.Application/DTOs/AutoProvisionDtos.cs` — `AutoProvisionResult` (Provisioned/AlreadyActive/Fallback factories) + `AutoProvisionRequest`
- `CareConnect.Application/Services/AutoProvisionService.cs` — full orchestration (token → provider → identity org → link → approve → loginUrl)
- `CareConnect.Infrastructure/Services/HttpIdentityOrganizationService.cs` — HTTP client for Identity org creation; all failures return null (graceful fallback)
- `CareConnect.Tests/Application/AutoProvisionTests.cs` — 10 tests, all pass

### New Files — Identity
- `AdminEndpointsLscc010` in `Identity.Api/Endpoints/AdminEndpoints.cs` — `POST /api/admin/organizations` (idempotent by deterministic name) + `GET /api/admin/organizations/{id}`

### New Files — Frontend
- (none; activation-form.tsx updated in place)

### Modified Files
- `CareConnect.Api/Endpoints/ReferralEndpoints.cs` — `POST /{id}/auto-provision` (public, token-gated)
- `CareConnect.Infrastructure/DependencyInjection.cs` — DI for `IIdentityOrganizationService` + `IAutoProvisionService`
- `apps/web/src/app/referrals/activate/activation-form.tsx` — calls auto-provision; renders 3 states: provisioned (green + login CTA), alreadyActive (blue + login CTA), fallback (amber + "team will follow up")
- `CareConnect.Tests/Application/ProviderActivationFunnelTests.cs` — fixed URL assertion bug (encoded-string vs plain-path mismatch)

### Behaviour
- **Happy path:** pending provider → org created → provider linked → request auto-approved → login redirect
- **Already active:** provider already linked → skip identity call → login redirect (idempotent)
- **Fallback:** any failure → LSCC-009 upsert → amber "request received" UI; no activation lost
- **Audit events:** `AutoProvisionStarted`, `AutoProvisionSucceeded`, `AutoProvisionFailed` (fire-and-forget)
- **Test score:** 341 pass, 5 pre-existing ProviderAvailability failures (unrelated)

---

## Step 35 — LSCC-009 Admin Activation Queue (2026-03-31)

Builds the admin workflow that closes the provider activation loop: collects activation
intent from the LSCC-008 funnel into durable database records, surfaces them in a
protected admin queue, and lets an admin approve each request (linking the provider
to an Identity Organisation) safely and idempotently.

### New Files — Backend
- `CareConnect.Domain/ActivationRequest.cs` — domain entity (Pending → Approved lifecycle, idempotent `Approve()`)
- `CareConnect.Infrastructure/Data/Config/ActivationRequestConfiguration.cs` — EF fluent config; unique index on `(ReferralId, ProviderId)` for deduplication
- `CareConnect.Application/Repositories/IActivationRequestRepository.cs` + `ActivationRequestRepository.cs` — CRUD + pending list + referral/provider lookup
- `CareConnect.Application/Interfaces/IActivationRequestService.cs` + `ActivationRequestService.cs` — upsert, getPending, getById, approve (with idempotency and pre-linked-provider guard)
- `CareConnect.Application/DTOs/ActivationRequestDtos.cs` — Summary / Detail / ApproveRequest / ApproveResponse DTOs
- `CareConnect.Api/Endpoints/ActivationAdminEndpoints.cs` — `GET /api/admin/activations`, `GET /api/admin/activations/{id}`, `POST /api/admin/activations/{id}/approve` (all require `Policies.PlatformOrTenantAdmin`)
- `CareConnect.Infrastructure/Data/Migrations/20260331204551_AddActivationRequestQueue` — EF migration
- `CareConnect.Tests/Application/ActivationQueueTests.cs` — 10 tests, all pass
- `analysis/careconnect/LSCC-009-report.md` — implementation report

### New Files — Frontend
- `apps/web/src/app/(platform)/careconnect/admin/activations/page.tsx` — admin queue list (server component, `requireAdmin()`)
- `apps/web/src/app/(platform)/careconnect/admin/activations/[id]/page.tsx` — detail page with approve panel (server component, `requireAdmin()`)
- `apps/web/src/app/(platform)/careconnect/admin/activations/[id]/approve-action.tsx` — client component: Organisation ID input, POST approve, inline success/already-approved states

### Modified Files
- `CareConnect.Infrastructure/Data/CareConnectDbContext.cs` — `DbSet<ActivationRequest> ActivationRequests`
- `CareConnect.Infrastructure/DependencyInjection.cs` — DI for `IActivationRequestRepository` + `IActivationRequestService`
- `CareConnect.Api/Program.cs` — `MapActivationAdminEndpoints()`
- `CareConnect.Application/DTOs/TrackFunnelEventRequest.cs` — added `RequesterName?` + `RequesterEmail?`
- `CareConnect.Application/Interfaces/IReferralService.cs` — extended `TrackFunnelEventAsync` signature
- `CareConnect.Application/Services/ReferralService.cs` — upserts `ActivationRequest` when `ActivationStarted` fires
- `apps/web/src/types/careconnect.ts` — `ActivationRequestSummary` + `ActivationRequestDetail` interfaces
- `apps/web/src/lib/careconnect-server-api.ts` — `adminActivations.getPending()` + `adminActivations.getById(id)`
- `apps/web/src/app/referrals/activate/activation-form.tsx` — sends `requesterName` + `requesterEmail` in track-funnel body

### Admin Approval Guard Rails
1. `organizationId` required in body — no auto-provisioning
2. Already Approved → idempotent success (`wasAlreadyApproved = true`), no side effects
3. Provider already linked → skip `LinkOrganizationAsync`, still mark Approved
4. Not found → 404 `NotFoundException`
5. Audit event `careconnect.activation.approved` emitted on every fresh approval

---

## Step 34 — LSCC-008 Provider Activation Funnel (2026-03-31)

Implements the full end-to-end funnel that routes a provider from the referral
notification email to either an activation intent form (pending/unlinked provider)
or the authenticated portal (active/linked provider).

### New Files
- `apps/services/careconnect/CareConnect.Application/DTOs/ReferralPublicSummaryResponse.cs` — public referral context DTO (minimal PHI, HMAC-gated)
- `apps/services/careconnect/CareConnect.Application/DTOs/TrackFunnelEventRequest.cs` — funnel event request DTO
- `apps/web/src/app/referrals/activate/page.tsx` — server component: activation intent capture, validates token, renders context + form
- `apps/web/src/app/referrals/activate/activation-form.tsx` — client component: name + email capture, emits ActivationStarted, confirmation screen
- `apps/web/src/app/referrals/accept/[referralId]/activation-landing.tsx` — client component: referral card + benefits + 3 CTAs (Activate / Log in / Direct accept)
- `apps/services/careconnect/CareConnect.Tests/Application/ProviderActivationFunnelTests.cs` — 22 test cases covering all paths
- `analysis/careconnect/LSCC-008-report.md` — implementation report

### Backend Changes
- `IReferralService` + `ReferralService` — `GetPublicSummaryAsync` (token-validated, version-checked) + `TrackFunnelEventAsync` (allowlisted event types, fire-and-forget audit)
- `ReferralEndpoints.cs` — `GET /api/referrals/{id}/public-summary` + `POST /api/referrals/{id}/track-funnel` (public, HMAC token-gated)

### Frontend Changes
- `middleware.ts` — `/referrals/activate` added to `PUBLIC_PATHS`
- `app/referrals/accept/[referralId]/page.tsx` — rebuilt as server component: fetches public summary, handles invalid/revoked/expired/already-accepted states, renders `ActivationLanding`

### Funnel Flow
```
Email link → /referrals/accept/[id]?token=...
  ├─ Token invalid        → /referrals/accept/invalid?reason=...
  ├─ Already accepted     → AlreadyAcceptedScreen
  └─ Pending referral     → ActivationLanding
        ├─ [Primary]   /referrals/activate?referralId=...&token=... → account activation form
        ├─ [Secondary] /login?returnTo=...&reason=referral-view
        └─ [Tertiary]  accept-by-token (no account, collapsible)
```

### Provider State Detection
`provider.OrganizationId.HasValue` → active (route to login) | null → pending (route to activation funnel)

---

## Step 33 — LSCC-007-01 Dashboard Deep-Links & Context Preservation (2026-03-31)

Wires `from=dashboard` into every referral link on the dashboard and propagates
the full list query-string through the referral list so the detail page back-button
is always contextually correct.

### New Files
- `apps/web/src/lib/referral-nav.ts` — pure utility module: `buildReferralDetailUrl`,
  `resolveReferralDetailBack`, `referralNavParamsToQs`

### Back-link Priority (resolveReferralDetailBack)
1. List filters present (`status`, `search`, `createdFrom`, `createdTo`) → back to
   filtered list with status-aware label (e.g. "← Back to Pending Referrals")
2. `from=dashboard` only → back to `/careconnect/dashboard`
3. Fallback → back to `/careconnect/referrals`

### Dashboard Changes
- All referral `href` values (StatCards, SectionCard viewAll, QuickActions,
  header button, Referral Activity KPI cards) now carry `from=dashboard`
- Referral Activity KPI cards (Total / Pending / Accepted) upgraded from static
  `<div>` to clickable `StatCard` with date-range deep-links

### Component Changes
- `ReferralQuickActions` — new `contextQs?: string` prop; View link uses `buildReferralDetailUrl`
- `ReferralListTable` — passes `currentQs` as `contextQs` to `ReferralQuickActions`
- `referrals/[id]/page.tsx` — `searchParams` extended with `status/search/createdFrom/createdTo`;
  manual `from` check replaced by `resolveReferralDetailBack(searchParams)`

---

## Step 32 — LSCC-007 CareConnect UX Layer (2026-03-31)

Frontend-only UX overhaul of the CareConnect referral experience.

### New Components
- `ReferralPageHeader` — detail page identity/status header (name, status badge, urgency, service, created date)
- `ReferralQueueToolbar` — debounced search input + status filter pills (client component, updates URL params)
- `ReferralQuickActions` — per-row quick actions with toast feedback and inline confirm for destructive actions

### Key Changes
- **Referral list page**: work-queue layout; pending rows highlighted (blue left-border accent); role-specific title/subtitle; search (client name, 320ms debounce, server-side via `clientName` API param); filter labels ("Pending" = "New" in backend); results count; back-to-dashboard link
- **Referral detail page**: reorganized into 5 sections: identity header → primary actions → book appointment → referral fields → delivery/access/audit; `hideHeader` prop on `ReferralDetailPanel` avoids duplicate header
- **Quick actions** per list row: View (all), Accept (receiver, non-terminal), Resend Email (referrer, New only), Revoke Link (referrer, with inline confirm)
- **Navigation**: `?from=dashboard` param makes detail back button context-aware (back to dashboard vs. referrals list)

### Files Changed
- `apps/web/src/components/careconnect/referral-page-header.tsx` (new)
- `apps/web/src/components/careconnect/referral-queue-toolbar.tsx` (new)
- `apps/web/src/components/careconnect/referral-quick-actions.tsx` (new)
- `apps/web/src/components/careconnect/referral-detail-panel.tsx` — `hideHeader?` prop
- `apps/web/src/components/careconnect/referral-list-table.tsx` — role props, quick actions, row highlighting
- `apps/web/src/app/(platform)/careconnect/referrals/page.tsx` — toolbar integration, search param
- `apps/web/src/app/(platform)/careconnect/referrals/[id]/page.tsx` — section reorganization

---

## Step 30 — IP Address Capture in Auth Audit Events

**IP address now recorded on all login and logout audit events** (both successful and failed).

### Changes
- **`Identity.Api/Endpoints/AuthEndpoints.cs`** — login endpoint now injects `HttpContext` and extracts the client IP via `X-Forwarded-For` (first segment) falling back to `RemoteIpAddress`. Passes `ip` to `LoginAsync`. Logout endpoint likewise sets `Actor.IpAddress` from the same header chain.
- **`Identity.Application/Interfaces/IAuthService.cs`** — `LoginAsync` signature extended: `Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default)`
- **`Identity.Application/Services/AuthService.cs`** — `LoginAsync` accepts `ipAddress`; sets `Actor.IpAddress` on the `identity.user.login.succeeded` event. `EmitLoginFailed` helper extended with `string? ipAddress = null`; all four call sites (`TenantNotFound`, `UserNotFound`, `InvalidCredentials`, `RoleLookupFailed`) pass the IP through.

### Result
- Activity Log IP Address column now shows the real client IP for login/logout events instead of `—`.
- Both successful and failed login attempts include the IP, supporting HIPAA §164.312(b) and NIST SP 800-92 requirements for contextual access logging.

---

## Step 37 — LSCC-011 Activation Funnel Analytics (2026-03-31)

Admin-only dashboard showing provider activation funnel metrics derived entirely from existing
`Referrals` + `ActivationRequests` tables — no new analytics tables or event pipelines.

### Design
- **6 parallel DB queries** at request time: ReferralsSent, ReferralsAccepted, ActivationStarted,
  AutoProvisionSucceeded (ApprovedByUserId IS NULL proxy), AdminApproved, FallbackPending + 2 snapshots
- **Rate math** is static/pure (`internal static ComputeRates` + `SafeRate`) — fully tested without DB
- `ReferralViewed` and direct `AutoProvisionFailed` are audit-log only, returned as `null` / shown as `—`
- **URL-based date filter** (`?days=7|30|90`) — presets only; backend supports custom ranges

### New Files — Backend
- `CareConnect.Application/DTOs/ActivationFunnelDto.cs` — `FunnelCounts`, `FunnelRates`, `ActivationFunnelMetrics`
- `CareConnect.Application/Interfaces/IActivationFunnelAnalyticsService.cs`
- `CareConnect.Infrastructure/Services/ActivationFunnelAnalyticsService.cs`
- `CareConnect.Infrastructure/Properties/AssemblyInfo.cs` — `InternalsVisibleTo("CareConnect.Tests")`
- `CareConnect.Api/Endpoints/AnalyticsEndpoints.cs` — `GET /api/admin/analytics/funnel?days=30`
- `CareConnect.Tests/Application/ActivationFunnelAnalyticsTests.cs` — 19 tests, 100% pass

### New Files — Frontend
- `apps/web/src/app/(platform)/careconnect/admin/analytics/activation/page.tsx` — server component
- `apps/web/src/app/(platform)/careconnect/admin/analytics/activation/date-filter.tsx` — client component

### Modified Files
- `CareConnect.Api/Program.cs` — `app.MapAnalyticsEndpoints()`
- `apps/web/src/types/careconnect.ts` — `FunnelCounts`, `FunnelRates`, `ActivationFunnelMetrics`
- `apps/web/src/lib/careconnect-server-api.ts` — `analytics.getFunnel()`
- `CareConnect.Infrastructure/Data/Migrations/20260331204551_AddActivationRequestQueue.cs` —
  Made fully idempotent (all `DropIndex`, `AddColumn`, `CreateTable`, `CreateIndex` wrapped in
  conditional SQL guards using `information_schema`) because MySQL DDL is non-transactional and
  a prior partially-applied run left schema changes without committing `__EFMigrationsHistory`

### Report
- `analysis/LSCC-011-report.md`

### Test Results
- 19/19 LSCC-011 tests pass
- Total suite: 360 pass (pre-existing 5 ProviderAvailability failures unchanged)

## Step 38 — Notifications Service (ARCHIVED Node.js → replaced by .NET 8)

Original Node.js notifications service was at `apps/services/notifications-nodejs/` — now archived to `_archived/notifications-nodejs/`. Replaced by `apps/services/notifications/` (.NET 8, 4-layer architecture: Api/Application/Domain/Infrastructure). The .NET version runs on the same port (5008) with identical gateway routing.

### Service Overview (.NET)
- **Port**: 5008
- **Stack**: ASP.NET Core 8 Minimal API + EF Core (Pomelo MySQL) + 3 BackgroundService workers
- **DB**: EF Core with MySQL (notifications_db); env vars `NOTIF_DB_*`
- **Auth**: Tenant context via `X-Tenant-Id` header; internal routes gated by `X-Internal-Service-Token`

### Route Groups (all prefixed `/v1/`)
| Prefix | Description |
|--------|-------------|
| `/v1/health` | Health check (anonymous) |
| `/v1/notifications` | Send + list notifications |
| `/v1/templates` | Template CRUD + versioning |
| `/v1/providers` | BYOP provider config management |
| `/v1/webhooks` | Inbound provider webhook ingestion |
| `/v1/billing` | Billing plans, rates, rate-limit policies |
| `/v1/contacts` | Contact suppression + policies |

### Workers
| Worker | Script | Purpose |
|--------|--------|---------|
| Provider-health | `src/workers/provider-health.worker.ts` | Periodic circuit-breaker health check |
| Notification dispatch | `src/workers/notification.worker.ts` | Queue-backed send (stub — queue TBD) |

### Environment Variables (DB — optional in dev, service starts without them)
| Variable | Description |
|----------|-------------|
| `NOTIF_DB_HOST` | MySQL host |
| `NOTIF_DB_PORT` | MySQL port (default 3306) |
| `NOTIF_DB_NAME` | Database name |
| `NOTIF_DB_USER` | Database user |
| `NOTIF_DB_PASSWORD` | Database password |

### Optional Provider Variables
- `SENDGRID_API_KEY`, `SENDGRID_DEFAULT_FROM_EMAIL`, `SENDGRID_DEFAULT_FROM_NAME`
- `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_DEFAULT_FROM_NUMBER`
- `PROVIDER_SECRET_ENCRYPTION_KEY` — AES-256 key for BYOP credential encryption

### .NET Service Files
- `apps/services/notifications/Notifications.Api/` — Program.cs, middleware (Tenant, InternalToken, RawBody), 9 endpoint groups
- `apps/services/notifications/Notifications.Application/` — DTOs, repository + service interfaces
- `apps/services/notifications/Notifications.Domain/` — 18 entities, Enums.cs
- `apps/services/notifications/Notifications.Infrastructure/` — DbContext, 18 entity configs, repositories, SendGrid/Twilio/SMTP adapters, webhook verifiers, 15 service implementations, 3 BackgroundService workers, DependencyInjection.cs

### Archived Node.js Files
- `_archived/notifications-nodejs/` — original Node.js service (kept for reference)
- `_archived/documents-nodejs/` — original Node.js documents service (replaced by documents)

### Gateway Routing
- `GET /notifications/health` — anonymous (health endpoint is at `/health`, not `/v1/health`)
- `* /notifications/**` — JWT-protected, strips `/notifications` prefix before forwarding to `:5008`

### TypeScript
- `tsc --noEmit` passes with 0 errors
- `GET http://localhost:5008/health` → `{"status":"healthy","service":"notifications",...}`

## UIX-001 — Control Center Admin API (22 endpoints)
- Full design documented in `analysis/UIX-001-01.md`
- MVP: 14 endpoints (A01–A08, A14–A17, A20–A21)
- Phase 2: 8 endpoints (A09–A13, A18–A19, A22) — avatar, MFA, session tracking
- `PortalOrigin` enum: `TENANT_PORTAL | CONTROL_CENTER` — used in PasswordResetTokens and UserInvitations

## UIX-002 — Tenant User Management (MVP) — COMPLETED 2026-04-01

### Backend changes (Identity service)
- **Domain:** `User.Activate()`, `UserOrganizationMembership.IsPrimary/SetPrimary/ClearPrimary`, new entities `TenantGroup`, `GroupMembership`, `UserInvitation`
- **Infrastructure:** EF configs for 3 new entities; `IdentityDbContext` updated with new DbSets
- **Migration:** `20260401000001_UIX002_UserManagement` — auto-applies on startup
- **Endpoints (12 new):** activate, invite, resend-invite, assign/set-primary/remove membership, list/get/create group, add/remove group member, list permissions
- `GetUser` now returns `memberships[]`, `groups[]`, `roles[]`
- `ListUsers` now returns `status: Invited`, `primaryOrg`, `groupCount`

### Frontend changes (Control Center)
- **Types:** `OrgMembershipSummary`, `UserGroupSummary`, `UserRoleSummary`, `GroupSummary`, `GroupDetail`, `GroupMemberSummary`, `PermissionCatalogItem`; extended `UserSummary` and `UserDetail`
- **API client:** 12 new methods across `users.*`, `groups.*`, `permissions.*`
- **Mappers:** `mapGroupSummary`, `mapGroupDetail`, `mapPermissionCatalogItem`; extended `mapUserSummary` and `mapUserDetail`
- **Nav:** Groups + Permissions added to IDENTITY section
- **Routes:** `Routes.groups`, `Routes.groupDetail(id)`, `Routes.permissions`
- **New pages:** `/groups`, `/groups/[id]`, `/permissions`, `/tenant-users/invite`
- **New components:** `GroupListTable`, `GroupDetailCard`, `PermissionCatalogTable`
- **Updated components:** `UserListTable` (Primary Org + Groups columns), `UserDetailCard` (membership/group/role panels), `UserActions` (wired activate/deactivate/resend-invite to real BFF)
- **BFF routes:** `/api/identity/admin/users/[id]/activate|deactivate|resend-invite`, `/api/identity/admin/users/invite`
- Full report: `analysis/UIX-002-report.md`

## Avatar, Profile Pictures & Tenant Logo — COMPLETED 2026-04-01

### T001 — CC Profile Page with Avatar Upload
- `apps/control-center/src/app/profile/page.tsx` — new profile page (requireAdmin guard)
- `apps/control-center/src/components/avatar/AvatarUpload.tsx` — CC avatar upload/remove component
  - Uses existing `/api/profile/avatar` (POST/DELETE) and `/api/profile/avatar/[id]` (GET) BFF routes
- `apps/control-center/src/components/shell/cc-shell.tsx` — avatar in top-bar now links to `/profile` with hover ring

### T002 — CC User Detail Shows User Avatar
- `Identity.Api/Endpoints/AdminEndpoints.cs` (`GetUser`) — now returns `avatarDocumentId` in response
- `apps/control-center/src/types/control-center.ts` — `UserDetail` extended with `avatarDocumentId?`
- `apps/control-center/src/lib/api-mappers.ts` — `mapUserDetail` maps `avatarDocumentId`
- `apps/control-center/src/app/api/admin/users/[userId]/avatar/[docId]/route.ts` — new proxy (passes `X-Admin-Target-Tenant` header)
- `apps/control-center/src/components/users/user-detail-card.tsx` — avatar display + initials fallback at top of user detail

### T003 — Tenant Logo Upload (Full Stack)

#### Backend (Identity service)
- `Identity.Domain/Tenant.cs` — `LogoDocumentId: Guid?`, `SetLogo(Guid)`, `ClearLogo()`
- `20260401200001_AddTenantLogo.cs` + snapshot — EF Core migration; auto-applies on startup
- `TenantBrandingResponse.cs` — new `LogoDocumentId?` parameter
- `TenantBrandingEndpoints.cs` — `GET /api/tenants/current/branding` now returns `logoDocumentId`
- `AdminEndpoints.cs` — `GetTenant` returns `logoDocumentId`; new endpoints:
  - `PATCH /api/admin/tenants/{id}/logo` — set logo (body: `{ documentId }`) with `identity.tenant.logo_set` audit event
  - `DELETE /api/admin/tenants/{id}/logo` — clear logo with `identity.tenant.logo_cleared` audit event

#### CC Frontend
- `apps/control-center/src/app/api/tenants/[id]/logo/route.ts` — `POST` (upload to Docs via gateway + persist) / `DELETE`. Routes Documents upload through `GATEWAY_URL/documents/documents` (not direct localhost:5006) for production compatibility.
- `apps/control-center/src/app/api/tenants/[id]/logo/content/[docId]/route.ts` — image proxy via gateway public logo endpoint (`/documents/public/logo/{docId}`). Anonymous, no auth needed.
- `apps/control-center/src/components/tenants/TenantLogoUpload.tsx` — logo upload/replace/remove panel
- `apps/control-center/src/app/tenants/[id]/page.tsx` — logo panel added to tenant detail (above session settings)
- `apps/control-center/src/types/control-center.ts` — `TenantDetail` extended with `logoDocumentId?`
- `apps/control-center/src/lib/api-mappers.ts` — `mapTenantDetail` maps `logoDocumentId`

#### Web Portal
- `apps/web/src/app/api/branding/logo/public/route.ts` — public logo proxy for tenant login pages. Routes through gateway for both branding lookup and logo retrieval. Supports `?tenantCode=` query param and hostname-based subdomain extraction.
- `apps/web/src/middleware.ts` — `/api/branding` added to `PUBLIC_PATHS` so login page can fetch tenant logo without auth
- `apps/web/src/app/api/branding/logo/[docId]/route.ts` — logo image proxy (requires session)
- `apps/web/src/types/index.ts` — `TenantBranding` extended with `logoDocumentId?`
- `apps/web/src/components/shell/top-bar.tsx` — shows tenant logo (`/api/branding/logo/{docId}`) when authenticated + logo set; falls back to LegalSynq logo

#### Gateway
- `documents-public-logo` route added: `GET /documents/public/logo/{id}` → Documents service `/public/logo/{id}` (Anonymous, Order 53)

### Document Type IDs
- Profile avatar: `20000000-0000-0000-0000-000000000001`
- Tenant logo:    `20000000-0000-0000-0000-000000000002`

### Audit Events
| Event | When |
|-------|------|
| `identity.user.avatar_set` | User uploads avatar |
| `identity.user.avatar_removed` | User removes avatar |
| `identity.tenant.logo_set` | Admin sets tenant logo |
| `identity.tenant.logo_cleared` | Admin removes tenant logo |

## UIX-004 — Audit & Activity Timeline — COMPLETED 2026-04-01

### Backend
- `GetUserActivity` handler: `GET /api/admin/users/{id}/activity` — queries `AuditLogs` by `EntityId = userId`, paged, `IsCrossTenantAccess` enforced.

### CC Types / Mappers / API Client
- `UserActivityEvent` type in `control-center.ts`
- `AUDIT_EVENT_LABELS` map + `mapEventLabel()` + `mapUserActivityEvent()` in `api-mappers.ts`
- `users.getActivity(id, { page, pageSize, category })` in `control-center-api.ts`
- `auditCanonical.listForUser({ userId, tenantId, page, pageSize })` convenience method

### CC BFF Route
- `GET /api/identity/admin/users/[id]/activity/route.ts` — protected by `requireAdmin()`

### CC Pages & Components
- `/audit-logs/page.tsx` — full featured: `requireAdmin()` (both PlatformAdmin + TenantAdmin), `AUDIT_READ_MODE` env-driven (legacy/canonical/hybrid), filters, pagination, canonical interactive table
- `UserActivityPanel` server component — canonical audit timeline on user detail page; graceful unavailable state
- Wired into `/tenant-users/[id]` page between security and access-control sections
- Nav: `/audit-logs` badge set to `LIVE`

Full report: `analysis/UIX-004-report.md`

## UIX-005 — Permissions & Effective Access Management — COMPLETED 2026-04-01

### Backend (Identity)
- `RoleCapabilityAssignment` domain entity (composite PK: RoleId + CapabilityId)
- EF Core config + migration `20260401220001_UIX005_AddRoleCapabilityAssignments`
- 4 new admin endpoints: `GetRolePermissions`, `AssignRolePermission`, `RevokeRolePermission`, `GetUserEffectivePermissions`
- `ListRoles`/`GetRole` now return `isSystemRole`, `capabilityCount`, `resolvedPermissions`
- `ListPermissions` supports `?search=` and `?productId=` server-side filtering

### CC Types / Mappers / API Client
- `RoleSummary` extended: `isSystemRole`, `capabilityCount`
- New types: `RoleCapabilityItem`, `EffectivePermission`, `PermissionSource`, `EffectivePermissionsResult`
- Mappers: `mapRoleCapabilityItem`, `mapEffectivePermission`, `mapEffectivePermissionsResult`
- `permissions.list()` bug fixed (was returning empty due to envelope mismatch)
- New API methods: `roles.getPermissions`, `roles.assignPermission`, `roles.revokePermission`, `users.getEffectivePermissions`

### CC BFF Routes
- `GET/POST /api/identity/admin/roles/[id]/permissions`
- `DELETE /api/identity/admin/roles/[id]/permissions/[capabilityId]`
- `GET /api/identity/admin/users/[id]/permissions`

### CC Components
- `RolePermissionPanel` — interactive assign/revoke with capability picker (client component)
- `EffectivePermissionsPanel` — read-only union view with source-role attribution badges
- `GroupPermissionsPanel` — informational notice (groups derive permissions through roles)
- `PermissionSearchBar` — client search input for `/permissions` page (URL-param navigation)

### CC Pages
- `/permissions` — product chip filter nav + text search + active filter summary + result count
- `/roles/[id]` — `RolePermissionPanel` wired in
- `/tenant-users/[id]` — `EffectivePermissionsPanel` wired in
- `/groups/[id]` — `GroupPermissionsPanel` wired in

Full report: `analysis/UIX-005-report.md`

## UIX-005-01 — Permissions Hardening — COMPLETED 2026-04-02

Extends UIX-005 to TenantAdmins and closes API security gaps.

**Backend (`AdminEndpoints.cs`):**
- `GetRolePermissions`: Added `ClaimsPrincipal caller`; cross-tenant guard (non-system roles only)
- `AssignRolePermission`: System-role guard (403 for TenantAdmin) + cross-tenant guard
- `RevokeRolePermission`: Same guards via `assignment.Role` navigation property

**BFF routes:**
- `GET/POST/DELETE /api/identity/admin/roles/[id]/permissions*` — widened `requirePlatformAdmin` → `requireAdmin`

**CC pages:**
- `/permissions` — widened to `requireAdmin`
- `/roles/[id]` — widened to `requireAdmin`; reads `session.isTenantAdmin` → `RolePermissionPanel`

**UI — `RolePermissionPanel`:**
- `isTenantAdmin?` prop for context-aware system-role notice text
- Success banner (auto-dismiss 3.5 s) after assign/revoke

**UI — `PermissionCatalogTable`:**
- Replaced flat table with product-grouped section cards
- Colour-coded product badges; per-product permission count; running total footer

**UIX-004 audit:** All T001–T008 tasks confirmed already implemented — no further work needed.

Full report: `analysis/UIX-005-01-report.md`

## LSCC-01-001-01 — Referral State Machine Correction — COMPLETED 2026-04-02

**Domain:**
- `Referral.ValidStatuses.InProgress` added as canonical active state
- `Referral.ValidStatuses.Scheduled` demoted to `ValidStatuses.Legacy.Scheduled`
- `ValidStatuses.All` now: New, Accepted, InProgress, Completed, Declined, Cancelled
- `Legacy.Normalize` maps Scheduled → InProgress (in addition to Received/Contacted → Accepted)

**Workflow Rules (`ReferralWorkflowRules.cs`):**
- `Accepted → InProgress | Declined | Cancelled` (Scheduled removed, Completed blocked)
- `InProgress → Completed | Cancelled`
- Legacy Scheduled entry: `Scheduled → InProgress | Cancelled`
- `RequiredCapabilityFor("InProgress")` → `ReferralUpdateStatus`

**Migration:** `20260402000000_ReferralInProgressState.cs` — SQL UPDATE Scheduled → InProgress

**Frontend:**
- `status-badge.tsx`: InProgress = amber badge; Scheduled kept for legacy display
- `referral-queue-toolbar.tsx`: STATUS_OPTIONS has InProgress (not Scheduled)
- `referral-list-table.tsx`: amber row highlight for InProgress
- `referral-status-actions.tsx`: "Mark In Progress" button for receiver when Accepted
- `referrals/[id]/page.tsx`: "Book Appointment" prompt removed (decoupled from referral status)

**Analytics:** `ActivationFunnelAnalyticsService` counts InProgress (not Scheduled) as accepted

**Tests:** 38 tests pass in `ReferralWorkflowRulesTests` — full canonical + legacy + new InProgress coverage

Full report: `analysis/LSCC-01-001-01-report.md`

## LSCC-01-002 — Referral Acceptance Flow Completion — COMPLETED 2026-04-02

Primary gap closed: **client acceptance email** added to `SendAcceptanceConfirmationsAsync`.
All other acceptance flow components (provider email, law firm email, token flow, login redirect) were already implemented.

**Domain (`NotificationType.cs`):**
- `ReferralAcceptedClient = "ReferralAcceptedClient"` added + registered in `All` set

**Email Service (`ReferralEmailService.cs`):**
- `SendAcceptanceConfirmationsAsync`: now sends to provider (1), referrer/law firm (2), and client (3)
- Client email skipped gracefully if `ClientEmail` is empty — acceptance never blocked; `LogWarning` emitted
- `BuildClientAcceptanceHtml()`: client-facing template — names provider, service, states provider will reach out; no appointment language
- `RetryNotificationAsync`: added `case ReferralAcceptedClient` — same pattern as referrer retry (address from stored record)
- Updated stale "schedule an appointment" copy in provider and referrer templates (decoupled per LSCC-01-001-01)

**Interface (`IReferralEmailService.cs`):**
- `SendAcceptanceConfirmationsAsync` docstring updated to document third recipient and graceful-skip contract

**Tests:** 10 new tests in `ReferralClientEmailTests.cs`; total 385 pass (390 total, 5 pre-existing failures unrelated)

Full report: `analysis/LSCC-01-002-report.md`

## LSCC-01-002-01 — Acceptance Model Lockdown — COMPLETED 2026-04-02

Eliminated the dual acceptance model. Providers **must now log in** before accepting a referral.

**Changes:**
- **Backend:** `POST /{id:guid}/accept-by-token` now returns **410 Gone** — no longer mutates referral state; safe handler for legacy links
- **Frontend `/referrals/view`:** Both `pending` AND `active` providers now route to `/login?returnTo=/careconnect/referrals/{id}&reason=referral-view` (unified; previously `pending` went to the public accept page)
- **Frontend `activation-landing.tsx`:** "Accept without creating an account" tertiary CTA and all direct-accept state/handlers removed; `'use client'` removed (no hooks remain); copy updated to "Log in to view and accept this referral"
- **Page docstrings** updated in `accept/[referralId]/page.tsx` and `view/page.tsx`

**Canonical flow post-lockdown:**
```
Email link → /referrals/view?token= → /login?returnTo=/careconnect/referrals/{id}
           → authenticated referral detail → ReferralStatusActions → Accept Referral
           → PUT /api/referrals/{id} (ReferralAccept capability gate) → New → Accepted
           → law firm + client notifications fire
```

**Tests:** 18 new tests in `ReferralAcceptanceLockdownTests.cs`; total 403 pass (408 total, 5 pre-existing failures unrelated)

Full report: `analysis/LSCC-01-002-01-report.md`

## LSCC-01-005 — Referral Performance Metrics (2026-04-02)

Admin-facing referral performance dashboard. Pure calculator layer is fully decoupled from EF — all metrics computed in-memory after two bounded DB queries.

### Metric Definitions
- **Cohort anchor:** `referral.CreatedAtUtc >= windowFrom` for all cohort metrics
- **AcceptedAt:** earliest `ChangedAtUtc` from `ReferralStatusHistory` where `NewStatus=="Accepted"`
- **TTA (Time to Accept):** `(AcceptedAtUtc - CreatedAtUtc).TotalHours` — negatives excluded (corrupt data)
- **Acceptance Rate:** `Accepted / Total`; returns `0.0` when Total=0
- **Avg TTA:** `null` when no valid accepted referrals
- **Aging:** ALL currently-New referrals (no window filter); buckets: <1h | [1h,24h) | [24h,72h) | ≥72h
- **Default window:** last 7 days (`?days=7`); max 90 days; `?since=<ISO>` overrides days

### New Files — Backend
- `CareConnect.Application/DTOs/ReferralPerformanceResult.cs` — `PerformanceSummary`, `AgingDistribution`, `ProviderPerformanceRow`, `RawReferralRecord`, `ReferralPerformanceResult`
- `CareConnect.Application/Interfaces/IReferralPerformanceService.cs`
- `CareConnect.Infrastructure/Services/ReferralPerformanceCalculator.cs` — pure static calculator (no DB)
- `CareConnect.Infrastructure/Services/ReferralPerformanceService.cs` — loads bounded dataset, calls calculator
- `CareConnect.Api/Endpoints/PerformanceEndpoints.cs` — `GET /api/admin/performance?days=7&since=<ISO>` (PlatformOrTenantAdmin)
- `CareConnect.Tests/Application/ReferralPerformanceCalculatorTests.cs` — 13 tests, all pass

### New Files — Frontend
- `apps/web/src/app/(platform)/careconnect/admin/performance/page.tsx` — server component; time-window presets, summary cards, aging bars, provider table

### Modified Files
- `CareConnect.Api/Program.cs` — `app.MapPerformanceEndpoints()`
- `CareConnect.Infrastructure/DependencyInjection.cs` — `IReferralPerformanceService` registered
- `apps/web/src/types/careconnect.ts` — `ReferralPerformanceResult`, `PerformanceSummary`, `AgingDistribution`, `ProviderPerformanceRow`
- `apps/web/src/lib/careconnect-server-api.ts` — `adminPerformance.getMetrics({ days?, since? })`

### API
```
GET /api/admin/performance?days=7        → last 7 days cohort (default)
GET /api/admin/performance?days=30       → last 30 days cohort
GET /api/admin/performance?since=<ISO>   → explicit UTC start
```
Response: `{ windowFrom, windowTo, summary, aging, providers[] }`

### Test Results
- 13/13 LSCC-01-005 calculator tests pass
- Total suite: 451 pass / 457 total (5 pre-existing `ProviderAvailabilityServiceTests` failures unchanged)

Full report: `analysis/LSCC-01-005-report.md`

---

## E2E Test Readiness Validation (2026-04-02)

Full report: `analysis/CC-E2E-VALIDATION-REPORT.md`

### Credentials
- margaret@hartwell.law / hartwell123! / HARTWELL → TenantAdmin, LAW_FIRM, orgId=40000000-...-0010
- james.whitmore@hartwell.law / hartwell123! / HARTWELL → StandardUser
- olivia.chen@hartwell.law / hartwell123! / HARTWELL → StandardUser
- dr.ramirez@meridiancare.com / meridian123! / MERIDIAN → TenantAdmin, PROVIDER, orgId=42000000-...-0001
- alex.diallo@meridiancare.com / meridian123! / MERIDIAN → StandardUser
- **admin@legalsynq.com / Admin1234! / LEGALSYNQ → PlatformAdmin** (password confirmed via bcrypt)

### Bugs Fixed
1. **BUG-001**: `BlockedProviderAccessLogs` table missing — migration was in history but table didn't exist; created table manually. `GET /api/admin/dashboard` and `GET /api/admin/providers/blocked` now return 200.
2. **BUG-002**: `ForbiddenException` → HTTP 500 — ExceptionHandlingMiddleware had no `catch (ForbiddenException)` handler. Fixed; now returns HTTP 403 with `code: "FORBIDDEN"`.

### LSCC-01-005-01 — PlatformAdmin Cross-Tenant Access Corrections (2026-04-02)

**DEF-001 FIXED**: `POST /api/admin/activations/{id}/approve` 404 for cross-tenant providers.
- Root cause: `ActivationRequestService.ApproveAsync` delegated to `IProviderService.LinkOrganizationAsync(tenantId, ...)` which used tenant-scoped lookup. Provider (MERIDIAN) had different TenantId than activation request (HARTWELL).
- Fix: Added `IProviderService.LinkOrganizationGlobalAsync(providerId, organizationId)` implemented with `GetByIdCrossAsync`. `ActivationRequestService.ApproveAsync` now always uses the global method (activation is always admin-only).

**DEF-002 FIXED**: PlatformAdmin 404 on per-record referral endpoints for other-tenant referrals.
- Root cause: `GetByIdAsync`, `GetHistoryAsync`, `ResendEmailAsync`, `GetNotificationsAsync`, `GetAuditTimelineAsync` all used tenant-scoped record lookup (`tenantId` from PlatformAdmin's JWT = `LEGALSYNQ`, not the referral's owner tenant).
- Fix: Added `bool isPlatformAdmin = false` parameter to all 5 `IReferralService` methods. When true, routes to `GetByIdGlobalAsync` (already existed). After global load, uses `referral.TenantId` for all sub-queries (notifications, history). Endpoints pass `ctx.IsPlatformAdmin`.
- E2E validation confirmed: 200 for `GET /referrals/{id}`, `/history`, `/notifications`, `/audit`, `POST /resend-email` all return 200 for PlatformAdmin on cross-tenant referrals.

**Architecture note**: `PlatformAdmin sees cross-tenant referral list AND now all per-record endpoints` (corrected from prior "limited to own tenant for single-record").

### Token Flow (Referral Public Token)
- Dev fallback secret: `LEGALSYNQ-DEV-REFERRAL-TOKEN-SECRET-2026`
- Format: `Base64url({referralId}:{tokenVersion}:{expiryUnixSeconds}:{hmacHex})`
- `resolve-view-token` → `routeType:"pending"` (provider not linked to org) or `"active"`
- `accept-by-token` → 410 by design (providers must log in)
- `revoke-token` increments `tokenVersion`, invalidating all prior tokens

### Architecture Notes
- BFF proxy path: `/api/careconnect/api/...` (double-api, by design — gateway routing)
- TenantAdmin bypasses ALL capability checks in `CareConnectAuthHelper.RequireAsync` (by design, line 26)
- PlatformAdmin sees cross-tenant referral list but is limited to their own tenant for single-record lookups

---

## Organization Type Management — Admin Update Endpoint (2026-04-03)

Added `PUT /api/admin/organizations/{id}` to the Identity service and wired it through the Control Center for managing organization types.

### Problem
MANERLAW's organization had `OrgType = "PROVIDER"` in the Identity DB when it should be `"LAW_FIRM"`. No admin endpoint existed to update an organization's type — the admin organizations page was a blank placeholder.

### Changes

**Identity Service (`AdminEndpoints.cs`):**
- `PUT /api/admin/organizations/{id}` — updates org name, display name, and/or org type
- Accepts `UpdateOrganizationRequest(Name?, DisplayName?, OrgType?)` — partial update semantics (omitted fields preserve existing values)
- Validates OrgType against `OrgType.IsValid()`, resolves `OrganizationTypeId` via `OrgTypeMapper`
- Calls `Organization.Update()` which keeps `OrgType` string and `OrganizationTypeId` FK in sync
- PlatformAdmin role check enforced in-handler (not just gateway)

**Control Center:**
- `control-center-api.ts` — `organizations.update(orgId, body)` method added (PUT via `apiClient.put`)
- `tenants/[id]/actions.ts` — `updateOrganizationType(orgId, orgType)` server action with `revalidateTag(CACHE_TAGS.tenants)` cache invalidation
- `TenantOrganizationsPanel` component — client component on tenant detail page listing organizations with inline org-type editing (dropdown + save/cancel)
- Tenant detail page (`tenants/[id]/page.tsx`) — fetches organizations via `controlCenterServerApi.organizations.listByTenant(id)` and renders the panel

### Cross-Tenant Referral Visibility
- Referrals are created under the **law firm's tenant** with `ReferringOrganizationId` (auto-set from caller's org) and `ReceivingOrganizationId` (auto-resolved from `Provider.OrganizationId`).
- **Provider orgs** (OrgType=PROVIDER) use cross-tenant receiver mode: referral search queries by `ReceivingOrganizationId` instead of `TenantId`, so providers see referrals addressed to them regardless of which tenant created them.
- **GetById** uses global lookup for provider orgs but enforces participant check (caller's org must match ReferringOrganizationId or ReceivingOrganizationId) for all users except PlatformAdmin.
- **Law firm orgs** use standard tenant-scoped queries. TenantAdmin on law firm sees all referrals in their tenant; regular users see only their org's outbound referrals.
- Key files: `ReferralEndpoints.cs`, `ReferralRepository.cs`, `GetReferralsQuery.cs` (CrossTenantReceiver flag), `ReferralService.cs` (auto-populates ReceivingOrganizationId).

## NOTIF-UI-009 — Tenant Notification Activity + Delivery Visibility

### Pages (apps/web — tenant portal)
| Path | Purpose |
|------|---------|
| `/notifications/activity` | Activity list — summary cards, delivery breakdown, filterable paginated table |
| `/notifications/activity/[notificationId]` | Activity detail — metadata, status, failure/block reasons, template usage, content preview, event timeline, issues |

### API Client Extensions
- `get(tenantId, id)` — single notification detail
- `events(tenantId, notificationId)` — delivery event timeline
- `issues(tenantId, notificationId)` — related delivery issues

### Shared Types Added
- `NotifDetail`, `NotifEvent`, `NotifIssue` in `notifications-shared.ts`

### Key Rules
- Tenant-scoped via `requireOrg()` + `x-tenant-id`
- Events/issues endpoints gracefully degrade if unavailable
- HTML content rendered in sandboxed iframes (CSP `script-src 'none'`)
- Template source (global vs override) displayed when backend provides it
- Metadata JSON fallback for template key, subject, body when direct fields unavailable

## NOTIF-UI-010 — Delivery Controls (Retry / Resend / Suppression Awareness)

### Capabilities
- Retry/resend failed notifications with confirmation dialogs on the activity detail page
- Suppression awareness panel for blocked/suppressed notifications
- Contact health card with on-demand health + suppression data loading
- Eligibility gating: only failed notifications can be retried/resent; blocked/suppressed/delivered cannot
- Post-action feedback with success/error banners and link to new notification

### Architecture
- **Server/Client split:** Detail page remains server component for data fetching; `DeliveryActionsClient` is client component for interactive actions
- **Server actions:** `retryNotification`, `resendNotification`, `fetchContactHealth`, `fetchContactSuppressions` in `activity/actions.ts`
- **Eligibility logic:** Derived client-side from notification status + failure category (conservative defaults)
- **Confirmation required:** Both retry and resend require explicit user confirmation via dialog

### API Client Methods Added
- `retry(tenantId, notificationId)` — POST, triggers retry
- `resend(tenantId, notificationId)` — POST, creates new notification attempt
- `contactHealth(tenantId, channel, contactValue)` — GET, contact health status
- `contactSuppressions(tenantId, channel, contactValue)` — GET, active suppressions

### Shared Types Added
- `RetryResult`, `ContactHealth`, `ContactSuppression`, `ActionEligibility` in `notifications-shared.ts`

### Key Rules
- Single-notification actions only — no bulk retry/resend
- Backend denial (409/422) mapped to clear user-facing messages
- Contact health loaded lazily (user clicks "Check Health")
- No suppression mutation (read-only suppression data)
- `router.refresh()` after successful action refreshes server-rendered data

## NOTIF-UI-008 — Tenant Template Override

### Capabilities
- Create tenant-scoped template overrides for any global template (same `templateKey + channel + productType`)
- Edit override draft content (HTML subject/body/text)
- Preview override with real backend rendering
- Publish override with confirmation — makes tenant override active
- Clear global vs override distinction at every level

### Pages Enhanced (apps/web — tenant portal)
| Path | Changes |
|------|---------|
| `/notifications/templates/[productType]` | Override status badges per template (Using Global / Override Draft / Override Active) |
| `/notifications/templates/[productType]/[templateId]` | Tabbed Global/Override view; override create/edit/publish/preview flows |

### Server Actions
- `createTenantOverride(globalTemplateId, productType)` — creates override template + initial version pre-populated from global
- `createOverrideVersion(overrideTemplateId, body)` — saves new version draft
- `publishOverrideVersion(overrideTemplateId, versionId)` — publishes override
- `previewOverrideVersion(overrideTemplateId, versionId, templateData)` — renders preview

### API Client Extensions
- `tenantTemplatesList`, `tenantTemplateGet`, `tenantTemplateCreate`, `tenantTemplateUpdate`
- `tenantTemplateVersions`, `tenantTemplateCreateVersion`, `tenantTemplatePublishVersion`, `tenantTemplatePreviewVersion`

### Shared Types Added
- `TenantTemplate`, `TenantTemplateListResponse`, `TenantTemplateVersion`, `OverrideStatus`, `TemplatePreviewResult`

### Backend Model
- Tenant overrides use the same `Template` model with `tenantId` set (not null)
- Backend route: `/v1/templates` (standard CRUD with `x-tenant-id` context)
- Resolution: tenant template > global template (by `templateKey + channel`)
- Immutable version lifecycle: draft → published → retired

## NOTIF-UI-007 — Tenant Template Visibility (Read-Only)

### Pages (apps/web — tenant portal)
| Path | Purpose |
|------|---------|
| `/notifications/templates` | Product selection entry — cards for each product type |
| `/notifications/templates/[productType]` | Product-scoped template list (table) |
| `/notifications/templates/[productType]/[templateId]` | Template detail + versions + branded preview |

### Components
| Component | File | Purpose |
|-----------|------|---------|
| `TemplateDetailClient` | `src/app/(platform)/notifications/templates/[productType]/[templateId]/template-detail-client.tsx` | Global version viewer, override editor, preview panel |

### Server Actions
- `previewTemplateVersion` — POST branded preview via backend (tenantId from session)

### API Client Extensions
- `globalTemplatesList(tenantId, { productType })` — product-scoped template list
- `globalTemplateGet(tenantId, id)` — single template detail
- `globalTemplateVersions(tenantId, templateId)` — version list
- `globalTemplatePreview(tenantId, templateId, versionId, body)` — branded preview

### Shared Types Added
- `GlobalTemplate`, `GlobalTemplateVersion`, `GlobalTemplateListResponse`, `BrandedPreviewResult` in `notifications-shared.ts`

### Key Rules
- Product-first access enforced: templates never shown without product selection
- tenantId derived from session, never from user input

## NOTIF-UI-006 — Tenant Branding Self-Service (Tenant Portal)

### Pages
| Path | Purpose |
|------|---------|
| `/notifications/branding` | Tenant branding list + create/edit/detail (apps/web tenant portal) |

### Components (apps/web)
| Component | File | Purpose |
|-----------|------|---------|
| `TenantBrandingForm` | `src/components/notifications/tenant-branding-form.tsx` | Shared create+edit form with live preview |
| `BrandingPreviewCard` | `src/components/notifications/branding-preview-card.tsx` | Visual brand preview (header, body, footer) |
| `BrandingEmptyState` | `src/components/notifications/branding-empty-state.tsx` | Empty state with CTA |
| `ProductTypeBadge` | `src/components/notifications/product-type-badge.tsx` | Colour-coded product type badge |
| `ColorSwatchField` | `src/components/notifications/color-swatch-field.tsx` | Colour picker + hex text input |

### Server Actions (apps/web)
- `createBranding` — creates branding for the authenticated tenant (tenantId from session)
- `updateBranding` — updates existing branding record

### API Client
- Extended `notifications-server-api.ts` with `brandingList`, `brandingGet`, `brandingCreate`, `brandingUpdate`
- `notifRequest()` supports POST/PATCH via `method` + `body` options
- All requests inject `x-tenant-id` from `session.tenantId` — never from user input

## NOTIF-UI-005 — Control Center Global Templates + Branding Admin UI

### Pages
| Path | Purpose |
|------|---------|
| `/notifications/templates/global` | Global templates list with product type/channel filters |
| `/notifications/templates/global/[id]` | Template detail + versions + metadata edit |
| `/notifications/branding` | Tenant branding list with product filter, create/edit forms |

### Components
| Component | File | Purpose |
|-----------|------|---------|
| `WysiwygEmailEditor` | `src/components/notifications/wysiwyg-email-editor.tsx` | Block-based email editor (heading/paragraph/button/divider/image blocks, brand token insertion, variable insertion) |
| `BrandedPreviewModal` | `src/components/notifications/branded-preview-modal.tsx` | Preview rendered template with tenant branding context |
| `GlobalTemplateCreateForm` | `src/components/notifications/global-template-create-form.tsx` | Create global template modal |
| `GlobalTemplateEditForm` | `src/components/notifications/global-template-edit-form.tsx` | Edit template metadata modal |
| `GlobalTemplateVersionForm` | `src/components/notifications/global-template-version-form.tsx` | Version create with WYSIWYG or HTML editor |
| `GlobalPublishVersionButton` | `src/components/notifications/global-publish-version-button.tsx` | Publish version with confirmation |
| `BrandingCreateForm` | `src/components/notifications/branding-create-form.tsx` | Create tenant branding |
| `BrandingEditForm` | `src/components/notifications/branding-edit-form.tsx` | Edit tenant branding |

### Cache Tags
- `notif:global-templates` — invalidated on template/version create/update/publish
- `notif:branding` — invalidated on branding create/update

### Server Actions (in `actions.ts`)
`createGlobalTemplate`, `updateGlobalTemplate`, `createGlobalTemplateVersion`, `publishGlobalTemplateVersion`, `previewGlobalTemplateVersion`, `createBranding`, `updateBranding`

### API Response Shape
Backend wraps all responses in `{ data: ... }`. `BrandedPreviewResult` has flat `subject`/`body`/`text` + nested `branding: { source, name, primaryColor }`.

## NOTIF-008 — Global Product Templates + Tenant Branding Backend

### New Route Groups (all prefixed `/v1/`)
| Prefix | Description |
|--------|-------------|
| `/v1/templates/global` | Global template CRUD + versioning + branded preview |
| `/v1/branding` | Tenant branding CRUD |

### New Models
- **TenantBranding** — per-tenant, per-product branding (colors, logo, support info, email header/footer)
  - Unique: `(tenant_id, product_type)`

### Template Model Extensions
- `productType` (nullable) — which product owns the template (careconnect, synqlien, etc.)
- `templateScope` — `global` or `tenant`
- `editorType` — `wysiwyg`, `html`, or `text`
- `category` (nullable) — optional grouping
- `isBrandable` — whether branding tokens are injected at render time

### TemplateVersion Extensions
- `editorJson` — WYSIWYG editor source of truth (JSON)
- `designTokensJson` — design token overrides
- `layoutType` — layout classification

### Branding Token System
- Reserved tokens: `{{brand.name}}`, `{{brand.logoUrl}}`, `{{brand.primaryColor}}`, etc.
- Injected at render time by `BrandingResolutionService`
- Fallback: product defaults → platform defaults (code-backed, replaceable)
- Caller template data cannot override branding tokens

### Product Types
`careconnect`, `synqlien`, `synqfund`, `synqrx`, `synqpayout`

### Valid OrgType Values
`LAW_FIRM`, `PROVIDER`, `FUNDER`, `LIEN_OWNER`, `INTERNAL`

## Artifacts API Server (artifacts/api-server)
- **Framework:** Express + Sequelize + PostgreSQL (TypeScript)
- **Port:** 5020 (dev) — started by `scripts/run-dev.sh`
- **Purpose:** Feedback traceability and artifact management service for Xenia v2.0

### XNA_Core-08-011 — Reverse Traceability & Artifact-Centric Feedback View
- Reverse lookup from artifact → feedback_action_links → feedback_action_items → feedback_records
- Admin-only API: `GET /api/admin/artifacts/:artifactType/:artifactId/feedback-links`
- Supported artifact types: FEATURE, DEFECT, REQUIREMENT, MITIGATION
- JWT-based admin RBAC middleware (requires PlatformAdmin or TenantAdmin)
- Deterministic ordering: status priority (OPEN → IN_PROGRESS → RESOLVED → DISMISSED), then date descending, then ID ascending
- CC UI: `/artifacts` pages with LinkedFeedbackPanel component
- CC nav: "TRACEABILITY → Artifacts" section in sidebar
- Database tables: `feedback_records`, `feedback_action_items`, `feedback_action_links`, `artifacts`

## LS-COR-AUT-005 — Admin UI Access Management Layer — COMPLETED 2026-04-10

Control Center UI for managing LS-COR-AUT-004 tenant-scoped Access Groups.

**Types (`types/control-center.ts`):**
- `AccessGroupSummary`, `AccessGroupMember`, `GroupProductAccess`, `GroupRoleAssignment`

**API client (`lib/control-center-api.ts`):**
- `controlCenterServerApi.accessGroups` namespace — full CRUD: list, getById, create, update, archive, addMember, removeMember, listMembers, grantProduct, revokeProduct, listProducts, assignRole, removeRole, listRoles, listUserGroups
- Gateway paths: `/identity/api/tenants/{tenantId}/groups/...`

**BFF routes (`app/api/access-groups/[tenantId]/...`):**
- POST create, PATCH update, DELETE archive groups
- POST add / DELETE remove members
- PUT grant / DELETE revoke products
- POST assign / DELETE remove roles
- All routes: `requireAdmin()` auth, `ServerApiError` status passthrough

**Pages:**
- `/groups` — tenant-context-aware Access Groups list (requires tenant context); `CreateAccessGroupButton` modal (Tenant/Product scope)
- `/access-groups/[tenantId]/[groupId]` — detail page with `AccessGroupInfoCard`, `AccessGroupMembersPanel`, `GroupProductAccessPanel`, `GroupRoleAssignmentPanel`, `AccessGroupActions`

**User detail integration:**
- `AccessGroupMembershipPanel` component on `/tenant-users/[id]` page — shows user's access group memberships with add/remove

**Route builder:** `Routes.accessGroupDetail(tenantId, groupId)` → `/access-groups/{tenantId}/{groupId}`

**Nav:** Groups entry marked `badge: 'LIVE'` in sidebar

## LS-COR-AUT-006 — Legacy Cleanup + Model Unification — COMPLETED 2026-04-10

Removed all legacy role resolution and group management systems. JWT `product_roles` claims now use exclusively `PRODUCT:Role` format (e.g., `SYNQ_CARECONNECT:CARECONNECT_RECEIVER`) from `EffectiveAccessService`.

**Removed:**
- `ProductRoleResolutionService`, `CareConnectRoleMapper`, `IProductRoleMapper`, `IProductRoleResolutionService`, `EffectiveAccessContext` DTO
- Legacy merge logic in `AuthService.LoginAsync` (was merging legacy bare role codes with effective-access roles)
- Legacy `/api/admin/groups/*` endpoints (5 routes + handlers) from `AdminEndpoints.cs`
- Legacy group UI: `groups/[id]` page, `GroupMembershipPanel`, `GroupDetailCard`, `GroupListTable`, `GroupPermissionsPanel`, BFF proxy routes
- Legacy `groups` namespace from `controlCenterServerApi`, `GroupSummary`/`GroupDetail`/`GroupMemberSummary` types, `mapGroupSummary`/`mapGroupDetail` mappers
- Static `ProductToRolesMap` dictionary from `ProductRoleClaimExtensions`

**Updated:**
- `ProductRoleClaimExtensions`: `HasProductAccess` now checks `PRODUCT:` prefix; `HasProductRole` checks `PRODUCT:ROLE` exact match
- Groups page: removed legacy fallback, requires tenant context
- Tenant user detail: removed `GroupMembershipPanel`, kept only `AccessGroupMembershipPanel`

**Retained:** `ScopedRoleAssignment` (used for system roles), `TenantGroup`/`GroupMembership` DB tables (for data migration)

## LS-COR-AUT-006A — Residual Legacy Closure + Validation Hardening — COMPLETED 2026-04-10

Final closure of the legacy authorization model. All frontend and backend consumers now use the unified `PRODUCT:Role` claim format.

**Fixed:**
- Frontend `ProductRole` constants in both `apps/web` and `apps/control-center` updated to `PRODUCT:Role` format (e.g., `SYNQ_CARECONNECT:CARECONNECT_REFERRER`)
- Fund service `CanReferFund`/`CanFundApplications` policies use `RequireClaim("product_roles", "SYNQ_FUND:...")` instead of broken `RequireRole`
- `AdminEndpoints.cs` ListUsers/GetUser queries replaced legacy `GroupMemberships` with `AccessGroupMemberships`
- Pre-existing type gaps fixed: `ApiResponse`, `TenantBranding`, `NavGroup.icon`, `NavItem.badgeKey`, optional `enabledProducts` null-safety

**Deprecated:**
- `TenantGroup.cs` and `GroupMembership.cs` marked `[Obsolete]` with `#pragma warning` suppression in `IdentityDbContext`
- DbSets retained for EF migration compatibility only — no runtime queries

**Documented:**
- `ScopedRoleAssignment.cs` XML doc describes the dual-boundary role model: SRA for system roles (GLOBAL scope), URA/GRA for product roles (JWT claims)

**Tests:**
- 20 xUnit tests in `BuildingBlocks.Tests` validating `ProductRoleClaimExtensions` (prefixed claims, bare code rejection, cross-product isolation, admin bypass, case-insensitive matching)

**Report:** `analysis/LS-COR-AUT-006A-report.md`

## LS-COR-AUT-007 — Enforcement Completion + Hardening — COMPLETED 2026-04-11

**Fund Enforcement:** `ApplicationEndpoints` group-level `.RequireProductAccess(ProductCodes.SynqFund)` + role-specific filters: create/update/submit → `SYNQFUND_REFERRER`; begin-review/approve/deny → `SYNQFUND_FUNDER`.

**CareConnect:** Confirmed all non-admin endpoints already enforced with `.RequireProductAccess(ProductCodes.SynqCareConnect)`. Admin endpoints correctly use `PlatformOrTenantAdmin` (admins bypass product checks via `IsTenantAdminOrAbove()`).

**Legacy Table Removal:**
- Deleted `TenantGroup.cs`, `GroupMembership.cs` entity files and EF configurations
- Removed `[Obsolete]` DbSets from `IdentityDbContext`
- Migration `20260411000001_DropLegacyGroupTables.cs` drops both tables
- Snapshot fully cleaned of entity blocks, FK relationships, and navigation blocks

**ScopedRoleAssignment GLOBAL-Only:**
- `ScopeTypes` simplified to single `Global` constant with `IsValid()` validator
- `Create()` rejects non-GLOBAL scopes with `ArgumentException`, forces Org/Product IDs to null
- `AdminEndpoints.AssignRole` blocks non-GLOBAL at API layer
- Diagnostic endpoint updated to use string literals for deprecated scope types

**Security Fix — HasProductAccess:**
- `ProductRoleClaimExtensions.HasProductAccess` now requires non-empty role segment (rejects `"SYNQ_FUND:"`)
- Previously only checked `StartsWith(prefix)` — empty role segment bypassed access check

**Effective Access UI:** `EffectivePermissionsPanel` enhanced with Direct (blue) vs Group (purple) source attribution badges, `SourceSummary` component, color-coded legend.

**Tests:** 45 xUnit tests total (20 original + 8 ScopedRoleAssignment domain + 17 claim hardening including empty-role-segment security fix)

**Report:** `analysis/LS-COR-AUT-007-report.md`

## LS-COR-AUT-008 — Observability + Scale Hardening — COMPLETED 2026-04-11

**Effective Access Caching:** `EffectiveAccessService` uses `IMemoryCache` with key `ea:{tenantId}:{userId}:{accessVersion}`, 5-min TTL. AccessVersion auto-invalidates on any role/product/group mutation. Stopwatch timing + cache hit/miss counters.

**Batch AccessVersion:** `GroupRoleAssignmentService` and `GroupProductAccessService` use `ExecuteUpdateAsync` for single-SQL batch version increment instead of N entity loads.

**Authorization Observability:** All 3 filters (`RequireProductAccessFilter`, `RequireProductRoleFilter`, `RequireOrgProductAccessFilter`) emit structured `AuthzDecision` logs (userId, tenantId, method, endpoint, product, requiredRoles, source, accessVersion). DENY=Warning, ALLOW=Information.

**Debug Endpoint:** `GET /api/admin/users/{id}/access-debug` — returns full access breakdown: products (with Direct/Group source), roles (with source), systemRoles, groups, entitlements, productRolesFlat, tenantRoles, accessVersion.

**Access Audit Viewer:** Quick-filter presets on `/audit-logs` page: Access Changes, Security Events, Role Assignments, Group Membership, Product Access.

**Access Explanation UI:** `AccessExplanationPanel` component on user detail page. Expandable product sections, Direct/Group badges, system roles, entitlements, group memberships, JWT claims preview. Fetches from `/access-debug` endpoint.

**Login Performance:** `AuthService.LoginAsync` instrumented with Stopwatch — logs `LoginPerf` with userId, tenantId, elapsedMs, accessVersion.

**Tests:** 57 xUnit tests total (45 prior + 12 new observability tests: cache key format, AuthzDecision fields, JWT claim format, empty-role-segment, access-debug source attribution).

**Report:** `analysis/LS-COR-AUT-008-report.md`

## LS-COR-AUT-009 — Permission / Capability Layer — COMPLETED 2026-04-11

**Permission Resolution:** `EffectiveAccessService.ResolvePermissionsAsync()` resolves capabilities from UserRoleAssignment → RoleCapabilityAssignment (Direct) and GroupRoleAssignment → ProductRole → RoleCapability (Inherited). Format: `{PRODUCT_CODE}.{capability_code}` (e.g., `SYNQ_CARECONNECT.referral:create`). Cross-product consistency enforced (`RoleProductId == CapabilityProductId`).

**JWT Claims:** `permissions` multi-value claim added alongside `product_roles`. Backward compatible — existing token consumers unaffected.

**RequirePermissionFilter:** New `IEndpointFilter` checking `permissions` JWT claim with admin bypass. Extension: `.RequirePermission("PRODUCT.capability")`. Structured `PermissionDecision` logging (DENY=Warning, ALLOW=Information). Error code: `PERMISSION_DENIED`.

**Claim Extensions:** `HasPermission(permissionCode)`, `GetPermissions()` on `ClaimsPrincipal` (case-insensitive, admin bypass).

**API Endpoints:** `GET /api/admin/permissions/by-product/{productCode}` — filtered permission catalog (admin-only). General catalog already at `GET /api/admin/permissions` (UIX-002).

**Access Debug:** `/access-debug` response extended with `permissions` (flat list) and `permissionSources` (with provenance: permissionCode, productCode, source, viaRoleCode, groupId, groupName).

**Admin UI:** `AccessExplanationPanel` shows Permissions section grouped by product with capability code, via-role, and source badge. JWT Claims Preview shows separate `product_roles` and `permissions` sub-sections.

**Tests:** 68 xUnit tests total (57 prior + 11 new permission tests: HasPermission match/case-insensitive/no-match/no-claims, admin bypass, cross-product isolation, partial code rejection, GetPermissions, multiple permissions).

**Report:** `analysis/LS-COR-AUT-009-report.md`

## LS-COR-AUT-010 — Permission Governance + Enforcement Migration — COMPLETED 2026-04-11

**Capability Entity Governance:** Added `Category` (max 100), `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy` governance columns. Domain methods: `Update()`, `Deactivate()`, `Activate()`. Naming convention: `^[a-z][a-z0-9]*(?:\:[a-z][a-z0-9]*)*$` (`{domain}:{action}` lowercase colon-separated). Validated in `IsValidCode()` and `Create()`. Seed data enriched with Category values.

**Permission CRUD API:** `POST /api/admin/permissions` (create with naming convention + duplicate validation, accepts `productCode` or `productId`), `PATCH /api/admin/permissions/{id}` (update name/description/category), `DELETE /api/admin/permissions/{id}` (soft deactivate). Admin-only. Audit events: `permission.created` (Info), `permission.updated` (Info), `permission.deactivated` (Warning) via inline `auditClient.IngestAsync()`.

**Enforcement Migration (Fund):** Migrated `Fund.Api/Endpoints/ApplicationEndpoints.cs` from `RequireProductRole` → `RequirePermission`. Permission mapping: `application:create` (create/update/submit), `application:evaluate` (begin-review), `application:approve` (approve), `application:decline` (deny). Admin bypass preserved.

**Admin UI CRUD:** Control Center permissions page upgraded from read-only to full CRUD. Components: `PermissionCreateDialog` (product selector, code validation, create form), `PermissionRowActions` (edit dialog, deactivate confirmation), updated `PermissionCatalogTable` (Category + Actions columns). Server actions in `permissions/actions.ts` with `requirePlatformAdmin()` guard. API client methods: `permissions.create()`, `permissions.update()`, `permissions.deactivate()`.

**Type/Mapper Updates:** `PermissionCatalogItem` extended with `category`, `productCode`, `updatedAtUtc`. `mapPermissionCatalogItem` updated. API client CRUD methods added with `revalidateTag(CACHE_TAGS.roles)`.

**Tests:** 39 new tests (107 total): naming convention (valid/invalid/null), `Capability.Create` (fields, normalization, exceptions, whitespace), `Update` (fields, validation), `Deactivate`/`Activate` (state transitions), `HasPermission` claim checks (match, non-match, empty, case-insensitive, multiple, cross-product blocking), admin bypass checks.

**Report:** `analysis/LS-COR-AUT-010-report.md`

## LS-COR-AUT-011 — Advanced Authorization (ABAC + Context-Aware Policies) — COMPLETED 2026-04-11

**Domain Entities:** `Policy` (code pattern `PRODUCT.domain.qualifier`, factory create + lifecycle), `PolicyRule` (11 supported fields, operator/field validation, AND/OR grouping), `PermissionPolicy` (junction linking permission codes to policies). Enums: `PolicyConditionType` (Attribute/Resource/Context), `RuleOperator` (10 operators including In/NotIn/Contains/StartsWith), `LogicalGroupType` (And/Or).

**EF Configuration:** `Policies`, `PolicyRules`, `PermissionPolicies` tables with proper indexes. Unique index on PolicyCode. Unique composite index on (PermissionCode, PolicyId).

**Policy Evaluation Engine:** `PolicyEvaluationService` — loads active policies linked to permission via PermissionPolicies, evaluates rules against merged user/resource/request attributes, returns `PolicyEvaluationResult` with full explainability (MatchedPolicy + RuleResult per rule). AND/OR logical grouping. Numeric comparison for amount/time fields. `DefaultAttributeProvider` extracts attributes from JWT claims, resource context dict, and HttpContext.

**RequirePermissionFilter Enhancement:** When `Authorization:EnablePolicyEvaluation=true`, filter calls `IPolicyEvaluationService.EvaluateAsync()` after PBAC claim check passes. Resource context injectable via `httpContext.Items["PolicyResourceContext"]` as `Dictionary<string,object?>`. Backward compatible — if no policies linked, existing behavior preserved.

**Admin API:** Full policy CRUD (list/create/update/deactivate), rule CRUD within policy, permission-policy mapping CRUD, supported-fields endpoint. Access debug extended with linked-policy info.

**Control Center UI:** Policy list page with product filter chips, policy detail page with tabbed view (Rules/Permissions/Info), visual rule builder with field/operator dropdowns, permission linking UI. Navigation entry under IDENTITY section. All operations via Next.js API route handlers proxying to server API.

**Tests:** 47 new tests (153 total in BuildingBlocks.Tests): policy code validation, domain creation/update/deactivation, rule field validation, operator constraints, PermissionPolicy lifecycle.

**Config:** `Authorization:EnablePolicyEvaluation=true` to activate. `Authorization:EnableRoleFallback=true` for PBAC fallback.

**Report:** `analysis/LS-COR-AUT-011-report.md`

## LS-COR-AUT-011A — Policy Engine Hardening + Observability — COMPLETED 2026-04-11

**PolicyEffect Enum:** `PolicyEffect.Allow` / `PolicyEffect.Deny` added to `Identity.Domain`. `Policy.Effect` property (default `Allow`). `Policy.Create()` accepts optional `effect` parameter. `Policy.Update()` accepts optional `PolicyEffect? effect` (null preserves existing).

**Deny-Override Semantics:** `PolicyEvaluationService` evaluates all policies in deterministic order (Priority ASC → PolicyCode ASC → Id ASC). If any matched Deny-effect policy's rules pass, the result is an immediate deny override regardless of Allow policies. `PolicyEvaluationResult.DenyWithOverride()` factory sets `DenyOverrideApplied=true` and `DenyOverridePolicyCode`.

**IMemoryCache Caching:** Cache key `policy:{tenantId}:{userId}:{permission}:{policyVersion}:{resourceHash}`. TTL configurable via `Authorization:PolicyCacheTtlSeconds` (default 60). Cache skipped when resource context is empty/incomplete. `PolicyVersion` in cache key auto-invalidates on policy changes.

**PolicyVersionProvider:** `InMemoryPolicyVersionProvider` (Singleton) with `Interlocked`-based thread-safe `CurrentVersion` and `Increment()`. All Admin API CRUD handlers (create/update/deactivate policy, create/update/deactivate rule, create/deactivate permission-policy mapping) call `Increment()` after `SaveChangesAsync`.

**IPolicyResourceContextAccessor:** Abstraction in BuildingBlocks for standardized resource context access. Implementation reads from `HttpContext.Items["PolicyResourceContext"]`.

**Structured Logging:** `RequirePermissionFilter` emits structured `PolicyDecision` log entries with full shape (permission, user, tenant, elapsed, matched policies, rule results, deny override, cache hit, resource context present). DENY at `Warn` severity, ALLOW at `Debug`.

**Admin API:** All policy responses include `effect` field. `SupportedFields` endpoint returns `effects` array. ABAC CRUD handlers in `AdminEndpoints` class.

**Frontend:** `PolicySummary.effect` field. Effect badge (emerald=Allow, red=Deny) on policy list table and detail panel. `SupportedFieldsResponse.effects`. `mapSupportedFields` mapper updated.

**Tests:** 153 total (PolicyDomain: PolicyEffect creation/update/preservation, PolicyVersionProvider: initial/increment/monotonic/thread-safety, PolicyEvaluationResult: Allow/Deny/AllowWithPolicies/DenyWithOverride factories, MatchedPolicy/RuleResult defaults).

## LS-COR-AUT-011B — Distributed Policy Engine + Multi-Instance Scaling — COMPLETED 2026-04-11

**Distributed Version Provider:** `RedisPolicyVersionProvider` uses Redis `INCR`/`GET` on key `legalsynq:policy:version` for global monotonic versioning across all instances. Falls back to in-memory `Interlocked` counter on Redis failure. Registered as Singleton; provider selected via `Authorization:PolicyVersioning:Provider` (InMemory|Redis).

**Distributed Cache:** `IPolicyEvaluationCache` abstraction replaces direct `IMemoryCache` usage. `RedisPolicyEvaluationCache` serializes `PolicyEvaluationResult` as JSON to Redis STRING with TTL. `InMemoryPolicyEvaluationCache` wraps `IMemoryCache` behind the same interface. Provider selected via `Authorization:PolicyCaching:Provider` (InMemory|Redis). All Redis operations fail-open with warnings.

**Immutable Cache Values:** Cache hits return a defensive copy (new `PolicyEvaluationResult` instance). Redis cache inherently creates new instances via JSON deserialization. No shared mutable state across requests.

**Cross-Node Invalidation:** All Admin API CRUD handlers call `IPolicyVersionProvider.Increment()` after mutations. With Redis, `INCR` is globally visible — all nodes see the new version immediately. Cache keys include version → stale entries become unreachable without explicit eviction.

**Logging Controls:** `PolicyLoggingOptions` — configurable `AllowLevel`/`DenyLevel` (Trace→Critical), `SampleRate` (0.0–1.0), `LogRuleResultsOnAllow` toggle, master `Enabled` switch. Thread-safe sampling via `ThreadLocal<Random>`.

**Performance Metrics:** `PolicyMetrics` singleton — `Interlocked`-based counters for evaluation count/latency, cache hits/misses/errors/latency, version read count/latency. `GetSnapshot()` returns `PolicyMetricsSnapshot` for admin endpoints.

**Resource Hashing:** `ComputeResourceHash` — deterministic, order-independent (sorted keys), case-insensitive (normalized to lowercase), SHA-256 truncated to 16 hex chars. Null values handled as literal "null". Empty context returns "empty".

**Fallback Behavior:** Redis failures → fail-open (compute from DB). Malformed cache → ignore. Version read failure → local fallback counter. All operations continue without authorization denial on infrastructure failure.

**Package:** `StackExchange.Redis 2.7.33` added to `Identity.Infrastructure`.

**Config:** `Authorization:PolicyCaching:Provider`, `Authorization:PolicyVersioning:Provider`, `Authorization:PolicyLogging:*`, `Authorization:Redis:Url`.

**Tests:** 195 total (27 new: config options defaults, Redis config, metrics thread-safety, InMemory cache roundtrip, resource hashing order-independence/case-insensitivity/null-handling, cache key segment verification/version isolation/tenant isolation).

**Report:** `analysis/LS-COR-AUT-011B-report.md`

## LS-COR-AUT-011C — Distributed Resilience + Performance Optimization — COMPLETED 2026-04-12

**Version Fallback — Freeze Mode (CRITICAL FIX):** `RedisPolicyVersionProvider` no longer increments a local counter on Redis failure. Instead, it enters FREEZE mode: returns last known version, skips all increments, disables cache writes. Auto-recovers when Redis becomes available. Prevents cross-node version divergence. `IPolicyVersionProvider.IsHealthy`/`IsFrozen` properties exposed.

**Cache Stampede Protection:** Per-key `SemaphoreSlim` coalescing in `PolicyEvaluationService`. First cache-miss request evaluates; concurrent same-key requests await the result. Inflight results stored in `ConcurrentDictionary` with 5s expiry. 5s timeout on lock acquisition prevents deadlocks — falls through to direct evaluation. Lock cleanup via `Task.Delay`.

**Tenant-Scoped Versioning:** `IPolicyVersionProvider.GetVersion(tenantId?)` and `IncrementVersion(tenantId?)`. Config: `PolicyVersioning:Scope` = Global|Tenant. Redis keys: `legalsynq:policy:version` (global), `legalsynq:policy:version:{tenantId}` (tenant). Default: Global. In-memory uses `ConcurrentDictionary<string, long>` for tenant versions.

**OpenTelemetry Metrics:** `System.Diagnostics.Metrics` instrumentation in `PolicyMetrics`. Meter: `LegalSynq.Policy`. Counters: evaluations, cache hits/misses/errors, stampede coalesced, freeze events. Histograms: evaluation/cache-read/version-read latency. Observable gauges: cache hit rate, average evaluation latency. Export via `AddOpenTelemetry().WithMetrics(m => m.AddMeter("LegalSynq.Policy"))`.

**Cache Memory Controls:** `PolicyCachingOptions.KeyPrefix` (default: `"policy"`). Configurable key prefix for environment scoping. TTL enforcement, version rotation, freeze-mode write disable, documented `maxmemory-policy allkeys-lru` recommendation.

**Resource Hashing Hardening:** Hash version prefix `v1:{hash}`. `SerializeValue()` handles null, string, numeric, `JsonElement`, arrays/collections (sorted). Arrays order-independent. Null vs empty string differentiated. 19-char output: `v1:` + 16 hex.

**Failure Modes:** All fail-open. Version read failure → freeze. Version increment failure → retry once → freeze. Cache read failure → compute from DB. Cache write failure → silently skip. All paths logged, deterministic, safe. No authorization denial from infrastructure failure.

**Tests:** 236 total (41 new: freeze mode, stampede SemaphoreSlim 1000-concurrent no-deadlock, tenant versioning isolation, hash edge cases incl. JsonElement canonicalization, security, performance benchmarks, concurrent cache operations).

**Report:** `analysis/LS-COR-AUT-011C-report.md`

## LS-COR-AUT-011D — Policy Simulation + Decision Testing — COMPLETED 2026-04-12

**Authorization Simulation Service:** `AuthorizationSimulationService` in `Identity.Infrastructure` — safe what-if testing against live or draft policies without mutating production state. Reuses `PolicyEvaluationService.EvaluatePolicy()`, `EvaluateOperator()`, `MergeAttributes()` (now `public static`). Resolves target user's effective access via `IEffectiveAccessService`. Admin bypass detection mirrors runtime behavior.

**Simulation Modes:** `Live` evaluates current active policies. `Draft` appends an in-memory policy definition alongside live policies. `ExcludePolicyIds` isolates specific policy effects by removing them from evaluation. No database writes in any mode.

**API Endpoint:** `POST /api/admin/authorization/simulate` in `AdminEndpoints`. Tenant-scoped: TenantAdmin restricted to own tenant, PlatformAdmin unrestricted. Validates permissionCode format, user/tenant existence, draft policy rules. Returns full explainability output: allow/deny decision, permission sources (direct/inherited), policy evaluation breakdown with per-rule results, draft policy highlighting, deny-override identification.

**Audit:** `authorization.simulation.executed` event — category `Administrative`, visibility `Platform`, severity `Info`. Tags: `["simulation", "authorization", "live"/"draft"]`. Fire-and-forget. Distinct from runtime `user.authorization.*` events.

**Control Center UI:** `/authorization-simulator` route. `simulator-form.tsx` client component with tenant/user/permission inputs, resource/request context JSON editors, collapsible draft policy builder (visual rule builder with field/operator/value/logicalGroup). Result panel: allow/deny banner, user identity + roles, permission source attribution, policy evaluation breakdown with rule results table, draft policy DRAFT badges, copy JSON + raw JSON toggle.

**Static Method Visibility:** `EvaluatePolicy`, `EvaluateRule`, `EvaluateOperator`, `MergeAttributes` changed from `private static` → `public static` in `PolicyEvaluationService` to enable direct reuse by simulation service and test verification.

**Tests:** 256 total (20 new: 13 SimulationTests, 3 SimulationSecurityTests, 4 SimulationRegressionTests). Covers operator evaluation, attribute merging, policy immutability, deny override, explainability output, public method accessibility.

**Report:** `analysis/LS-COR-AUT-011D-report.md`

## Login Page Logo Fix — 2026-04-13

### Fix: Preserve `tenant_code` cookie on logout
- `apps/web/src/app/api/auth/logout/route.ts` — no longer clears the `tenant_code` cookie on logout. The cookie is non-sensitive (stores only the tenant code, e.g. "MANER") and keeping it lets the login page `TenantBrandingProvider` resolve the correct tenant branding for returning users, without requiring subdomain DNS resolution.
- `PublicLogoEndpoints` DocumentTypeId filter intentionally preserved — CC logo uploads already use the correct type (`20000000-0000-0000-0000-000000000002`), and removing the filter would create a broken-access-control risk.

### Root cause
- Logout cleared `tenant_code` cookie → `TenantBrandingProvider` had no header/cookie for tenant resolution → Identity branding endpoint returned default branding with null `logoDocumentId` → no logo sources in the cascade → no logo rendered on login page

### OrganizationType Seed IDs
- Internal: `70000000-0000-0000-0000-000000000001`
- LawFirm: `70000000-0000-0000-0000-000000000002`
- Provider: `70000000-0000-0000-0000-000000000003`
- Funder: `70000000-0000-0000-0000-000000000004`
- LienOwner: `70000000-0000-0000-0000-000000000005`

## TenantAdmin Product Role Auto-Grant — 2026-04-13

### Summary
When a TenantAdmin logs in, they automatically receive the full scope of all product roles for every product enabled on their tenant — no explicit `UserRoleAssignment` records needed.

### Root Cause
`EffectiveAccessService.ComputeEffectiveAccessAsync` was querying `TenantProductEntitlements` (a newer, unpopulated table) instead of `TenantProducts` (the authoritative source for tenant product enablement). This caused zero active entitlements, so the auto-grant logic never fired.

### Changes
- **`Identity.Infrastructure/Services/EffectiveAccessService.cs`** — (1) Changed entitlement query from `TenantProductEntitlements` to `TenantProducts.Where(tp => tp.IsEnabled).Select(tp => tp.Product.Code)` (TenantProducts is the authoritative source). (2) Added `isTenantAdmin` check via `ScopedRoleAssignments` (GLOBAL scope, Role.Name == "TenantAdmin"). (3) If TenantAdmin: auto-adds all entitled products to effective products, queries active `ProductRoles` filtered by entitled product codes at DB level. (4) Permission resolution (`ResolvePermissionsAsync`) now includes TenantAdmin auto-granted role codes, with "TenantAdmin" source attribution. (5) Debug logging for auto-grant counts.

### Verified
- MANER-LAW TenantAdmin (`maner@xentrasolutions.com`) now receives 3 products, 8 product roles, and 29 permissions on login
- Product roles: `SYNQ_CARECONNECT:CARECONNECT_RECEIVER`, `SYNQ_CARECONNECT:CARECONNECT_REFERRER`, `SYNQ_FUND:SYNQFUND_APPLICANT_PORTAL`, `SYNQ_FUND:SYNQFUND_FUNDER`, `SYNQ_FUND:SYNQFUND_REFERRER`, `SYNQ_LIENS:SYNQLIEN_BUYER`, `SYNQ_LIENS:SYNQLIEN_HOLDER`, `SYNQ_LIENS:SYNQLIEN_SELLER`
- BFF login response includes all auto-granted product roles in session envelope
- PlatformAdmin (non-TenantAdmin) is unaffected — no regression
- Cache works correctly (HIT on second request)

## Liens Domain Entity Foundation — 2026-04-13

### Summary
Defined foundational domain entities for the Liens microservice following v2 patterns from Fund and CareConnect services. Domain layer remains pure — no persistence, API, or auth logic.

### Entities Created
- **Case** (`Liens.Domain/Entities/Case.cs`) — TenantId, OrgId, CaseNumber, ExternalReference, inline client fields, status lifecycle (PreDemand→DemandSent→InNegotiation→CaseSettled→Closed), insurance fields, demand/settlement amounts
- **Contact** (`Liens.Domain/Entities/Contact.cs`) — ContactType (LawFirm, Provider, LienHolder, CaseManager, InternalUser), FirstName/LastName/DisplayName, inline address, IsActive
- **Facility** (`Liens.Domain/Entities/Facility.cs`) — Name, Code, ExternalReference, inline address, OrganizationId soft FK to Identity, IsActive
- **LookupValue** (`Liens.Domain/Entities/LookupValue.cs`) — Category-driven (CaseStatus, LienStatus, etc.), tenant-scoped or global, IsSystem guard
- **ServicingItem** (`Liens.Domain/Entities/ServicingItem.cs`) — LS-LIENS-UI-004: task management entity with TenantId, OrgId, TaskNumber (unique per tenant), TaskType, Description, Status lifecycle (Pending→InProgress→Completed/Escalated/OnHold), Priority (auto-escalation to Urgent on escalate), AssignedTo, DueDate, CaseId/LienId cross-entity links, Notes, Resolution, timeline timestamps (StartedAtUtc, CompletedAtUtc, EscalatedAtUtc). Full backend stack: entity → repository → service → DTOs → endpoints. Table: `liens_ServicingItems`. EF migration: `AddServicingItem`. API: 5 endpoints at `/api/liens/servicing`.

### Supporting Types
- **Enums:** `CaseStatus`, `ContactType`, `LienType`, `LienStatus`, `ServicingStatus`, `ServicingPriority`, `LookupCategory` (all string constants with `IReadOnlySet<string> All`)
- **Value Objects:** `Address` (sealed record with factory method)

### Patterns Followed
- Inherits `AuditableEntity` from BuildingBlocks
- Private constructors + static `Create()` factory methods
- Private setters + domain update methods
- `ArgumentException.ThrowIfNullOrWhiteSpace` validation guards
- String constants for statuses (CareConnect pattern, not C# enums)

### v1 Compatibility
- All v1 SynqLiens field names preserved or mapped with simple transformation
- `programId` → `TenantId`/`OrgId`
- `clientName` → `ClientFirstName`/`ClientLastName` (structured)
- `zipCode` → `PostalCode` (v2 convention)

### Build
- Full Liens service stack: 0 warnings, 0 errors
- Report: `analysis/LS-LIENS-03-001-report.md`

## Core Lien Domain Entity — 2026-04-13

### Summary
Created the core `Lien` domain entity as the central business object of the SynqLiens product. Models a medical/legal lien through its full lifecycle — from draft creation through marketplace listing, sale, servicing, and settlement.

### Entity Created
- **Lien** (`Liens.Domain/Entities/Lien.cs`) — 28 properties, 10 domain methods. Full marketplace lifecycle with multi-party ownership (Seller → Buyer → Holder).

### Supporting Types Added/Modified
- **`LienParticipantRole`** (new) — Seller, Buyer, Holder constants
- **`LienStatus`** (expanded) — 9 statuses: Draft, Offered, UnderReview, Sold, Active, Settled, Withdrawn, Cancelled, Disputed. Includes `Open`, `Terminal` subsets and explicit `AllowedTransitions` matrix.

### Key Design Decisions
- Financial fields: `OriginalAmount`, `CurrentBalance`, `OfferPrice`, `PurchasePrice`, `PayoffAmount` (plain decimal, matching Fund/CareConnect v2 pattern)
- Multi-party: `SellingOrgId`, `BuyingOrgId`, `HoldingOrgId` as Guid FKs (no navigation properties)
- Subject party: Inline `SubjectFirstName`/`SubjectLastName` snapshot + `SubjectPartyId` FK-ready
- Transition matrix enforced in domain: `TransitionStatus()` validates against `AllowedTransitions` dictionary
- All domain methods set `ClosedAtUtc` on terminal transitions consistently

### Build
- Full Liens service stack: 0 warnings, 0 errors
- Report: `analysis/LS-LIENS-03-002-report.md`

## Database Table Prefix Convention — 2026-04-13

### Convention
Each microservice uses a table name prefix for organizational clarity:

| Service | Prefix | DB Engine | Example Table |
|---------|--------|-----------|---------------|
| Identity | `idt_` | MySQL | `idt_Tenants`, `idt_Users`, `idt_Organizations` |
| Fund | `fund_` | MySQL | `fund_Applications` |
| CareConnect | `cc_` | MySQL | `cc_Referrals`, `cc_Providers`, `cc_Appointments` |
| Notifications | `ntf_` | MySQL | `ntf_Notifications`, `ntf_Templates` |
| Audit | `aud_` | MySQL/SQLite | `aud_AuditEventRecords`, `aud_LegalHolds` |
| Documents | `docs_` | PostgreSQL | `docs_documents`, `docs_document_versions` |
| Liens | `liens_` | MySQL | `liens_Cases`, `liens_Liens`, `liens_BillsOfSale` |

### Implementation
- Each service's EF Core entity configurations use `builder.ToTable("prefix_TableName")`
- Documents service includes auto-migration SQL to rename old unprefixed tables on startup
- The `document_types`, `artifacts`, `feedback_*` tables are managed by Node.js services (not EF Core) and remain unprefixed

### Files Changed
- Identity: 33 configuration files in `Identity.Infrastructure/Data/Configurations/`
- Fund: 1 configuration file
- CareConnect: 23 configuration files
- Notifications: 18 ToTable calls across 5 configuration files + `SchemaRenamer.cs` startup migration (tables, columns, indexes)
- Audit: 7 configuration files
- Documents: `DocsDbContext.cs` + `schema.sql` + `Program.cs` (auto-rename migration)

## LienOffer Domain Entity — 2026-04-13

### Summary
Created the `LienOffer` domain entity to model marketplace buyer offers against liens. Supports the full offer lifecycle: create, update pending, accept, reject, withdraw, expire.

### Entity Created
- **LienOffer** (`Liens.Domain/Entities/LienOffer.cs`) — 15 properties, 6 domain methods. Buyer→Seller marketplace negotiation with clock-based expiration guard.

### Supporting Types Added
- **`OfferStatus`** (new) — Pending, Accepted, Rejected, Withdrawn, Expired. Includes `Terminal` subset and `AllowedTransitions` matrix.

### Key Design Decisions
- `BuyerOrgId` + `SellerOrgId` (snapshot) for party identification
- `Notes` (buyer) + `ResponseNotes` (seller) separation
- `ExpiresAtUtc` with domain-enforced expiration via `EnsurePendingAndNotExpired()` guard
- `IsExpired` computed property covers both explicit and clock-based expiry
- `Expire()` accepts optional `Guid?` for system vs user-triggered expiration

### Build
- Full Liens service stack: 0 warnings, 0 errors
- Report: `analysis/LS-LIENS-03-003-report.md`

## Liens DbContext + Initial Migration (LS-LIENS-04-002) — 2026-04-14

### Summary
Created `LiensDbContext` with 7 DbSets, wired into DI with MySQL/Pomelo, added design-time factory, and generated the initial EF Core migration.

### DbContext
- **Location:** `Liens.Infrastructure/Persistence/LiensDbContext.cs`
- **DbSets:** Cases, Contacts, Facilities, LookupValues, Liens, LienOffers, BillsOfSale
- **OnModelCreating:** `ApplyConfigurationsFromAssembly(typeof(LiensDbContext).Assembly)`
- **SaveChangesAsync:** Overridden — auto-populates `CreatedAtUtc`/`UpdatedAtUtc` on `AuditableEntity` entries (same as Fund/CareConnect)

### DI Registration
- `DependencyInjection.cs` → `AddLiensServices()` registers `LiensDbContext` via `AddDbContext<LiensDbContext>` with `UseMySql()` (Pomelo, MySQL 8.0)
- Connection string key: `LiensDb` (placeholder in appsettings.json)

### Design-Time Factory
- `Liens.Api/DesignTimeDbContextFactory.cs` — reads appsettings, builds context for `dotnet ef` CLI

### Migration
- **Name:** `InitialCreate` (timestamp: 20260414041807)
- **Location:** `Liens.Infrastructure/Persistence/Migrations/`
- **Tables:** `liens_Cases`, `liens_Contacts`, `liens_Facilities`, `liens_LookupValues`, `liens_Liens`, `liens_LienOffers`, `liens_BillsOfSale`
- **FKs:** Lien→Case(Restrict), Lien→Facility(Restrict), LienOffer→Lien(Restrict), BillOfSale→Lien(Restrict), BillOfSale→LienOffer(Restrict) — all within-service only
- **Auto-migration:** `Program.cs` calls `db.Database.Migrate()` in Development environment

### Build
- All 4 Liens projects: 0 errors, 0 warnings
- Identity, Gateway: 0 errors, 0 warnings
- Report: `analysis/LS-LIENS-04-002-report.md`

## Liens Repository Layer (LS-LIENS-05-001) — 2026-04-14

### Summary
Implemented 7 repository interfaces in `Liens.Application/Repositories/` and 7 EF Core implementations in `Liens.Infrastructure/Repositories/`, following the CareConnect repository pattern. All wired into DI.

### Interfaces (Liens.Application/Repositories/)
- `ICaseRepository` — GetById, GetByCaseNumber, Search(tenantId, search, status, page, pageSize), Add, Update
- `IContactRepository` — GetById, Search(tenantId, search, contactType, isActive, page, pageSize), Add, Update
- `IFacilityRepository` — GetById, Search(tenantId, search, isActive, page, pageSize), Add, Update
- `ILookupValueRepository` — GetById(tenantId?, id), GetByCategory(tenantId?, category), GetByCode(tenantId?, category, code), Add, Update
- `ILienRepository` — GetById, GetByLienNumber, Search(tenantId, search, status, lienType, caseId, facilityId, page, pageSize), GetByCaseId, GetByFacilityId, Add, Update
- `ILienOfferRepository` — GetById, GetByLienId, Search(tenantId, lienId, status, buyerOrgId, sellerOrgId, page, pageSize), HasActiveOfferAsync(tenantId, lienId, buyerOrgId), Add, Update
- `IBillOfSaleRepository` — GetById, GetByLienOfferId, GetByLienId, Search(tenantId, lienId, status, page, pageSize), Add, Update

### Implementations (Liens.Infrastructure/Repositories/)
- All use constructor-injected `LiensDbContext`
- Repository-level `SaveChangesAsync()` on every write (no unit-of-work)
- All read queries are tenant-scoped (`TenantId == tenantId`)
- LookupValue: tenant-scoped with system-wide fallback (`TenantId == null || TenantId == tenantId`)
- Search methods return `(List<T> Items, int TotalCount)` for paginated results

### DI Registration (DependencyInjection.cs)
All 7 repositories registered as `AddScoped<IXRepository, XRepository>()`

### Build
- All 4 Liens projects: 0 errors, 0 warnings

## Liens Case HTTP APIs — 2026-04-14

### Summary
Implemented the first real database-backed Liens API surface: five Case endpoints with full CRUD, authorization, and tenant isolation.

### Endpoints
| Method | Route | Permission |
|---|---|---|
| GET | `/api/liens/cases` | `SYNQ_LIENS.case:read` |
| GET | `/api/liens/cases/{id}` | `SYNQ_LIENS.case:read` |
| GET | `/api/liens/cases/by-number/{caseNumber}` | `SYNQ_LIENS.case:read` |
| POST | `/api/liens/cases` | `SYNQ_LIENS.case:create` |
| PUT | `/api/liens/cases/{id}` | `SYNQ_LIENS.case:update` |

### Files Created
- `Liens.Application/DTOs/CaseResponse.cs`, `CreateCaseRequest.cs`, `UpdateCaseRequest.cs`, `PaginatedResult.cs`
- `Liens.Application/Interfaces/ICaseService.cs`
- `Liens.Application/Services/CaseService.cs`
- `Liens.Api/Endpoints/CaseEndpoints.cs`

### Files Changed
- `Liens.Domain/LiensPermissions.cs` — added `CaseRead`, `CaseCreate`, `CaseUpdate`
- `Liens.Infrastructure/DependencyInjection.cs` — registered `ICaseService`
- `Liens.Api/Program.cs` — mapped `CaseEndpoints`
- `Liens.Api/Middleware/ExceptionHandlingMiddleware.cs` — added `UnauthorizedAccessException` → 401 handling

## Liens Service JWT Auth Integration — 2026-04-13

### Summary
Integrated Liens microservice with the v2 JWT auth/identity pattern used by Fund, CareConnect, and other services.

### Changes
- **`Liens.Api/Program.cs`** — JWT Bearer auth fully wired: issuer/audience/signing-key validation, `MapInboundClaims=false`, shared authorization policies (`AuthenticatedUser`, `AdminOnly`, `PlatformOrTenantAdmin`), `/context` diagnostic endpoint (auth-required), `/health` and `/info` (anonymous)
- **`Liens.Infrastructure/DependencyInjection.cs`** — Registered `ICurrentRequestContext` → `CurrentRequestContext` (scoped) + `IHttpContextAccessor`, matching Fund/CareConnect pattern
- **`Liens.Api/appsettings.json`** — Added `Jwt` section with placeholder signing key (overridden per environment)
- **`Liens.Api/appsettings.Development.json`** — Dev JWT config: issuer `legalsynq-identity`, audience `legalsynq-platform`, shared dev signing key
- **`Liens.Api/Properties/launchSettings.json`** — Created with `ASPNETCORE_ENVIRONMENT=Development` on port 5009
- **`Liens.Api.csproj` / `Liens.Infrastructure.csproj`** — Added `BuildingBlocks` and `Microsoft.AspNetCore.Authentication.JwtBearer` references
- **`Gateway.Api/appsettings.json`** — `liens-protected` route: removed `AuthorizationPolicy: "Anonymous"`, now inherits global `RequireAuthorization()` (auth-required for all non-health/info Liens routes)

### Verified
- `dotnet build` succeeds for Liens.Api and Gateway.Api (0 warnings, 0 errors)
- `/health` → 200 (anonymous), `/info` → 200 (anonymous), `/context` → 401 (unauthenticated)
- `/context` with valid JWT → 200, returns full identity claims (userId, tenantId, tenantCode, email, orgId, orgType, roles, productRoles)
- Gateway paths: `/liens/health` anonymous OK, `/liens/context` requires auth, all verified end-to-end

## LienOffer HTTP APIs (LS-LIENS-06-003) — 2026-04-14

### Summary
Implemented four database-backed LienOffer endpoints for marketplace offer creation and retrieval. Clean separation from sale-finalization workflow.

### Endpoints
| Method | Route | Permission |
|---|---|---|
| GET | `/api/liens/offers` | `SYNQ_LIENS.lien:read` |
| GET | `/api/liens/offers/{id}` | `SYNQ_LIENS.lien:read` |
| POST | `/api/liens/offers` | `SYNQ_LIENS.lien:offer` |
| GET | `/api/liens/liens/{lienId}/offers` | `SYNQ_LIENS.lien:read` |

### Application Service
- `ILienOfferService` / `LienOfferService` — SearchAsync, GetByIdAsync, GetByLienIdAsync, CreateAsync
- Create validates: lien exists, lien in offerable state (Offered/UnderReview), positive amount, future expiry, buyer≠seller, one active offer per buyer per lien
- Seller org derived from `lien.SellingOrgId ?? lien.OrgId`
- Buyer org from request context (never client-supplied)

### Files
- `Liens.Application/DTOs/LienOfferResponse.cs` — 16-field response DTO with computed `IsExpired`
- `Liens.Application/DTOs/CreateLienOfferRequest.cs` — 4 fields (lienId, offerAmount, notes, expiresAtUtc)
- `Liens.Application/Interfaces/ILienOfferService.cs`
- `Liens.Application/Services/LienOfferService.cs`
- `Liens.Api/Endpoints/LienOfferEndpoints.cs`
- Modified: `ILienOfferRepository` (added buyerOrgId/sellerOrgId search filters, HasActiveOfferAsync)
- Modified: `LienOfferRepository` (implemented new methods)
- Modified: `DependencyInjection.cs` (registered ILienOfferService)
- Modified: `Program.cs` (mapped LienOfferEndpoints)

### Report
Full analysis at `analysis/LS-LIENS-06-003-report.md`

## BillOfSale HTTP APIs (LS-LIENS-06-004) — 2026-04-14

### Summary
Implemented four read-only BillOfSale HTTP endpoints for retrieval and listing. Follows the established Case/Lien/LienOffer patterns. No mutation endpoints exposed — sale finalization workflow is separate.

### Endpoints
- `GET /api/liens/bill-of-sales` — paginated search (filters: search, status, lienId, sellerOrgId, buyerOrgId)
- `GET /api/liens/bill-of-sales/{id}` — get by id
- `GET /api/liens/bill-of-sales/by-number/{billOfSaleNumber}` — get by BOS number
- `GET /api/liens/liens/{lienId}/bill-of-sales` — all BOS for a lien

### Key Details
- Auth: `AuthenticatedUser` + `RequireProductAccess(SYNQ_LIENS)` + `RequirePermission(LienRead)`
- Permission gap: no dedicated `bos:read` permission yet; using `LienRead`
- `IBillOfSaleService` / `BillOfSaleService` — GetByIdAsync, GetByBillOfSaleNumberAsync, SearchAsync, GetByLienIdAsync
- Returns scalar IDs only (no cross-service enrichment)

### Files
- `Liens.Application/DTOs/BillOfSaleResponse.cs` — 22-field response DTO
- `Liens.Application/Interfaces/IBillOfSaleService.cs`
- `Liens.Application/Services/BillOfSaleService.cs`
- `Liens.Api/Endpoints/BillOfSaleEndpoints.cs`
- Modified: `IBillOfSaleRepository` (added GetByBillOfSaleNumberAsync, extended SearchAsync with buyerOrgId/sellerOrgId/search)
- Modified: `BillOfSaleRepository` (implemented new methods)
- Modified: `DependencyInjection.cs` (registered IBillOfSaleService)
- Modified: `Program.cs` (mapped BillOfSaleEndpoints)

### Report
Full analysis at `analysis/LS-LIENS-06-004-report.md`

## Sale Finalization Endpoint (LS-LIENS-06-005) — 2026-04-14

### Summary
Exposed the existing `ILienSaleService.AcceptOfferAsync` workflow through a single thin HTTP endpoint. No workflow logic duplicated — endpoint only extracts context, delegates to service, and returns result.

### Endpoint
- `POST /api/liens/offers/{offerId}/accept` — bodyless, accepts offer and finalizes sale

### Key Details
- Auth: `AuthenticatedUser` + `RequireProductAccess(SYNQ_LIENS)` + `RequirePermission(LienUpdate)`
- Permission gap: no dedicated `sale:finalize` permission yet; using `LienUpdate`
- Returns `SaleFinalizationResult` DTO (12 fields including BOS details, competing offers rejected count)
- Idempotent: repeat calls return existing result if offer already accepted
- Error handling: NotFoundException, ConflictException, ValidationException from service propagated via middleware

### Files
- Modified: `Liens.Api/Endpoints/LienOfferEndpoints.cs` (added route + handler)

### Report
Full analysis at `analysis/LS-LIENS-06-005-report.md`

## BillOfSale Document Integration (LS-LIENS-06-006) — 2026-04-14

### Summary
Integrated Liens service with Documents service for automated BOS PDF generation and storage on sale finalization. Uses post-commit recoverable pattern — document failures never block the sale transaction.

### Components Added
- `IBillOfSalePdfGenerator` / `BillOfSalePdfGenerator` — QuestPDF 2024.10.2, Letter-size PDF with seller/buyer/financial/dates sections
- `IBillOfSaleDocumentService` / `BillOfSaleDocumentService` — multipart HTTP client to Documents service (`POST /documents`)
- DI: PDF generator (singleton), document service (scoped), named HttpClient `"DocumentsService"` (base URL from `Services:DocumentsUrl`, defaults to `http://localhost:5006`)

### How It Works
1. Sale transaction commits (offer accepted, BOS created, competing offers rejected, lien marked sold)
2. Post-commit: PDF generated → uploaded to Documents service → `DocumentId` attached to BOS record
3. If document step fails: logged as warning, BOS exists with `DocumentId = null`, sale response still returns success

### Key Details
- Well-known BOS DocumentTypeId: `00000000-0000-0000-0000-000000000B05`
- `SaleFinalizationResult` DTO now includes nullable `DocumentId` (backward compatible)
- `OperationCanceledException` properly re-thrown (not swallowed)
- Post-commit document logic runs in isolated try/catch (separate from transaction rollback)

### Files
- Created: `Liens.Application/Interfaces/IBillOfSalePdfGenerator.cs`, `IBillOfSaleDocumentService.cs`
- Created: `Liens.Infrastructure/Documents/BillOfSalePdfGenerator.cs`, `BillOfSaleDocumentService.cs`
- Modified: `LienSaleService.cs`, `SaleFinalizationResult.cs`, `DependencyInjection.cs`, `Liens.Infrastructure.csproj`

### Report
Full analysis at `analysis/LS-LIENS-07-001-report.md`

## BillOfSale Document Retrieval API (LS-LIENS-07-002) — 2026-04-14

### Summary
Secure BOS document retrieval through Liens service. Liens validates business ownership (tenant scope, BOS existence, DocumentId) then proxies the file download from the Documents service. No direct Documents access exposed to callers.

### Endpoints Added
- `GET /api/liens/bill-of-sales/{id}/document` — download BOS document by BOS ID
- `GET /api/liens/bill-of-sales/by-number/{billOfSaleNumber}/document` — download by BOS number
- Auth: `AuthenticatedUser` + `RequireProductAccess(SYNQ_LIENS)` + `RequirePermission(LienRead)`

### Architecture
- **Proxy download model**: Liens calls `GET /documents/{id}/content?type=download` on Documents service, follows 302 redirect to signed storage URL, streams file back to caller
- `IBillOfSaleDocumentQueryService` (application layer): orchestrates BOS lookup → validation → document retrieval
- `IBillOfSaleDocumentService.RetrieveDocumentAsync` (infrastructure): HTTP call to Documents, returns disposable `DocumentRetrievalResult`
- `DocumentRetrievalResult` implements `IDisposable`/`IAsyncDisposable` to properly dispose `HttpResponseMessage` after streaming
- Endpoint uses `RegisterForDispose` to tie response lifecycle to HTTP pipeline

### Error Handling
- BOS not found → 404
- DocumentId null → 409 (code: `DOCUMENT_NOT_AVAILABLE`)
- Documents service failure → 502 (new `ServiceUnavailableException` in BuildingBlocks)
- Transport errors (`HttpRequestException`) → caught and mapped to 502

### Files
- Created: `DocumentRetrievalResult.cs`, `IBillOfSaleDocumentQueryService.cs`, `BillOfSaleDocumentQueryService.cs`, `ServiceUnavailableException.cs`
- Modified: `IBillOfSaleDocumentService.cs`, `BillOfSaleDocumentService.cs`, `BillOfSaleEndpoints.cs`, `ExceptionHandlingMiddleware.cs`, `DependencyInjection.cs`

### Report
Full analysis at `analysis/LS-LIENS-07-002-report.md`

## Liens Audit Integration — 2026-04-14

### Summary
Integrated Liens service with the v2 Audit service using the shared `LegalSynq.AuditClient` SDK. All critical business write operations now emit structured audit events via fire-and-forget publishing. Audit failures never block business workflows.

### Pattern
- Uses shared `LegalSynq.AuditClient` NuGet (same as Identity, CareConnect, Notifications)
- `IAuditPublisher` (application interface) → `AuditPublisher` (infrastructure implementation)
- Fire-and-observe: `_client.IngestAsync(...).ContinueWith(...)` — never awaited, failures logged as warnings
- SourceSystem: `liens-service`, EventCategory: `Business`, ScopeType: `Tenant`
- Idempotency keys generated via `IdempotencyKey.ForWithTimestamp`

### Audit Events
| Event Type | Service | Trigger |
|---|---|---|
| `liens.lien.created` | LienService | Lien creation |
| `liens.lien.updated` | LienService | Lien update |
| `liens.offer.created` | LienOfferService | Offer submission |
| `liens.sale.finalized` | LienSaleService | Offer accepted, BOS created (after commit) |
| `liens.case.created` | CaseService | Case creation |
| `liens.case.updated` | CaseService | Case update |

### Configuration
- `AuditClient` section added to `appsettings.json` (BaseUrl, SourceSystem, TimeoutSeconds)
- Registered via `services.AddAuditEventClient(configuration)` in DI

### Files
- Created: `IAuditPublisher.cs` (Application), `AuditPublisher.cs` (Infrastructure/Audit)
- Modified: `LienService.cs`, `LienOfferService.cs`, `LienSaleService.cs`, `CaseService.cs`, `DependencyInjection.cs`, `Liens.Infrastructure.csproj`, `appsettings.json`

## Notifications DB Naming Convention Fix — 2026-04-14

### Summary
Standardized all Notifications service table/column/index names from `ntf_snake_case` to `ntf_PascalCase` convention, matching the platform-wide pattern used by Identity, Liens, CareConnect, Audit, and Fund services.

### Changes
- **5 configuration files updated**: `NotificationConfiguration.cs`, `TemplateConfiguration.cs`, `EventConfigurations.cs`, `ProviderConfigurations.cs`, `BillingConfigurations.cs`
  - 18 tables renamed from `ntf_snake_case` to `ntf_PascalCase` (e.g., `ntf_notifications` → `ntf_Notifications`)
  - All explicit `.HasColumnName("snake_case")` calls removed — EF Core now uses default PascalCase from domain property names
  - 14 indexes renamed from `idx_/uq_` pattern to `IX_/UX_` pattern (e.g., `idx_attempts_notification_id` → `IX_NotificationAttempts_NotificationId`)
- **New file**: `SchemaRenamer.cs` — startup migration helper that safely renames tables, columns, and indexes on existing databases
  - Handles both legacy unprefixed tables and `ntf_snake_case` tables → `ntf_PascalCase`
  - Column renames: only multi-word snake_case columns (e.g., `tenant_id` → `TenantId`) since MySQL column names are case-insensitive for single-word identifiers
  - All operations are idempotent with existence checks before each rename
- **Program.cs simplified**: replaced inline rename logic with `SchemaRenamer.RenameSchemaAsync()` call; restored `MapBrandingEndpoints()` and `MapInternalEndpoints()`

## LS-LIENS-UI-003: Liens API Integration — 2026-04-14

### Summary
Wired the Liens UI (list page, detail page, create modal) to real backend APIs, replacing Zustand mock store reads with the same layered service pattern used by Cases.

### Service Layer (`apps/web/src/lib/liens/`)
- **5-file pattern** matching Cases: `liens.types.ts` → `liens.api.ts` → `liens.mapper.ts` → `liens.service.ts` → `index.ts`
- **Backend routes**: `GET /lien/api/liens/liens` (list with `?search`, `?status`, `?lienType`, `?caseId`, `?page`, `?pageSize`), `GET .../liens/{id}`, `POST .../liens` (create), `PUT .../liens/{id}` (update), `GET .../liens/{id}/offers`, `POST .../offers`, `POST .../offers/{id}/accept`
- **DTO parity**: Frontend types match backend DTOs (`LienResponse`, `CreateLienRequest`, `UpdateLienRequest`, `LienOfferResponse`, `CreateLienOfferRequest`, `SaleFinalizationResult`)

### Pages Rewritten
- **`liens/page.tsx`**: Server-side filtering + pagination via `liensService.getLiens()`, loading/error states with retry, `onCreated` callback for list refresh after creation
- **`liens/[id]/page.tsx`**: `liensService.getLien()` + `getLienOffers()` for live data, `liensService.acceptOffer()` for offer acceptance (creates Bill of Sale), cross-entity case lookup via `casesService.getCase(caseId)` for linked case display with navigation
- **`create-lien-modal.tsx`**: Calls `liensService.createLien()` with `CreateLienRequestDto`, lien number field added (required by backend), proper error display

### Cross-Entity Integration
- **Case → Lien**: Already working (`cases/[id]/page.tsx` fetches `getCaseLiens(id)` and links to `/lien/liens/{id}`)
- **Lien → Case**: New (`liens/[id]/page.tsx` fetches `casesService.getCase(caseId)` and shows case number + client name with link to `/lien/cases/{caseId}`); stale link cleared on navigation

### Remaining Store Usage
- `useLienStore` still used for: `currentRole` (role gating), `addToast` (notifications). All data reads now come from API.

---

## LS-LIENS-UI-006: Contacts & Participants Integration

### Summary
Built full Contact CRUD backend stack (service + endpoints) on existing entity/repository, created 5-file frontend service layer, and rewrote contacts list/detail/add-contact pages to consume real API instead of mock store data.

### Backend
- **`ContactService.cs`**: Full CRUD with validation, audit publishing, `NotFoundException`/`ValidationException` pattern matching `ServicingItemService`
- **`ContactEndpoints.cs`**: MinimalAPI group `/api/liens/contacts` — `GET /` (list+search), `GET /{id}`, `POST /`, `PUT /{id}`, `PUT /{id}/deactivate`, `PUT /{id}/reactivate`
- **DTOs**: `ContactResponse`, `CreateContactRequest`, `UpdateContactRequest`
- **DI**: `IContactService` → `ContactService` registered; `app.MapContactEndpoints()` in Program.cs

### Service Layer (`apps/web/src/lib/contacts/`)
- **5-file pattern**: `contacts.types.ts` → `contacts.api.ts` → `contacts.mapper.ts` → `contacts.service.ts` → `index.ts`
- **Gateway path**: `/lien/api/liens/contacts`
- **Field mapping**: Backend `firstName`/`lastName`/`displayName`/`addressLine1`/`postalCode` mapped to UI-friendly fields

### Pages Rewritten
- **`contacts/page.tsx`**: `contactsService.getContacts()` with search/type filter, side drawer preview, Add Contact modal
- **`contacts/[id]/page.tsx`**: `contactsService.getContact()` for detail, deactivate/reactivate actions
- **`add-contact-form.tsx`**: `contactsService.createContact()` with validation, proper field mapping

### Store Usage
- `useLienStore` only for `currentRole`, `addToast` — all contact data from API

---

## LS-LIENS-UI-007: Bill of Sale & Settlement Flow

### Summary
Added status transition endpoints to BillOfSale backend (submit/execute/cancel), built 5-file frontend service layer, rewrote list and detail pages to use real API.

### Backend Changes
- **`IBillOfSaleService`**: Added `SubmitForExecutionAsync`, `ExecuteAsync`, `CancelAsync`
- **`BillOfSaleService`**: Implemented transitions with `NotFoundException`, `ValidationException`, audit publishing
- **`BillOfSaleEndpoints`**: Added `PUT /{id}/submit`, `PUT /{id}/execute`, `PUT /{id}/cancel`
- Existing read endpoints unchanged

### Service Layer (`apps/web/src/lib/billofsale/`)
- **5-file pattern**: `billofsale.types.ts` → `billofsale.api.ts` → `billofsale.mapper.ts` → `billofsale.service.ts` → `index.ts`
- **Gateway path**: `/lien/api/liens/bill-of-sales`
- **Document URL**: `/api/lien/api/liens/bill-of-sales/{id}/document` (BFF proxy path for browser downloads)
- **Utilities**: `formatCurrency`, `formatDate` moved from mock data to service layer

### Pages Rewritten
- **`bill-of-sales/page.tsx`**: `billOfSaleService.getBillOfSales()`, KPI cards, status actions via API
- **`bill-of-sales/[id]/page.tsx`**: `billOfSaleService.getBillOfSale()`, workflow stepper, status transitions, PDF download

### Service Layer (`apps/web/src/lib/audit/`)
- **5-file pattern**: `audit.types.ts` → `audit.api.ts` → `audit.mapper.ts` → `audit.service.ts` → `index.ts`
- **Gateway path**: `/audit-service/audit/entity/{entityType}/{entityId}` (via Next.js fallback rewrite → gateway → audit cluster)
- **Entity types**: `Case`, `Lien`, `ServicingItem`, `BillOfSale`, `Contact`, `Document` (typed as `AuditEntityType` union)
- **Enums**: Backend serializes enums as strings (`JsonStringEnumConverter`); frontend types use string unions
- **Query params**: `Page`, `PageSize`, `EventTypes`, `SortDescending` (PascalCase matching backend `AuditEventQueryRequest`)
- **Component**: `EntityTimeline` at `apps/web/src/components/lien/entity-timeline.tsx` — reusable, takes `entityType` + `entityId`, handles loading/error/empty/pagination

### Pages with EntityTimeline
- `cases/[id]/page.tsx` — entity type `Case`
- `liens/[id]/page.tsx` — entity type `Lien`
- `servicing/[id]/page.tsx` — entity type `ServicingItem`
- `bill-of-sales/[id]/page.tsx` — entity type `BillOfSale`
- `contacts/[id]/page.tsx` — entity type `Contact`

### Store Usage
- `useLienStore` only for `currentRole`, `addToast` — all BOS data from API

## Reports Service (reports/)
- **Story**: LS-REPORTS-00-001 — Service Bootstrap
- **Story**: LS-REPORTS-00-002 — Adapter Interface Hardening
- **Story**: LS-REPORTS-01-001 — Template Data Model & Persistence Foundation
- **Story**: LS-REPORTS-01-001-01 — Persistence Model Alignment (`ReportDefinition` → `ReportTemplate`)
- **Story**: LS-REPORTS-01-002 — Template Management API (CRUD + versioning + publish)
- **Story**: LS-REPORTS-01-003 — Persistence Finalization & Integration Readiness (migration applied to AWS RDS MySQL, 37/37 API assertions pass against live DB, concurrency validated)
- **Framework**: .NET 8 ASP.NET Core Web API, clean layered architecture
- **Structure**: `Reports.sln` with 7 source projects (Api, Application, Domain, Infrastructure, Worker, Contracts, Shared) + 3 test projects
- **Design**: Standalone, platform-agnostic microservice. No LegalSynq-specific logic. Adapter-based integration pattern.
- **Context Models**: `RequestContext` (correlation/request ID), `UserContext`, `TenantContext`, `ProductContext` in `Reports.Contracts/Context/`
- **Adapter Result**: `AdapterResult<T>` generic wrapper (Success/Fail with error codes, retryability, metadata). `AdapterErrors` static class with standard error codes (NOT_FOUND, UNAUTHORIZED, FORBIDDEN, UNAVAILABLE, TIMEOUT, etc.)
- **Adapters**: 7 adapter interfaces in `Reports.Contracts/Adapters/` — all accept `RequestContext` as first param, use typed context models, return `AdapterResult<T>`. Mock implementations in `Reports.Infrastructure/Adapters/`
- **Typed DTOs**: `StoreReportRequest`, `StoredDocumentInfo`, `ReportContent`, `ReportNotification`, `ProductDataQuery`, `ProductDataResult`
- **Endpoints**: `GET /api/v1/health` (basic health), `GET /api/v1/ready` (component readiness with 9 checks, semantic probe evaluation). Template Management: `POST /api/v1/templates`, `PUT /api/v1/templates/{id}`, `GET /api/v1/templates/{id}`, `GET /api/v1/templates?productCode&organizationType&page&pageSize`, `POST /api/v1/templates/{id}/versions`, `GET /api/v1/templates/{id}/versions`, `GET /api/v1/templates/{id}/versions/latest`, `GET /api/v1/templates/{id}/versions/published`, `POST /api/v1/templates/{id}/versions/{versionNumber}/publish`
- **Middleware**: `RequestLoggingMiddleware` with X-Correlation-Id support
- **Worker**: `ReportWorkerService` (BackgroundService) polls `IJobQueue` every 10s
- **Guardrails**: `IGuardrailValidator` with `ValidateExecutionLimits()` and `ValidateReportTemplate()` stubs
- **Persistence**: MySQL + EF Core (Pomelo 8.0.2) with conditional fallback — when `ConnectionStrings:ReportsDb` is set, uses `ReportsDbContext` + EF repositories; when empty, falls back to mock repositories. Physical tables prefixed `rpt_` (rpt_ReportDefinitions, rpt_ReportTemplateVersions, rpt_ReportExecutions). Physical table/column names kept stable; code uses `ReportTemplate` terminology with explicit `ToTable()`/`HasColumnName()` mappings.
- **Domain**: `ReportTemplate` (Code, Name, Description, ProductCode, OrganizationType, IsActive, CurrentVersion, timestamps, Versions collection), `ReportTemplateVersion` (template body, output format, change tracking, publish state via IsPublished/PublishedAtUtc/PublishedByUserId), `ReportExecution` (tenant-scoped, FK to template via `ReportTemplateId`) — EF-free POCOs
- **Contracts**: `IReportRepository` (execution CRUD), `ITemplateRepository` (template + version management + publish queries) — strongly-typed, using `ReportTemplate` naming
- **Service Layer**: `ITemplateManagementService` / `TemplateManagementService` in Application/Templates — CRUD orchestration, validation, sequential versioning, single-published-version governance, audit hooks. Request/Response DTOs in Application/Templates/DTOs/. `ServiceResult<T>` generic wrapper for consistent error propagation.
- **EF Configurations**: Fluent API in `Infrastructure/Persistence/Configurations/` — `ReportTemplateConfiguration`, `ReportTemplateVersionConfiguration`, `ReportExecutionConfiguration`. Unique indexes on template Code and (templateId, versionNumber), cascade delete on versions, restrict delete on executions. FK columns mapped via `HasColumnName("ReportDefinitionId")` for schema stability.
- **Design-Time Factory**: `DesignTimeDbContextFactory` in Api project for `dotnet ef migrations` tooling
- **Utility**: `ReportWriter` in Shared — writes implementation reports to `/analysis`
- **Integration Test**: In-process test harness at `reports/scripts/IntegrationTest/` — 37 assertions covering all 9 endpoints, concurrency, validation, and error handling
- **Analysis**: Reports at `analysis/LS-REPORTS-00-001-report.md`, `analysis/LS-REPORTS-00-002-report.md`, `analysis/LS-REPORTS-01-001-report.md`, `analysis/LS-REPORTS-01-001-01-results.md`, `analysis/LS-REPORTS-01-002-report.md`, `analysis/LS-REPORTS-01-003-report.md`
