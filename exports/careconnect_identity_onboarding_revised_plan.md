# CareConnect Identity Onboarding Revised Plan

## Purpose

This plan defines the target onboarding behavior for CareConnect users across self-enrollment, tenant invites, and cross-tenant account linking.

The main goal is to keep tenant membership and organization membership separate while still supporting explicit global or tenant-scoped organization membership where the business flow requires it.

## Identity Model Rules

- `idt_Users` is global: one user record per person or email.
- `idt_UserTenants` is tenant membership only.
- `idt_Organizations` supports two scopes:
  - Global organization: `TenantId = NULL`
  - Tenant-scoped organization: `TenantId = <tenantId>`
- `idt_UserOrganizationMemberships` is explicit organization membership only.
- Tenant membership must not automatically create organization membership.
- Organization membership must not automatically create tenant membership.
- Tenant-scoped organization membership requires a matching active `idt_UserTenants` row first.
- Global organization membership does not require tenant membership.

## CareConnect Provider Self-Enrollment

### Target Behavior

Provider self-enrollment creates or resolves a global provider organization, creates or reuses the user, links the user to the global provider organization, and creates tenant membership for the target tenant.

Expected records:

```text
idt_Organizations
  TenantId = NULL
  OrgType = PROVIDER

idt_Users

idt_UserOrganizationMemberships
  OrganizationId = <global provider org id>
  MemberRole = Member

idt_UserTenants
  TenantId = <target tenant id>
```

### Plan

- Change provider self-enrollment organization creation to create or resolve a global `PROVIDER` organization.
- Create or reuse the user in `idt_Users`.
- Link the user to the global provider organization through `idt_UserOrganizationMemberships`.
- Add the user to the target tenant through `idt_UserTenants`.
- Do not create tenant-scoped provider organization membership.

## CareConnect Law-Firm Self-Enrollment

### Target Behavior

Law-firm self-enrollment creates or resolves a global law-firm organization, creates or reuses the user, links the user to the global law-firm organization, and creates tenant membership for the target tenant.

Expected records:

```text
idt_Organizations
  TenantId = NULL
  OrgType = LAW_FIRM

idt_Users

idt_UserOrganizationMemberships
  OrganizationId = <global law firm org id>
  MemberRole = Member

idt_UserTenants
  TenantId = <target tenant id>
```

### Plan

- Change law-firm organization creation to create or resolve a global `LAW_FIRM` organization.
- Create or reuse the user.
- Link the user to the global law-firm organization.
- Add the user to the target tenant.
- Do not attach the global law-firm organization to the tenant.

## Generic Tenant Invite

### Target Behavior Without Organization Details

If no organization details are entered, the invite creates only the user, tenant membership, and invitation.

Expected records:

```text
idt_Users
idt_UserTenants
idt_UserInvitations
```

### Target Behavior With Manual Organization Details

If organization details are entered, Identity creates or resolves a tenant-scoped organization from those details, then links the invited user to that organization.

Expected records:

```text
idt_Users
idt_UserTenants

idt_Organizations
  TenantId = <target tenant id>

idt_UserOrganizationMemberships
  MemberRole = Member

idt_UserInvitations
```

### Plan

- Generic invites must never guess the first or primary organization.
- Invite forms may include optional manual organization details.
- If organization details are absent, create only:
  - `idt_Users`
  - `idt_UserTenants`
  - `idt_UserInvitations`
- If organization details are present:
  - create or resolve a tenant-scoped organization from those details
  - link the invited user to that organization
- The user should not select an existing organization in this flow.
- The system should not infer organization membership from tenant membership.

## Invite Acceptance

### Plan

- Remove automatic primary organization assignment from invite acceptance.
- Accepting an invite should only:
  - validate the token
  - set the user's password
  - activate the user
  - mark the invitation accepted
  - return the tenant portal URL
- Any organization membership must already have been explicitly created at invite time.

## Existing Active User Joining Another Tenant

### Target Behavior

Existing active users invited or provisioned into another tenant receive tenant access only.

Expected records:

```text
idt_Users
  reuse existing user

idt_UserTenants
  TenantId = <new tenant id>
```

### Plan

- Reuse the existing `idt_Users` record.
- Create only the missing `idt_UserTenants` row.
- Do not create `idt_UserOrganizationMemberships`.
- Do not assign a primary organization.
- Organization membership must be added later through an explicit organization-membership flow.

## Internal Tenant Assignment

The current internal tenant assignment behavior is aligned with this model.

### Plan

- Keep `assign-tenant` as tenant membership only.
- It may assign tenant-level roles.
- It must not create organization membership.

## Centralized Explicit Organization Membership Logic

Add reusable Identity service/helper methods for explicit organization and membership operations.

Candidate methods:

- `EnsureGlobalOrganizationAsync`
- `EnsureTenantScopedOrganizationAsync`
- `EnsureUserTenantAsync`
- `EnsureUserOrganizationMembershipAsync`

Validation rules:

- Global organization membership does not require tenant membership.
- Tenant-scoped organization membership requires a matching active `idt_UserTenants` row first.
- Duplicate memberships are idempotent.
- Default member role is `Member`.
- Primary organization is never set unless explicitly requested.

## Code Areas To Change

### Provider and Law-Firm Enrollment

- `apps/services/careconnect/CareConnect.Api/Endpoints/EnrollmentEndpoints.cs`
- `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`

### Generic Invite

- `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`

### Invite Acceptance

- `apps/services/identity/Identity.Api/Endpoints/AuthEndpoints.cs`

### Internal Tenant Assignment

- `apps/services/identity/Identity.Infrastructure/Services/UserMembershipService.cs`

## Test Plan

Add or update tests for the following cases:

- Provider self-enrollment creates a global provider organization, organization membership, and tenant membership.
- Law-firm self-enrollment creates a global law-firm organization, organization membership, and tenant membership.
- Generic tenant invite without organization details creates no organization membership.
- Generic tenant invite with organization details creates tenant-scoped organization membership.
- Invite acceptance does not create guessed organization membership.
- Existing active user joining another tenant gets tenant membership only.
- Global organization membership works without tenant membership.
- Tenant-scoped organization membership fails without matching tenant membership.
- `assign-tenant` never creates organization membership.

## Open Implementation Notes

- Provider and law-firm self-enrollment should share the same global-organization membership pattern with different `OrgType` values.
- Generic invite organization details should use the same organization-detail inputs and normalization behavior as CareConnect sign-up.
- Tenant-scoped organization membership should be explicit and validated centrally, not inferred in invite acceptance or login flows.
- Any existing `/no-org` handling should be revisited so users with tenant membership but no organization membership can still land in an appropriate tenant-level experience.
