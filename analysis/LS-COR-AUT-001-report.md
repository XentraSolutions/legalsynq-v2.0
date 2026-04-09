# LS-COR-AUT-001: Product Authorization Enforcement Layer

**Status:** Complete  
**Date:** 2026-04-09  
**Service:** CareConnect.Api (initial rollout), shared BuildingBlocks  

---

## Summary

Implemented a declarative endpoint filter system that enforces product-level access control on CareConnect API routes using JWT `product_roles` claims. The system provides a standardized 403 response structure and integrates with the existing authentication pipeline as a post-authentication, pre-handler filter chain.

## Architecture

### Filter Chain Execution Order

```
HTTP Request
  → Authentication Middleware (JWT validation)
  → Authorization Policy (RequireAuthorization: Policies.AuthenticatedUser / PlatformOrTenantAdmin)
  → RequireProductAccessFilter (coarse product check via product_roles claims)
  → RequireProductRoleFilter (specific role check, if applied)
  → RequireOrgProductAccessFilter (org-scoped check with orgId extraction)
  → Endpoint Handler
```

### Bypass Rules

| Role            | Product Filter | Role Filter | Org Filter |
|-----------------|---------------|-------------|------------|
| PlatformAdmin   | Bypass        | Bypass      | Bypass     |
| TenantAdmin     | Bypass        | Bypass      | Bypass     |
| Member (w/ role)| Enforced      | Enforced    | Enforced   |
| Member (no role)| 403           | 403         | 403        |

### New Shared Components (BuildingBlocks)

| File | Purpose |
|------|---------|
| `Authorization/ProductCodes.cs` | Canonical product code constants (`SYNQ_CARECONNECT`, `SYNQ_FUND`, `SYNQ_LIENS`) |
| `Authorization/ProductRoleClaimExtensions.cs` | `ClaimsPrincipal` extension methods: `HasProductAccess()`, `HasProductRole()`, `IsTenantAdminOrAbove()` |
| `Authorization/Filters/RequireProductAccessFilter.cs` | Coarse product access endpoint filter |
| `Authorization/Filters/RequireProductRoleFilter.cs` | Role-specific endpoint filter |
| `Authorization/Filters/RequireOrgProductAccessFilter.cs` | Org-scoped product access filter (sets `HttpContext.Items["ProductAuth:OrgId"]`) |
| `Authorization/Filters/ProductAuthorizationExtensions.cs` | Fluent builder extensions on `RouteHandlerBuilder` and `RouteGroupBuilder` |
| `Exceptions/ProductAccessDeniedException.cs` | Typed exception with factory methods for structured 403 responses |
| `Authorization/ProductAccessDeniedResult.cs` | Standardized JSON 403 response structure |

### Standardized 403 Response

```json
{
  "error": {
    "code": "PRODUCT_ACCESS_DENIED",
    "message": "User does not have access to this product.",
    "productCode": "SYNQ_CARECONNECT",
    "requiredRoles": null,
    "organizationId": null
  }
}
```

Error codes: `PRODUCT_ACCESS_DENIED`, `PRODUCT_ROLE_REQUIRED`, `ORG_PRODUCT_ACCESS_DENIED`.

## CareConnect Endpoint Coverage

### Protected with RequireProductAccess (SYNQ_CARECONNECT)

| Endpoint File | Routes Protected | Notes |
|---------------|-----------------|-------|
| ProviderEndpoints | Group-level + per-route role filters | POST/PUT also have RequireProductRole |
| ReferralEndpoints | All authenticated routes | Write ops also have RequireOrgProductAccess |
| AppointmentEndpoints | All authenticated routes | Write ops also have RequireOrgProductAccess |
| AppointmentNoteEndpoints | GET, POST, PUT | All 3 routes |
| AttachmentEndpoints | All 4 routes (referral + appointment) | GET + POST pairs |
| AvailabilityExceptionEndpoints | GET, POST, PUT, apply-exceptions | All 4 routes |
| AvailabilityTemplateEndpoints | GET, POST, PUT | Also have CareConnectAuthHelper capability checks |
| CategoryEndpoints | GET /api/categories | Read-only |
| FacilityEndpoints | GET, POST, PUT | Group routes |
| NotificationEndpoints | GET list, GET by ID | Read-only |
| ReferralNoteEndpoints | GET, POST, PUT | All 3 routes |
| ServiceOfferingEndpoints | GET, POST, PUT | Group routes |
| SlotEndpoints | POST generate, GET search | Also have CareConnectAuthHelper capability checks |

