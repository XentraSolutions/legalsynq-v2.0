using Identity.Domain;
using BuildingBlocks.Authorization;
using Identity.Infrastructure.Services;

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
