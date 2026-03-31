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
- **Framework:** Next.js 14 App Router + TypeScript + Tailwind CSS
- **Port:** 3000 (dev)
- **Session:** HttpOnly cookie (`platform_session`) set by BFF login route; validated via BFF `/api/auth/me` — frontend never decodes raw JWT
- **BFF Routes:** `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/logout` — Next.js API routes that proxy to Identity service with Bearer auth
- **API:** All requests proxy through gateway via Next.js rewrites `/api/*` → `http://localhost:5000/*`
- **Environment:** `apps/web/.env.local` (gitignored) — `NEXT_PUBLIC_ENV=development`, `NEXT_PUBLIC_TENANT_CODE=LEGALSYNQ`, `GATEWAY_URL=http://localhost:5000`
- **node_modules:** Installed at monorepo root (`/home/runner/workspace/node_modules`) — `apps/web` inherits via Node.js module resolution traversal

### Frontend Structure
```
apps/web/
  src/
    types/index.ts              ← PlatformSession, TenantBranding, OrgType, ProductRole, NavGroup
    lib/
      api-client.ts             ← apiClient + ApiError (correlationId-aware)
      session.ts                ← getServerSession() — calls /auth/me (server-side)
      auth-guards.ts            ← requireAuthenticated/Org/ProductRole/Admin (server components)
      nav.ts                    ← buildNavGroups(session) — role-driven nav derivation
    providers/
      session-provider.tsx      ← SessionProvider — fetches BFF /api/auth/me client-side on mount
      tenant-branding-provider.tsx ← TenantBrandingProvider — anonymous branding fetch + CSS vars + X-Tenant-Code header
    hooks/
      use-session.ts            ← useSession() / useRequiredSession()
      use-tenant-branding.ts    ← re-exports useTenantBranding()
    contexts/
      product-context.tsx         ← ProductProvider + useProduct() — infers activeProductId from pathname
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
    app/api/
      careconnect/[...path]/route.ts ← BFF catch-all proxy for CareConnect client calls
      fund/[...path]/route.ts        ← BFF catch-all proxy for Fund client calls
      lien/[...path]/route.ts        ← BFF catch-all proxy for SynqLien client calls
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
      (admin)/                  ← route group: requireAdmin() guard + AppShell
        layout.tsx
        admin/users/page.tsx
      portal/                   ← injured party portal (separate session shape — Phase 2)
        login/page.tsx
        my-application/page.tsx
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
                                            ProductRole, Capability, RoleCapability
                                            UserOrganizationMembership, UserRoleAssignment
      Identity.Infrastructure/
        Data/IdentityDbContext.cs         ← 14 DbSets (existing + 8 new)
        Data/Configurations/              ← IEntityTypeConfiguration<T> per entity (15 configs)
        Auth/CapabilityService.cs         ← ICapabilityService impl, 5-min IMemoryCache TTL
        Persistence/Migrations/           ← InitialIdentitySchema
                                            AddMultiOrgProductRoleModel (8 tables + seed)
                                            SeedAdminOrgMembership
                                            AddTenantDomains (TenantDomains table)
                                            SeedTenantDomains (legalsynq.legalsynq.com)
                                            CorrectSynqLienRoleMappings (SELLER→PROVIDER)
                                            DropStaleApplicationsTable (identity_db cleanup)
        Services/JwtTokenService.cs       ← emits org_id, org_type, product_roles JWT claims
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

## Identity Domain Model

| Entity | Table | PK | Key constraints |
|---|---|---|---|
| Tenant | Tenants | Id (Guid) | Code unique |
| User | Users | Id (Guid) | (TenantId, Email) unique |
| Role | Roles | Id (Guid) | (TenantId, Name) unique |
| UserRole | UserRoles | (UserId, RoleId) | FK→Users Cascade, FK→Roles Cascade |
| Product | Products | Id (Guid) | Code unique |
| TenantProduct | TenantProducts | (TenantId, ProductId) | FK→Tenants Cascade |

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
- Referral: `New → Accepted → Scheduled → Completed/Cancelled`; `New → Declined`. Legacy: `Received`/`Contacted` normalize to `Accepted` via `Referral.ValidStatuses.Legacy.Normalize()`.
- Appointment: `Pending → Confirmed → Completed/Cancelled`; `Rescheduled` as real status. `Scheduled` retained as backward-compat alias.

**Org-scoped referral list:** `GET /api/referrals` applies `ReferringOrgId`/`ReceivingOrgId` filters from JWT `org_id` claim based on user's product roles. Admins see all.

**xUnit test suite:** `CareConnect.Tests` — 141 tests covering `CareConnectCapabilityService`, `ReferralWorkflowRules`, `AppointmentWorkflowRules`, `OrgScopingTests`, `ProviderAvailabilityServiceTests`, `CareConnectParticipantHelperTests`, `AppointmentOrgScopingTests`. All passing.

**LSCC-002 — Access hardening (complete):**
- `GET /api/referrals/{id}` — row-level participant check: non-participant callers receive 404 (not 403).
- `GET /api/appointments` — org-scoped: mirrors referral list scoping (receiver sees receiving-org appointments, referrer sees referring-org appointments, admins see all).
- `GET /api/appointments/{id}` — row-level participant check: non-participant callers receive 404.
- `PUT /api/admin/providers/{id}/link-organization` — explicit admin backfill for providers with null `OrganizationId`.
- `Appointment.Create` now denormalizes `ReferringOrganizationId` and `ReceivingOrganizationId` from the source Referral at booking time.
- `CareConnectParticipantHelper` — shared static helper: `IsAdmin`, `IsReferralParticipant`, `IsAppointmentParticipant`, `GetReferralOrgScope`, `GetAppointmentOrgScope`.

## CareConnect Provider Geo / Map-Ready Discovery

- **Radius search:** `latitude` + `longitude` + `radiusMiles` (max 100 mi). Bounding-box filter in `ProviderGeoHelper.BoundingBox`.
- **Viewport search:** `southLat` + `northLat` + `westLng` + `eastLng`. northLat must be >= southLat.
- **Conflict rule:** Radius + viewport together → 400 validation error on `search` key.
- **`GET /api/providers/map`:** Returns `ProviderMarkerResponse[]`, capped at 500 markers, only geo-located providers. Shares all filter params with the list endpoint.
- **`GET /api/providers/{id}/availability`:** Returns `ProviderAvailabilityResponse` with open slot summaries for a date range (max 90 days). Optional `facilityId`/`serviceOfferingId` filters. Requires `provider:search` capability.
- **Display fields (both endpoints):** `DisplayLabel = OrganizationName ?? Name`; `MarkerSubtitle = "City, State[ · PrimaryCategory]"`; `PrimaryCategory` = first category alphabetically.
- **`BuildBaseQuery`:** Shared LINQ filter builder in `ProviderRepository` used by both `SearchAsync` and `GetMarkersAsync` to avoid duplication.

## Docs Service (apps/services/documents-nodejs) — Test Coverage

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

## .NET Documents Service (apps/services/documents-dotnet)

**Port**: 5006  
**Framework**: .NET 8 Minimal APIs + EF Core 8 + Npgsql (PostgreSQL)  
**Architecture**: 4-project layered monorepo (Domain → Application → Infrastructure → Api)  
**Status**: Fully implemented, builds cleanly (0 errors, 0 warnings)

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
- **Storage**: `local` (dev) or `s3` (prod), selected via `Storage:Provider` config
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
dotnet build apps/services/documents-dotnet/Documents.Api/Documents.Api.csproj
```

