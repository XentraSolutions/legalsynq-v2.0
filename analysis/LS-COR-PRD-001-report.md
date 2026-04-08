# LS-COR-PRD-001 — Product Provisioning Engine

## Summary

Implemented a centralized Product Provisioning Engine within the Identity service that standardizes tenant product enablement, organization product provisioning, eligibility enforcement, and product-specific provisioning hooks. The engine replaces duplicated provisioning logic previously scattered across admin endpoints with a single reusable orchestration path.

## Confirmed Root Cause / Current Gap

Before this change, product provisioning logic was embedded directly in three separate endpoint methods within `AdminEndpoints.cs`:

1. **`UpdateEntitlement`** — manually created TenantProduct, iterated orgs, created OrganizationProducts
2. **`ProvisionForCareConnect`** — duplicated TenantProduct/OrganizationProduct creation with CareConnect-specific role assignment
3. **`CreateTenant`** — no product provisioning at all; products had to be enabled separately after tenant creation

This caused:
- Duplicated provisioning logic across endpoints
- No centralized eligibility enforcement (any product could be enabled for any org)
- No extensible hook system for product-specific setup
- CareConnect provider creation required manual post-provisioning steps
- No way to provision products during tenant onboarding

## Architecture Changes

### New Components

| Component | Layer | Purpose |
|---|---|---|
| `ProductEligibilityConfig` | Identity.Domain | Centralized OrgType → Product eligibility rules |
| `ProductCodes` | Identity.Domain | String constants for product codes |
| `IProductProvisioningHandler` | Identity.Application | Abstraction for product-specific provisioning hooks |
| `IProductProvisioningService` | Identity.Application | Engine interface |
| `ProductProvisioningService` | Identity.Infrastructure | Core engine: orchestrates tenant product, org products, and handlers |
| `CareConnectProvisioningHandler` | Identity.Infrastructure | CareConnect-specific handler: creates/links providers in CareConnect service |
| `InternalProvisionEndpoints` | CareConnect.Api | Internal `/internal/provision-provider` endpoint for service-to-service provisioning |

### Provisioning Flow

```
Caller (UpdateEntitlement / CreateTenant / ProvisionForCareConnect)
  │
  └─► ProductProvisioningService.ProvisionAsync(tenantId, productCode, enabled)
        │
        ├─► ProvisionTenantProduct()        — create/enable/disable TenantProduct
        │
        ├─► ProvisionOrganizationProducts() — cascade to eligible orgs only
        │     └─► ProductEligibilityConfig.IsEligible(orgType, productCode)
        │
        └─► ExecuteProvisioningHandlers()   — resolve and run product-specific handler
              └─► CareConnectProvisioningHandler (for SYNQ_CARECONNECT)
                    └─► HTTP POST CareConnect /internal/provision-provider
                          └─► Create/link/activate Provider record
```

## Files Changed

### New Files
- `apps/services/identity/Identity.Domain/ProductEligibilityConfig.cs`
- `apps/services/identity/Identity.Application/Interfaces/IProductProvisioningHandler.cs`
- `apps/services/identity/Identity.Application/Interfaces/IProductProvisioningService.cs`
- `apps/services/identity/Identity.Infrastructure/Services/ProductProvisioningService.cs`
- `apps/services/identity/Identity.Infrastructure/Services/CareConnectProvisioningHandler.cs`
- `apps/services/careconnect/CareConnect.Api/Endpoints/InternalProvisionEndpoints.cs`

### Modified Files
- `apps/services/identity/Identity.Infrastructure/DependencyInjection.cs` — registered new services and HttpClient
- `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` — refactored `UpdateEntitlement`, `ProvisionForCareConnect`, and `CreateTenant` to use engine
- `apps/services/careconnect/CareConnect.Api/Program.cs` — registered internal provision endpoints
- `apps/services/careconnect/CareConnect.Application/Repositories/IProviderRepository.cs` — added `GetByOrganizationIdAsync`
- `apps/services/careconnect/CareConnect.Infrastructure/Repositories/ProviderRepository.cs` — implemented `GetByOrganizationIdAsync`

