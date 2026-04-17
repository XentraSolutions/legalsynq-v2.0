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
