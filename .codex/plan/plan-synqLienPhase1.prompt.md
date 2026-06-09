# Plan: SynqLien Phase 1 — Lien Sale Workflow & Portfolio Management (SYNQLIEN-SALE-001)

## TL;DR
Build the first monetization layer of SynqLien by extending the existing `apps/services/liens` .NET service and `apps/web` Next.js frontend. Adds 4 new DB tables, 9 backend API endpoints, 5 RBAC permissions, 4 frontend screens, full audit trail, and analytics. Delivered in 7 sprints across 9 epics (~31 stories).

---

## Sprint 1: Database Schema + Backend Foundation (Epic 1 — Sale Portfolio Foundation)

### Steps
1. **Domain Entities** — Add to `apps/services/liens/Liens.Domain/Entities/`:
   - `LienSalePortfolio.cs` (id UUID, tenant_id, name, description, portfolio_code, status enum, published_at, notes, created_by, created_at, updated_at)
   - `LienSalePortfolioItem.cs` (id, portfolio_id FK, lien_id FK, added_by, added_at)
   - `LienSaleOpportunity.cs` (id, portfolio_id FK, status, tenant_id, created_at, updated_at)
   - `LienSaleActivity.cs` (id, portfolio_id FK, action enum, actor_id, metadata JSON, created_at — immutable)
   - `LienSalePortfolioStatus.cs` (enum: DRAFT, READY_FOR_REVIEW, PUBLISHED, UNDER_REVIEW, ACCEPTED, REJECTED, WITHDRAWN, CLOSED)

2. **EF Core Configurations** — Add to `apps/services/liens/Liens.Infrastructure/Persistence/Configurations/`:
   - `LienSalePortfolioConfiguration.cs`
   - `LienSalePortfolioItemConfiguration.cs`
   - `LienSaleOpportunityConfiguration.cs`
   - `LienSaleActivityConfiguration.cs`

3. **Register in DbContext** — Update `Liens.Infrastructure/Persistence/LiensDbContext.cs` to add 4 new DbSet properties and apply configurations.

4. **EF Migration** — Run `dotnet ef migrations add AddLienSalePortfolioFoundation` in `Liens.Api`. Migration creates: `lien_sale_portfolios`, `lien_sale_portfolio_items`, `lien_sale_opportunities`, `lien_sale_activity`.

5. **Repository Interfaces** — Add to `Liens.Application/Repositories/`:
   - `ILienSalePortfolioRepository.cs`
   - `ILienSalePortfolioItemRepository.cs`
   - `ILienSaleActivityRepository.cs`

6. **Repository Implementations** — Add to `Liens.Infrastructure/Repositories/`:
   - `LienSalePortfolioRepository.cs`
   - `LienSalePortfolioItemRepository.cs`
   - `LienSaleActivityRepository.cs`

7. **Register DI** — Update `Liens.Infrastructure/DependencyInjection.cs` (or equivalent startup) to register new repos.

8. **Stories 1.1–1.4**: Portfolio entity ✅, status lifecycle ✅, CRUD service layer (no endpoints yet), search/filter query support.

---

## Sprint 2: Portfolio CRUD APIs (Epics 1 cont. + partial Epic 2)

*Depends on Sprint 1.*

9. **Application Services** — Add to `Liens.Application/Services/`:
   - `LienSalePortfolioService.cs` — CreatePortfolio, UpdatePortfolio, GetPortfolio, ListPortfolios (paginated/filtered)
   - `LienEligibilityService.cs` — ValidateLienEligibility rules: balance > 0, status != CLOSED/WRITTEN_OFF, tenant ownership, not already assigned

10. **Minimal API Endpoints** — Add to `Liens.Api/Endpoints/LienSale/`:
    - `LienSalePortfolioEndpoints.cs` — POST/PUT/GET (list)/GET (detail) for `/api/lien-sale/portfolios`

11. **Stories 1.3, 1.4**: Create/Update/Get/List portfolio APIs with filtering (status, created date, portfolio_code, receivable range, lien count), pagination ✅

---

## Sprint 3: Portfolio Item Workflows (Epics 2 + 3)

*Depends on Sprint 2.*

12. **Portfolio Item Application Service** — `LienSalePortfolioItemService.cs`:
    - AddLienToPortfolio (calls LienEligibilityService, prevents duplicate)
    - RemoveLienFromPortfolio (updates exposure totals, logs activity)
    - BulkAddLiens (partial failure handling)