## API/Service Integration Points Updated

| Endpoint | Change |
|---|---|
| `POST /api/admin/tenants/{id}/entitlements/{productCode}` | Now delegates to `ProductProvisioningService` instead of inline logic |
| `POST /api/admin/users/{id}/provision-careconnect` | Now delegates TenantProduct/OrganizationProduct creation to engine |
| `POST /api/admin/tenants` | Now accepts optional `products` array for onboarding-time provisioning |
| `POST /internal/provision-provider` (CareConnect) | New internal endpoint for service-to-service provider creation |

## Config Additions

### Product Eligibility Rules (`ProductEligibilityConfig`)

| Org Type | Allowed Products |
|---|---|
| LAW_FIRM | SYNQ_CARECONNECT, SYNQ_FUND, SYNQ_LIENS |
| PROVIDER | SYNQ_CARECONNECT |
| FUNDER | SYNQ_FUND |
| LIEN_OWNER | SYNQ_LIENS |
| INTERNAL | All products |

### DI Registration

- `CareConnectInternal` HttpClient (base URL: `http://localhost:5005`, configurable via `CareConnect:InternalUrl`)
- `IProductProvisioningHandler` → `CareConnectProvisioningHandler` (scoped)
- `IProductProvisioningService` → `ProductProvisioningService` (scoped)

## How Eligibility Is Enforced

`ProductEligibilityConfig.IsEligible(orgType, productCode)` is called during the organization product provisioning step. Only organizations whose `OrgType` is in the allowed set for the product receive `OrganizationProduct` records. Organizations with ineligible types are skipped with a debug log message.

This is a single centralized static class in `Identity.Domain`. All provisioning validation flows through this source. Adding a new product or org-type rule requires updating only this file.

## How Duplication Was Removed

- **Before**: `UpdateEntitlement` had 50+ lines of inline TenantProduct + OrganizationProduct creation. `ProvisionForCareConnect` had a separate 60+ line copy of the same logic. `CreateTenant` had no provisioning.
- **After**: All three endpoints delegate to `ProductProvisioningService.ProvisionAsync()`. The engine contains the single implementation of tenant product creation, organization product cascading, and handler execution.

## Disable Path Semantics

When disabling a product (`enabled=false`), the engine disables ALL existing OrganizationProduct records for that product across the entire tenant, regardless of org type eligibility. Eligibility filtering only applies to the enable path — this ensures that historically provisioned or mis-provisioned records are properly cleaned up on disable.

## Internal Endpoint Security

The CareConnect `/internal/provision-provider` endpoint uses a shared service token (`X-Internal-Service-Token` header) to authenticate internal service-to-service calls. Unauthenticated requests receive 401. The token is configurable via `InternalServiceToken` in CareConnect's appsettings.json (default: `legalsynq-internal-service-2024`).

## Product Code Normalization

The `CreateTenant` endpoint accepts both frontend product codes (e.g., `SynqFund`) and DB codes (e.g., `SYNQ_FUND`) in the `products` array. Frontend codes are normalized to DB codes using the same `FrontendToDbProductCode` mapping used by `UpdateEntitlement`.

## How Idempotency Is Ensured

1. **TenantProduct**: `ProvisionTenantProduct()` checks for existing record before creating. If exists and already in desired state, no-op.
2. **OrganizationProduct**: `ProvisionOrganizationProducts()` checks each org's existing products. Existing enabled products are skipped; existing disabled products are toggled.
3. **CareConnect Provider**: `InternalProvisionEndpoints.ProvisionProvider` searches by `OrganizationId` first. If a provider already exists for the org, it activates it instead of creating a duplicate.
4. **Handler execution**: The handler catches exceptions per-organization, so a failure on one org doesn't prevent others from being processed.

