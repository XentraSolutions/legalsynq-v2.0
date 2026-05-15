# LS-INT-01 — Commerce + Tenant Billing Platform Integration

> **Status:** COMPLETE

---

## 1. Executive Summary

LS-INT-01 integrates the standalone Commerce + Tenant Billing bounded contexts into the LegalSynq platform through identity, tenant context, gateway routing, and RBAC — without merging databases, domains, or billing logic.

Both services remain fully independent standalone runtimes. Integration is additive and dual-mode:
- **Primary path**: LegalSynq JWT → `tenant_id` claim → canonical tenant context
- **Fallback path**: `X-Internal-Token` + `X-Tenant-Id` header (existing standalone behavior, preserved unchanged)

**Architecture rule enforced throughout**: Platform/SaaS billing (Commerce) and Tenant/Customer billing (Tenant Billing) remain isolated bounded contexts. No shared DbContext, no merged invoices/payments/statements, no direct cross-domain EF access.

---

## 2. Uploaded Source Audit

### 2.1 Services Found

| Service | Path | Runtime Status |
|---|---|---|
| Commerce | `apps/services/commerce/` | **Canonical** — active standalone runtime |
| Tenant Billing | `apps/services/tenant-billing/` | **Canonical** — active standalone runtime |
| Legacy Tenant Billing API | `apps/services/tenant-billing-api/` | **Preserved artifact** — not bound to any workflow, not referenced; retained for rollback safety per TB-MERGE-02; final deletion deferred to TB-MERGE-03 |

### 2.2 Commerce Service (COM-B01 → COM-B08)

**Solution**: `apps/services/commerce/Commerce.sln`
**Projects**: Commerce.Domain, Commerce.Contracts, Commerce.Application, Commerce.Infrastructure, Commerce.Api, Commerce.Tests

| Component | Status |
|---|---|
| `BillingAccount` domain model | ✅ Present — `Commerce.Domain.Billing.BillingAccount` |
| Entitlement publishing (TB-INT-01/02) | ✅ Present — `TenantBillingEntitlementPublisher` |
| Durable outbox (TB-INT-04) | ✅ Present — `EfTenantBillingEntitlementOutbox`, `TenantBillingEntitlementPublishOutboxRow` |
| Diagnostics endpoints | ✅ Present — `GET /api/commerce/integration/tenant-billing/*/diagnostics` |
| Auto-publish (TB-INT-03) | ✅ Present — `ITenantBillingEntitlementPublishQueue` + `TenantBillingEntitlementPublishWorker` |
| Standalone runtime | ✅ `Commerce:TenantBilling:Enabled=false` default; `/health`, `/ready` work without DB |
| JWT placeholder | ✅ `Jwt:Enabled=false` in appsettings — placeholder ready for real implementation |
| `IHostIdentityContextAccessor` | ✅ Interface + `LocalHostIdentityContextAccessor` stub (anonymous, `IsAuthenticated=false`) |
| `IHostTenantResolver` | ✅ Interface + `NoopHostTenantResolver` (EF-backed, resolves from `BillingAccountExternalRef`) |
| `IHostIntegrationAdapter` | ✅ Composite interface, `LocalHostIntegrationAdapter` as default |

### 2.3 Tenant Billing Service (TB-DATA-01 → TB-ENF-01)

**Solution**: `apps/services/tenant-billing/Billing.sln`
**Projects**: Billing.Domain, Billing.Infrastructure, Billing.Api + tests

| Component | Status |
|---|---|
| Standalone runtime | ✅ `X-Internal-Token` gate + `X-Tenant-Id` header; works without JWT |
| `DbContext` isolation | ✅ `BillingDbContext` — no Commerce tables; migrations are disjoint |
| Enforcement | ✅ `RequireTenantBillingAccessAttribute` on 9 controllers; `Billing:EntitlementEnforcement:Enabled=false` default |
| Entitlement snapshots | ✅ `TenantBillingEntitlementSnapshot` + `TenantBillingProfile` domain; tables `billing_tenant_billing_entitlement_snapshots`, `billing_tenant_billing_profiles` |
| Invoice/Payment/Template/Statement | ✅ Full domain — 17 controllers covering invoices, payments, refunds, adjustments, customers, statements, templates, ERP, delivery analytics |
| `ITenantContext` | ✅ `HttpHeaderTenantContext` — reads from `HttpContext.Items` set by `TenantResolutionMiddleware` |
| Internal token middleware | ✅ `RequireInternalTokenMiddleware` — fails closed when no token configured |

