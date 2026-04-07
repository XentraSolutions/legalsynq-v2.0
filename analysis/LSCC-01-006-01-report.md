# LSCC-01-006-01: Tenant Domain Verification and Activation Hardening

## Summary

Implements a multi-stage provisioning lifecycle with DNS and HTTP verification gates. Tenants must now pass through Pending → InProgress → Provisioned → Verifying → Active before login is allowed on their subdomain.

## Changes

### Domain Model (`Identity.Domain`)

| File | Change |
|------|--------|
| `Tenant.cs` | Added `Provisioned`, `Verifying` to `ProvisioningStatus` enum |
| `Tenant.cs` | Added `ProvisioningFailureStage` enum (`None`, `DnsProvisioning`, `DnsVerification`, `HttpVerification`) |
| `Tenant.cs` | Added `ProvisioningFailureStage` property to `Tenant` entity |
| `Tenant.cs` | Added `MarkProvisioningProvisioned()`, `MarkProvisioningVerifying()` lifecycle methods |
| `Tenant.cs` | Updated `MarkProvisioningFailed()` to accept optional `ProvisioningFailureStage` parameter |
| `TenantDomain.cs` | Added `VerifiedAtUtc` nullable datetime property |
| `TenantDomain.cs` | Added `MarkVerified()` method that sets `IsVerified = true` and timestamps |

### Application Layer (`Identity.Application`)

| File | Change |
|------|--------|
| `Interfaces/ITenantProvisioningService.cs` | Updated `ProvisioningResult` record to include `FailureStage` |
| `Interfaces/ITenantVerificationService.cs` | New interface with `VerifyAsync` returning `VerificationResult` |
| `Services/AuthService.cs` | Added provisioning status check — rejects login for non-Active tenants with `InvalidOperationException` |

### Infrastructure Layer (`Identity.Infrastructure`)

| File | Change |
|------|--------|
| `Services/TenantVerificationService.cs` | New service implementing DNS resolution + HTTP endpoint verification |
| `Services/TenantVerificationOptions.cs` | New configuration class (`Enabled`, `DevBypass`, timeouts, endpoint path) |
| `Services/TenantProvisioningService.cs` | Rewritten with full Provisioned → Verifying → Active lifecycle, integrated verification |
| `DependencyInjection.cs` | Registered `ITenantVerificationService` (scoped), bound `TenantVerification` config section |

### API Layer (`Identity.Api`)

| File | Change |
|------|--------|
| `Endpoints/AdminEndpoints.cs` | Added `provisioningFailureStage` to tenant detail response |
| `Endpoints/AdminEndpoints.cs` | Added `POST /api/admin/tenants/{id}/verification/retry` endpoint |
| `appsettings.json` | Added `TenantVerification` config section (DevBypass enabled by default) |

### Web App (`apps/web`)

| File | Change |
|------|--------|
| `src/app/.well-known/tenant-verify/route.ts` | New anonymous endpoint returning `tenant-verify-ok` for HTTP verification |
| `src/app/api/auth/login/route.ts` | BFF returns 503 with user-friendly message when tenant is not fully provisioned |

### Control Center (`apps/control-center`)

| File | Change |
|------|--------|
| `src/types/control-center.ts` | Added `Provisioned`, `Verifying` to `ProvisioningStatus` type; added `ProvisioningFailureStage` type; added `provisioningFailureStage` to `TenantDetail` |
| `src/components/tenants/tenant-detail-card.tsx` | Added badges for Provisioned/Verifying statuses; shows failure stage; renders RetryVerificationButton when applicable |
| `src/components/tenants/tenant-list-table.tsx` | Updated ProvisioningBadge with Provisioned/Verifying labels and styles |
| `src/components/tenants/retry-verification-button.tsx` | New client component for retrying verification separately from provisioning |
| `src/app/tenants/actions.ts` | Added `retryVerificationAction` server action |
| `src/lib/control-center-api.ts` | Added `retryVerification` API method |

## Provisioning Lifecycle

```
Pending → InProgress → Provisioned → Verifying → Active
                 ↓           ↓             ↓
              Failed      Failed        Failed
           (DnsProv)    (DnsVerify)   (HttpVerify)
```

- **Pending**: Tenant created, no DNS work started
- **InProgress**: DNS record creation in progress
- **Provisioned**: DNS record created successfully, verification not yet started
- **Verifying**: DNS/HTTP verification in progress
- **Active**: Fully provisioned and verified — login allowed
- **Failed**: One of the stages failed — `ProvisioningFailureStage` indicates which

## Verification Flow

1. **DNS Verification**: Resolves the hostname via `Dns.GetHostAddressesAsync` with configurable timeout
2. **HTTP Verification**: Makes HTTPS GET to `/.well-known/tenant-verify` and checks for `tenant-verify-ok` response
3. Both checks can be bypassed via `TenantVerification.DevBypass = true` (default in dev)

## Login Hardening

- `AuthService.LoginAsync` now rejects login attempts for tenants where `ProvisioningStatus != Active`
- BFF login route detects provisioning-related errors and returns a 503 with "This tenant is still being set up" message

## Database Migration Notes

Two new columns require an EF migration:
- `Tenants.ProvisioningFailureStage` (int, default 0 = None)
- `TenantDomains.VerifiedAtUtc` (nullable datetime)

## Validation

- TypeScript type-check: No new errors in any changed files (web or control-center)
- All pre-existing TS errors are unrelated (Next.js 15 async params pattern, other pages)
