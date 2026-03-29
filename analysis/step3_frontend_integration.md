# Step 3 — CareConnect Frontend Integration Analysis

**Date:** 2026-03-29  
**Scope:** `apps/web` (tenant portal) — CareConnect section  
**Auditor:** Agent review

---

## 1. Executive Summary

The CareConnect frontend integration is **approximately 60% functional**. The session/auth chain, sidebar navigation, provider search, and referral viewing all work correctly. However three critical blockers prevent the core booking workflow from ever completing, and a fourth bloc means standard users (non-admin) cannot create referrals at all.

| Area | Status | Notes |
|------|--------|-------|
| Login / session | ✅ Fixed today | Middleware bug fixed; cookie set correctly |
| Sidebar navigation | ✅ Working | Role-gated correctly |
| Provider search list | ✅ Working | 200 confirmed live |
| Provider detail | ✅ Working | Client-side fetch works |
| Referral list | ✅ Working | Server-side fetch works |
| Referral detail | ✅ Working | Server-side fetch works |
| Referral create (TenantAdmin) | ✅ Working | Margaret can create |
| Referral create (StandardUser) | ❌ **BLOCKER** | 403 — wrong policy |
| Availability page | ❌ **BLOCKER** | Endpoint doesn't exist (404) |
| Create appointment | ❌ **BLOCKER** | Payload completely mismatched |
| Appointment list display | ⚠️ Partial | Wrong field names; timestamps won't show |
| Appointment detail display | ⚠️ Partial | Missing client fields, status history |

---

## 2. Files Inspected

### Frontend (apps/web)
| File | Purpose |
|------|---------|
| `src/lib/session.ts` | Server-side session via `/identity/api/auth/me` |
| `src/lib/auth-guards.ts` | `requireOrg()`, `requireProductRole()` guards |
| `src/lib/careconnect-api.ts` | Split server/client API clients |
| `src/lib/api-client.ts` | Browser fetch wrapper → `/api/*` BFF |
| `src/lib/server-api-client.ts` | Server fetch wrapper → gateway directly |
| `src/lib/nav.ts` | `buildNavGroups()` — role-based nav |
| `src/middleware.ts` | Route protection (middleware fixed today) |
| `src/providers/session-provider.tsx` | Client-side session context |
| `src/hooks/use-session.ts` | `useSession()` hook |
| `src/app/api/careconnect/[...path]/route.ts` | BFF catch-all proxy for CareConnect |
| `src/app/api/auth/me/route.ts` | BFF session refresh proxy |
| `src/app/(platform)/layout.tsx` | Platform layout: `requireOrg()` guard |
| `src/app/(platform)/careconnect/providers/page.tsx` | Server Component: provider search |
| `src/app/(platform)/careconnect/providers/[id]/page.tsx` | Client Component: provider detail + referral form |
| `src/app/(platform)/careconnect/providers/[id]/availability/page.tsx` | Client Component: availability + booking |
| `src/app/(platform)/careconnect/referrals/page.tsx` | Server Component: referral list |
| `src/app/(platform)/careconnect/referrals/[id]/page.tsx` | Server Component: referral detail |
| `src/app/(platform)/careconnect/appointments/page.tsx` | Server Component: appointment list |
| `src/app/(platform)/careconnect/appointments/[id]/page.tsx` | Server Component: appointment detail |
| `src/components/careconnect/*.tsx` | 10 CareConnect UI components |
| `src/types/careconnect.ts` | TypeScript DTO types |
| `src/types/index.ts` | `ProductRole`, `PlatformSession`, etc. |

### Backend (apps/services/careconnect, apps/gateway)
| File | Purpose |
|------|---------|
| `CareConnect.Api/Program.cs` | Authorization policies registered |
| `CareConnect.Api/Endpoints/ProviderEndpoints.cs` | Provider CRUD + map |
| `CareConnect.Api/Endpoints/ReferralEndpoints.cs` | Referral CRUD |
| `CareConnect.Api/Endpoints/AppointmentEndpoints.cs` | Appointment CRUD |
| `CareConnect.Api/Endpoints/SlotEndpoints.cs` | Slot generation + search |
| `CareConnect.Api/Endpoints/AvailabilityTemplateEndpoints.cs` | Availability templates (admin) |
| `CareConnect.Application/DTOs/AppointmentDTOs.cs` | Backend appointment request/response DTOs |
| `CareConnect.Application/DTOs/ProviderResponse.cs` | Backend provider response DTO |
| `CareConnect.Application/DTOs/ReferralResponse.cs` | Backend referral response DTO |
| `CareConnect.Application/DTOs/CreateReferralRequest.cs` | Backend referral creation DTO |
| `CareConnect.Application/DTOs/SlotDTOs.cs` | Backend slot response DTO |
| `apps/gateway/Gateway.Api/appsettings.json` | YARP route config |

