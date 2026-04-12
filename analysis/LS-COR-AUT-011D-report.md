# LS-COR-AUT-011D — Policy Simulation + Decision Testing Report

**Date:** 2026-04-12
**Status:** COMPLETE
**Build:** 3 services — 0 errors, 0 warnings
**Tests:** 256 total (20 new) — 0 failures

---

## 1. Simulation Service Design

### Interface

```csharp
IAuthorizationSimulationService
  Task<SimulationResult> SimulateAsync(SimulationRequest request, CancellationToken ct)
```

Located in: `Identity.Application/Interfaces/IAuthorizationSimulationService.cs`

### Implementation

`AuthorizationSimulationService` — `Identity.Infrastructure/Services/AuthorizationSimulationService.cs`

**Key Design Decisions:**

1. **Reuses real evaluation logic.** The simulation service calls `PolicyEvaluationService.EvaluatePolicy()` and `PolicyEvaluationService.EvaluateOperator()` — the same static methods used by the runtime engine. No separate evaluator.

2. **Read-only. No side effects.** The service queries the database read-only. It does not:
   - Write to the policy evaluation cache
   - Increment policy versions
   - Mutate user/role/permission state
   - Create or modify policies

3. **Effective Access Integration.** Calls `IEffectiveAccessService.GetEffectiveAccessAsync()` to resolve the target user's full permission set, roles, and group memberships — identical to the login-time resolution.

4. **Admin bypass detection.** If the target user holds TenantAdmin or PlatformAdmin roles, the simulation reports `allowed: true` with reason "Admin bypass" — matching runtime behavior.

5. **DI registration:** Scoped lifetime in `DependencyInjection.cs`.

### Simulation Modes

| Mode | Behavior |
|------|----------|
| **Live** | Evaluates current active policies mapped to the permission code |
| **Draft** | Evaluates live policies + an in-memory draft policy definition |

### Request Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `tenantId` | Guid | Yes | Target tenant |
| `userId` | Guid | Yes | Target user (must belong to tenant) |
| `permissionCode` | string | Yes | Permission to simulate (e.g. `SYNQ_FUND.application:approve`) |
| `resourceContext` | Dictionary | No | Resource attributes for ABAC evaluation |
| `requestContext` | Dictionary | No | Request attributes (method, path, etc.) |
| `draftPolicy` | DraftPolicyInput | No | In-memory policy to evaluate alongside live policies |
| `excludePolicyIds` | List<Guid> | No | Active policy IDs to exclude from evaluation |

### Response Fields

| Field | Description |
|-------|-------------|
| `allowed` | Final simulation decision |
| `permissionPresent` | Whether the permission is in the user's effective set |
| `roleFallbackUsed` | Whether role fallback would apply |
| `permissionCode` | The tested permission |
| `policyDecision` | Full policy evaluation breakdown |
| `reason` | Human-readable explanation |
| `mode` | Live or Draft |
| `user` | User identity summary (email, display name, roles, permissions) |
| `permissionSources` | Attribution: how the permission was acquired (direct/inherited, via which role/group) |
| `evaluationElapsedMs` | Execution time |

---

## 2. API Endpoints Added

### POST /api/admin/authorization/simulate

**Route:** `/api/admin/authorization/simulate`
**Gateway path:** `/identity/api/admin/authorization/simulate`
**Handler:** `AdminEndpoints.SimulateAuthorization`
**Registration:** `AdminEndpoints.MapAdminEndpoints()` (line 185)

**Security:**
- Admin-only (gateway JWT enforcement)
- `IsCrossTenantAccess()` check: TenantAdmin restricted to own tenant, PlatformAdmin unrestricted
- Target user must exist and belong to specified tenant
- Draft policy rules validated before execution

**Validation:**
- `permissionCode` required, must contain dot separator
- `tenantId` required, must exist
- `userId` required, must exist in tenant
- Draft policy: `policyCode` and `name` required; rules must have `field` and `value`
- Malformed resource/request context rejected by JSON deserialization

---

## 3. UI Pages / Components Added

### Authorization Simulator Page

**Route:** `/authorization-simulator`
**File:** `apps/control-center/src/app/authorization-simulator/page.tsx`

**Navigation:** Added to CC sidebar under IDENTITY section with `ri-test-tube-line` icon, badge `LIVE`.

### Components

| File | Type | Description |
|------|------|-------------|
| `page.tsx` | Server Component | Auth guard, loads tenant list, renders shell |
| `simulator-form.tsx` | Client Component | Interactive form + result display |
| `actions.ts` | Server Action | Calls `controlCenterServerApi.simulation.simulate()` |
| `loading.tsx` | Loading UI | Spinner during page load |

### UI Features

