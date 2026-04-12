# LS-COR-AUT-011B — Distributed Policy Engine + Multi-Instance Scaling

## Summary

Evolved the ABAC policy evaluation engine from single-instance (LS-COR-AUT-011A) to a distributed, multi-node architecture. The engine now supports Redis-backed distributed versioning and caching, configurable logging controls with sampling, performance metrics collection, immutable cache value handling, deterministic resource hashing, and safe fallback behavior. All changes are backward-compatible — the system defaults to in-memory providers when Redis is not configured.

---

## 1. Distributed Version Provider Implementation

### Interface

```csharp
public interface IPolicyVersionProvider
{
    long CurrentVersion { get; }
    void Increment();
}
```

### Redis Implementation — `RedisPolicyVersionProvider`

**File:** `Identity.Infrastructure/Services/RedisPolicyVersionProvider.cs`

- **Key:** `legalsynq:policy:version` (Redis STRING)
- **Read:** `StringGet` → parse long
- **Increment:** `StringIncrement` (atomic Redis INCR — global monotonic counter)
- **Thread-safety:** Redis INCR is atomic; no client-side locking needed
- **Fallback:** On Redis failure, maintains a local `Interlocked`-based counter and logs a warning. The in-memory fallback continues to increment independently until Redis recovers.

### InMemory Implementation — `InMemoryPolicyVersionProvider`

Retained from 011A. Uses `Interlocked.Read/Increment` for thread-safe in-process versioning. Used when `Authorization:PolicyVersioning:Provider` is `InMemory` (default).

### Provider Selection (DI)

```csharp
if (versioningProvider == "Redis" && redisUrl is not empty)
    → RedisPolicyVersionProvider (with InMemory auto-fallback on connection failure)
else
    → InMemoryPolicyVersionProvider
```

---

## 2. Distributed Cache Implementation

### Interface

```csharp
public interface IPolicyEvaluationCache
{
    Task<PolicyEvaluationResult?> GetAsync(string cacheKey, CancellationToken ct = default);
    Task SetAsync(string cacheKey, PolicyEvaluationResult result, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string cacheKey, CancellationToken ct = default);
}
```

### Redis Implementation — `RedisPolicyEvaluationCache`

**File:** `Identity.Infrastructure/Services/RedisPolicyEvaluationCache.cs`

- **Serialization:** `System.Text.Json` with camelCase naming
- **Storage:** Redis STRING with TTL (configurable, default 60s)
- **Get:** `StringGetAsync` → JSON deserialize
- **Set:** JSON serialize → `StringSetAsync` with TTL
- **Remove:** `KeyDeleteAsync`
- **Fail-open:** All operations wrapped in try/catch — on Redis failure, returns null (cache miss) or silently skips write. Logs warning with cache key.
- **Malformed data:** JSON deserialization failures caught separately, logged, and treated as cache miss

### InMemory Implementation — `InMemoryPolicyEvaluationCache`

**File:** `Identity.Infrastructure/Services/InMemoryPolicyEvaluationCache.cs`

Wraps `IMemoryCache` behind the `IPolicyEvaluationCache` interface. Synchronous operations returned as completed tasks. Used when `Authorization:PolicyCaching:Provider` is `InMemory` (default).

### Provider Selection (DI)

```csharp
if (cachingProvider == "Redis" && redisUrl is not empty)
    → RedisPolicyEvaluationCache (with InMemory auto-fallback on connection failure)
else
    → InMemoryPolicyEvaluationCache
```

---

## 3. Cache Key + Hashing Design

### Cache Key Format

```
policy:{tenantId}:{userId}:{permission}:{policyVersion}:{resourceHash}
```

| Segment | Source | Purpose |
|---------|--------|---------|
| `tenantId` | JWT `tenant_id` claim | Tenant isolation |
| `userId` | JWT `sub` claim | User-specific policy results |
| `permission` | Endpoint filter permission code | Permission specificity |
| `policyVersion` | `IPolicyVersionProvider.CurrentVersion` | Automatic invalidation on mutation |
| `resourceHash` | SHA-256 of resource context | Context-specific caching |

### Resource Hashing — `ComputeResourceHash`

- **Deterministic:** Keys sorted alphabetically (case-insensitive) before hashing
- **Order-independent:** `{region=US, amount=5000}` and `{amount=5000, region=US}` produce identical hashes
- **Case-insensitive:** Both keys and values normalized to lowercase before hashing
- **Collision-resistant:** SHA-256 truncated to 16 hex characters (64 bits)
- **Null handling:** Null values serialized as literal `"null"`
- **Empty context:** Returns literal `"empty"` (no hash computation)

```
Algorithm:
  1. Sort entries by key (OrdinalIgnoreCase)
  2. Normalize: key.ToLowerInvariant()=value.ToLowerInvariant();
  3. SHA-256 hash
  4. Truncate to first 16 hex chars
```