---

## 3. Route Map — CareConnect Pages

```
(platform) layout — requireOrg() gate
│
├── /careconnect/providers                   [Server Component]
│   guard: productRoles.includes(CARECONNECT_REFERRER) → redirect /dashboard
│   data:  careConnectServerApi.providers.search(...)
│          → serverApi.get(`/careconnect/api/providers?...`)
│          → Gateway /careconnect/api/providers → CareConnect :5003 /api/providers
│
├── /careconnect/providers/[id]              [Client Component]
│   guard: useSession() → if !session redirect /login
│   data:  careConnectApi.providers.getById(id)
│          → fetch /api/careconnect/api/providers/{id}  (BFF proxy)
│          → Gateway /careconnect/api/providers/{id}
│   mutation: CreateReferralForm → careConnectApi.referrals.create(...)
│
├── /careconnect/providers/[id]/availability [Client Component]
│   guard: !isReferrer → redirect /dashboard
│   data:  careConnectApi.providers.getAvailability(id, {from, to})
│          → fetch /api/careconnect/api/providers/{id}/availability?from=...&to=...
│          → Gateway /careconnect/api/providers/{id}/availability  ← DOES NOT EXIST
│
├── /careconnect/referrals                   [Server Component]
│   guard: !isReferrer && !isReceiver → inline denial message
│   data:  careConnectServerApi.referrals.search(...)
│
├── /careconnect/referrals/[id]              [Server Component]
│   guard: !isReferrer && !isReceiver → inline denial message
│   data:  careConnectServerApi.referrals.getById(id)
│
├── /careconnect/appointments                [Server Component]
│   guard: !isReferrer && !isReceiver → inline denial message
│   data:  careConnectServerApi.appointments.search(...)
│
└── /careconnect/appointments/[id]           [Server Component]
    guard: !isReferrer && !isReceiver → inline denial message
    data:  careConnectServerApi.appointments.getById(id)
```

---

## 4. UI Authorization Model

### How the Frontend Reads the Session

**Server Components** call `getServerSession()` in `src/lib/session.ts`:
1. Reads `platform_session` HttpOnly cookie from the request
2. Forwards token as `Authorization: Bearer` to `${GATEWAY_URL}/identity/api/auth/me`
3. Maps the `AuthMeResponse` into a `PlatformSession` object
4. `session.productRoles` is an array of strings (e.g. `["CARECONNECT_REFERRER"]`)

**Client Components** use the `SessionProvider` context:
1. On mount, calls `GET /api/auth/me` (BFF route handler, not the rewrite)
2. BFF route reads `platform_session` cookie, calls identity service, returns envelope
3. Session is stored in React context; `useSession()` hook exposes it

### How Product Roles Are Checked

```typescript
// In nav.ts — determines sidebar visibility
const ccRoles = ['CARECONNECT_REFERRER', 'CARECONNECT_RECEIVER'];
if (roles.some(r => ccRoles.includes(r))) { /* show CareConnect nav group */ }

// "Find Providers" only for referrers
if (roles.includes(ProductRole.CareConnectReferrer)) { /* add providers nav item */ }

// In page Server Components
if (!session.productRoles.includes(ProductRole.CareConnectReferrer)) redirect('/dashboard');

// In Client Components
const isReferrer = session?.productRoles.includes(ProductRole.CareConnectReferrer) ?? false;
```

### ProductRole Constant Values

```typescript
// src/types/index.ts
ProductRole.CareConnectReferrer = 'CARECONNECT_REFERRER'
ProductRole.CareConnectReceiver = 'CARECONNECT_RECEIVER'
```

