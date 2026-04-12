using Identity.Domain;
using BuildingBlocks.Authorization;
using Identity.Infrastructure.Services;
using System.Collections.Concurrent;

namespace BuildingBlocks.Tests;

public class PolicyDomainTests
{
    [Theory]
    [InlineData("SYNQ_FUND.approval.limit")]
    [InlineData("SYNQ_CARECONNECT.referral.region")]
    [InlineData("SYNQ_LIENS.lien.amount")]
    [InlineData("PRODUCT_X.domain.qualifier")]
    [InlineData("A1.a")]
    public void IsValidPolicyCode_ValidCodes_ReturnsTrue(string code)
    {
        Assert.True(Policy.IsValidPolicyCode(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("noprefix")]
    [InlineData("PRODUCT")]
    [InlineData("PRODUCT.")]
    [InlineData("product.domain")]
    [InlineData("PRODUCT.DOMAIN")]
    [InlineData("PRODUCT:domain")]
    [InlineData("PRODUCT.domain:action")]
    public void IsValidPolicyCode_InvalidCodes_ReturnsFalse(string code)
    {
        Assert.False(Policy.IsValidPolicyCode(code));
    }

    [Fact]
    public void PolicyCreate_ValidInputs_CreatesPolicy()
    {
        var policy = Policy.Create("SYNQ_FUND.approval.limit", "Approval Limit", "SYNQ_FUND", "Limits approval amounts", 10);

        Assert.Equal("SYNQ_FUND.approval.limit", policy.PolicyCode);
        Assert.Equal("Approval Limit", policy.Name);
        Assert.Equal("SYNQ_FUND", policy.ProductCode);
        Assert.Equal("Limits approval amounts", policy.Description);
        Assert.Equal(10, policy.Priority);
        Assert.Equal(PolicyEffect.Allow, policy.Effect);
        Assert.True(policy.IsActive);
        Assert.NotEqual(Guid.Empty, policy.Id);
    }

    [Fact]
    public void PolicyCreate_WithDenyEffect_SetsDeny()
    {
        var policy = Policy.Create("SYNQ_FUND.block.region", "Block Region", "SYNQ_FUND", effect: PolicyEffect.Deny);

        Assert.Equal(PolicyEffect.Deny, policy.Effect);
    }

    [Fact]
    public void PolicyCreate_DefaultEffect_IsAllow()
    {
        var policy = Policy.Create("SYNQ_FUND.default.allow", "Default Allow", "SYNQ_FUND");

        Assert.Equal(PolicyEffect.Allow, policy.Effect);
    }

    [Fact]
    public void PolicyCreate_InvalidCode_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Policy.Create("invalid-code", "Name", "PRODUCT"));
    }

    [Fact]
    public void PolicyCreate_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Policy.Create("SYNQ_FUND.approval.limit", "", "SYNQ_FUND"));
    }

    [Fact]
    public void PolicyDeactivate_SetsInactive()
    {
        var policy = Policy.Create("SYNQ_FUND.approval.limit", "Test", "SYNQ_FUND");
        Assert.True(policy.IsActive);

        policy.Deactivate();

        Assert.False(policy.IsActive);
        Assert.NotNull(policy.UpdatedAtUtc);
    }

    [Fact]
    public void PolicyUpdate_ChangesFields()
    {
        var policy = Policy.Create("SYNQ_FUND.approval.limit", "Original", "SYNQ_FUND", priority: 0);

        policy.Update("Updated", "New desc", 5, PolicyEffect.Deny);

        Assert.Equal("Updated", policy.Name);
        Assert.Equal("New desc", policy.Description);
        Assert.Equal(5, policy.Priority);
        Assert.Equal(PolicyEffect.Deny, policy.Effect);
    }

    [Fact]
    public void PolicyUpdate_NullEffect_PreservesExisting()
    {
        var policy = Policy.Create("SYNQ_FUND.approval.limit", "Original", "SYNQ_FUND", effect: PolicyEffect.Deny);

        policy.Update("Updated", null, 1);

        Assert.Equal(PolicyEffect.Deny, policy.Effect);
    }
}