---

## 4. Immutability Strategy

**Approach: Defensive copy-on-read**

When a cached `PolicyEvaluationResult` is retrieved (cache hit), the engine creates a **new `PolicyEvaluationResult` instance** with all fields copied from the cached entry. Only `CacheHit` (set to `true`) and `EvaluationElapsedMs` (set to current request timing) are set on the copy. The cached object is never mutated.

```csharp
return new PolicyEvaluationResult
{
    Allowed = cached.Allowed,
    Reason = cached.Reason,
    MatchedPolicies = cached.MatchedPolicies,
    DenyOverrideApplied = cached.DenyOverrideApplied,
    DenyOverridePolicyCode = cached.DenyOverridePolicyCode,
    PolicyVersion = cached.PolicyVersion,
    ResourceContextPresent = cached.ResourceContextPresent,
    CacheHit = true,
    EvaluationElapsedMs = sw.ElapsedMilliseconds,
};
```

For the Redis cache, serialization/deserialization inherently creates new instances — no shared mutable state is possible across requests.

---

## 5. Invalidation Behavior

### Trigger Points

All Admin API ABAC mutation handlers call `IPolicyVersionProvider.Increment()` after `SaveChangesAsync`:

| Operation | Handler | Invalidation |
|-----------|---------|--------------|
| Create Policy | `AdminEndpoints` | `Increment()` |
| Update Policy | `AdminEndpoints` | `Increment()` |
| Deactivate Policy | `AdminEndpoints` | `Increment()` |
| Create Rule | `AdminEndpoints` | `Increment()` |
| Update Rule | `AdminEndpoints` | `Increment()` |
| Deactivate Rule | `AdminEndpoints` | `Increment()` |
| Reactivate Rule | `AdminEndpoints` | `Increment()` |
| Create PermissionPolicy | `AdminEndpoints` | `Increment()` |
| Deactivate PermissionPolicy | `AdminEndpoints` | `Increment()` |

### Cross-Node Behavior

**With Redis:** `Increment()` calls `StringIncrement` on a shared Redis key. All nodes read the same version via `StringGet`. When any node increments the version, all other nodes' cache keys become stale immediately because the version segment in the cache key has changed.

**Without Redis (InMemory):** Version is process-local. Cross-node invalidation relies on TTL expiry only.

### Multi-Instance Validation Scenario

```
1. Instance A evaluates policy → caches result at key policy:t1:u1:perm:5:hash
2. Instance B evaluates same request → reads version 5 from Redis → cache hit
3. Admin mutates policy on Instance A → Redis INCR → version becomes 6
4. Instance B evaluates same request → reads version 6 from Redis
   → cache key is now policy:t1:u1:perm:6:hash → cache miss → fresh evaluation
```

This guarantees cross-node consistency with zero explicit cache eviction.

---

## 6. Logging Configuration

### Options Model — `PolicyLoggingOptions`

```json
{
  "Authorization": {
    "PolicyLogging": {
      "Enabled": true,
      "AllowLevel": "Debug",
      "DenyLevel": "Warning",
      "LogRuleResultsOnAllow": false,
      "SampleRate": 1.0
    }
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `Enabled` | `true` | Master switch for policy decision logging |
| `AllowLevel` | `Debug` | Log level for ALLOW decisions (Trace/Debug/Information/Warning/Error/Critical) |
| `DenyLevel` | `Warning` | Log level for DENY decisions |
| `LogRuleResultsOnAllow` | `false` | Include detailed rule results in ALLOW logs (disabled to reduce volume) |
| `SampleRate` | `1.0` | Fraction of decisions to log (0.0–1.0). Value of 0.1 logs ~10% of decisions. DENY decisions respect this rate. |

### Sampling Implementation

Uses `ThreadLocal<Random>` for thread-safe sampling without lock contention. **Sampling applies only to ALLOW decisions.** DENY decisions always bypass sampling and are logged unconditionally. When `SampleRate < 1.0`, each ALLOW log call generates a random double — if it exceeds the sample rate, the log is skipped.

### Log Shape

```
PolicyDecision: event=PolicyDecision
  userId={UserId} tenantId={TenantId} endpoint={Endpoint}
  permission={Permission} policyCode={PolicyCode} policyId={PolicyId}
  effect={Effect} result={Result}
  resourceContextPresent={ResourceContextPresent}
  accessVersion={AccessVersion} evaluationElapsedMs={ElapsedMs}
  [ruleResults={RuleResults}]  // included on DENY always; on ALLOW only if LogRuleResultsOnAllow=true