### 2.4 Legacy Tenant Billing API

- 171 files, contains `TenantBilling.Api` + `TenantBilling.Domain` projects
- Predecessor to `Billing.*` canonical service
- **No workflow binding**, **no project references from other code**
- Preserved for rollback per TB-MERGE-02; deletion deferred to TB-MERGE-03

---

## 3. Canonical Runtime Determination

| Service | Canonical | Notes |
|---|---|---|
| `apps/services/commerce/` | ✅ YES | COM-B01 through COM-B08 complete |
| `apps/services/tenant-billing/` | ✅ YES | TB-DATA-01 through TB-ENF-01 complete |
| `apps/services/tenant-billing-api/` | ❌ NO | Legacy; artifact only |

---

## 4. Standalone Boundary Validation

**Confirmed**: No cross-service project references.
- `Billing.*` has zero `<ProjectReference>` to Commerce
- `Commerce.*` has zero `<ProjectReference>` to Billing
- Commerce → Tenant Billing bridge is HTTP-only, config-gated, outbound-only
- No shared `DbContext`; `CommerceDbContext` and `BillingDbContext` are completely separate
- Migration sets are disjoint (Commerce: 11 migrations; TB: 19 migrations)
- Domain types with same names (`Invoice`, `Payment`) are different CLR types in different assemblies

---

## 5. Commerce Integration Validation

**Publisher bridge** (`Commerce:TenantBilling`): Config-gated, disabled by default. When enabled, Commerce POSTs to TB `POST /api/tenant-billing/entitlements/apply` with `X-Internal-Token` + `X-Tenant-Id`. No direct DB access. Durable outbox backed by `tenant_billing_entitlement_publish_outbox` table with retry, linear backoff, circuit breaker.

**Auto-publish triggers** (TB-INT-03): Subscription lifecycle + AccountStanding recalculation trigger enqueue; background worker drains. `RenewAsync` intentionally excluded (no entitlement change).

**Outbox** (TB-INT-04): Stale recovery, claim-with-lock, per-row retry/abandon policy, publisher-disabled → reschedule-without-consuming-attempt.

---

## 6. Identity Integration Design

### JWT Claim Conventions (LegalSynq Identity)

From `Identity.Infrastructure/Services/JwtTokenService.cs`:

| Claim | Value | Notes |
|---|---|---|
| `sub` | `user.Id` (Guid) | Stable user identity |
| `tenant_id` | `tenant.Id` (Guid) | **Primary canonical tenant identifier** |
| `tenant_code` | `tenant.Code` (string) | Human-readable tenant code |
| `email` | user email | |
| `role` | role name (multiple) | Short name — `PlatformAdmin`, `TenantAdmin`, etc. |
| `org_id` | organization.Id (Guid) | When org context present |
| `org_type` | org type code | |
| `product_codes` | product code (multiple) | |
| `product_roles` | product role (multiple) | |
| Issuer | `legalsynq-identity` | |
| Audience | `legalsynq-platform` | |
| Algorithm | HS256 | Key from `Jwt__SigningKey` secret |

### Commerce Identity Integration

New components in `apps/services/commerce/src/Commerce.Infrastructure/Integration/HostAdapters/`:
- `LegalSynqIdentityOptions` — `LegalSynq:Identity` config section (Enabled, Issuer, Audience, SigningKey, HostPlatformKey)
- `LegalSynqJwtHostIdentityContextAccessor` — reads `HttpContext.User` claims → `HostIdentityContext` with `IsAuthenticated=true`; falls back to `LocalHostIdentityContextAccessor` pattern when no principal
- `LegalSynqJwtHostTenantResolver` — reads `tenant_id` claim as `ExternalTenantId`, delegates to existing EF `BillingAccountExternalRef` lookup
- `LegalSynqCommerceHostIntegrationAdapter` — replaces `LocalHostIntegrationAdapter`, `HostPlatformKey = "legalsynq"`
- JWT bearer authentication added to `Program.cs` (gated by `LegalSynq:Identity:Enabled`)
- `LegalSynqCommerceDiExtensions.AddLegalSynqCommerceIntegration()` — registers all adapters

### Tenant Billing Identity Integration