These match the JWT claim values from Identity — **no mismatch here**.

### Authorization Guards Summary

| Guard | Behavior |
|-------|---------|
| `requireOrg()` | Redirects to `/no-org` if `orgId` absent |
| `requireProductRole(role)` | Redirects to `/dashboard` if role missing |
| Inline role check | Shows denial message instead of redirect |
| `requirePlatformAdmin()` | Redirects to `/dashboard` if not PlatformAdmin |

---

## 5. API Integration Mapping

### Request Path Chain

```
Browser Client Component
  → fetch /api/careconnect/api/providers?...
  → BFF catch-all: /api/careconnect/[...path]
  → Gateway: /careconnect/api/providers?...
  → Strip /careconnect prefix (YARP transform)
  → CareConnect.Api: /api/providers?...                ← registered route

Server Component  
  → serverApi.get(`/careconnect/api/providers?...`)
  → ${GATEWAY_URL}/careconnect/api/providers?...
  → YARP strips /careconnect
  → CareConnect.Api: /api/providers?...
```

The BFF catch-all proxy reconstructs the path as:
```javascript
// /api/careconnect/api/providers → params.path = ['api','providers']
const gatewayPath = `/careconnect/${params.path.join('/')}`;
// → /careconnect/api/providers  ✓
```

### Endpoint Mapping Table

| Frontend Call | Gateway Path | Backend Endpoint | Status |
|--------------|-------------|-----------------|--------|
| `GET /api/careconnect/api/providers` | `GET /careconnect/api/providers` | `GET /api/providers` | ✅ |
| `GET /api/careconnect/api/providers/{id}` | `GET /careconnect/api/providers/{id}` | `GET /api/providers/{id:guid}` | ✅ |
| `GET /api/careconnect/api/providers/{id}/availability` | `GET /careconnect/api/providers/{id}/availability` | **MISSING** | ❌ 404 |
| `GET /api/careconnect/api/referrals` | `GET /careconnect/api/referrals` | `GET /api/referrals` | ✅ |
| `POST /api/careconnect/api/referrals` | `POST /careconnect/api/referrals` | `POST /api/referrals` | ⚠️ Auth + payload issues |
| `GET /api/careconnect/api/referrals/{id}` | `GET /careconnect/api/referrals/{id}` | `GET /api/referrals/{id:guid}` | ✅ |
| `GET /api/careconnect/api/appointments` | `GET /careconnect/api/appointments` | `GET /api/appointments` | ⚠️ Response mismatch |
| `POST /api/careconnect/api/appointments` | `POST /careconnect/api/appointments` | `POST /api/appointments` | ❌ Auth + payload mismatch |
| `GET /api/careconnect/api/appointments/{id}` | `GET /careconnect/api/appointments/{id}` | `GET /api/appointments/{id:guid}` | ⚠️ Response mismatch |

---

## 6. Mismatches & Blockers

### BLOCKER 1 — Availability Endpoint Does Not Exist

**Frontend calls:**
```
GET /careconnect/api/providers/{id}/availability?from=2026-03-29&to=2026-04-05
```

**Backend has:**
- `GET /api/providers/{providerId}/availability-templates` — returns recurring schedule templates (admin concept, not bookable slots)
- `GET /api/slots?providerId=...&from=...&to=...` — returns `PagedResponse<SlotResponse>` (different path, different structure)

**Live test result:** `HTTP 404`

**Frontend expects** `ProviderAvailabilityResponse`:
```typescript
{ providerId, providerName, from, to, slots: AvailabilitySlot[] }
// where AvailabilitySlot = { id, startUtc, endUtc, durationMinutes, isAvailable, serviceType?, location? }
```

**Backend `SlotResponse`** (from `GET /api/slots`):
```csharp
{ Id, TenantId, ProviderId, ProviderName, FacilityId, FacilityName,
  ServiceOfferingId, ServiceOfferingName,
  StartAtUtc, EndAtUtc,          // camelCase: startAtUtc, endAtUtc — NOT startUtc/endUtc
  Capacity, ReservedCount, AvailableCount, Status }
// No: durationMinutes, isAvailable boolean
```