13. **Lifecycle/Transition Engine** — `LienSaleLifecycleService.cs`:
    - Enforces state machine transition matrix
    - PublishPortfolio (DRAFT→PUBLISHED, records published_at, audit event)
    - WithdrawPortfolio (→WITHDRAWN, marks read-only, audit event)

14. **Portfolio Item API Endpoints** — Add to `Liens.Api/Endpoints/LienSale/`:
    - `LienSalePortfolioItemEndpoints.cs` — POST `/api/lien-sale/portfolios/:id/liens`, DELETE `/api/lien-sale/portfolios/:id/liens/:lienId`

15. **Lifecycle API Endpoints**:
    - `POST /api/lien-sale/portfolios/:id/publish`
    - `POST /api/lien-sale/portfolios/:id/withdraw`

16. **Stories 2.1–2.4, 3.1–3.3** ✅

---

## Sprint 4: Analytics APIs + Reporting Foundation (Epics 4 + 8)

*Depends on Sprint 3. Runs parallel with Sprint 5 UI work.*

17. **Analytics Application Service** — `LienSaleAnalyticsService.cs`:
    - Financial metrics: total receivables, outstanding balance, settlement exposure, payment totals, average lien balance
    - Aging buckets: 0–30, 31–60, 61–90, 90–120, 120+
    - Exposure analytics: provider/law firm/lien concentration/balance concentration
    - Activity analytics: created/published/withdrawn portfolios, lifecycle durations

18. **Analytics API Endpoint**:
    - `GET /api/lien-sale/portfolios/:id/analytics`

19. **Reporting Views** — EF migration adds DB views:
    - `v_lien_sale_portfolio_exposure` (avoids OLTP overload per Story 8.3)
    - `v_lien_sale_aging`
    - `v_lien_sale_lifecycle`
    - `v_lien_sale_concentration`

20. **Stories 4.1–4.4, 8.1–8.3** ✅

---

## Sprint 5: Seller Portal UI (Epic 5)

*Depends on Sprint 3 APIs. Runs parallel with Sprint 4.*

21. **Next.js Routes** — Add under `apps/web/src/app/(platform)/lien/sale/`:
    - `portfolios/page.tsx` — Portfolio List Screen (Story 5.1)
    - `portfolios/[id]/page.tsx` — Portfolio Detail Screen (Story 5.2)
    - `portfolios/[id]/builder/page.tsx` — Portfolio Builder Screen (Story 5.3)
    - `analytics/page.tsx` — Portfolio Analytics Dashboard (Story 5.4)

22. **Frontend Navigation** — Add "Lien Sales" nav entry under SynqLien section in `apps/web/src/components/` nav/sidebar.

23. **API Client Layer** — Add to `apps/web/src/lib/liens/`:
    - `lienSalePortfolioApi.ts` — typed fetch wrappers for all `/api/lien-sale/...` endpoints
    - `lienSalePortfolioTypes.ts` — TypeScript interfaces matching backend DTOs

24. **UI Components** — Add to `apps/web/src/components/lien/sale/`:
    - `PortfolioTable.tsx` — sortable/filterable table with status badges, receivable totals, aging indicators
    - `PortfolioDetailCard.tsx` — financial summary cards
    - `PortfolioBuilderLienSearch.tsx` — search + bulk select + eligibility warnings
    - `PortfolioLifecycleTimeline.tsx` — status timeline component
    - `PortfolioActivityFeed.tsx` — chronological event feed
    - `PortfolioAnalyticsDashboard.tsx` — KPI cards, aging charts, exposure charts, portfolio trends

25. **Stories 5.1–5.4** ✅

---

## Sprint 6: Audit, Activity Tracking, Security & RBAC (Epics 6 + 7)

*Depends on Sprint 3. Runs parallel with Sprint 4.*

26. **Activity Logging Engine** — `LienSaleActivityLoggingService.cs`:
    - Log create, update, publish, withdraw, lien add/remove, status changes
    - Immutable rows (no update/delete in repository implementation)
    - Actor attribution (user ID from JWT claims)

27. **Activity Feed API**:
    - `GET /api/lien-sale/portfolios/:id/activity` — paginated, chronological, actor-attributed

28. **Immutable Audit Protection** — Application-layer enforcement in `LienSaleActivityConfiguration.cs`; no UPDATE/DELETE on activity table.

29. **Tenant Isolation** — All repository queries enforce `WHERE tenant_id = @tenantId`; cross-tenant access returns 403/404.

30. **RBAC Permissions** — Register 5 new permissions in the identity/permissions system:
    - `lienSale.create`, `lienSale.update`, `lienSale.publish`, `lienSale.withdraw`, `lienSale.viewAnalytics`

