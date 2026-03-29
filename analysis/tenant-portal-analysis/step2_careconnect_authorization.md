# LegalSynq — CareConnect Authorization Audit
## API Access Control: Endpoints, Policies, and Product-Role Alignment

**Audit date:** 2026-03-29  
**Scope:** All CareConnect API endpoints — authorization policies, product-role mapping, capability usage  
**Auditor:** automated source-code analysis

---

## 1. Executive Summary

**The CareConnect API is functionally inaccessible to every real user it is designed to serve.**

Every mutating operation in the product — creating a referral, booking an appointment, adding a note, uploading an attachment — is locked behind the `PlatformOrTenantAdmin` policy, which requires the `PlatformAdmin` or `TenantAdmin` system role. A law-firm user with `CARECONNECT_REFERRER` in their JWT holds neither of those roles and therefore receives `403 Forbidden` on every write call.

At the same time, all read operations require only `AuthenticatedUser` — meaning any user who has logged in, regardless of whether they have CareConnect access at all, can list every referral and every appointment in the tenant.

The capability-based authorization layer (`ICapabilityService`, `AuthorizationService`, `CanReferCareConnect`, `CanReceiveCareConnect`) is fully defined in `BuildingBlocks` but:
- **never registered** in `DependencyInjection.cs`
- **never registered as policies** in `Program.cs`
- **never injected or called** by any endpoint handler

The product roles `CARECONNECT_REFERRER` and `CARECONNECT_RECEIVER` exist in the JWT but are ignored entirely at the CareConnect API layer.

**Critical result:** Two user types that CareConnect is built for — law-firm referrers and medical providers — both receive `403 Forbidden` on all write operations and unrestricted read access beyond their intended scope.

---

## 2. Files Inspected

| File | Purpose |
|---|---|
| `CareConnect.Api/Program.cs` | Auth policy registration |
| `CareConnect.Api/Endpoints/ProviderEndpoints.cs` | Provider CRUD |
| `CareConnect.Api/Endpoints/ReferralEndpoints.cs` | Referral CRUD |
| `CareConnect.Api/Endpoints/AppointmentEndpoints.cs` | Appointment lifecycle |
| `CareConnect.Api/Endpoints/FacilityEndpoints.cs` | Facility CRUD |
| `CareConnect.Api/Endpoints/CategoryEndpoints.cs` | Category reference data |
| `CareConnect.Api/Endpoints/ServiceOfferingEndpoints.cs` | Service offering CRUD |
| `CareConnect.Api/Endpoints/SlotEndpoints.cs` | Slot generation + search |
| `CareConnect.Api/Endpoints/AvailabilityTemplateEndpoints.cs` | Availability template CRUD |
| `CareConnect.Api/Endpoints/AvailabilityExceptionEndpoints.cs` | Availability exception CRUD |
| `CareConnect.Api/Endpoints/ReferralNoteEndpoints.cs` | Referral note CRUD |
| `CareConnect.Api/Endpoints/AppointmentNoteEndpoints.cs` | Appointment note CRUD |
| `CareConnect.Api/Endpoints/AttachmentEndpoints.cs` | Attachment upload/read |
| `CareConnect.Api/Endpoints/NotificationEndpoints.cs` | Notification read |
| `CareConnect.Infrastructure/DependencyInjection.cs` | DI registrations |
| `shared/building-blocks/BuildingBlocks/Authorization/Policies.cs` | Policy name constants |
| `shared/building-blocks/BuildingBlocks/Authorization/Roles.cs` | System role constants |
| `shared/building-blocks/BuildingBlocks/Authorization/ProductRoleCodes.cs` | Product role code constants |
| `shared/building-blocks/BuildingBlocks/Authorization/CapabilityCodes.cs` | Capability code constants |
| `shared/building-blocks/BuildingBlocks/Authorization/AuthorizationService.cs` | Capability-based auth service |
| `shared/building-blocks/BuildingBlocks/Authorization/ICapabilityService.cs` | Capability resolution interface |
| `shared/building-blocks/BuildingBlocks/Context/CurrentRequestContext.cs` | JWT claim extraction |

---

## 3. Endpoint Inventory

