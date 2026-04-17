# Flow Service

A generic, reusable workflow and task orchestration service. Flow provides primitives for defining workflows, managing task lifecycles, processing events, and (in later phases) participating in audit and notification flows.

## Service Boundary

Flow is a **detachable, standalone platform service**:

- Owns its own database (`flow_db`); never shares schema or connections with consuming applications.
- Exposes a REST API for external integration; consumers must not access `flow_db` directly.
- Uses generic `ContextReference` linkage instead of product-specific entities (no `caseId`/`lienId`/`referralId` in core models).
- Adapter interfaces let consuming products plug in product-specific behavior without polluting Flow's core.

## Layout

```
apps/services/flow/
  backend/      .NET 8 Web API (Flow.Api / Flow.Application / Flow.Domain / Flow.Infrastructure)
  frontend/    Next.js 16 / React 19 / Tailwind v4 admin UI
  docs/        architecture + merge notes
```

## Running locally (Phase 1)

Backend (separate from `LegalSynq.sln`):

```bash
dotnet build apps/services/flow/backend/Flow.sln
dotnet run --project apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj
```

Frontend (uses npm, isolated from the main pnpm workspace):

```bash
cd apps/services/flow/frontend
npm install
npm run dev
```

## Database

- Connection-string key: `ConnectionStrings:FlowDb`
- Default DB name: `flow_db`
- Provider: MySQL (Pomelo.EntityFrameworkCore.MySql)

## Planned Integrations (deferred to later phases)

- Identity v2 (auth, tenant context)
- Notifications service (event delivery)
- Audit service (event capture)
- Cross-service event bus
- DB / sln consolidation decisions

See `merge-phase-1-notes.md` for the full deferral list.

---

## Phase 2 — Platform Integration (2026-04-17)

Flow is now a first-class LegalSynq platform service. Key changes:

- **Auth:** JWT bearer auth using shared `Jwt:*` config; `[Authorize(Policy = AuthenticatedUser)]` on all V1 controllers.
- **Tenant:** strict claims-based resolution (`tenant_id` JWT claim). No silent default fallback.
- **CORS:** environment-driven via `Cors:AllowedOrigins`.
- **Gateway:** `/flow/health`, `/flow/info`, `/flow/api/v1/status` (anonymous) and `/flow/{**catch-all}` (protected) routed to `localhost:5012`.
- **Adapters:** `IAuditAdapter` and `INotificationAdapter` seams + safe logging baselines + optional HTTP impls (activate via `Audit:BaseUrl` / `Notifications:BaseUrl`).
- **Internal events:** `IFlowEventDispatcher` + `Flow.Application/Events/*` (in-process only).

See `merge-phase-2-notes.md` for the full Phase 2 changelog and deferrals.