31. **API Authorization Middleware** — Apply `[Authorize]` + permission checks to all `/api/lien-sale/...` endpoints using existing auth middleware pattern from `Liens.Api`.

32. **Stories 6.1–6.3, 7.1–7.3** ✅

---

## Sprint 7: QA / Hardening (Epic 9)

*Depends on all prior sprints.*

33. **Automated API Tests** — `Liens.Api.Tests/` (or integration test project):
    - CRUD API coverage (Story 9.1)
    - Validation rule tests (eligibility, status transitions)
    - Lifecycle transition tests (valid/invalid transitions)
    - RBAC tests (unauthorized access returns 403)
    - Tenant isolation tests (cross-tenant access returns 403/404)

34. **UI Workflow Tests** — Playwright tests in `apps/web/e2e/`:
    - Portfolio creation flow (Story 9.2)
    - Lien assignment flow
    - Publish workflow
    - Withdraw workflow

35. **Security Validation** (Story 9.3):
    - Tenant isolation integration test
    - RBAC enforcement sweep
    - Unauthorized access prevention validation

36. **UAT + Implementation Report** ✅

---

## Relevant Files

### Backend — extend existing service
- `apps/services/liens/Liens.Domain/` — add domain entities + status enum
- `apps/services/liens/Liens.Infrastructure/Persistence/LiensDbContext.cs` — register new DbSets
- `apps/services/liens/Liens.Infrastructure/Persistence/Configurations/` — add 4 new EF configs
- `apps/services/liens/Liens.Infrastructure/Persistence/Migrations/` — new migration(s)
- `apps/services/liens/Liens.Infrastructure/Repositories/` — add 3 new repository implementations
- `apps/services/liens/Liens.Application/Repositories/` — add 3 new repository interfaces
- `apps/services/liens/Liens.Application/Services/` — add 5 new application services
- `apps/services/liens/Liens.Api/Endpoints/LienSale/` — add 3 new endpoint files
- `apps/services/liens/Liens.Api/Program.cs` — register new endpoint mappings

### Frontend
- `apps/web/src/app/(platform)/lien/sale/` — 4 new page routes
- `apps/web/src/components/lien/sale/` — 6 new UI components
- `apps/web/src/lib/liens/` — API client + TypeScript types for lien sale

### Architecture Reference
- Follow minimal API pattern from `apps/services/liens/Liens.Api/Endpoints/`
- Follow repo interface pattern from `apps/services/liens/Liens.Application/Repositories/`
- Follow EF config pattern from `apps/services/liens/Liens.Infrastructure/Persistence/Configurations/`
- Follow frontend page pattern from `apps/web/src/app/(platform)/lien/`
- Follow frontend component pattern from `apps/web/src/components/lien/`

---

## Verification

1. `dotnet build` on `Liens.sln` — no compile errors
2. EF migration dry-run confirms 4 new tables created
3. `POST /api/lien-sale/portfolios` returns 201 with UUID, tenant-scoped
4. `POST /api/lien-sale/portfolios/:id/liens` with invalid lien (closed/zero balance) returns 422 with validation message
5. `POST /api/lien-sale/portfolios/:id/publish` with DRAFT status transitions to PUBLISHED
6. Invalid lifecycle transition (e.g., WITHDRAWN→PUBLISHED) returns 400
7. Cross-tenant portfolio access returns 403/404
8. Missing `lienSale.create` permission returns 403
9. `GET /api/lien-sale/portfolios/:id/analytics` returns correct financial metrics
10. Audit rows cannot be updated/deleted (test at DB level)
11. Playwright: full portfolio creation → lien assignment → publish flow passes
12. All automated API test suites pass

---

## Decisions

- **Scope excluded from Phase 1**: Buyer onboarding, buyer review portal, document package sharing, negotiation, offer management, payment execution, mobile workflows
- **Service boundary**: Lien sale workflow lives inside the existing `apps/services/liens` service (not a new microservice) — reuses lien data access
- **Reporting isolation**: DB views prevent analytics queries from hitting OLTP tables
- **Audit immutability**: Application-layer enforcement (no update/delete repo methods on `lien_sale_activity`), optionally backed by DB-level constraints
- **Event model**: 6 domain events defined (`PORTFOLIO_CREATED/UPDATED`, `LIEN_ADDED/REMOVED`, `PORTFOLIO_PUBLISHED/WITHDRAWN`) — internal activity logging sufficient for Phase 1; event bus integration deferred