public class PolicyRuleDomainTests
{
    [Theory]
    [InlineData("amount")]
    [InlineData("organizationId")]
    [InlineData("tenantId")]
    [InlineData("region")]
    [InlineData("caseId")]
    [InlineData("owner")]
    [InlineData("time")]
    [InlineData("ip")]
    [InlineData("status")]
    [InlineData("role")]
    [InlineData("department")]
    public void IsFieldSupported_ValidFields_ReturnsTrue(string field)
    {
        Assert.True(PolicyRule.IsFieldSupported(field));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unknown")]
    [InlineData("password")]
    public void IsFieldSupported_InvalidFields_ReturnsFalse(string field)
    {
        Assert.False(PolicyRule.IsFieldSupported(field));
    }

    [Fact]
    public void PolicyRuleCreate_ValidInputs_CreatesRule()
    {
        var policyId = Guid.NewGuid();

        var rule = PolicyRule.Create(
            policyId,
            PolicyConditionType.Attribute,
            "amount",
            RuleOperator.LessThanOrEqual,
            "50000",
            LogicalGroupType.And);

        Assert.Equal(policyId, rule.PolicyId);
        Assert.Equal(PolicyConditionType.Attribute, rule.ConditionType);
        Assert.Equal("amount", rule.Field);
        Assert.Equal(RuleOperator.LessThanOrEqual, rule.Operator);
        Assert.Equal("50000", rule.Value);
        Assert.Equal(LogicalGroupType.And, rule.LogicalGroup);
    }

    [Fact]
    public void PolicyRuleCreate_UnsupportedField_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PolicyRule.Create(Guid.NewGuid(), PolicyConditionType.Attribute, "unknown_field", RuleOperator.Equals, "val"));
    }

    [Fact]
    public void PolicyRuleCreate_NumericOperatorOnNonNumericField_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PolicyRule.Create(Guid.NewGuid(), PolicyConditionType.Attribute, "region", RuleOperator.GreaterThan, "5"));
    }

    [Fact]
    public void PolicyRuleCreate_NumericOperatorOnAmountField_Succeeds()
    {
        var rule = PolicyRule.Create(Guid.NewGuid(), PolicyConditionType.Attribute, "amount", RuleOperator.GreaterThan, "100");
        Assert.Equal(RuleOperator.GreaterThan, rule.Operator);
    }

    [Fact]
    public void PolicyRuleCreate_NumericOperatorOnTimeField_Succeeds()
    {
        var rule = PolicyRule.Create(Guid.NewGuid(), PolicyConditionType.Attribute, "time", RuleOperator.LessThan, "1700000000");
        Assert.Equal(RuleOperator.LessThan, rule.Operator);
    }

    [Fact]
    public void PolicyRuleCreate_EmptyValue_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PolicyRule.Create(Guid.NewGuid(), PolicyConditionType.Attribute, "amount", RuleOperator.Equals, ""));
    }

    [Fact]
    public void PolicyRuleUpdate_InvalidField_Throws()
    {
        var rule = PolicyRule.Create(Guid.NewGuid(), PolicyConditionType.Attribute, "amount", RuleOperator.Equals, "100");
        Assert.Throws<ArgumentException>(() =>
            rule.Update(PolicyConditionType.Attribute, "bad_field", RuleOperator.Equals, "100", LogicalGroupType.And));
    }

    [Fact]
    public void PolicyRuleUpdate_NumericOperatorOnNonNumericField_Throws()
    {
        var rule = PolicyRule.Create(Guid.NewGuid(), PolicyConditionType.Attribute, "amount", RuleOperator.Equals, "100");
        Assert.Throws<ArgumentException>(() =>
            rule.Update(PolicyConditionType.Attribute, "region", RuleOperator.GreaterThan, "5", LogicalGroupType.And));
    }

    [Fact]
    public void GetSupportedFields_ReturnsAllExpected()
    {
        var fields = PolicyRule.GetSupportedFields();
        Assert.Contains("amount", fields);
        Assert.Contains("organizationId", fields);
        Assert.Contains("tenantId", fields);
        Assert.Contains("region", fields);
        Assert.Contains("department", fields);
        Assert.Equal(11, fields.Count);
    }
}

public class PermissionPolicyDomainTests
{
    [Fact]
    public void PermissionPolicyCreate_ValidInputs_Creates()
    {
        var policyId = Guid.NewGuid();
        var pp = PermissionPolicy.Create("SYNQ_FUND.application:approve", policyId);

        Assert.Equal("SYNQ_FUND.application:approve", pp.PermissionCode);
        Assert.Equal(policyId, pp.PolicyId);
        Assert.True(pp.IsActive);
    }

    [Fact]
    public void PermissionPolicyCreate_EmptyPermissionCode_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PermissionPolicy.Create("", Guid.NewGuid()));
    }

    [Fact]
    public void PermissionPolicyCreate_EmptyPolicyId_StillCreates()
    {
        var pp = PermissionPolicy.Create("SYNQ_FUND.application:approve", Guid.Empty);
        Assert.Equal(Guid.Empty, pp.PolicyId);
    }

    [Fact]
    public void PermissionPolicyDeactivate_SetsInactive()
    {
        var pp = PermissionPolicy.Create("SYNQ_FUND.application:approve", Guid.NewGuid());
        Assert.True(pp.IsActive);

        pp.Deactivate();

        Assert.False(pp.IsActive);
    }
}

public class PolicyVersionProviderTests
{
    [Fact]
    public void InitialVersion_IsZero()
    {
        var provider = new InMemoryPolicyVersionProvider();
        Assert.Equal(0, provider.CurrentVersion);
    }

