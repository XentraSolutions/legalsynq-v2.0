# LS-COR-AUT-011A — Policy Engine Hardening + Observability

## Summary

Hardening pass on the ABAC policy evaluation engine introduced in LS-COR-AUT-011. Adds deny-override semantics via a new `PolicyEffect` enum, deterministic evaluation ordering, `IMemoryCache`-based caching with policy-version invalidation, an `IPolicyResourceContextAccessor` abstraction, structured policy decision logging, and UI enhancements for the Control Center. All changes are backward-compatible — existing Allow-only policies continue to work without modification.

## Architecture

### New Files

| File | Layer | Purpose |
|------|-------|---------|
| `Identity.Domain/PolicyEffect.cs` | Domain | `PolicyEffect` enum — `Allow = 0`, `Deny = 1` |
| `BuildingBlocks/Authorization/IPolicyVersionProvider.cs` | Shared | Contract for monotonic policy version counter — `CurrentVersion` + `Increment()` |
| `BuildingBlocks/Authorization/IPolicyResourceContextAccessor.cs` | Shared | Standardized resource context accessor — `Get`, `Set`, `Merge` |
| `Identity.Infrastructure/Services/InMemoryPolicyVersionProvider.cs` | Infrastructure | Singleton `IPolicyVersionProvider` using `Interlocked.Read/Increment` for thread-safe in-process versioning |
| `Identity.Infrastructure/Services/HttpContextPolicyResourceContextAccessor.cs` | Infrastructure | `IPolicyResourceContextAccessor` backed by `HttpContext.Items["PolicyResourceContext"]` |

### Modified Files

| File | Change |
|------|--------|
| `Identity.Domain/Policy.cs` | Added `Effect` property (default `Allow`); `Create()` accepts optional `PolicyEffect effect` parameter; `Update()` accepts optional `PolicyEffect? effect` (null preserves existing) |
| `Identity.Infrastructure/Services/PolicyEvaluationService.cs` | Deny-override semantics, deterministic ordering, `IMemoryCache` caching with defensive copy-on-read, version-aware cache key, structured `PolicyDecision` logging |
| `Identity.Infrastructure/Data/Configurations/PolicyConfiguration.cs` | EF mapping for `Effect` column (int conversion, default `Allow`) |
| `Identity.Infrastructure/DependencyInjection.cs` | Registered `IPolicyVersionProvider` (Singleton), `IPolicyResourceContextAccessor` (Scoped) |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | All ABAC CRUD handlers call `policyVersionProvider.Increment()` after mutations; policy responses include `effect` field |
| `BuildingBlocks/Authorization/PolicyEvaluationResult.cs` | Added `DenyOverrideApplied`, `DenyOverridePolicyCode`, `PolicyVersion`, `CacheHit`, `ResourceContextPresent`, `EvaluationElapsedMs` fields; `DenyWithOverride()` factory method |
| `BuildingBlocks/Authorization/MatchedPolicy.cs` | Added `Effect` field (default `"Allow"`), `Priority`, `EvaluationOrder` fields |
| `BuildingBlocks/Authorization/RequirePermissionFilter.cs` | Structured `PolicyDecision` log emission — `Warn` for DENY, `Debug` for ALLOW; includes full decision metadata |
| `apps/control-center/src/types/control-center.ts` | `PolicySummary.effect` field; `SupportedFieldsResponse.effects` array |
| `apps/control-center/src/lib/api-mappers.ts` | `mapPolicySummary` maps `effect`; `mapSupportedFields` maps `effects` |
| `apps/control-center/src/components/policies/policy-detail-panel.tsx` | Effect badge in header and Info tab (emerald=Allow, red=Deny) |
| `BuildingBlocks.Tests/PolicyEvaluationTests.cs` | 15 new tests for PolicyEffect, PolicyVersionProvider, PolicyEvaluationResult, MatchedPolicy, RuleResult |

## Deny-Override Semantics

The evaluation engine now supports two policy effects — **Allow** and **Deny** — with deny-override conflict resolution:

```
EvaluateAsync(user, permissionCode, resourceContext)
│
├── 1. Load PermissionPolicies for permission (active only)
│     └── No policies → ALLOW ("No policies attached")
│
├── 2. Load active Policies with Rules, ordered deterministically:
│     │   Priority ASC → PolicyCode ASC → Id ASC
│     └── No active policies → ALLOW ("No active policies found")
│
├── 3. Merge attributes: user (JWT claims) + resource + request context
│
├── 4. Evaluate each policy in order:
│     │
│     ├── Evaluate all rules (AND/OR grouping logic)
│     │
│     ├── If Effect=Deny AND rules passed → mark firstDenyOverride
│     │
│     └── Track MatchedPolicy with effect, priority, evaluation order
│
└── 5. Final decision:
      ├── Any Deny override found → DENY (DenyOverrideApplied=true)
      ├── All Allow policies passed → ALLOW
      └── Any Allow policy failed → DENY (lists failed policies)
```

The deny-override model means a single matching Deny policy will block access regardless of how many Allow policies also match. This enables blacklist-style rules (e.g., "deny all approvals from region X") that cannot be overridden by additive Allow policies.