New components in `apps/services/tenant-billing/src/Billing.Api/`:
- `LegalSynqTenantContextOptions` — `LegalSynq:TenantContext` section (Enabled, PreferJwtTenant, AllowHeaderFallback, AllowInternalTokenFallback)
- `ITenantIdentityContextResolver` — dual-mode interface: `ResolveAsync(HttpContext) → TenantResolutionResult`
- `LegalSynqJwtTenantContextResolver` — Priority 1: JWT `tenant_id` claim; Priority 2: `X-Tenant-Id` header fallback; Priority 3: internal-service claim fallback
- `TenantResolutionMiddleware` updated to use `ITenantIdentityContextResolver` when `LegalSynq:TenantContext:Enabled=true`; otherwise original header-only behavior
- JWT bearer auth in `Program.cs` (gated by `LegalSynq:Identity:Enabled`)

---

## 7. Tenant Context Resolution Design

### Resolution Hierarchy (dual-mode)

```
Request → TenantResolutionMiddleware
  │
  ├─ LegalSynq:TenantContext:Enabled = false → legacy X-Tenant-Id only (unchanged)
  │
  └─ LegalSynq:TenantContext:Enabled = true
       │
       ├─ 1. JWT valid + tenant_id claim present → use JWT tenant_id
       │
       ├─ 2. JWT valid + internal_service claim → use X-Tenant-Id header (service-to-service)
       │
       └─ 3. LegalSynq:TenantContext:AllowHeaderFallback = true → X-Tenant-Id header
                (standalone / staged migration / operational rollback)
```

### Standalone Compatibility Guarantee

- `LegalSynq:TenantContext:Enabled=false` (default): zero behavior change; original `X-Internal-Token` + `X-Tenant-Id` pipeline runs unchanged
- `LegalSynq:Identity:Enabled=false` (default): no JWT auth middleware registered; Commerce and TB boot and serve requests exactly as before

---

## 8. RBAC Mapping

### Platform Role Constants (`LegalSynqPlatformRoles`)

| Constant | Claim Value | Commerce Access | Tenant Billing Access |
|---|---|---|---|
| `PlatformAdmin` | `PlatformAdmin` | Full admin + all billing accounts | All tenants |
| `TenantAdmin` | `TenantAdmin` | Own billing account | Own tenant |
| `BillingManager` | `BillingManager` | Billing + subscription writes | Invoice/payment writes |
| `BillingReadOnly` | `BillingReadOnly` | Read-only | Read-only |
| `SupportAgent` | `SupportAgent` | Account standing + audit read | Customer + invoice read |
| `InternalService` | `InternalService` | Integration endpoints | Entitlement apply |

**Principle**: Identity determines WHO; Entitlement determines WHAT. These are never mixed.

---

## 9. Middleware Changes

### Commerce `Program.cs` additions (additive, gated)

```
app.UseMiddleware<CorrelationIdMiddleware>();     // pre-existing
app.UseMiddleware<ProblemDetailsExceptionMiddleware>(); // pre-existing
// NEW (when LegalSynq:Identity:Enabled=true):
app.UseAuthentication();
app.UseAuthorization();
```

### Tenant Billing `Program.cs` additions (additive, gated)

```
app.UseMiddleware<RequireInternalTokenMiddleware>();   // pre-existing (unchanged)
app.UseMiddleware<TenantResolutionMiddleware>();       // updated: uses ITenantIdentityContextResolver when enabled
// NEW (when LegalSynq:Identity:Enabled=true):
app.UseAuthentication();
app.UseAuthorization();
```

Both pipelines: internal token mode still works. Standalone mode still works. JWT mode is opt-in.

---

## 10. Gateway/BFF Preparation

YARP routes added to `apps/gateway/Gateway.Api/appsettings.json`:
- `commerce-health` (`/commerce/health`) — anonymous
- `commerce-protected` (`/commerce/{**}`) — authenticated
- `billing-health` (`/billing/health`, `/billing/healthz`) — anonymous
- `billing-protected` (`/billing/{**}`) — authenticated

Cluster ports:
- Commerce: `:5030`
- Tenant Billing: `:5031`

---

## 11. Enforcement Compatibility Validation

- `RequireTenantBillingAccessAttribute` unchanged — reads `ITenantContext.TenantId` which is populated by `TenantResolutionMiddleware` regardless of JWT or header source
- `Billing:EntitlementEnforcement:Enabled=false` default unchanged
- Commerce `Jwt:Enabled=false` default unchanged — Commerce controller authorization not enforced until enabled

---

## 12. Configuration Changes

### Commerce `appsettings.json` additions