**Input Panel:**
- Tenant selector (PlatformAdmin: all tenants; TenantAdmin: own tenant only)
- User ID input
- Permission code input
- Resource context JSON editor
- Request context JSON editor

**Draft Policy Tester (collapsible):**
- Policy code and name
- Effect selector (Allow/Deny)
- Priority input
- Rule builder with field dropdown (supported fields), operator dropdown, value input, logical group toggle (AND/OR)
- Add/remove rules dynamically

**Result Panel:**
- Allow/Deny banner with color coding (green/red)
- Summary cards: Permission presence, Mode (Live/Draft), Elapsed time
- User identity section with roles
- Permission source attribution (Direct/Inherited, via role, group)
- Deny override indicator
- Policy evaluation breakdown:
  - Policy code, effect, priority, evaluation order
  - Draft policies highlighted with amber DRAFT badge
  - Rule results table: field, operator, expected, actual, pass/fail
- Copy JSON button
- Raw JSON toggle

---

## 4. Draft Policy Testing Approach

Draft policy simulation is handled entirely in-memory:

1. Admin provides draft policy definition via the simulation request (`draftPolicy` field)
2. The `AuthorizationSimulationService` builds in-memory rule structures
3. Rules are evaluated using `PolicyEvaluationService.EvaluateOperator()` — the same operator logic as production
4. Draft policy results are appended to the live policy results with `isDraft: true` flag
5. Deny-override logic includes the draft policy in its evaluation
6. **No database writes occur** — the draft policy is never persisted

### Draft Rule Validation

- Field, operator, and value are required for each rule
- Operators parsed from string (e.g., "LessThan") via `Enum.TryParse<RuleOperator>`
- Invalid operators fall back to `Equals`
- Logical group defaults to "And"

### UI Support

The simulator form includes a collapsible "Draft Policy Testing" panel with:
- Visual rule builder (no JSON editing required)
- Supported fields dropdown matching `PolicyRule.SupportedFields`
- All 10 operators available
- AND/OR logical group per rule

---

## 5. Explainability Output Model

The simulation result provides full decision traceability:

```
SimulationResult
├── allowed: bool
├── reason: string (human-readable)
├── permissionPresent: bool
├── roleFallbackUsed: bool
├── user
│   ├── displayName, email
│   ├── roles[] (product:role format)
│   └── permissions[] (effective set)
├── permissionSources[]
│   ├── source: "Direct" | "Inherited"
│   ├── viaRole: string
│   └── groupId/groupName (if inherited)
├── policyDecision
│   ├── evaluated: bool
│   ├── policyVersion: long
│   ├── denyOverrideApplied: bool
│   ├── denyOverridePolicyCode: string?
│   └── matchedPolicies[]
│       ├── policyCode, policyName
│       ├── effect: "Allow" | "Deny"
│       ├── priority, evaluationOrder
│       ├── result: "ALLOW" | "DENY"
│       ├── isDraft: bool
│       └── ruleResults[]
│           ├── field, operator
│           ├── expected, actual
│           └── passed: bool
└── evaluationElapsedMs: long
```

The output is both machine-readable (JSON) and UI-friendly (rendered in the Control Center with color coding, badges, and tables).

---

## 6. Logging / Audit Behavior

### Audit Event

Each simulation execution emits an audit event via `IAuditEventClient.IngestAsync()`:

| Field | Value |
|-------|-------|
| EventType | `authorization.simulation.executed` |
| EventCategory | `Administrative` (not Security) |
| Visibility | `Platform` |
| Severity | `Info` |
| SourceSystem | `Identity` |
| SourceService | `AdminEndpoints` |
| Action | `SimulateAuthorization` |
| Outcome | `allow` or `deny` |
| Metadata | JSON: permissionCode, mode, allowed |
| Tags | `["simulation", "authorization", "live"/"draft"]` |
| IdempotencyKey | `sim:{adminId}:{userId}:{permissionCode}:{timestamp}` |

### Separation from Runtime Logs

- EventCategory is `Administrative`, not `Security`
- EventType prefix is `authorization.simulation.*`, distinct from runtime `user.authorization.*`
- Tags include `simulation` for easy filtering
- **The simulation service does not call `LogPolicyDecision()`** — it evaluates policies but does not emit runtime log entries
- No fake ALLOW/DENY log lines are generated

---

## 7. Validation Rules Enforced

| Rule | Location | Behavior |
|------|----------|----------|
| `permissionCode` required | Endpoint | 400 Bad Request |
| `permissionCode` must contain dot separator | Endpoint | 400 Bad Request |
| `tenantId` required and must exist | Endpoint | 400/404 |
| `userId` required and must exist in tenant | Endpoint | 400/404 |
| Cross-tenant access blocked for TenantAdmin | Endpoint | 403 Forbid |
| Draft policy: `policyCode` required | Endpoint | 400 |
| Draft policy: `name` required | Endpoint | 400 |
| Draft rule: `field` required | Endpoint | 400 |
| Draft rule: `value` required | Endpoint | 400 |
| Invalid operator string | Service | Falls back to `Equals` |
| Resource/request context JSON | UI | Validated before submission |