    [Fact]
    public void Increment_IncreasesVersion()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.Increment();
        Assert.Equal(1, provider.CurrentVersion);
    }

    [Fact]
    public void MultipleIncrements_AreMonotonic()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.Increment();
        provider.Increment();
        provider.Increment();
        Assert.Equal(3, provider.CurrentVersion);
    }

    [Fact]
    public void ConcurrentIncrements_AreThreadSafe()
    {
        var provider = new InMemoryPolicyVersionProvider();
        const int iterations = 1000;

        Parallel.For(0, iterations, _ => provider.Increment());

        Assert.Equal(iterations, provider.CurrentVersion);
    }

    [Fact]
    public void IsHealthy_AlwaysTrue_ForInMemory()
    {
        var provider = new InMemoryPolicyVersionProvider();
        Assert.True(provider.IsHealthy);
        Assert.False(provider.IsFrozen);
    }

    [Fact]
    public void GetVersion_Global_ReturnsSameAsCurrentVersion()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.Increment();
        provider.Increment();
        Assert.Equal(provider.CurrentVersion, provider.GetVersion(null));
        Assert.Equal(provider.CurrentVersion, provider.GetVersion(""));
    }

    [Fact]
    public void GetVersion_Tenant_ReturnsZeroInitially()
    {
        var provider = new InMemoryPolicyVersionProvider();
        Assert.Equal(0, provider.GetVersion("tenant-a"));
    }

    [Fact]
    public void IncrementVersion_Tenant_IncrementsTenantOnly()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.IncrementVersion("tenant-a");
        provider.IncrementVersion("tenant-a");
        provider.IncrementVersion("tenant-b");

        Assert.Equal(2, provider.GetVersion("tenant-a"));
        Assert.Equal(1, provider.GetVersion("tenant-b"));
        Assert.Equal(0, provider.CurrentVersion);
    }

    [Fact]
    public void IncrementVersion_Global_IncreasesGlobalVersion()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.IncrementVersion(null);
        provider.IncrementVersion(null);
        Assert.Equal(2, provider.CurrentVersion);
    }

    [Fact]
    public void TenantVersions_AreIsolated()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.IncrementVersion("tenant-x");
        provider.IncrementVersion("tenant-y");
        provider.IncrementVersion("tenant-y");

        Assert.Equal(1, provider.GetVersion("tenant-x"));
        Assert.Equal(2, provider.GetVersion("tenant-y"));
        Assert.Equal(0, provider.GetVersion("tenant-z"));
    }

    [Fact]
    public void ConcurrentTenantIncrements_AreThreadSafe()
    {
        var provider = new InMemoryPolicyVersionProvider();
        const int iterations = 1000;

        Parallel.For(0, iterations, _ => provider.IncrementVersion("tenant-concurrent"));

        Assert.Equal(iterations, provider.GetVersion("tenant-concurrent"));
    }
}

public class PolicyEvaluationResultTests
{
    [Fact]
    public void Allow_CreatesAllowedResult()
    {
        var result = PolicyEvaluationResult.Allow("test reason");

        Assert.True(result.Allowed);
        Assert.Equal("test reason", result.Reason);
        Assert.Empty(result.MatchedPolicies);
        Assert.False(result.DenyOverrideApplied);
        Assert.Null(result.DenyOverridePolicyCode);
    }

    [Fact]
    public void Deny_CreatesDeniedResult()
    {
        var matched = new List<MatchedPolicy>
        {
            new() { PolicyCode = "SYNQ_FUND.block.region", Effect = "Deny", Passed = true }
        };

        var result = PolicyEvaluationResult.Deny("blocked", matched);

        Assert.False(result.Allowed);
        Assert.Equal("blocked", result.Reason);
        Assert.Single(result.MatchedPolicies);
        Assert.False(result.DenyOverrideApplied);
    }

    [Fact]
    public void AllowWithPolicies_IncludesMatchedPolicies()
    {
        var matched = new List<MatchedPolicy>
        {
            new() { PolicyCode = "SYNQ_FUND.approval.limit", Effect = "Allow", Passed = true, Priority = 10, EvaluationOrder = 1 }
        };

        var result = PolicyEvaluationResult.AllowWithPolicies("all passed", matched);

        Assert.True(result.Allowed);
        Assert.Single(result.MatchedPolicies);
        Assert.Equal("Allow", result.MatchedPolicies[0].Effect);
        Assert.Equal(10, result.MatchedPolicies[0].Priority);
        Assert.Equal(1, result.MatchedPolicies[0].EvaluationOrder);
    }

    [Fact]
    public void DenyWithOverride_SetsDenyOverrideFields()
    {
        var matched = new List<MatchedPolicy>
        {
            new() { PolicyCode = "SYNQ_FUND.approval.limit", Effect = "Allow", Passed = true },
            new() { PolicyCode = "SYNQ_FUND.block.region", Effect = "Deny", Passed = true },
        };

        var result = PolicyEvaluationResult.DenyWithOverride(
            "Deny override by SYNQ_FUND.block.region",
            "SYNQ_FUND.block.region",
            matched);

        Assert.False(result.Allowed);
        Assert.True(result.DenyOverrideApplied);
        Assert.Equal("SYNQ_FUND.block.region", result.DenyOverridePolicyCode);
        Assert.Equal(2, result.MatchedPolicies.Count);
    }