**Impact:** The entire availability page crashes with a 404 error. No slots displayed. The "Book Appointment" flow is completely broken.

---

### BLOCKER 2 — CreateAppointmentRequest Payload Mismatch

**Frontend sends** (`CreateAppointmentRequest` in `types/careconnect.ts`):
```typescript
{
  providerId:       string;   // EXTRA — backend ignores
  referralId?:      string;   // optional in frontend
  slotId?:          string;   // WRONG NAME — backend uses appointmentSlotId
  scheduledAtUtc:   string;   // EXTRA — backend ignores
  durationMinutes?: number;   // EXTRA — backend ignores
  serviceType?:     string;   // EXTRA — backend ignores
  notes?:           string;   // ✅ matches
  clientFirstName:  string;   // EXTRA — backend ignores
  clientLastName:   string;   // EXTRA — backend ignores
  clientDob?:       string;   // EXTRA — backend ignores
  clientPhone?:     string;   // EXTRA — backend ignores
  clientEmail?:     string;   // EXTRA — backend ignores
  caseNumber?:      string;   // EXTRA — backend ignores
}
```

**Backend expects** (`CreateAppointmentRequest` in `AppointmentDTOs.cs`):
```csharp
{
  ReferralId:        Guid,   // REQUIRED, non-nullable — frontend treats as optional
  AppointmentSlotId: Guid,   // REQUIRED — frontend sends as slotId (name mismatch)
  Notes?:            string
}
```

**Impact:**
- `referralId` is optional in frontend but required (non-nullable `Guid`) in backend → appointment creation without a referral fails with 400
- `slotId` vs `AppointmentSlotId` name mismatch → backend receives `Guid.Empty`, likely 400 or slot not found
- Client info fields are silently discarded; backend stores them differently (pulled from referral)

---

### BLOCKER 3 — AppointmentResponse Field Name Mismatches

**Backend returns** (`AppointmentResponse`):
```csharp
{ ScheduledStartAtUtc, ScheduledEndAtUtc,   // camelCase: scheduledStartAtUtc, scheduledEndAtUtc
  FacilityId, FacilityName,
  ServiceOfferingId, ServiceOfferingName }
// Missing: durationMinutes, clientFirstName/LastName, caseNumber, serviceType, statusHistory
```

**Frontend expects** (`AppointmentDetail`):
```typescript
{ scheduledAtUtc,          // ← field name mismatch; backend sends scheduledStartAtUtc
  scheduledEndAtUtc,       // ← partial match (backend sends scheduledEndAtUtc ✅)
  durationMinutes,         // ← MISSING in backend response
  serviceType,             // ← MISSING (backend uses serviceOfferingName)
  clientFirstName/LastName, // ← MISSING in backend response
  caseNumber,              // ← MISSING in backend response
  statusHistory }          // ← MISSING; needs separate GET /appointments/{id}/history
```

**Impact:**
- Appointment timestamps will be `undefined` → "—" shown in date columns
- Duration, service type, client name, case number all blank
- Status history timeline is always empty

---

### BLOCKER 4 — Authorization: StandardUser Cannot Create Referrals or Appointments

**Backend policy on `POST /api/referrals` and `POST /api/appointments`:**
```csharp
.RequireAuthorization(Policies.PlatformOrTenantAdmin)
// Defined as: RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin)
```

**Test users and their system roles:**
| User | Email | System Role | Can Create Referral? |
|------|-------|-------------|---------------------|
| Margaret Hartwell | margaret@hartwell.law | **TenantAdmin** | ✅ Yes |
| James Whitmore | james.whitmore@hartwell.law | **StandardUser** | ❌ 403 |
| Olivia Chen | olivia.chen@hartwell.law | **StandardUser** | ❌ 403 |
| Dr. Ramirez | dr.ramirez@meridiancare.com | **TenantAdmin** | ✅ Yes |
| Alex Diallo | alex.diallo@meridiancare.com | **StandardUser** | ❌ 403 |

**Live test result:** James (StandardUser) → `HTTP 403` confirmed.

**Impact:** The UI correctly shows James and Olivia the "Create Referral" button (they have `CARECONNECT_REFERRER` product role). But clicking it always returns 403. They see a generic "Access denied" error. No referrals can be created by standard users.