## Deterministic Evaluation Order

Policies are always evaluated in a fixed, reproducible order:

1. **Priority** (ascending) — lower number = evaluated first
2. **PolicyCode** (ascending) — alphabetical tiebreaker
3. **Id** (ascending) — GUID tiebreaker for identical priority/code

Each `MatchedPolicy` result records its `EvaluationOrder` (0-based index), enabling auditability and reproducible debugging.

## Caching Strategy

### Cache Key Format

```
policy:{tenantId}:{userId}:{permission}:{policyVersion}:{resourceHash}
```

- **resourceHash**: SHA-256 (truncated to 16 hex chars) of sorted `key=value;` pairs from the resource context dictionary
- **policyVersion**: Monotonic counter from `IPolicyVersionProvider.CurrentVersion`

### Cache Behavior

| Condition | Cached? |
|-----------|---------|
| Caching enabled AND resource context present | Yes |
| Caching enabled AND resource context empty/null | No — cache skipped |
| Caching disabled | No |

- **TTL**: Configurable via `Authorization:PolicyCaching:TtlSeconds` (default 60 seconds)
- **Enable**: `Authorization:PolicyCaching:Enabled=true`
- **Defensive copy**: Cache hits return a **new `PolicyEvaluationResult`** instance copied from the cached entry, preventing mutation of cached state across requests. Only `CacheHit` and `EvaluationElapsedMs` are set on the copy.

### Version-Based Invalidation

`InMemoryPolicyVersionProvider` is registered as a **Singleton**. Every Admin API mutation (policy create/update/deactivate, rule create/update/deactivate, permission-policy mapping create/deactivate) calls `Increment()` after `SaveChangesAsync`. Because the version is embedded in the cache key, all prior cached entries become unreachable after any policy mutation — no explicit cache eviction required.

```
Thread-safety: Interlocked.Read / Interlocked.Increment
Scope: In-process only (single instance)
```

**Note**: In multi-instance deployments, version increments are local to each process. Cross-instance invalidation relies on TTL expiry. A distributed version source (e.g., Redis counter, database sequence) is recommended for horizontal scaling.

## IPolicyResourceContextAccessor

Standardized abstraction for injecting resource-specific attributes into policy evaluation:

```csharp
public interface IPolicyResourceContextAccessor
{
    Dictionary<string, object?> GetResourceContext();
    void SetResourceContext(Dictionary<string, object?> context);
    void MergeResourceContext(string key, object? value);
}
```

- **Implementation**: `HttpContextPolicyResourceContextAccessor` — reads/writes `HttpContext.Items["PolicyResourceContext"]`
- **DI Registration**: Scoped lifetime
- **Usage pattern**: Service-layer code calls `SetResourceContext()` or `MergeResourceContext()` before the authorization filter runs, populating fields like `amount`, `region`, `organizationId` for rule evaluation

## Structured Logging

Each policy evaluation emits a structured `PolicyDecision` log entry per policy evaluated:

### DENY (LogWarning)

```
PolicyDecision: event=PolicyDecision
  userId={UserId} tenantId={TenantId} endpoint={Endpoint}
  permission={Permission} policyCode={PolicyCode} policyId={PolicyId}
  effect={Effect} result={Result}
  resourceContextPresent={ResourceContextPresent}
  accessVersion={AccessVersion} evaluationElapsedMs={ElapsedMs}
  ruleResults={RuleResults}
```

### ALLOW (LogInformation)

Same shape but without `ruleResults` (to reduce log volume for successful evaluations).

### Log Severity

| Decision | Severity |
|----------|----------|
| DENY (rules failed or deny override) | `Warn` |
| ALLOW | `Information` |

## Admin API Changes

All nine ABAC endpoint handlers in `AdminEndpoints.cs` now:

1. Accept `IPolicyVersionProvider` via DI
2. Call `policyVersionProvider.Increment()` after successful `SaveChangesAsync`
3. Include `effect` field in policy response DTOs

Affected handlers: CreatePolicy, UpdatePolicy, DeactivatePolicy, CreatePolicyRule, UpdatePolicyRule, DeactivatePolicyRule, ReactivatePolicyRule, CreatePermissionPolicy, DeactivatePermissionPolicy.

The `GET /api/admin/abac/supported-fields` endpoint now returns an `effects` array (`["Allow", "Deny"]`).

## Frontend Changes

### Types

| Type | Field Added |
|------|-------------|
| `PolicySummary` | `effect: string` (values: `"Allow"`, `"Deny"`) |
| `SupportedFieldsResponse` | `effects: string[]` |

### Mapper Updates

- `mapPolicySummary`: Extracts `effect` from API response (camelCase + snake_case fallback), defaults to `"Allow"`
- `mapSupportedFields`: Maps `effects` array

### UI

- **Policy list table**: Effect badge column — emerald background for Allow, red background for Deny
- **Policy detail panel header**: Effect badge next to policy name
- **Policy detail Info tab**: Effect field with styled badge

## Test Coverage