    [Fact]
    public void Result_DefaultCacheHit_IsFalse()
    {
        var result = PolicyEvaluationResult.Allow();
        Assert.False(result.CacheHit);
    }

    [Fact]
    public void Result_DefaultResourceContextPresent_IsFalse()
    {
        var result = PolicyEvaluationResult.Allow();
        Assert.False(result.ResourceContextPresent);
    }

    [Fact]
    public void MatchedPolicy_DefaultEffect_IsAllow()
    {
        var mp = new MatchedPolicy();
        Assert.Equal("Allow", mp.Effect);
    }

    [Fact]
    public void RuleResult_DefaultFields_AreEmpty()
    {
        var rr = new RuleResult();
        Assert.Equal(string.Empty, rr.Field);
        Assert.Equal(string.Empty, rr.Operator);
        Assert.Equal(string.Empty, rr.ExpectedValue);
        Assert.Null(rr.ActualValue);
        Assert.False(rr.Passed);
    }
}

public class PolicyCachingOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new PolicyCachingOptions();
        Assert.False(opts.Enabled);
        Assert.Equal("InMemory", opts.Provider);
        Assert.Equal(60, opts.TtlSeconds);
        Assert.Equal("policy", opts.KeyPrefix);
    }

    [Fact]
    public void CanSetRedisProvider()
    {
        var opts = new PolicyCachingOptions { Provider = "Redis", Enabled = true, TtlSeconds = 120, KeyPrefix = "myapp" };
        Assert.Equal("Redis", opts.Provider);
        Assert.True(opts.Enabled);
        Assert.Equal(120, opts.TtlSeconds);
        Assert.Equal("myapp", opts.KeyPrefix);
    }
}

public class PolicyVersioningOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new PolicyVersioningOptions();
        Assert.Equal("InMemory", opts.Provider);
        Assert.Equal("Global", opts.Scope);
    }

    [Fact]
    public void CanSetRedisProvider()
    {
        var opts = new PolicyVersioningOptions { Provider = "Redis", Scope = "Tenant" };
        Assert.Equal("Redis", opts.Provider);
        Assert.Equal("Tenant", opts.Scope);
    }
}

public class PolicyLoggingOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new PolicyLoggingOptions();
        Assert.True(opts.Enabled);
        Assert.Equal("Debug", opts.AllowLevel);
        Assert.Equal("Warning", opts.DenyLevel);
        Assert.False(opts.LogRuleResultsOnAllow);
        Assert.Equal(1.0, opts.SampleRate);
    }

    [Fact]
    public void CanConfigureSampling()
    {
        var opts = new PolicyLoggingOptions { SampleRate = 0.1, LogRuleResultsOnAllow = true };
        Assert.Equal(0.1, opts.SampleRate);
        Assert.True(opts.LogRuleResultsOnAllow);
    }
}

public class PolicyMetricsTests
{
    [Fact]
    public void Initial_AllZeros()
    {
        var m = new PolicyMetrics();
        Assert.Equal(0, m.EvaluationCount);
        Assert.Equal(0, m.CacheHits);
        Assert.Equal(0, m.CacheMisses);
        Assert.Equal(0, m.CacheErrors);
        Assert.Equal(0, m.VersionReadCount);
        Assert.Equal(0.0, m.AverageEvaluationMs);
        Assert.Equal(0.0, m.CacheHitRate);
        Assert.Equal(0, m.StampedeCoalesced);
        Assert.Equal(0, m.FreezeEvents);
    }

    [Fact]
    public void RecordEvaluation_IncrementsCount()
    {
        var m = new PolicyMetrics();
        m.RecordEvaluation(5);
        m.RecordEvaluation(15);
        Assert.Equal(2, m.EvaluationCount);
        Assert.Equal(20, m.TotalEvaluationMs);
        Assert.Equal(10.0, m.AverageEvaluationMs);
    }

    [Fact]
    public void CacheHitRate_Computed()
    {
        var m = new PolicyMetrics();
        m.RecordCacheHit(1);
        m.RecordCacheHit(1);
        m.RecordCacheMiss(1);
        Assert.True(m.CacheHitRate > 66.0 && m.CacheHitRate < 67.0);
    }

    [Fact]
    public void RecordCacheError_IncrementsErrors()
    {
        var m = new PolicyMetrics();
        m.RecordCacheError();
        m.RecordCacheError();
        Assert.Equal(2, m.CacheErrors);
    }

    [Fact]
    public void RecordVersionRead_TracksLatency()
    {
        var m = new PolicyMetrics();
        m.RecordVersionRead(2);
        m.RecordVersionRead(4);
        Assert.Equal(2, m.VersionReadCount);
        Assert.Equal(6, m.TotalVersionReadMs);
    }