### Not Protected (By Design)

| Endpoint File | Reason |
|---------------|--------|
| InternalProvisionEndpoints | Internal service-to-service (AllowAnonymous + X-Internal-Service-Token) |
| CareConnectIntegrityEndpoints | Operational health (AllowAnonymous) |
| ReferralEndpoints (5 public routes) | `resolve-view-token`, `public-summary`, `track-funnel`, `auto-provision`, `accept-by-token` — token-gated public flows |

### Admin Endpoints (Implicitly Covered)

These use `Policies.PlatformOrTenantAdmin` which always bypasses the product filter. No separate product filter needed:

- ActivationAdminEndpoints
- AdminBackfillEndpoints
- AdminDashboardEndpoints
- AnalyticsEndpoints
- PerformanceEndpoints
- ProviderAdminEndpoints

## Middleware Integration

`ExceptionHandlingMiddleware` updated to catch `ProductAccessDeniedException` before `ForbiddenException`, returning the structured 403 JSON response with the correct content type.

## Test Results

| Scenario | Expected | Result |
|----------|----------|--------|
| No auth token | 401 | 401 |
| Invalid JWT | 401 | 401 |
| Valid JWT with CareConnect roles | 200 | 200 |
| Valid JWT without CareConnect roles | 403 structured | 403 structured |
| PlatformAdmin (no product roles) | 200 (bypass) | 200 (bypass) |
| Member with no product roles | 403 structured | 403 structured |
| Public endpoints (integrity, referral public) | 200 | 200 |
| Internal endpoints (provision) | Works with service token | Works |

## Build Verification

- `CareConnect.Api` — 0 errors, 0 warnings
- `Identity.Api` — 0 errors, 0 warnings (shares BuildingBlocks)
- Both services start and respond to health checks

## Security Notes

1. **TenantAdmin bypass is intentional** — TenantAdmins manage all products for their tenant. Product-level restrictions apply to Members.
2. **PlatformAdmin bypass is intentional** — PlatformAdmins have global access.
3. **Claim-based fast path** — No DB calls needed for product access checks. The `product_roles` claim is populated at login time from the user's effective access context.
4. **OrgId in HttpContext.Items** — `RequireOrgProductAccessFilter` extracts `organizationId` from the route and stores it in `HttpContext.Items["ProductAuth:OrgId"]` for downstream handlers.
5. **Deny-by-default for unknown product codes** — `HasProductAccess()` returns `false` for product codes not in the `ProductToRolesMap`. New products must be explicitly registered.
6. **Product-scoped role validation** — `HasProductRole()` validates that required roles belong to the product's valid role set before checking user claims, preventing cross-product privilege bleed.

## Files Changed

```
shared/building-blocks/BuildingBlocks/Authorization/ProductCodes.cs (new)
shared/building-blocks/BuildingBlocks/Authorization/ProductRoleClaimExtensions.cs (new)
shared/building-blocks/BuildingBlocks/Authorization/ProductAccessDeniedResult.cs (new)
shared/building-blocks/BuildingBlocks/Authorization/Filters/RequireProductAccessFilter.cs (new)
shared/building-blocks/BuildingBlocks/Authorization/Filters/RequireProductRoleFilter.cs (new)
shared/building-blocks/BuildingBlocks/Authorization/Filters/RequireOrgProductAccessFilter.cs (new)
shared/building-blocks/BuildingBlocks/Authorization/Filters/ProductAuthorizationExtensions.cs (new)
shared/building-blocks/BuildingBlocks/Exceptions/ProductAccessDeniedException.cs (new)
apps/services/careconnect/CareConnect.Api/Middleware/ExceptionHandlingMiddleware.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/ProviderEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/ReferralEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/AppointmentEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/AppointmentNoteEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/AttachmentEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/AvailabilityExceptionEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/AvailabilityTemplateEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/CategoryEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/FacilityEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/NotificationEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/ReferralNoteEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/ServiceOfferingEndpoints.cs (modified)
apps/services/careconnect/CareConnect.Api/Endpoints/SlotEndpoints.cs (modified)
```
