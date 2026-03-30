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
| `GET /careconnect/api/providers` | GET | Bearer (AuthenticatedUser) | List providers (tenant-scoped) |
| `GET /careconnect/api/providers/{id}` | GET | Bearer (AuthenticatedUser) | Get provider by ID |
| `POST /careconnect/api/providers` | POST | Bearer + PlatformOrTenantAdmin | Create provider |
| `PUT /careconnect/api/providers/{id}` | PUT | Bearer + PlatformOrTenantAdmin | Update provider |
| `GET /careconnect/api/referrals` | GET | Bearer (AuthenticatedUser) | List referrals (tenant-scoped) |
| `GET /careconnect/api/referrals/{id}` | GET | Bearer (AuthenticatedUser) | Get referral by ID |
| `POST /careconnect/api/referrals` | POST | Bearer + PlatformOrTenantAdmin | Create referral |
| `PUT /careconnect/api/referrals/{id}` | PUT | Bearer + PlatformOrTenantAdmin | Update referral |
| `GET /careconnect/api/slots` | GET | Bearer (AuthenticatedUser) | List slots (tenant-scoped, filterable) |
| `POST /careconnect/api/providers/{id}/slots/generate` | POST | Bearer + PlatformOrTenantAdmin | Generate slots from templates |
| `POST /careconnect/api/appointments` | POST | Bearer + PlatformOrTenantAdmin | Book appointment |
| `GET /careconnect/api/appointments` | GET | Bearer (AuthenticatedUser) | List appointments |
| `GET /careconnect/api/appointments/{id}` | GET | Bearer (AuthenticatedUser) | Get appointment |
| `PUT /careconnect/api/appointments/{id}` | PUT | Bearer + PlatformOrTenantAdmin | Update status/notes |
| `POST /careconnect/api/appointments/{id}/cancel` | POST | Bearer + PlatformOrTenantAdmin | Cancel appointment |
| `POST /careconnect/api/appointments/{id}/reschedule` | POST | Bearer + PlatformOrTenantAdmin | Reschedule appointment |
| `GET /careconnect/api/appointments/{id}/history` | GET | Bearer (AuthenticatedUser) | Appointment status history |
| `GET /careconnect/api/providers/{id}/availability-exceptions` | GET | Bearer (AuthenticatedUser) | List provider exceptions |
| `POST /careconnect/api/providers/{id}/availability-exceptions` | POST | Bearer + PlatformOrTenantAdmin | Create exception |
| `PUT /careconnect/api/availability-exceptions/{id}` | PUT | Bearer + PlatformOrTenantAdmin | Update exception |
| `POST /careconnect/api/providers/{id}/slots/apply-exceptions` | POST | Bearer + PlatformOrTenantAdmin | Block slots overlapping active exceptions |
| `GET /careconnect/api/referrals/{id}/notes` | GET | Bearer (AuthenticatedUser) | List referral notes (newest first) |
| `POST /careconnect/api/referrals/{id}/notes` | POST | Bearer + PlatformOrTenantAdmin | Create referral note |
| `PUT /careconnect/api/referral-notes/{id}` | PUT | Bearer + PlatformOrTenantAdmin | Update referral note |
| `GET /careconnect/api/appointments/{id}/notes` | GET | Bearer (AuthenticatedUser) | List appointment notes (newest first) |
| `POST /careconnect/api/appointments/{id}/notes` | POST | Bearer + PlatformOrTenantAdmin | Create appointment note |
| `PUT /careconnect/api/appointment-notes/{id}` | PUT | Bearer + PlatformOrTenantAdmin | Update appointment note |
| `GET /careconnect/api/referrals/{id}/attachments` | GET | Bearer (AuthenticatedUser) | List referral attachment metadata (newest first) |
| `POST /careconnect/api/referrals/{id}/attachments` | POST | Bearer + PlatformOrTenantAdmin | Create referral attachment metadata |
| `GET /careconnect/api/appointments/{id}/attachments` | GET | Bearer (AuthenticatedUser) | List appointment attachment metadata (newest first) |
| `POST /careconnect/api/appointments/{id}/attachments` | POST | Bearer + PlatformOrTenantAdmin | Create appointment attachment metadata |
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

## CareConnect Provider Geo / Map-Ready Discovery

- **Radius search:** `latitude` + `longitude` + `radiusMiles` (max 100 mi). Bounding-box filter in `ProviderGeoHelper.BoundingBox`.
- **Viewport search:** `southLat` + `northLat` + `westLng` + `eastLng`. northLat must be >= southLat.
- **Conflict rule:** Radius + viewport together → 400 validation error on `search` key.
- **`GET /api/providers/map`:** Returns `ProviderMarkerResponse[]`, capped at 500 markers, only geo-located providers. Shares all filter params with the list endpoint.
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

### CareConnect Migration
```
20260330110001_AlignCareConnectToPlatformIdentity.cs   — Provider.OrganizationId, Facility.OrganizationId,
                                                          Referral.OrganizationRelationshipId, Appointment.OrganizationRelationshipId
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

#### CareConnect — Integrity Reporting
- **`GET /api/admin/integrity`** (`CareConnectIntegrityEndpoints.cs`): Returns four integrity counters (referrals with org-pair but null relationship; appointments missing relationship where referral has one; providers/facilities without OrganizationId). Always returns 200; `-1` on individual query failure. `clean: true` when all counters are zero.

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