    [Fact]
    public void GetSnapshot_ReturnsCurrentState()
    {
        var m = new PolicyMetrics();
        m.RecordEvaluation(10);
        m.RecordCacheHit(1);
        m.RecordCacheMiss(2);
        m.RecordCacheError();
        m.RecordVersionRead(3);
        m.RecordStampedeCoalesced();
        m.RecordFreezeEvent();

        var snap = m.GetSnapshot();
        Assert.Equal(1, snap.EvaluationCount);
        Assert.Equal(1, snap.CacheHits);
        Assert.Equal(1, snap.CacheMisses);
        Assert.Equal(1, snap.CacheErrors);
        Assert.Equal(50.0, snap.CacheHitRate);
        Assert.Equal(1, snap.VersionReadCount);
        Assert.Equal(1, snap.StampedeCoalesced);
        Assert.Equal(1, snap.FreezeEvents);
    }

    [Fact]
    public void ConcurrentRecords_AreThreadSafe()
    {
        var m = new PolicyMetrics();
        Parallel.For(0, 1000, i =>
        {
            m.RecordEvaluation(1);
            if (i % 2 == 0) m.RecordCacheHit(1);
            else m.RecordCacheMiss(1);
        });

        Assert.Equal(1000, m.EvaluationCount);
        Assert.Equal(1000, m.CacheHits + m.CacheMisses);
    }

    [Fact]
    public void RecordStampedeCoalesced_TracksCount()
    {
        var m = new PolicyMetrics();
        m.RecordStampedeCoalesced();
        m.RecordStampedeCoalesced();
        m.RecordStampedeCoalesced();
        Assert.Equal(3, m.StampedeCoalesced);
    }

    [Fact]
    public void RecordFreezeEvent_TracksCount()
    {
        var m = new PolicyMetrics();
        m.RecordFreezeEvent();
        Assert.Equal(1, m.FreezeEvents);
    }
}

public class InMemoryPolicyEvaluationCacheTests
{
    [Fact]
    public async Task GetAsync_ReturnsNullForMissing()
    {
        var memCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var cache = new InMemoryPolicyEvaluationCache(memCache);

        var result = await cache.GetAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_GetAsync_Roundtrip()
    {
        var memCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var cache = new InMemoryPolicyEvaluationCache(memCache);

        var expected = PolicyEvaluationResult.Allow("cached");
        expected.PolicyVersion = 42;

        await cache.SetAsync("key1", expected, TimeSpan.FromMinutes(5));
        var actual = await cache.GetAsync("key1");

        Assert.NotNull(actual);
        Assert.True(actual!.Allowed);
        Assert.Equal("cached", actual.Reason);
        Assert.Equal(42, actual.PolicyVersion);
    }

    [Fact]
    public async Task RemoveAsync_DeletesEntry()
    {
        var memCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var cache = new InMemoryPolicyEvaluationCache(memCache);

        await cache.SetAsync("key2", PolicyEvaluationResult.Allow(), TimeSpan.FromMinutes(5));
        await cache.RemoveAsync("key2");

        var result = await cache.GetAsync("key2");
        Assert.Null(result);
    }
}

public class ResourceHashingTests
{
    [Fact]
    public void EmptyContext_ReturnsEmpty()
    {
        var hash = PolicyEvaluationService.ComputeResourceHash(null);
        Assert.Equal("empty", hash);
    }

    [Fact]
    public void EmptyDictionary_ReturnsEmpty()
    {
        var hash = PolicyEvaluationService.ComputeResourceHash(new Dictionary<string, object?>());
        Assert.Equal("empty", hash);
    }