**Root cause:** The policy uses the system role (`TenantAdmin`) but should use product role (`CARECONNECT_REFERRER`). The `CanReferCareConnect` policy from Step 2 has not yet been implemented in `Program.cs`.

---

### ISSUE 5 — No Product-Role Enforcement on Read Endpoints

All read endpoints use `Policies.AuthenticatedUser` (any authenticated user). This means:
- A user with `SYNQFUND_REFERRER` but no CareConnect role can read providers and referrals
- The backend relies solely on tenant-ID scoping, not product-role scoping
- The frontend's UX guards (nav hiding, page redirects) are the only CareConnect role check on reads

This is the Step 2 issue (`CanUseCareConnect` policy) and is a security concern but not a UX blocker.

---

### ISSUE 6 — GATEWAY_URL Fallback Points to Wrong Port

In three server-side files:
```typescript
const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5000';
//                                                            ^^^^
//                                              Should be 5010 (gateway port)
//                                              5000 is the web app itself
```

Files affected:
- `src/lib/session.ts`
- `src/lib/server-api-client.ts`
- `src/app/api/careconnect/[...path]/route.ts`
- `src/app/api/auth/me/route.ts`

**Current impact:** None — `GATEWAY_URL=http://localhost:5010` is correctly set in `scripts/run-dev.sh`. But if the env var is ever unset (e.g. in a new deployment environment), all server-side API calls will loop back to the web app itself, creating a circular request that returns the login page HTML instead of JSON.

---

### ISSUE 7 — StatusBadge Missing Appointment Statuses

`src/components/careconnect/status-badge.tsx` defines styles only for referral statuses:
```typescript
const STATUS_STYLES: Record<string, string> = {
  Pending:   '...',  Accepted: '...',  Declined: '...',
  Completed: '...',  Cancelled: '...',
};
```

Appointment statuses `Scheduled`, `Confirmed`, `NoShow` fall through to the default gray. They display but without meaningful colour coding.

---

## 7. Required Frontend Changes

### F1 — Fix availability page to call the correct backend endpoint

The availability page must call `GET /api/slots?providerId={id}&from=...&to=...` instead of the non-existent `/api/providers/{id}/availability`.

The response must be mapped from `SlotResponse` to `AvailabilitySlot`:
```typescript
// SlotResponse → AvailabilitySlot mapping
{
  id:              slot.id,
  startUtc:        slot.startAtUtc,          // rename startAtUtc → startUtc
  endUtc:          slot.endAtUtc,            // rename endAtUtc → endUtc
  durationMinutes: computeMinutes(slot.startAtUtc, slot.endAtUtc),
  isAvailable:     slot.availableCount > 0,  // derive from availableCount
  serviceType:     slot.serviceOfferingName, // rename
  location:        slot.facilityName,        // rename
}
// Wrap in: { providerId, providerName, from, to, slots: [] }
```

Changes needed:
- `src/types/careconnect.ts` — update `AvailabilitySlot` to match `SlotResponse` shape (or keep and add mapper)
- `src/lib/careconnect-api.ts` — change `getAvailability` call from `providers/{id}/availability` to `slots?providerId={id}&from=...&to=...` and add response mapper
- `src/app/(platform)/careconnect/providers/[id]/availability/page.tsx` — update search params passed to API

### F2 — Fix CreateAppointmentRequest payload

```typescript
// Currently (WRONG):
const payload: CreateAppointmentRequest = {
  providerId, referralId?, slotId?, scheduledAtUtc, durationMinutes, ...clientFields
};

// Correct (aligned with backend):
const payload = {
  referralId:        referral.id,   // REQUIRED — must have a referral first
  appointmentSlotId: slot.id,       // rename slotId → appointmentSlotId
  notes:             notes || undefined,
};
```

Also update `CreateAppointmentRequest` in `src/types/careconnect.ts`:
```typescript
export interface CreateAppointmentRequest {
  referralId:        string;   // required
  appointmentSlotId: string;   // was: slotId
  notes?:            string;
}
```

**UX implication:** Booking without a referral is not supported by the backend. The `BookingPanel` should always require a `referral` prop (currently optional).

### F3 — Fix AppointmentSummary and AppointmentDetail types