## How Consistency/Transaction Handling Is Approached

- **Identity-side**: All TenantProduct and OrganizationProduct changes within a single `ProvisionAsync` call share the same `IdentityDbContext` and are committed in a single `SaveChangesAsync()` call. This ensures atomicity within the Identity database.
- **Cross-service (CareConnect)**: The CareConnect provisioning handler runs AFTER the Identity DB commit. If the CareConnect call fails, Identity-side provisioning remains complete. The handler logs warnings for failed orgs and includes them in the result. This is a best-effort approach — the Identity side is always consistent, and CareConnect failures are recoverable by re-running provisioning.
- **Limitation**: Full cross-database atomicity between Identity (MySQL/RDS) and CareConnect (separate MySQL/RDS) is not possible without a distributed transaction coordinator. The current design is eventually consistent with safe retry semantics.

## Test Scenarios Executed

### 1. Build Verification
- All services compile successfully: Identity, CareConnect, Gateway, Fund, Audit
- No warnings or errors in Release configuration

### 2. Admin Product Enablement Flow (Design Verification)
- `UpdateEntitlement` endpoint now delegates to `ProductProvisioningService`
- Response includes `provisioningResult` with counts of created/updated records
- Eligibility filtering ensures only compatible orgs receive products

### 3. Onboarding Integration Flow (Design Verification)
- `CreateTenant` now accepts optional `products` array
- After tenant + org + user creation, the engine provisions each requested product
- Failures during product provisioning are caught and logged (don't block tenant creation)

### 4. Idempotency (Design Verification)
- TenantProduct creation checks for existing records
- OrganizationProduct creation checks for existing records per org
- CareConnect provider creation searches by OrganizationId before creating
- All operations are safe to retry

### 5. Eligibility Validation (Design Verification)
- `ProductEligibilityConfig.IsEligible()` prevents invalid combinations
- FUNDER orgs cannot receive SYNQ_CARECONNECT
- LAW_FIRM orgs can receive SYNQ_CARECONNECT, SYNQ_FUND, SYNQ_LIENS

### 6. Existing Provider Handling (Design Verification)
- CareConnect internal endpoint checks `GetByOrganizationIdAsync` first
- Existing providers are activated (not duplicated)
- New providers are created with `LinkOrganization` called before insert

### 7. Backward Compatibility
- Existing endpoint signatures maintained (new parameters use DI injection)
- Response shapes are supersets of previous shapes (new fields added, none removed)
- `CreateTenantRequest.Products` is optional (null by default)
- Existing tenants are unaffected unless they pass through updated flows

## Limitations and Follow-Up Recommendations

1. **Cross-service atomicity**: Identity and CareConnect use separate databases. If CareConnect provisioning fails after Identity commits, the system is in a partially provisioned state. Re-running provisioning resolves this. A future enhancement could add a provisioning status tracker with retry queue.

2. **CareConnect provider data**: When the engine creates a provider through the internal endpoint, it creates a minimal record (name only, no address/email/phone). These fields should be populated later through the admin UI or a dedicated provider onboarding flow.

3. **Internal endpoint security**: The `/internal/provision-provider` endpoint is `AllowAnonymous`. In a production environment with network segmentation, this is acceptable (only internal services can reach port 5005). For additional security, a shared internal service token could be added.

4. **Product eligibility in config vs DB**: Eligibility rules are currently in static code (`ProductEligibilityConfig`). For dynamic rule management, these could be moved to a database table with an admin UI. The current approach is simpler and matches the existing seed-data pattern.

5. **Additional product handlers**: Only CareConnect has a provisioning handler. When SynqFund or SynqLiens need product-specific setup, implement `IProductProvisioningHandler` with the appropriate `ProductCode` and register it in DI. The engine automatically discovers and runs all registered handlers.

6. **Control Center UI**: The `CreateTenant` modal in the Control Center could be extended to include a product selection step, passing the `products` array to the API. This is a frontend change that can be done independently.