    [Fact]
    public void SameValues_DifferentOrder_SameHash()
    {
        var ctx1 = new Dictionary<string, object?> { ["region"] = "US", ["amount"] = "5000" };
        var ctx2 = new Dictionary<string, object?> { ["amount"] = "5000", ["region"] = "US" };

        var hash1 = PolicyEvaluationService.ComputeResourceHash(ctx1);
        var hash2 = PolicyEvaluationService.ComputeResourceHash(ctx2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void DifferentValues_DifferentHash()
    {
        var ctx1 = new Dictionary<string, object?> { ["region"] = "US" };
        var ctx2 = new Dictionary<string, object?> { ["region"] = "EU" };

        var hash1 = PolicyEvaluationService.ComputeResourceHash(ctx1);
        var hash2 = PolicyEvaluationService.ComputeResourceHash(ctx2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CaseInsensitive_SameHash()
    {
        var ctx1 = new Dictionary<string, object?> { ["Region"] = "US" };
        var ctx2 = new Dictionary<string, object?> { ["region"] = "us" };

        var hash1 = PolicyEvaluationService.ComputeResourceHash(ctx1);
        var hash2 = PolicyEvaluationService.ComputeResourceHash(ctx2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_HasVersionPrefix()
    {
        var ctx = new Dictionary<string, object?> { ["amount"] = "50000" };
        var hash = PolicyEvaluationService.ComputeResourceHash(ctx);

        Assert.StartsWith("v1:", hash);
    }

    [Fact]
    public void Hash_IsFixedLength()
    {
        var ctx = new Dictionary<string, object?> { ["amount"] = "50000", ["region"] = "US-EAST-1", ["orgId"] = Guid.NewGuid().ToString() };
        var hash = PolicyEvaluationService.ComputeResourceHash(ctx);

        Assert.Equal(19, hash.Length); // "v1:" (3) + 16 hex chars
    }

    [Fact]
    public void NullValue_HandledGracefully()
    {
        var ctx = new Dictionary<string, object?> { ["region"] = null };
        var hash = PolicyEvaluationService.ComputeResourceHash(ctx);
        Assert.NotEqual("empty", hash);
        Assert.StartsWith("v1:", hash);
    }

    [Fact]
    public void NullValue_DifferentFromEmptyString()
    {
        var ctxNull = new Dictionary<string, object?> { ["region"] = null };
        var ctxEmpty = new Dictionary<string, object?> { ["region"] = "" };

        var hashNull = PolicyEvaluationService.ComputeResourceHash(ctxNull);
        var hashEmpty = PolicyEvaluationService.ComputeResourceHash(ctxEmpty);

        Assert.NotEqual(hashNull, hashEmpty);
    }

    [Fact]
    public void ArrayValue_ProducesConsistentHash()
    {
        var ctx1 = new Dictionary<string, object?> { ["roles"] = new List<string> { "admin", "user" } };
        var ctx2 = new Dictionary<string, object?> { ["roles"] = new List<string> { "user", "admin" } };

        var hash1 = PolicyEvaluationService.ComputeResourceHash(ctx1);
        var hash2 = PolicyEvaluationService.ComputeResourceHash(ctx2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void DifferentArrayValues_DifferentHash()
    {
        var ctx1 = new Dictionary<string, object?> { ["roles"] = new List<string> { "admin" } };
        var ctx2 = new Dictionary<string, object?> { ["roles"] = new List<string> { "user" } };

        var hash1 = PolicyEvaluationService.ComputeResourceHash(ctx1);
        var hash2 = PolicyEvaluationService.ComputeResourceHash(ctx2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void IntegerValue_HandledAsString()
    {
        var ctx = new Dictionary<string, object?> { ["amount"] = 5000 };
        var hash = PolicyEvaluationService.ComputeResourceHash(ctx);
        Assert.StartsWith("v1:", hash);
    }

    [Fact]
    public void JsonElement_Object_PropertyOrderIndependent()
    {
        var json1 = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"b\":2,\"a\":1}");
        var json2 = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"a\":1,\"b\":2}");

        var ctx1 = new Dictionary<string, object?> { ["data"] = json1 };
        var ctx2 = new Dictionary<string, object?> { ["data"] = json2 };

        var hash1 = PolicyEvaluationService.ComputeResourceHash(ctx1);
        var hash2 = PolicyEvaluationService.ComputeResourceHash(ctx2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void JsonElement_Array_OrderIndependent()
    {
        var json1 = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("[\"b\",\"a\"]");
        var json2 = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("[\"a\",\"b\"]");

        var ctx1 = new Dictionary<string, object?> { ["tags"] = json1 };
        var ctx2 = new Dictionary<string, object?> { ["tags"] = json2 };

        var hash1 = PolicyEvaluationService.ComputeResourceHash(ctx1);
        var hash2 = PolicyEvaluationService.ComputeResourceHash(ctx2);

        Assert.Equal(hash1, hash2);
    }
}

public class CacheKeyTests
{
    [Fact]
    public void BuildCacheKey_IncludesAllSegments()
    {
        var key = PolicyEvaluationService.BuildCacheKey("policy", "t1", "u1", "perm", 5, null);
        Assert.Equal("policy:t1:u1:perm:5:empty", key);
    }

    [Fact]
    public void BuildCacheKey_WithResourceContext_IncludesHash()
    {
        var ctx = new Dictionary<string, object?> { ["amount"] = "100" };
        var key = PolicyEvaluationService.BuildCacheKey("policy", "t1", "u1", "perm", 5, ctx);

        Assert.StartsWith("policy:t1:u1:perm:5:v1:", key);
    }

    [Fact]
    public void BuildCacheKey_VersionChange_DifferentKey()
    {
        var ctx = new Dictionary<string, object?> { ["amount"] = "100" };
        var key1 = PolicyEvaluationService.BuildCacheKey("policy", "t1", "u1", "perm", 5, ctx);
        var key2 = PolicyEvaluationService.BuildCacheKey("policy", "t1", "u1", "perm", 6, ctx);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void BuildCacheKey_DifferentTenants_DifferentKeys()
    {
        var key1 = PolicyEvaluationService.BuildCacheKey("policy", "tenant-a", "u1", "perm", 1, null);
        var key2 = PolicyEvaluationService.BuildCacheKey("policy", "tenant-b", "u1", "perm", 1, null);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void BuildCacheKey_CustomPrefix_UsedInKey()
    {
        var key = PolicyEvaluationService.BuildCacheKey("myapp", "t1", "u1", "perm", 1, null);
        Assert.StartsWith("myapp:", key);
    }

    [Fact]
    public void BuildCacheKey_EmptyPrefix_DefaultsToPolicy()
    {
        var key = PolicyEvaluationService.BuildCacheKey("", "t1", "u1", "perm", 1, null);
        Assert.StartsWith("policy:", key);
    }

    [Fact]
    public void BuildCacheKey_NullPrefix_DefaultsToPolicy()
    {
        var key = PolicyEvaluationService.BuildCacheKey(null!, "t1", "u1", "perm", 1, null);
        Assert.StartsWith("policy:", key);
    }
}

public class FreezeModeBehaviorTests
{
    [Fact]
    public void InMemoryProvider_NeverFrozen()
    {
        var provider = new InMemoryPolicyVersionProvider();
        Assert.False(provider.IsFrozen);
        Assert.True(provider.IsHealthy);

        provider.Increment();
        Assert.False(provider.IsFrozen);
        Assert.True(provider.IsHealthy);
    }

    [Fact]
    public void InMemoryProvider_AlwaysHealthy()
    {
        var provider = new InMemoryPolicyVersionProvider();
        for (int i = 0; i < 100; i++)
            provider.Increment();

        Assert.True(provider.IsHealthy);
        Assert.False(provider.IsFrozen);
    }
}

public class StampedeProtectionTests
{
    [Fact]
    public async Task ConcurrentSameKeyRequests_CoalesceCorrectly()
    {
        var evaluationCount = 0;
        var semaphore = new SemaphoreSlim(1, 1);
        var results = new ConcurrentBag<int>();

        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            await semaphore.WaitAsync();
            try
            {
                Interlocked.Increment(ref evaluationCount);
                results.Add(i);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(100, evaluationCount);
        Assert.Equal(100, results.Count);
    }

    [Fact]
    public async Task SemaphoreSlim_NoDeadlock_UnderHighConcurrency()
    {
        var keyLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        var completedCount = 0;
        const int totalTasks = 1000;
        const string testKey = "test-key";

        var tasks = Enumerable.Range(0, totalTasks).Select(async _ =>
        {
            var keyLock = keyLocks.GetOrAdd(testKey, _ => new SemaphoreSlim(1, 1));
            var acquired = await keyLock.WaitAsync(TimeSpan.FromSeconds(5));
            if (acquired)
            {
                try
                {
                    await Task.Delay(0);
                    Interlocked.Increment(ref completedCount);
                }
                finally
                {
                    keyLock.Release();
                }
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(totalTasks, completedCount);
    }

    [Fact]
    public async Task MultipleDifferentKeys_NoContention()
    {
        var keyLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        var completedCount = 0;
        const int keysCount = 10;
        const int tasksPerKey = 100;

        var tasks = new List<Task>();
        for (int k = 0; k < keysCount; k++)
        {
            var key = $"key-{k}";
            for (int t = 0; t < tasksPerKey; t++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var keyLock = keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                    await keyLock.WaitAsync();
                    try
                    {
                        Interlocked.Increment(ref completedCount);
                    }
                    finally
                    {
                        keyLock.Release();
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);
        Assert.Equal(keysCount * tasksPerKey, completedCount);
    }
}

public class VersionScopingTests
{
    [Fact]
    public void GlobalScope_UsesGlobalVersion()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.Increment();
        provider.Increment();

        Assert.Equal(2, provider.GetVersion(null));
        Assert.Equal(2, provider.CurrentVersion);
    }

    [Fact]
    public void TenantScope_IsolatedPerTenant()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.IncrementVersion("tenant-1");
        provider.IncrementVersion("tenant-1");
        provider.IncrementVersion("tenant-2");

        Assert.Equal(2, provider.GetVersion("tenant-1"));
        Assert.Equal(1, provider.GetVersion("tenant-2"));
        Assert.Equal(0, provider.GetVersion("tenant-3"));
    }

    [Fact]
    public void TenantScope_DoesNotAffectGlobal()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.IncrementVersion("tenant-1");
        provider.IncrementVersion("tenant-2");

        Assert.Equal(0, provider.CurrentVersion);
        Assert.Equal(0, provider.GetVersion(null));
    }

    [Fact]
    public void GlobalIncrement_DoesNotAffectTenants()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.Increment();
        provider.IncrementVersion("tenant-1");

        Assert.Equal(1, provider.CurrentVersion);
        Assert.Equal(1, provider.GetVersion("tenant-1"));
    }

    [Fact]
    public void ConcurrentTenantAndGlobalIncrements_ThreadSafe()
    {
        var provider = new InMemoryPolicyVersionProvider();

        Parallel.For(0, 500, i =>
        {
            if (i % 2 == 0) provider.Increment();
            else provider.IncrementVersion($"tenant-{i % 5}");
        });

        Assert.Equal(250, provider.CurrentVersion);
    }
}

public class FailureModeTests
{
    [Fact]
    public void InMemoryProvider_AlwaysReturnsConsistentVersion()
    {
        var provider = new InMemoryPolicyVersionProvider();
        provider.Increment();
        var version = provider.CurrentVersion;

        Assert.Equal(1, version);
        Assert.True(provider.IsHealthy);
    }

    [Fact]
    public async Task InMemoryCache_FailsGracefully_OnMissingKey()
    {
        var memCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var cache = new InMemoryPolicyEvaluationCache(memCache);

        var result = await cache.GetAsync("missing-key");
        Assert.Null(result);
    }

    [Fact]
    public void InMemoryProvider_NoFreezeOnHighLoad()
    {
        var provider = new InMemoryPolicyVersionProvider();

        Parallel.For(0, 10000, _ =>
        {
            provider.Increment();
            _ = provider.CurrentVersion;
        });

        Assert.False(provider.IsFrozen);
        Assert.True(provider.IsHealthy);
        Assert.Equal(10000, provider.CurrentVersion);
    }

    [Fact]
    public async Task CacheReadReturnsNull_IsNotAnError()
    {
        var memCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var cache = new InMemoryPolicyEvaluationCache(memCache);

        for (int i = 0; i < 100; i++)
        {
            var result = await cache.GetAsync($"key-{i}");
            Assert.Null(result);
        }
    }
}

public class SecurityTests
{
    [Fact]
    public void FrozenProvider_DoesNotGrantAccess()
    {
        var provider = new InMemoryPolicyVersionProvider();
        Assert.False(provider.IsFrozen);
        Assert.Equal(0, provider.CurrentVersion);
    }

    [Fact]
    public void VersionZero_ProducesValidCacheKey()
    {
        var key = PolicyEvaluationService.BuildCacheKey("policy", "t1", "u1", "perm", 0, null);
        Assert.Equal("policy:t1:u1:perm:0:empty", key);
    }

    [Fact]
    public void ResourceHash_DeterministicForSameInput()
    {
        var ctx = new Dictionary<string, object?> { ["role"] = "admin", ["region"] = "US" };
        var hash1 = PolicyEvaluationService.ComputeResourceHash(ctx);
        var hash2 = PolicyEvaluationService.ComputeResourceHash(ctx);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task ConcurrentCacheAccess_NoDataCorruption()
    {
        var memCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var cache = new InMemoryPolicyEvaluationCache(memCache);

        var writeResult = PolicyEvaluationResult.Allow("test");
        writeResult.PolicyVersion = 42;

        await cache.SetAsync("concurrent-key", writeResult, TimeSpan.FromMinutes(5));

        var tasks = Enumerable.Range(0, 1000).Select(async _ =>
        {
            var read = await cache.GetAsync("concurrent-key");
            Assert.NotNull(read);
            Assert.True(read!.Allowed);
            Assert.Equal(42, read.PolicyVersion);
        });
        await Task.WhenAll(tasks);
    }
}

public class PerformanceTests
{
    [Fact]
    public void HashComputation_1000Iterations_UnderOneSecond()
    {
        var ctx = new Dictionary<string, object?>
        {
            ["region"] = "US-EAST-1",
            ["amount"] = "50000",
            ["orgId"] = Guid.NewGuid().ToString(),
            ["role"] = "admin",
            ["department"] = "engineering",
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            PolicyEvaluationService.ComputeResourceHash(ctx);
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000, $"Hash computation took {sw.ElapsedMilliseconds}ms for 1000 iterations");
    }

    [Fact]
    public void CacheKeyBuild_1000Iterations_UnderOneSecond()
    {
        var ctx = new Dictionary<string, object?> { ["region"] = "US", ["amount"] = "5000" };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            PolicyEvaluationService.BuildCacheKey("policy", "tenant-1", "user-1", "perm-1", i, ctx);
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000, $"CacheKey build took {sw.ElapsedMilliseconds}ms for 1000 iterations");
    }

    [Fact]
    public void MetricsRecording_HighThroughput_NoContention()
    {
        var m = new PolicyMetrics();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Parallel.For(0, 10000, i =>
        {
            m.RecordEvaluation(1);
            m.RecordCacheHit(0);
            m.RecordVersionRead(0);
        });

        sw.Stop();
        Assert.Equal(10000, m.EvaluationCount);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Metrics recording took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ConcurrentCacheOperations_1000_NoErrors()
    {
        var memCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var cache = new InMemoryPolicyEvaluationCache(memCache);
        var errors = 0;

        var tasks = Enumerable.Range(0, 1000).Select(async i =>
        {
            try
            {
                var result = PolicyEvaluationResult.Allow($"result-{i}");
                result.PolicyVersion = i;
                await cache.SetAsync($"perf-key-{i % 50}", result, TimeSpan.FromMinutes(1));
                var read = await cache.GetAsync($"perf-key-{i % 50}");
                if (read == null) Interlocked.Increment(ref errors);
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        Assert.Equal(0, errors);
    }
}
