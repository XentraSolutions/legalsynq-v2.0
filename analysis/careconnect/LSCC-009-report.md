# LSCC-009: Admin Activation Queue — Implementation Report

**Status:** Complete  
**Date:** 2026-03-31  
**Builds cleanly:** Yes (0 errors)  
**New tests:** 10/10 pass  
**Pre-existing failures:** 5 (ProviderAvailability — unrelated)

---

## Overview

LSCC-009 delivers the admin-side workflow that closes the provider activation loop opened by LSCC-008.  When a provider submits the LSCC-008 activation form, the system now:

1. Creates a durable `ActivationRequest` record in CareConnect's database.
2. Surfaces that record in a protected admin queue at `/careconnect/admin/activations`.
3. Lets an admin review the full context (provider, referral, requester) and approve with a single form action.
4. Approval links the provider to an Identity service Organisation and marks the request as `Approved` — safely idempotent.

---

## Architecture

### Domain Layer

**`ActivationRequest` entity** (`CareConnect.Domain`)

| Field                | Type      | Notes                                   |
|----------------------|-----------|-----------------------------------------|
| `Id`                 | `Guid`    | Primary key                             |
| `TenantId`           | `Guid`    | Isolates requests by tenant             |
| `ReferralId`         | `Guid`    | FK → Referral                           |
| `ProviderId`         | `Guid`    | FK → Provider                           |
| `ProviderName/Email` | `string`  | Snapshot — survives provider edits      |
| `RequesterName/Email`| `string?` | Captured from LSCC-008 activation form  |
| `ClientName`         | `string?` | Referral context snapshot               |
| `ReferringFirmName`  | `string?` | Referral context snapshot               |
| `RequestedService`   | `string?` | Referral context snapshot               |
| `Status`             | `string`  | `"Pending"` → `"Approved"`              |
| `ApprovedByUserId`   | `Guid?`   | Admin who approved                      |
| `LinkedOrganizationId`| `Guid?`  | Org the provider was linked to          |

**Deduplication:** A unique index on `(ReferralId, ProviderId)` ensures one record per referral/provider pair. Repeat form submissions call `UpdateRequesterDetails()` instead of creating duplicates.

**Domain actions:**
- `Create(...)` — factory, sets `Status = Pending`
- `UpdateRequesterDetails(name, email)` — upsert for intent data
- `Approve(userId, orgId)` — idempotent (second call returns `false`)

### Infrastructure Layer

- `ActivationRequestConfiguration` — EF fluent config, dedup index, navigation properties
- `CareConnectDbContext.ActivationRequests` — `DbSet<ActivationRequest>`
- `ActivationRequestRepository` — CRUD + `GetPendingAsync()` / `GetByReferralAndProviderAsync()`
- **Migration:** `20260331204551_AddActivationRequestQueue`

### Application Layer

**`ActivationRequestService`** implements `IActivationRequestService`:

| Method | Behaviour |
|--------|-----------|
| `UpsertAsync(...)` | Insert or update requester details on duplicate |
| `GetPendingAsync()` | List of `ActivationRequestSummary` DTOs |
| `GetByIdAsync(id)` | Full `ActivationRequestDetail` DTO with nav properties |
| `ApproveAsync(id, orgId, userId)` | Link provider → emit audit → mark Approved; idempotent |

**Approval guard rails:**
1. Request not found → `NotFoundException` (404)
2. Already Approved → early return `WasAlreadyApproved = true`, no side effects
3. Provider already has `OrganizationId` → skip `LinkOrganizationAsync`, still mark Approved

**`ReferralService.TrackFunnelEventAsync`** updated: when `eventType == "ActivationStarted"`, calls `UpsertAsync` with requester name + email from the activation form body.

**DTOs** (`ActivationRequestDtos.cs`):
- `ActivationRequestSummary` — list row
- `ActivationRequestDetail` — detail page (includes live provider nav fields)
- `ApproveActivationRequest` — approve body (`OrganizationId` required)
- `ApproveActivationResponse` — approve result (`WasAlreadyApproved`, `ProviderAlreadyLinked`, `LinkedOrganizationId`)

### API Layer

**`ActivationAdminEndpoints`** — three endpoints under `Policies.PlatformOrTenantAdmin`:

| Method | Route | Description |
|--------|-------|-------------|
| `GET`  | `/api/admin/activations` | Pending queue list |
| `GET`  | `/api/admin/activations/{id}` | Detail for one request |
| `POST` | `/api/admin/activations/{id}/approve` | Approve and link provider |

### Frontend

**Admin pages** (Next.js App Router, server components):

| Route | File | Description |
|-------|------|-------------|
| `/careconnect/admin/activations` | `page.tsx` | Queue table, newest first |
| `/careconnect/admin/activations/[id]` | `page.tsx` | Detail + approve panel |

Both pages call `requireAdmin()` (TenantAdmin or PlatformAdmin) before rendering.

**`ApproveAction` client component** (`approve-action.tsx`):
- Text input for Organisation ID (required)
- `POST /api/careconnect/api/admin/activations/{id}/approve`
- Success state rendered inline — no page reload required
- Handles idempotent re-approval gracefully
- Shows "provider already linked" variant when applicable

**`careconnect-server-api.ts`** extended with `adminActivations.getPending()` and `adminActivations.getById(id)`.

**`careconnect.ts` types** extended with `ActivationRequestSummary` and `ActivationRequestDetail`.

**`activation-form.tsx`** (LSCC-008): updated to include `requesterName` and `requesterEmail` in the `track-funnel` POST body.

---

## Test Coverage

File: `CareConnect.Tests/Application/ActivationQueueTests.cs` — **10 tests, all pass**

| Test | Scenario |
|------|----------|
| `UpsertAsync_NewPair_AddsRequest` | New `(referralId, providerId)` creates record |
| `UpsertAsync_ExistingPair_UpdatesRequesterDetails` | Duplicate form submission updates, no insert |
| `GetPendingAsync_ReturnsMappedSummaries` | DTO fields mapped correctly |
| `GetByIdAsync_UnknownId_ReturnsNull` | Missing ID returns null |
| `GetByIdAsync_WithProvider_FormatsAddress` | Address formatted as single string |
| `GetByIdAsync_ProviderAlreadyLinked_IsAlreadyActiveTrue` | `IsAlreadyActive = true` when org linked |
| `ApproveAsync_PendingRequest_LinksProviderAndReturnsSuccess` | Full approval flow |
| `ApproveAsync_AlreadyApproved_ReturnsIdempotentSuccess` | Idempotent re-approve — no side effects |
| `ApproveAsync_ProviderAlreadyLinked_SkipsLinkCallStillApproves` | Skip link, still Approve |
| `ApproveAsync_NotFound_ThrowsNotFoundException` | Missing record → 404 |

---

## Security

- All three admin API endpoints require `Policies.PlatformOrTenantAdmin`.
- All admin frontend pages call `requireAdmin()` before rendering any data.
- Approval requires `organizationId` explicitly from the admin — no auto-provisioning.
- Audit event emitted on every successful approval (`careconnect.activation.approved`, `EventCategory.Business`, `Visibility.Tenant`).
- Approval is idempotent and audited — safe for retry without duplicate side effects.

---

## Migration

```
20260331204551_AddActivationRequestQueue
```

Creates `ActivationRequests` table with:
- All domain fields
- FK to `Providers` (cascade delete)
- FK to `Referrals` (restrict)
- Unique index on `(ReferralId, ProviderId)` for deduplication
- Index on `(TenantId, Status)` for queue queries