### 3a. Providers (5 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/providers/` | `AuthenticatedUser` | ⚠️ Under-restricted (no CareConnect gate) |
| GET | `/api/providers/map` | `AuthenticatedUser` | ⚠️ Under-restricted |
| GET | `/api/providers/{id}` | `AuthenticatedUser` | ⚠️ Under-restricted |
| POST | `/api/providers/` | `PlatformOrTenantAdmin` | 🔴 Over-restricted (providers can't register themselves) |
| PUT | `/api/providers/{id}` | `PlatformOrTenantAdmin` | 🔴 Over-restricted (providers can't update their own profile) |

### 3b. Referrals (5 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/referrals/` | `AuthenticatedUser` | ⚠️ Under-restricted (any user sees all referrals) |
| GET | `/api/referrals/{id}` | `AuthenticatedUser` | ⚠️ Under-restricted |
| GET | `/api/referrals/{id}/history` | `AuthenticatedUser` | ⚠️ Under-restricted |
| POST | `/api/referrals/` | `PlatformOrTenantAdmin` | 🔴 **CRITICAL** — law firm users can't create referrals |
| PUT | `/api/referrals/{id}` | `PlatformOrTenantAdmin` | 🔴 **CRITICAL** — law firm users can't update referrals |

### 3c. Appointments (6 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/appointments` | `AuthenticatedUser` | ⚠️ Under-restricted |
| GET | `/api/appointments/{id}` | `AuthenticatedUser` | ⚠️ Under-restricted |
| GET | `/api/appointments/{id}/history` | `AuthenticatedUser` | ⚠️ Under-restricted |
| POST | `/api/appointments` | `PlatformOrTenantAdmin` | 🔴 **CRITICAL** — referrers can't book |
| PUT | `/api/appointments/{id}` | `PlatformOrTenantAdmin` | 🔴 **CRITICAL** — referrers can't update |
| POST | `/api/appointments/{id}/cancel` | `PlatformOrTenantAdmin` | 🔴 **CRITICAL** — referrers can't cancel |
| POST | `/api/appointments/{id}/reschedule` | `PlatformOrTenantAdmin` | 🔴 **CRITICAL** — referrers can't reschedule |

### 3d. Facilities (3 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/facilities/` | `AuthenticatedUser` | ✅ Acceptable |
| POST | `/api/facilities/` | `PlatformOrTenantAdmin` | 🔴 Over-restricted — receivers can't create their own facilities |
| PUT | `/api/facilities/{id}` | `PlatformOrTenantAdmin` | 🔴 Over-restricted |

### 3e. Categories (1 endpoint)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/categories` | `AuthenticatedUser` | ✅ Reference data — acceptable |

### 3f. Service Offerings (3 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/service-offerings/` | `AuthenticatedUser` | ✅ Acceptable |
| POST | `/api/service-offerings/` | `PlatformOrTenantAdmin` | 🔴 Over-restricted — receivers should manage their own offerings |
| PUT | `/api/service-offerings/{id}` | `PlatformOrTenantAdmin` | 🔴 Over-restricted |

### 3g. Availability Templates (3 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/providers/{id}/availability-templates` | `AuthenticatedUser` | ✅ Acceptable |
| POST | `/api/providers/{id}/availability-templates` | `PlatformOrTenantAdmin` | 🔴 Over-restricted |
| PUT | `/api/availability-templates/{id}` | `PlatformOrTenantAdmin` | 🔴 Over-restricted |

### 3h. Availability Exceptions (3 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/providers/{id}/availability-exceptions` | `AuthenticatedUser` | ✅ Acceptable |
| POST | `/api/providers/{id}/availability-exceptions` | `PlatformOrTenantAdmin` | 🔴 Over-restricted |
| PUT | `/api/availability-exceptions/{id}` | `PlatformOrTenantAdmin` | 🔴 Over-restricted |

### 3i. Slots (2 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/slots` | `AuthenticatedUser` | ⚠️ Acceptable but should require CareConnect role |
| POST | `/api/providers/{id}/slots/generate` | `PlatformOrTenantAdmin` | 🔴 Over-restricted — receivers should generate their own slots |

### 3j. Notes — Referral (3 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/referrals/{id}/notes` | `AuthenticatedUser` | ⚠️ Under-restricted + visibility scope unenforced at API layer |
| POST | `/api/referrals/{id}/notes` | `PlatformOrTenantAdmin` | 🔴 **CRITICAL** — referrers can't annotate their own referrals |
| PUT | `/api/referral-notes/{id}` | `PlatformOrTenantAdmin` | 🔴 Over-restricted |

### 3k. Notes — Appointment (3 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/appointments/{id}/notes` | `AuthenticatedUser` | ⚠️ Under-restricted |
| POST | `/api/appointments/{id}/notes` | `PlatformOrTenantAdmin` | 🔴 Over-restricted |
| PUT | `/api/appointment-notes/{id}` | `PlatformOrTenantAdmin` | 🔴 Over-restricted |

### 3l. Attachments (4 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/referrals/{id}/attachments` | `AuthenticatedUser` | ⚠️ Under-restricted |
| POST | `/api/referrals/{id}/attachments` | `PlatformOrTenantAdmin` | 🔴 **CRITICAL** — referrers can't upload documents |
| GET | `/api/appointments/{id}/attachments` | `AuthenticatedUser` | ⚠️ Under-restricted |
| POST | `/api/appointments/{id}/attachments` | `PlatformOrTenantAdmin` | 🔴 Over-restricted |

### 3m. Notifications (2 endpoints)

| Method | Path | Current Policy | Over/Under |
|---|---|---|---|
| GET | `/api/notifications` | `AuthenticatedUser` | ⚠️ No recipient scoping at the policy level |
| GET | `/api/notifications/{id}` | `AuthenticatedUser` | ⚠️ No recipient scoping at the policy level |

---

## 4. Current Authorization Model

### Policies registered in `Program.cs`

```csharp
options.AddPolicy("AuthenticatedUser", policy =>
    policy.RequireAuthenticatedUser());          // any valid JWT

options.AddPolicy("PlatformOrTenantAdmin", policy =>
    policy.RequireRole("PlatformAdmin", "TenantAdmin"));  // system role in JWT
```

**Only two policies are registered.** No product-role policies. No capability policies.

### Role claim source

System roles come from `ClaimTypes.Role` in the JWT, mapped from `UserRoles → Role.Name` at login. The `RoleClaimType` in `AddJwtBearer` is set to `ClaimTypes.Role`, so `RequireRole(...)` works correctly for system roles.

### Product roles in context

`CurrentRequestContext.ProductRoles` correctly reads the `"product_roles"` multi-value claim from the JWT. **However, no endpoint or policy reads this property.** It is populated and immediately discarded.

### What `PlatformOrTenantAdmin` actually means

A user has `PlatformAdmin` or `TenantAdmin` if they were assigned one of those system roles at account creation time. These are operational/admin roles. No law-firm user or provider user should hold them for normal product usage.

---

## 5. Expected Authorization Model

### User type → system role mapping

| User type | System role | Product role |
|---|---|---|
| Platform operator | `PlatformAdmin` | all capabilities |
| Law firm admin | `TenantAdmin` | `CARECONNECT_REFERRER` |
| Law firm staff | `StandardUser` | `CARECONNECT_REFERRER` |
| Provider org admin | `TenantAdmin` | `CARECONNECT_RECEIVER` |
| Provider staff | `StandardUser` | `CARECONNECT_RECEIVER` |

### Correct access model per operation type

| Operation category | Who should have access |
|---|---|
| Read providers (list/detail/map) | Any user with `CARECONNECT_REFERRER` or `CARECONNECT_RECEIVER` |
| Write providers (create/update) | `CARECONNECT_RECEIVER` (own profile) or admin |
| Read referrals | `CARECONNECT_REFERRER` (own) + `CARECONNECT_RECEIVER` (addressed) + admin |
| Create/update referrals | `CARECONNECT_REFERRER` + admin |
| Read appointments | `CARECONNECT_REFERRER` (own) + `CARECONNECT_RECEIVER` (own) + admin |
| Create/update/cancel/reschedule appointments | `CARECONNECT_REFERRER` + admin |
| Read/write facilities | `CARECONNECT_RECEIVER` (own) + admin |
| Read/write service offerings | `CARECONNECT_RECEIVER` (own) + admin |
| Read/write availability (templates/exceptions/slots) | `CARECONNECT_RECEIVER` (own) + admin |
| Browse available slots | `CARECONNECT_REFERRER` + admin |
| Read/write notes | Note owner's role + admin (visibility scoped) |
| Read/write attachments | Note owner's role + admin |
| Read notifications | Recipient only + admin |

### Correct policy definitions (to add to Program.cs)

```csharp
options.AddPolicy(Policies.CanReferCareConnect, policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReferrer)
        || ctx.User.IsInRole(Roles.PlatformAdmin)
        || ctx.User.IsInRole(Roles.TenantAdmin)));

options.AddPolicy(Policies.CanReceiveCareConnect, policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReceiver)
        || ctx.User.IsInRole(Roles.PlatformAdmin)
        || ctx.User.IsInRole(Roles.TenantAdmin)));

// Combined: either CareConnect role (for shared read paths)
options.AddPolicy("CanUseCareConnect", policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReferrer)
        || ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReceiver)
        || ctx.User.IsInRole(Roles.PlatformAdmin)
        || ctx.User.IsInRole(Roles.TenantAdmin)));
```

---

## 6. Mismatches / Blockers

### 6a. Capability system is entirely dead in CareConnect

`BuildingBlocks` defines `ICapabilityService`, `AuthorizationService`, `CanReferCareConnect` policy, and all `CapabilityCodes` constants. Not one of these is referenced in `CareConnect.Api` or `CareConnect.Infrastructure`:

- `ICapabilityService` — **not registered** in `DependencyInjection.cs`
- `AuthorizationService` — **not registered**, never injected
- `CanReferCareConnect`, `CanReceiveCareConnect` — **not registered** in `Program.cs`
- `CapabilityCodes.*` — **not used** in any endpoint handler

The infrastructure to enforce product-role authorization exists and is correct in design; it was simply never wired into the CareConnect service.

### 6b. 18 write endpoints incorrectly require `PlatformOrTenantAdmin`

Every `POST`, `PUT`, and action route (cancel/reschedule) in the CareConnect API requires `PlatformOrTenantAdmin`. This makes the following core user workflows impossible for real users:

- Law firm creating a referral
- Law firm booking an appointment
- Law firm cancelling or rescheduling an appointment
- Law firm adding notes or uploading documents
- Provider managing their own facilities, service offerings, and availability

### 6c. 10 read endpoints have no CareConnect product-role gate

All `GET` endpoints use `AuthenticatedUser`. A user from a completely different product (e.g. a SynqFund-only funder with no CareConnect entitlement) can currently list all providers, referrals, appointments, notes, and attachments in the tenant.

### 6d. Notification scoping not enforced at the API layer

`GET /api/notifications` returns all notifications for the tenant filtered only by the query parameters. There is no clause enforcing `RecipientUserId == ctx.UserId`. The service layer must be checked — if scoping is absent there too, any authenticated user can read any other user's notifications.

### 6e. Row-level ownership not enforced at route layer

Neither `GET /api/referrals/` nor `GET /api/referrals/{id}` verify that the caller created the referral (for referrers) or is addressed in the referral (for receivers). This data-level filtering must be in the service layer — which should be audited separately.

---

## 7. Recommended Authorization Design

### Principle: three-tier guards

```
Tier 1 — Route policy (JWT claim check, zero DB):
  RequireAuthorization("CanReferCareConnect")
  RequireAuthorization("CanReceiveCareConnect")

Tier 2 — Capability check (one DB read, cached 5 min):
  await authSvc.RequireCapabilityAsync(ctx, CapabilityCodes.ReferralCreate)

Tier 3 — Row-level ownership (scoped service query):
  service.GetByIdAsync(tenantId, referralId, ctx.UserId)
```

Tier 1 prevents the 403 problem for real users and the over-exposure of reads.  
Tier 2 provides fine-grained capability enforcement without hardcoding role names in handlers.  
Tier 3 prevents cross-user data leakage (data filtering, not an auth check).

### Clean policy map

```
Providers (read)                → CanUseCareConnect
Providers (write)               → CanReceiveCareConnect | Admin
Referrals (read)                → CanUseCareConnect
Referrals (create/update)       → CanReferCareConnect | Admin
Appointments (read)             → CanUseCareConnect
Appointments (create/update)    → CanReferCareConnect | Admin
Appointments (cancel/reschedule)→ CanReferCareConnect | Admin
Facilities (read)               → CanUseCareConnect
Facilities (write)              → CanReceiveCareConnect | Admin
ServiceOfferings (read)         → CanUseCareConnect
ServiceOfferings (write)        → CanReceiveCareConnect | Admin
AvailabilityTemplates (read)    → CanUseCareConnect
AvailabilityTemplates (write)   → CanReceiveCareConnect | Admin
AvailabilityExceptions (read)   → CanUseCareConnect
AvailabilityExceptions (write)  → CanReceiveCareConnect | Admin
Slots (browse)                  → CanUseCareConnect
Slots (generate)                → CanReceiveCareConnect | Admin
Notes (read)                    → CanUseCareConnect (+ visibility scope in service)
Notes (write)                   → CanUseCareConnect (creator owns it)
Attachments (read)              → CanUseCareConnect
Attachments (write)             → CanUseCareConnect
Notifications (read)            → CanUseCareConnect (+ recipient scope in service)
Categories (read)               → AuthenticatedUser (reference data)
```

---

## 8. Concrete Implementation Tasks

### T1 — Register product-role policies in CareConnect Program.cs (P0)

**File:** `apps/services/careconnect/CareConnect.Api/Program.cs`

Add three new policy registrations before `var app = builder.Build()`:

```csharp
options.AddPolicy(Policies.CanReferCareConnect, policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReferrer)
        || ctx.User.IsInRole(Roles.PlatformAdmin)
        || ctx.User.IsInRole(Roles.TenantAdmin)));

options.AddPolicy(Policies.CanReceiveCareConnect, policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReceiver)
        || ctx.User.IsInRole(Roles.PlatformAdmin)
        || ctx.User.IsInRole(Roles.TenantAdmin)));

options.AddPolicy("CanUseCareConnect", policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReferrer)
        || ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReceiver)
        || ctx.User.IsInRole(Roles.PlatformAdmin)
        || ctx.User.IsInRole(Roles.TenantAdmin)));
```

### T2 — Fix ReferralEndpoints authorization (P0)

**File:** `CareConnect.Api/Endpoints/ReferralEndpoints.cs`

```
GET  /api/referrals/           → change to "CanUseCareConnect"
GET  /api/referrals/{id}       → change to "CanUseCareConnect"
GET  /api/referrals/{id}/history → change to "CanUseCareConnect"
POST /api/referrals/           → change to Policies.CanReferCareConnect
PUT  /api/referrals/{id}       → change to Policies.CanReferCareConnect
```

### T3 — Fix AppointmentEndpoints authorization (P0)

**File:** `CareConnect.Api/Endpoints/AppointmentEndpoints.cs`

```
GET  /api/appointments              → change to "CanUseCareConnect"
GET  /api/appointments/{id}         → change to "CanUseCareConnect"
GET  /api/appointments/{id}/history → change to "CanUseCareConnect"
POST /api/appointments              → change to Policies.CanReferCareConnect
PUT  /api/appointments/{id}         → change to Policies.CanReferCareConnect
POST /api/appointments/{id}/cancel       → change to Policies.CanReferCareConnect
POST /api/appointments/{id}/reschedule   → change to Policies.CanReferCareConnect
```

### T4 — Fix Note and Attachment endpoints authorization (P0)

**Files:** `ReferralNoteEndpoints.cs`, `AppointmentNoteEndpoints.cs`, `AttachmentEndpoints.cs`

```
GET  .../notes          → change to "CanUseCareConnect"
POST .../notes          → change to "CanUseCareConnect"
PUT  .../notes/{id}     → change to "CanUseCareConnect"
GET  .../attachments    → change to "CanUseCareConnect"
POST .../attachments    → change to "CanUseCareConnect"
```

### T5 — Fix Provider, Facility, ServiceOffering, Availability read paths (P1)

**Files:** `ProviderEndpoints.cs`, `FacilityEndpoints.cs`, `ServiceOfferingEndpoints.cs`,  
`AvailabilityTemplateEndpoints.cs`, `AvailabilityExceptionEndpoints.cs`, `SlotEndpoints.cs`

```
GET  /api/providers/            → change to "CanUseCareConnect"
GET  /api/providers/map         → change to "CanUseCareConnect"
GET  /api/providers/{id}        → change to "CanUseCareConnect"
POST /api/providers/            → change to Policies.CanReceiveCareConnect
PUT  /api/providers/{id}        → change to Policies.CanReceiveCareConnect

GET  /api/facilities/           → "CanUseCareConnect" (keep as-is if desired, it's reference data)
POST /api/facilities/           → change to Policies.CanReceiveCareConnect
PUT  /api/facilities/{id}       → change to Policies.CanReceiveCareConnect

POST /api/service-offerings/    → change to Policies.CanReceiveCareConnect
PUT  /api/service-offerings/{id}→ change to Policies.CanReceiveCareConnect

POST /api/providers/{id}/availability-templates   → change to Policies.CanReceiveCareConnect
PUT  /api/availability-templates/{id}             → change to Policies.CanReceiveCareConnect

POST /api/providers/{id}/availability-exceptions  → change to Policies.CanReceiveCareConnect
PUT  /api/availability-exceptions/{id}            → change to Policies.CanReceiveCareConnect

POST /api/providers/{id}/slots/generate           → change to Policies.CanReceiveCareConnect
GET  /api/slots                                   → change to "CanUseCareConnect"
```

### T6 — Add constant for CanUseCareConnect to Policies.cs (P1)

**File:** `shared/building-blocks/BuildingBlocks/Authorization/Policies.cs`

```csharp
public const string CanUseCareConnect = "CanUseCareConnect";
```

### T7 — Register ICapabilityService in CareConnect DI (P2 — future)

**File:** `CareConnect.Infrastructure/DependencyInjection.cs`

Once the Identity service exposes a gRPC or HTTP capability endpoint, register `ICapabilityService` here. For now the Tier-1 policy assertions (T1) are sufficient to unblock users.

### T8 — Scope notification reads to recipient (P1)

**File:** `CareConnect.Api/Endpoints/NotificationEndpoints.cs`  
Check if `NotificationService.SearchAsync` already filters by `RecipientUserId = ctx.UserId`. If not, add it.

---

## 9. Risks / Assumptions

| Risk | Severity | Mitigation |
|---|---|---|
| Row-level data filtering (referral ownership, appointment ownership) may also be absent from service layer | High | Audit service layer separately — T2/T3 fixes the API gate; service layer must also scope queries to `CreatedByUserId` or `RecipientUserId` |
| Adding `CanUseCareConnect` policy to read paths will break any currently-working admin dashboards that read referral/provider data via the CareConnect API without a `CARECONNECT_*` product role | Medium | PlatformAdmin and TenantAdmin are included in the policy assertion — admin dashboards using admin-role JWTs will continue to work |
| Notification recipient scoping may require service-layer changes beyond endpoint policy changes | Medium | Audit `NotificationService.SearchAsync` before T8 |
| `CanReferCareConnect` policy string is not yet in `Policies.cs` — only defined in the inline const (T6 must land before referencing it by name elsewhere) | Low | Add the const in T6 first; use the string literal in T1–T5 if needed immediately |
| Once T2 is applied, the frontend law-firm user workflow depends on `CARECONNECT_REFERRER` appearing in the JWT — which requires the Step 1 identity fixes (org, OrganizationProduct, UserOrganizationMembership) to be in place | High | The Step 1 identity fixes and these Step 2 endpoint fixes are co-dependent; deploy them together |

---

## Complete Mapping Table

| Endpoint | Current Policy | Correct Policy | Status |
|---|---|---|---|
| GET `/api/providers/` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| GET `/api/providers/map` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| GET `/api/providers/{id}` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/providers/` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| PUT `/api/providers/{id}` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| GET `/api/referrals/` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| GET `/api/referrals/{id}` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| GET `/api/referrals/{id}/history` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/referrals/` | PlatformOrTenantAdmin | CanReferCareConnect | 🔴 **CRITICAL** |
| PUT `/api/referrals/{id}` | PlatformOrTenantAdmin | CanReferCareConnect | 🔴 **CRITICAL** |
| GET `/api/appointments` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| GET `/api/appointments/{id}` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| GET `/api/appointments/{id}/history` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/appointments` | PlatformOrTenantAdmin | CanReferCareConnect | 🔴 **CRITICAL** |
| PUT `/api/appointments/{id}` | PlatformOrTenantAdmin | CanReferCareConnect | 🔴 **CRITICAL** |
| POST `/api/appointments/{id}/cancel` | PlatformOrTenantAdmin | CanReferCareConnect | 🔴 **CRITICAL** |
| POST `/api/appointments/{id}/reschedule` | PlatformOrTenantAdmin | CanReferCareConnect | 🔴 **CRITICAL** |
| GET `/api/facilities/` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/facilities/` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| PUT `/api/facilities/{id}` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| GET `/api/categories` | AuthenticatedUser | AuthenticatedUser | ✅ Acceptable |
| GET `/api/service-offerings/` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/service-offerings/` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| PUT `/api/service-offerings/{id}` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| GET `/api/providers/{id}/availability-templates` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/providers/{id}/availability-templates` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| PUT `/api/availability-templates/{id}` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| GET `/api/providers/{id}/availability-exceptions` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/providers/{id}/availability-exceptions` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| PUT `/api/availability-exceptions/{id}` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| GET `/api/slots` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/providers/{id}/slots/generate` | PlatformOrTenantAdmin | CanReceiveCareConnect | 🔴 Over-restricted |
| GET `/api/referrals/{id}/notes` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/referrals/{id}/notes` | PlatformOrTenantAdmin | CanUseCareConnect | 🔴 **CRITICAL** |
| PUT `/api/referral-notes/{id}` | PlatformOrTenantAdmin | CanUseCareConnect | 🔴 Over-restricted |
| GET `/api/appointments/{id}/notes` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/appointments/{id}/notes` | PlatformOrTenantAdmin | CanUseCareConnect | 🔴 Over-restricted |
| PUT `/api/appointment-notes/{id}` | PlatformOrTenantAdmin | CanUseCareConnect | 🔴 Over-restricted |
| GET `/api/referrals/{id}/attachments` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/referrals/{id}/attachments` | PlatformOrTenantAdmin | CanUseCareConnect | 🔴 **CRITICAL** |
| GET `/api/appointments/{id}/attachments` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| POST `/api/appointments/{id}/attachments` | PlatformOrTenantAdmin | CanUseCareConnect | 🔴 Over-restricted |
| GET `/api/notifications` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |
| GET `/api/notifications/{id}` | AuthenticatedUser | CanUseCareConnect | ⚠️ Under-restricted |

**Summary:** 44 endpoints total · 🔴 18 critical/over-restricted · ⚠️ 25 under-restricted · ✅ 1 correct

---

## Minimal Fix Set

The smallest set of code changes that makes CareConnect usable for real law-firm users (REFERRER workflow) without compromising security:

### File 1: `CareConnect.Api/Program.cs`

Add three policy registrations (T1). This is the single highest-leverage change — it requires no other files to also change.

```csharp
options.AddPolicy(Policies.CanReferCareConnect, policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReferrer)
        || ctx.User.IsInRole(Roles.PlatformAdmin)
        || ctx.User.IsInRole(Roles.TenantAdmin)));