```typescript
export interface AppointmentSummary {
  id:                  string;
  referralId:          string;       // non-optional (backend requires referral)
  providerId:          string;
  providerName:        string;
  facilityId:          string;       // add
  facilityName:        string;       // add
  serviceOfferingName: string;       // rename from serviceType
  scheduledStartAtUtc: string;       // rename from scheduledAtUtc
  scheduledEndAtUtc:   string;
  status:              string;
  notes?:              string;
  createdAtUtc:        string;
  updatedAtUtc:        string;
  // REMOVE: durationMinutes, clientFirstName, clientLastName, caseNumber, serviceType
}

export interface AppointmentDetail extends AppointmentSummary {
  // status history still needs separate GET /appointments/{id}/history call
  statusHistory: AppointmentStatusHistoryItem[];
}
```

Update all components that reference removed/renamed fields.

### F4 — Add appointment status colours to StatusBadge

```typescript
const STATUS_STYLES: Record<string, string> = {
  // existing referral statuses...
  Scheduled:  'bg-blue-50    text-blue-700    border-blue-200',
  Confirmed:  'bg-green-50   text-green-700   border-green-200',
  NoShow:     'bg-purple-50  text-purple-700  border-purple-200',
};
```

### F5 — Fix GATEWAY_URL fallback ports

```typescript
// In all four BFF route files and session.ts:
const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5010';
//                                                            ^^^^
```

---

## 8. Required Backend Changes

### B1 — Add `CanReferCareConnect` policy (Step 2 fix)

In `CareConnect.Api/Program.cs`:
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, ...);
    options.AddPolicy(Policies.PlatformOrTenantAdmin, ...);

    // NEW: Product-role policies
    options.AddPolicy("CanReferCareConnect", policy =>
        policy.RequireClaim("product_roles", "CARECONNECT_REFERRER"));
    options.AddPolicy("CanReceiveCareConnect", policy =>
        policy.RequireClaim("product_roles", "CARECONNECT_RECEIVER"));
    options.AddPolicy("CanUseCareConnect", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim("product_roles", "CARECONNECT_REFERRER") ||
            ctx.User.HasClaim("product_roles", "CARECONNECT_RECEIVER")));
});
```

Then update endpoint authorization:
```csharp
// POST /api/referrals — currently PlatformOrTenantAdmin; must be CanReferCareConnect
.RequireAuthorization("CanReferCareConnect");