### Database Setup
```bash
# Apply EF Core migrations
dotnet ef migrations add InitialCreate \
  --project apps/services/documents-dotnet/Documents.Infrastructure \
  --startup-project apps/services/documents-dotnet/Documents.Api
dotnet ef database update \
  --startup-project apps/services/documents-dotnet/Documents.Api
```
Or run `apps/services/documents-dotnet/Documents.Infrastructure/Database/schema.sql` directly against PostgreSQL.

### Analysis Documents (7 + 6 phases)
Architecture phases in `apps/services/documents-nodejs/analysis/`:
- `dotnet_phase1_discovery_and_mapping.md` — TS→.NET translation decisions
- `dotnet_phase2_scaffolding.md` — project structure and dependency graph
- `dotnet_phase3_domain_and_contracts.md` — entities, enums, interfaces, invariants
- `dotnet_phase4_api_and_application.md` — services, RBAC, endpoints, configuration
- `dotnet_phase5_infrastructure.md` — EF Core, repositories, storage, scanner, token stores
- `dotnet_phase6_security_and_tenancy.md` — threat model, three-layer isolation, HIPAA notes
- `dotnet_phase7_parity_review.md` — 13/13 endpoint parity, A- grade, gaps, next steps

ClamAV phases in `apps/services/documents-dotnet/analysis/`:
- `dotnet_clamav_phase1_design.md` — async scan architecture, quarantine model, ADRs
- `dotnet_clamav_phase2_provider.md` — ClamAV TCP implementation, provider selection
- `dotnet_clamav_phase3_worker.md` — BackgroundService, Channel queue, scan lifecycle
- `dotnet_clamav_phase4_quarantine_and_access.md` — quarantine prefix, access enforcement, API changes
- `dotnet_clamav_phase5_review.md` — audit events, config reference, parity gaps, production notes
- `dotnet_clamav_final_summary.md` — complete summary, security posture, schema changes