options.AddPolicy(Policies.CanReceiveCareConnect, policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReceiver)
        || ctx.User.IsInRole(Roles.PlatformAdmin)
        || ctx.User.IsInRole(Roles.TenantAdmin)));

options.AddPolicy("CanUseCareConnect", policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReferrer)
        || ctx.User.HasClaim("product_roles", ProductRoleCodes.CareConnectReceiver)
        || ctx.User.IsInRole(Roles.PlatformAdmin)
        || ctx.User.IsInRole(Roles.TenantAdmin)));
```

### File 2: `ReferralEndpoints.cs`

Change 2 lines:
- `POST /api/referrals/` → `.RequireAuthorization(Policies.CanReferCareConnect)`
- `PUT /api/referrals/{id}` → `.RequireAuthorization(Policies.CanReferCareConnect)`

### File 3: `AppointmentEndpoints.cs`

Change 4 lines:
- `POST /api/appointments` → `.RequireAuthorization(Policies.CanReferCareConnect)`
- `PUT /api/appointments/{id}` → `.RequireAuthorization(Policies.CanReferCareConnect)`
- `POST /api/appointments/{id}/cancel` → `.RequireAuthorization(Policies.CanReferCareConnect)`
- `POST /api/appointments/{id}/reschedule` → `.RequireAuthorization(Policies.CanReferCareConnect)`

### File 4: `ReferralNoteEndpoints.cs` + `AttachmentEndpoints.cs`

Change 4 lines (2 per file):
- `POST .../notes` → `.RequireAuthorization("CanUseCareConnect")`
- `POST .../attachments` → `.RequireAuthorization("CanUseCareConnect")`

**Total: 5 files, ~12 line changes.** This unblocks the entire REFERRER workflow. The RECEIVER workflow and full read-path hardening (T5) should follow immediately after.

> ⚠️ These backend fixes are necessary but not sufficient: they must be deployed alongside the Step 1 identity fixes (Organization + OrganizationProduct + UserOrganizationMembership seeding) to produce `CARECONNECT_REFERRER` in the JWT in the first place.