---

## 8. Test Results

**256 total tests — 0 failures** (20 new)

### New Tests (20 added in 011D)

| Test Class | Count | Validates |
|-----------|-------|-----------|
| SimulationTests | 13 | Allow policy evaluation, deny policy evaluation, failed allow, all operators, null handling, attribute merging, first-source-wins, no-rules default match, input immutability, multi-rule AND logic, rule results with actual values, missing field handling, public method accessibility |
| SimulationSecurityTests | 3 | Policy object immutability, deny override identification, deny not-matched behavior |
| SimulationRegressionTests | 4 | Policy creation unchanged, rule validation unchanged, PolicyEvaluationResult factory methods unchanged, cache key generation unchanged |

### Test Categories

- **Unit Tests:** Simulation result generation, operator evaluation, attribute merging, explainability output
- **Security Tests:** Policy object immutability (simulation doesn't modify domain objects)
- **Regression Tests:** Existing policy/rule/cache behavior unchanged

---

## 9. Build Status

| Service | Errors | Warnings |
|---------|--------|----------|
| Identity.Api | 0 | 0 |
| Fund.Api | 0 | 0 |
| CareConnect.Api | 0 | 0 |
| BuildingBlocks.Tests | 0 | 1 (pre-existing CS8619) |

---

## 10. Known Limitations

1. **User lookup requires GUID.** The simulator requires a user ID (GUID), not an email or username. The UI provides a text input; a user search feature would improve UX.

2. **Endpoint-level simulation is permission-based.** The simulator evaluates a permission code, not a route path. A route→permission mapping layer could be added as an enhancement.

3. **Draft policy field validation is lenient.** Draft rule fields are not validated against `PolicyRule.SupportedFields` at the API level. Invalid fields will simply not match any attributes, producing a clear "actual: (null)" result.

4. **No cross-service attribute resolution.** The simulation service does not call `IAttributeProvider.GetUserAttributesAsync()` because it requires a `ClaimsPrincipal` which isn't available for the target user. Attributes come from the resource/request context provided by the admin.

5. **Concurrent simulation requests are not coalesced.** Unlike runtime evaluation, simulation does not use stampede protection — each simulation runs independently.

6. **Control Center UI is single-tenant per simulation.** PlatformAdmin can switch tenants, but each simulation targets one tenant at a time.

---

## 11. Assumptions

1. **Gateway enforces authentication.** The simulation endpoint trusts that the gateway has validated the caller's JWT.

2. **Tenant and user IDs are valid GUIDs.** The endpoint validates existence but relies on GUID format from the client.

3. **Draft policy testing is one-shot.** Each simulation request includes at most one draft policy. Testing multiple draft policies requires multiple simulation calls.

4. **Policy version reflects current state.** The simulation uses the current `IPolicyVersionProvider.CurrentVersion` — it does not support point-in-time version queries.

5. **Audit events are fire-and-forget.** Simulation audit logging uses the same `_ = auditClient.IngestAsync()` pattern as other admin endpoints — audit delivery does not block the simulation response.

---

## Files Modified

### New Files

| File | Description |
|------|-------------|
| `Identity.Application/Interfaces/IAuthorizationSimulationService.cs` | Service interface + DTOs |
| `Identity.Infrastructure/Services/AuthorizationSimulationService.cs` | Service implementation |
| `apps/control-center/src/app/authorization-simulator/page.tsx` | Simulator page |
| `apps/control-center/src/app/authorization-simulator/simulator-form.tsx` | Interactive form component |
| `apps/control-center/src/app/authorization-simulator/actions.ts` | Server action |
| `apps/control-center/src/app/authorization-simulator/loading.tsx` | Loading state |

### Modified Files

| File | Change |
|------|--------|
| `Identity.Infrastructure/Services/PolicyEvaluationService.cs` | `EvaluatePolicy`, `EvaluateRule`, `EvaluateOperator`, `MergeAttributes` changed from `private static` to `public static` |
| `Identity.Infrastructure/DependencyInjection.cs` | Added `IAuthorizationSimulationService` registration |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Added `POST /api/admin/authorization/simulate` endpoint + handler |
| `apps/control-center/src/lib/routes.ts` | Added `authorizationSimulator` route |
| `apps/control-center/src/lib/nav.ts` | Added Simulator nav entry |
| `apps/control-center/src/lib/control-center-api.ts` | Added `simulation.simulate()` method |
| `BuildingBlocks.Tests/PolicyEvaluationTests.cs` | Added 20 simulation tests |