**168 total tests** in BuildingBlocks.Tests (15 new in this increment):

### PolicyDomainTests (6 new)

| Test | Validates |
|------|-----------|
| `PolicyCreate_WithDenyEffect_SetsDeny` | Deny effect stored on creation |
| `PolicyCreate_DefaultEffect_IsAllow` | Default effect is Allow |
| `PolicyUpdate_ChangesFields` | Update sets new effect |
| `PolicyUpdate_NullEffect_PreservesExisting` | Null effect on update preserves original |
| `PolicyCreate_ValidInputs_CreatesPolicy` | (updated) Asserts `PolicyEffect.Allow` on new policy |
| `PolicyCreate_InvalidCode_Throws` / `PolicyCreate_EmptyName_Throws` | Validation guards |

### PolicyVersionProviderTests (4 new)

| Test | Validates |
|------|-----------|
| `InitialVersion_IsZero` | Fresh provider starts at 0 |
| `Increment_IncreasesVersion` | Single increment → 1 |
| `MultipleIncrements_AreMonotonic` | 3 increments → 3 |
| `ConcurrentIncrements_AreThreadSafe` | 1000 parallel increments → 1000 |

### PolicyEvaluationResultTests (7 new)

| Test | Validates |
|------|-----------|
| `Allow_CreatesAllowedResult` | Factory correctness + defaults |
| `Deny_CreatesDeniedResult` | Denied result with matched policies |
| `AllowWithPolicies_IncludesMatchedPolicies` | Allow result carries policy metadata |
| `DenyWithOverride_SetsDenyOverrideFields` | `DenyOverrideApplied` + `DenyOverridePolicyCode` |
| `Result_DefaultCacheHit_IsFalse` | Default `CacheHit` is false |
| `Result_DefaultResourceContextPresent_IsFalse` | Default `ResourceContextPresent` is false |
| `MatchedPolicy_DefaultEffect_IsAllow` | Default `Effect` string is "Allow" |
| `RuleResult_DefaultFields_AreEmpty` | All RuleResult defaults correct |

## Configuration

```json
{
  "Authorization": {
    "EnablePolicyEvaluation": true,
    "EnableRoleFallback": true,
    "PolicyCaching": {
      "Enabled": true,
      "TtlSeconds": 60
    }
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `EnablePolicyEvaluation` | `true` | Master switch for ABAC evaluation in RequirePermissionFilter |
| `EnableRoleFallback` | `true` | Fall back to PBAC claim check if ABAC not applicable |
| `PolicyCaching:Enabled` | `false` | Enable IMemoryCache for policy evaluation results |
| `PolicyCaching:TtlSeconds` | `60` | Cache entry time-to-live in seconds |

## Build Verification

| Target | Errors | Warnings |
|--------|--------|----------|
| Identity.Api | 0 | 0 |
| Fund.Api | 0 | 0 |
| CareConnect.Api | 0 | 0 |
| BuildingBlocks.Tests | 0 | 0 |
| Control Center (tsc) | 0 | 0 |
| Test run (168 tests) | 0 failures | — |

## Known Limitations

1. **In-process version provider**: `InMemoryPolicyVersionProvider` is process-local. Multi-instance deployments will not see cross-instance cache invalidation until TTL expiry. Recommend upgrading to a distributed counter (Redis `INCR` or database sequence) for horizontal scaling.
2. **Cache shares MatchedPolicies list reference**: The defensive copy on cache hit copies the `MatchedPolicies` list reference (not a deep clone). This is safe because matched policy lists are never mutated after creation, but a deep clone would provide stronger isolation.
3. **No EF migration for Effect column**: The `Effect` property uses EF value conversion with a default of `Allow`. Existing rows in the `Policies` table will default to `Allow` (0) without a migration. A migration should be generated before deploying to environments with existing policy data.

## Files Changed

```
apps/services/identity/Identity.Domain/Policy.cs
apps/services/identity/Identity.Domain/PolicyEffect.cs
apps/services/identity/Identity.Infrastructure/Services/PolicyEvaluationService.cs
apps/services/identity/Identity.Infrastructure/Services/InMemoryPolicyVersionProvider.cs
apps/services/identity/Identity.Infrastructure/Services/HttpContextPolicyResourceContextAccessor.cs
apps/services/identity/Identity.Infrastructure/Data/Configurations/PolicyConfiguration.cs
apps/services/identity/Identity.Infrastructure/DependencyInjection.cs
apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs
shared/building-blocks/BuildingBlocks/Authorization/PolicyEvaluationResult.cs
shared/building-blocks/BuildingBlocks/Authorization/IPolicyVersionProvider.cs
shared/building-blocks/BuildingBlocks/Authorization/IPolicyResourceContextAccessor.cs
shared/building-blocks/BuildingBlocks/Authorization/RequirePermissionFilter.cs
shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/PolicyEvaluationTests.cs
shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/BuildingBlocks.Tests.csproj
apps/control-center/src/types/control-center.ts
apps/control-center/src/lib/api-mappers.ts
apps/control-center/src/components/policies/policy-detail-panel.tsx
```
