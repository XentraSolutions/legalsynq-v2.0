# LS-FLOW-MERGE-P2 Report

**Status:** IN PROGRESS
**Date:** 2026-04-17

## Scope Executed

Phase 2 — Platform integration of the Flow service:
- Identity v2 (JWT + claims-based context) on Flow.Api
- Strict tenant resolution (no silent default fallback)
- Environment-driven CORS
- Internal in-process event abstraction
- Audit adapter seam + safe baseline (logging) + optional HTTP adapter
- Notifications adapter seam + safe baseline (logging) + optional HTTP adapter
- Gateway routing for `/flow/health`, `/flow/info`, `/flow/{**catch-all}`
- Controller `[Authorize]` enforcement
- Frontend `/tasks` Suspense fix; auth header propagation
- Documentation updates

## Assumptions

1. Flow continues as a bounded service under `/apps/services/flow` with its own DB (`flow_db`) and its own `Flow.sln`. No DB consolidation, no merge into `LegalSynq.sln`.
2. JWT scheme matches the platform-wide convention used by Reports/Liens/Fund/Comms: shared `Jwt:Issuer = legalsynq-identity`, `Jwt:Audience = legalsynq-platform`, signing key delivered via secret/config (`Jwt:SigningKey`).
3. Tenant identity is resolved from the JWT `tenant_id` claim through `BuildingBlocks.Context.ICurrentRequestContext`, the same interface every other service uses. No more `X-Tenant-Id` header trust by default.
4. Existing schema column `TenantId` is `string`. The JWT claim is a `Guid`. The new `ClaimsTenantProvider` formats the claim as a stable lowercase string ("d") and throws when no authenticated tenant is available — making "default" data effectively unreachable to authenticated callers (no migration needed).
5. CORS origins are read from `Cors:AllowedOrigins`. Local dev defaults preserve the existing two origins (`http://localhost:3000`, `http://localhost:3001`); platform/gateway origins must be supplied via config in higher environments.
6. Audit and Notifications integration is wired as **adapter seams + safe-baseline (logging) implementations**, with conditional HTTP-backed implementations that activate only when `Audit:BaseUrl` / `Notifications:BaseUrl` are configured. Adapter interfaces are intentionally narrow; deep wiring into every TaskService/WorkflowService CRUD path is partially done at the most natural sites and the rest is explicitly deferred to Phase 3 product-consumption work.
7. Flow listens on port **5012** (next free port: 5011 = Comms, 5010 = Gateway, 5009 = Liens).
8. Flow is **not** added to `LegalSynq.sln` and **not** added to `scripts/run-dev.sh` startup orchestration in this phase — start it independently. This is consistent with Phase 1's boundary preservation; revisit only when product consumption begins.

## Repository / Architecture Notes

(Filled progressively as work lands.)

## Identity Integration Notes

(Filled progressively.)

## Tenant Resolution Notes

(Filled progressively.)

## Security / CORS Notes

(Filled progressively.)

## Gateway Integration Notes

(Filled progressively.)

## Audit Integration Notes

(Filled progressively.)

## Notifications Integration Notes

(Filled progressively.)

## Frontend Integration Notes

(Filled progressively.)

## Documentation Changes

(Filled progressively.)

## Validation Results

(Filled at end.)

## Known Issues / Gaps

(Filled at end.)

## Recommendation

(Filled at end.)