// POST /api/appointments — same fix
.RequireAuthorization("CanReferCareConnect");
```

This is already fully documented in `analysis/step2_careconnect_authorization.md`.

### B2 — Extend AppointmentResponse with client fields

The frontend expects client info (`clientFirstName`, `clientLastName`, etc.) in the appointment response. These should be populated from the linked referral when the appointment is fetched:

```csharp
public class AppointmentResponse
{
    // existing fields...
    // ADD from linked referral:
    public string? ClientFirstName { get; init; }
    public string? ClientLastName  { get; init; }
    public string? ClientDob       { get; init; }
    public string? ClientPhone     { get; init; }
    public string? ClientEmail     { get; init; }
    public string? CaseNumber      { get; init; }
    public int     DurationMinutes { get; init; }  // computed from slot end - start
}
```

Alternatively, the frontend can be updated to no longer expect these fields (F3 above), fetching them from the referral instead. The backend-side fix is cleaner.

### B3 — Embed statusHistory in AppointmentResponse

The frontend `AppointmentDetail.statusHistory` is currently an empty array because it's never fetched. Either:
- Include `statusHistory` in `GET /appointments/{id}` response (preferred — single network call)
- Or: frontend must call `GET /appointments/{id}/history` separately and merge

The endpoint `GET /api/appointments/{id}/history` already exists but the frontend never calls it.

---

## 9. Minimal Steps to Make UI Functional

Listed in priority order — each step is independently deployable:

### Step A: Allow standard users to create referrals (1 backend change)
**Files:** `CareConnect.Api/Program.cs`, `ReferralEndpoints.cs`  
**Change:** Add `CanReferCareConnect` policy; swap `PlatformOrTenantAdmin` for it on `POST /api/referrals`  
**Unblocks:** James Whitmore, Olivia Chen can now create referrals via UI

### Step B: Fix availability endpoint (1 backend + 1 frontend change)
**Backend:** Add `GET /api/providers/{providerId}/availability?from=&to=` endpoint that returns `ProviderAvailabilityResponse` using existing slot infrastructure  
**Frontend:** Update `careconnect-api.ts` to call new path; update types  
**Unblocks:** Availability page loads; slot selection works

### Step C: Fix CreateAppointmentRequest (frontend + backend)
**Frontend:** Update `CreateAppointmentRequest` type and `booking-panel.tsx` payload construction  
**Backend:** Ensure `referralId` is required; add `appointmentSlotId` field name  
**Unblocks:** Appointments can be created

### Step D: Fix AppointmentResponse field names (frontend types only)
**Frontend:** Rename `scheduledAtUtc → scheduledStartAtUtc`; remove non-existent fields  
**Unblocks:** Appointment timestamps display correctly

### Step E: Fix StatusBadge colours (cosmetic)
**Frontend:** Add `Scheduled`, `Confirmed`, `NoShow` to `STATUS_STYLES`

---

## 10. Risks & Assumptions

| Risk | Severity | Notes |
|------|----------|-------|
| Backend appointment schema differs from frontend — client info not stored on appointment | High | Backend stores client via referral link; frontend assumes it's denormalised |
| `ReferralId` required on appointment — booking without a prior referral not supported | Medium | Current UX allows booking from availability without referral context |
| `GATEWAY_URL` fallback is wrong port | Low | No current impact since env var is set; would cause cascading silent failure if var is unset |
| Availability template vs slot confusion | Medium | Two different backend concepts exist; frontend only needs slots |
| Standard JWT claim format for product_roles | Medium | `product_roles` claim may be a space-separated string in JWT rather than an array; `CanReferCareConnect` policy must handle both |

---

## Happy Path Walkthrough

### Margaret (TenantAdmin, CARECONNECT_REFERRER) creating a referral and booking an appointment

1. **Login** → `POST /api/auth/login` → BFF sets `platform_session` cookie ✅  
2. **Dashboard** → redirects to `/careconnect/referrals` (first nav item) ✅  
3. **Referrals page** → `GET /careconnect/api/referrals` → 200, table renders ✅  
4. **Click "New Referral"** → navigates to `/careconnect/providers` ✅  
5. **Providers list** → `GET /careconnect/api/providers?isActive=true&page=1` → 200 ✅  
6. **Click provider** → `/careconnect/providers/{id}` → `GET /careconnect/api/providers/{id}` → 200 ✅  
7. **Click "Create Referral"** → `CreateReferralForm` modal opens ✅  
8. **Submit form** → `POST /careconnect/api/referrals` → **201** (Margaret is TenantAdmin) ✅  
9. **Redirected** to `/careconnect/referrals/{id}` → referral detail page ✅  
10. **Click "Book Appointment"** → navigates to `/careconnect/providers/{id}/availability?referralId={id}` ✅  
11. **Availability page loads** → `GET /careconnect/api/providers/{id}/availability` → **404** ❌ STOPS HERE

---

## Failure Points

| # | Where | What Breaks | HTTP code |
|---|-------|-------------|-----------|
| 1 | `/careconnect/providers/{id}/availability` (page load) | Endpoint `GET /api/providers/{id}/availability` does not exist | **404** |
| 2 | `BookingPanel.handleSubmit()` | Payload missing `appointmentSlotId`; has `slotId` instead | **400** |
| 3 | `BookingPanel.handleSubmit()` | `referralId` required by backend but optional in frontend | **400** |
| 4 | Appointment list/detail | `scheduledAtUtc` field is `undefined` (backend sends `scheduledStartAtUtc`) | Renders `—` |
| 5 | Appointment list/detail | `clientFirstName/LastName` always `undefined` (backend doesn't include them) | Renders blank |
| 6 | Appointment status history | Never fetched (component shows empty timeline) | 0 entries |
| 7 | `CreateReferralForm` for James/Olivia | System role is `StandardUser` → `POST /api/referrals` policy rejects | **403** |
| 8 | Status badge for appointments | `Scheduled`, `Confirmed`, `NoShow` statuses have no colour style | Gray (cosmetic) |