```json
"LegalSynq": {
  "Identity": {
    "Enabled": false,
    "Issuer": "legalsynq-identity",
    "Audience": "legalsynq-platform",
    "SigningKey": "",
    "HostPlatformKey": "legalsynq"
  }
}
```

### Tenant Billing `appsettings.json` additions

```json
"LegalSynq": {
  "Identity": {
    "Enabled": false,
    "Issuer": "legalsynq-identity",
    "Audience": "legalsynq-platform",
    "SigningKey": ""
  },
  "TenantContext": {
    "Enabled": false,
    "PreferJwtTenant": true,
    "AllowHeaderFallback": true,
    "AllowInternalTokenFallback": true
  }
}
```

**Rules**: Safe defaults (all `Enabled=false`). Existing configs untouched. No real secrets committed. Production supplies `Jwt__SigningKey` via environment variable.

---

## 13. Build/Test Validation

*(Updated incrementally as work completes)*

### Pre-integration baseline (from TB-CURRENT-STATE-DEEP-VALIDATION)
- Commerce: `dotnet build` → 0 errors, NU1902 advisory only
- Tenant Billing: `dotnet build` → 0 errors
- `Billing.Domain.Tests`: 529/529 passing
- `Commerce.Tests`: 362/363 (one pre-existing date-sensitive failure: `ChangePlan_closes_old_item_and_creates_new`)

### Post-integration

| Target | Result | Notes |
|---|---|---|
| `Commerce.sln` build | ✅ 0 errors | NU1902 advisory (OTLP pkg, pre-existing) |
| `Billing.sln` build | ✅ 0 errors | |
| `LegalSynqCommerceIdentityTests` | ✅ 19/19 passed | Config defaults, role helpers, standalone guarantees |
| `LegalSynqTenantContextResolverTests` | ✅ 21/21 passed | JWT claim, internal-service, header fallback, safe defaults |
| `Billing.Domain.Tests` | ✅ Baseline intact | Pre-existing 529/529 passing; not re-run (no domain changes) |

### global.json SDK version fix (collateral)

Both `apps/services/commerce/global.json` and `apps/services/tenant-billing/global.json` pinned SDK
`8.0.416` with `rollForward: latestFeature`, which caused NuGet restore to fail in the environment
(SDK 8.0.412 installed). Both updated to `8.0.412`. This is a fix for the standalone build,
independent of LS-INT-01 content.

---

## 14. Risks / Deferred Items

| Item | Risk | Mitigation |
|---|---|---|
| `SigningKey` must match LegalSynq Identity `Jwt__SigningKey` exactly | High (auth failure) | Both services read from env var `COMMERCE_LEGALSYNQ_SIGNING_KEY` / `BILLING_LEGALSYNQ_SIGNING_KEY`; same key as `Jwt__SigningKey` |
| `BillingAccountExternalRef` must exist for JWT tenant resolution (Commerce) | Medium | `IHostTenantResolver` returns `null` gracefully when no ref exists; Commerce continues to operate with anonymous context |
| Port assignments (5030/5031) conflict with future services | Low | Can be reconfigured via env var |
| TB-MERGE-03 (legacy `tenant-billing-api` deletion) deferred | Low | Legacy service is unbound, unreferenced |
| `SELECT FOR UPDATE SKIP LOCKED` not implemented in outbox | Low | Documented in TB-INT-04; single-pod deployment recommended until added |

---

## 15. Confirmation of Non-Merge Boundaries

- ✅ `BillingDbContext` and `CommerceDbContext` are separate — no shared tables
- ✅ No `ProjectReference` from TB to Commerce or vice versa
- ✅ Commerce `Invoice` ≠ TB `Invoice` — different CLR types, different tables
- ✅ Commerce `Payment` ≠ TB `Payment` — different CLR types, different tables
- ✅ Commerce → TB bridge is HTTP-only, config-gated, outbound-only
- ✅ No shared entitlement enforcement logic
- ✅ No shared customer/contact identity logic
- ✅ `LegalSynq:Identity:Enabled=false` by default — zero behavior change on deploy

---

## 16. Recommended Next Integration Block

**LS-INT-02** — Control Center awareness:
- Control Center page: Commerce service health + billing account summary
- Control Center page: Tenant Billing profile + entitlement status per tenant
- YARP gateway route for `/commerce/**` and `/billing/**` already prepared
- Entitlement enforcement opt-in toggle via Control Center UI
- Monitoring health probe registration for Commerce + Tenant Billing