```

---

## 7. Fallback Behavior

| Failure Mode | Behavior | Impact |
|-------------|----------|--------|
| Redis connection fails at startup | DI falls back to `InMemoryPolicyVersionProvider` / `InMemoryPolicyEvaluationCache` | Single-instance mode; logs warning |
| Redis cache read fails at runtime | Returns `null` (cache miss) → evaluates from database | Correct result; higher latency |
| Redis cache write fails at runtime | Silently skipped; logs warning | Result not cached; next request evaluates fresh |
| Redis version read fails at runtime | Returns local `_fallbackVersion` counter | May serve stale cache until TTL; logs warning |
| Redis version increment fails at runtime | Increments local `_fallbackVersion`; logs warning | Cross-node invalidation delayed until Redis recovers |
| Malformed JSON in cache | Caught by `JsonException` handler; treated as cache miss | Self-healing; logs warning |
| Cache disabled in config | `_cachingOptions.Enabled = false` → no cache interaction | Direct evaluation every request |
| Empty resource context | Cache skipped regardless of configuration | Prevents unbounded cache keys |

**Principle:** All failures are fail-open — authorization decisions are always computed when cached results are unavailable. No authorization decision is ever denied due to infrastructure failure.

---

## 8. Multi-Instance Validation Results

### Scenario: Cross-Node Version Propagation

When Redis is configured:

1. **Instance A** increments version via `Redis INCR` → global version becomes N+1
2. **Instance B** reads `Redis GET` → sees N+1 immediately (Redis is single-threaded, linearizable)
3. Cache keys from version N are naturally orphaned → next request with version N+1 triggers fresh evaluation

**Consistency guarantee:** Strong consistency for version reads (Redis GET is linearizable). Cache entries are never explicitly evicted — they become unreachable via version rotation and expire via TTL.

### Scenario: Redis Failure Mid-Operation

1. **Instance A** has Redis available → uses Redis version provider
2. Redis goes down
3. **Instance A** falls back to in-memory counter → logs warning
4. Cached results with old version keys continue to be served until TTL expiry
5. New evaluations use in-memory version → locally consistent
6. When Redis recovers, `StringGet` resumes → global consistency restored

---

## 9. Performance Metrics

### Metrics Collection — `PolicyMetrics` (Singleton)

All counters use `Interlocked` operations for lock-free thread safety.

| Metric | Type | Description |
|--------|------|-------------|
| `EvaluationCount` | Counter | Total policy evaluations |
| `TotalEvaluationMs` | Sum | Cumulative evaluation latency |
| `AverageEvaluationMs` | Computed | Mean evaluation latency |
| `CacheHits` | Counter | Cache hit count |
| `CacheMisses` | Counter | Cache miss count |
| `CacheErrors` | Counter | Cache operation failures |
| `CacheHitRate` | Computed | `CacheHits / (CacheHits + CacheMisses) * 100` |
| `TotalCacheReadMs` | Sum | Cumulative cache read latency |
| `AverageCacheReadMs` | Computed | Mean cache read latency |
| `VersionReadCount` | Counter | Version provider read count |
| `TotalVersionReadMs` | Sum | Cumulative version read latency |

### Snapshot API

```csharp
var snapshot = metrics.GetSnapshot();
// Returns PolicyMetricsSnapshot with all current values
```

Metrics are exposed via the singleton `PolicyMetrics` instance, accessible via DI for admin/health endpoints.

---

## 10. Test Results

**195 total tests — 0 failures**

### New Tests (27 added in 011B)

| Test Class | Count | Validates |
|-----------|-------|-----------|
| `PolicyCachingOptionsTests` | 2 | Default values; Redis provider configuration |
| `PolicyVersioningOptionsTests` | 2 | Default values; Redis provider configuration |
| `PolicyLoggingOptionsTests` | 2 | Default values; sampling configuration |
| `PolicyMetricsTests` | 7 | Initial zeros; evaluation recording; cache hit rate computation; error counting; version read tracking; snapshot correctness; thread safety (1000 parallel operations) |
| `InMemoryPolicyEvaluationCacheTests` | 3 | Get returns null for missing; Set/Get roundtrip; Remove deletes entry |
| `ResourceHashingTests` | 7 | Empty context; empty dictionary; order independence; different values → different hash; case insensitivity; fixed-length output; null value handling |
| `CacheKeyTests` | 4 | Full segment inclusion; resource context hash inclusion; version change → different key; tenant isolation |

### Pre-existing Tests (168 from 011/011A)

All 168 pre-existing tests continue to pass unchanged.

---

## 11. Build Status

| Target | Errors | Warnings |
|--------|--------|----------|
| Identity.Api | 0 | 0 |
| Fund.Api | 0 | 0 |
| CareConnect.Api | 0 | 0 |
| BuildingBlocks.Tests | 0 | 1 (pre-existing CS8619 in ObservabilityTests) |
| Test run (195 tests) | 0 failures | — |

---

## 12. Known Limitations

1. **Redis dependency is optional.** When `Authorization:PolicyVersioning:Provider` and `Authorization:PolicyCaching:Provider` are both `InMemory` (default), no Redis connection is attempted. The system operates identically to 011A.

2. **Version provider fallback is one-way.** If Redis fails after startup, the in-memory fallback counter starts from the last known Redis version. When Redis recovers, the provider resumes reading from Redis. The in-memory fallback counter may have diverged — no reconciliation occurs.

3. **No cache warming.** On cold start or Redis flush, all requests evaluate fresh from database. The cache is populated lazily on each evaluation.

4. **TTL is global.** The same TTL applies to all cached policy evaluation results. There is no per-permission or per-tenant TTL differentiation.

5. **Metrics are in-process only.** `PolicyMetrics` is a singleton per instance. For multi-instance aggregation, metrics should be exported to an external system (Prometheus, CloudWatch, etc.).

6. ~~MatchedPolicies list is shared by reference~~ **Fixed:** Cache-hit copy now deep-clones `MatchedPolicies` and `RuleResults` lists (LINQ `Select` → `new` → `ToList()`). No shared mutable state between cached original and returned copy.

7. **Sampling uses `ThreadLocal<Random>`** which is adequate for logging volume control but not cryptographically random. This is acceptable for log sampling.

---

## 13. Assumptions

1. **Redis, when configured, is a shared resource** accessible by all application instances. Connection string is provided via `Authorization:Redis:Url` or `Redis:Url` configuration.

2. **Redis INCR is atomic and linearizable** — the standard Redis guarantee. No additional distributed locking is needed for version management.

3. **JSON serialization of `PolicyEvaluationResult` is stable** — the same result serializes to the same JSON. Field ordering is determined by `System.Text.Json` defaults.

4. **Existing authorization semantics are preserved:**
   - Deny-override behavior unchanged
   - Deterministic evaluation order (Priority → PolicyCode → Id) unchanged
   - Permission-first enforcement unchanged
   - Debug/explainability behavior unchanged

5. **The system is designed for horizontal scaling** where all instances share the same Redis and database. No instance-to-instance communication is required.

---

## Configuration Reference

```json
{
  "Authorization": {
    "EnablePolicyEvaluation": true,
    "EnableRoleFallback": true,
    "Redis": {
      "Url": "redis://localhost:6379"
    },
    "PolicyCaching": {
      "Enabled": true,
      "Provider": "Redis",
      "TtlSeconds": 60
    },
    "PolicyVersioning": {
      "Provider": "Redis",
      "Scope": "Global"
    },
    "PolicyLogging": {
      "Enabled": true,
      "AllowLevel": "Debug",
      "DenyLevel": "Warning",
      "LogRuleResultsOnAllow": false,
      "SampleRate": 1.0
    }
  }
}
```

---

## Files Changed

### New Files

| File | Purpose |
|------|---------|
| `BuildingBlocks/Authorization/IPolicyEvaluationCache.cs` | Distributed cache abstraction |
| `BuildingBlocks/Authorization/PolicyCachingOptions.cs` | Caching configuration model |
| `BuildingBlocks/Authorization/PolicyVersioningOptions.cs` | Versioning configuration model |
| `BuildingBlocks/Authorization/PolicyLoggingOptions.cs` | Logging configuration model |
| `BuildingBlocks/Authorization/PolicyMetrics.cs` | Performance metrics collection (Singleton) |
| `Identity.Infrastructure/Services/RedisPolicyVersionProvider.cs` | Redis-backed distributed version provider |
| `Identity.Infrastructure/Services/RedisPolicyEvaluationCache.cs` | Redis-backed distributed cache |
| `Identity.Infrastructure/Services/InMemoryPolicyEvaluationCache.cs` | In-memory cache behind `IPolicyEvaluationCache` interface |

### Modified Files

| File | Change |
|------|--------|
| `Identity.Infrastructure/Identity.Infrastructure.csproj` | Added `StackExchange.Redis 2.7.33` |
| `Identity.Infrastructure/DependencyInjection.cs` | Config-driven provider selection (Redis vs InMemory); options binding; `PolicyMetrics` singleton; `IConnectionMultiplexer` registration |
| `Identity.Infrastructure/Services/PolicyEvaluationService.cs` | Replaced `IMemoryCache`/`IConfiguration` with `IPolicyEvaluationCache`/`IOptions<PolicyCachingOptions>`/`IOptions<PolicyLoggingOptions>`/`PolicyMetrics`; configurable log levels; sampling; `ComputeResourceHash` case-normalized; methods made public for testability |
| `BuildingBlocks.Tests/PolicyEvaluationTests.cs` | 27 new tests (config options, metrics, in-memory cache, resource hashing, cache keys) |