Enterprise hardening phases in `apps/services/documents-dotnet/analysis/`:
- `dotnet_enterprise_phase1_durable_queue.md` — Redis Streams durable queue, IScanJobQueue lease/ack redesign
- `dotnet_enterprise_phase2_retries_and_scaling.md` — exponential backoff retry, WorkerCount concurrency, duplicate prevention
- `dotnet_enterprise_phase3_backpressure.md` — QueueSaturationException (503), fail-fast upload, Retry-After header
- `dotnet_enterprise_phase4_audit_and_observability.md` — SCAN_ACCESS_DENIED event, 11 Prometheus metrics, health checks
- `dotnet_enterprise_phase5_clamav_hardening.md` — ClamAV PING/PONG health, timeout isolation, fail-closed review
- `dotnet_enterprise_final_summary.md` — complete architecture, production deployment guidance, remaining risks

Phase 4 Final Hardening in `apps/services/documents-dotnet/analysis/`:
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
`apps/services/platform-audit-event-service/`

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
**Integrity spec:** `apps/services/platform-audit-event-service/Docs/integrity-model.md`

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
**Operator reference:** `apps/services/platform-audit-event-service/Docs/ingest-auth.md`

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
| System Health | IN PROGRESS | `app/monitoring/page.tsx` |
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

## Step 30 — IP Address Capture in Auth Audit Events

**IP address now recorded on all login and logout audit events** (both successful and failed).

### Changes
- **`Identity.Api/Endpoints/AuthEndpoints.cs`** — login endpoint now injects `HttpContext` and extracts the client IP via `X-Forwarded-For` (first segment) falling back to `RemoteIpAddress`. Passes `ip` to `LoginAsync`. Logout endpoint likewise sets `Actor.IpAddress` from the same header chain.
- **`Identity.Application/Interfaces/IAuthService.cs`** — `LoginAsync` signature extended: `Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default)`
- **`Identity.Application/Services/AuthService.cs`** — `LoginAsync` accepts `ipAddress`; sets `Actor.IpAddress` on the `identity.user.login.succeeded` event. `EmitLoginFailed` helper extended with `string? ipAddress = null`; all four call sites (`TenantNotFound`, `UserNotFound`, `InvalidCredentials`, `RoleLookupFailed`) pass the IP through.

### Result
- Activity Log IP Address column now shows the real client IP for login/logout events instead of `—`.
- Both successful and failed login attempts include the IP, supporting HIPAA §164.312(b) and NIST SP 800-92 requirements for contextual access logging.
