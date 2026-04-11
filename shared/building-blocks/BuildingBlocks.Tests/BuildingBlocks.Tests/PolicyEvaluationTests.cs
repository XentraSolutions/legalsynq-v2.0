using Identity.Domain;

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
        Assert.True(policy.IsActive);
        Assert.NotEqual(Guid.Empty, policy.Id);
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

        policy.Update("Updated", "New desc", 5);

        Assert.Equal("Updated", policy.Name);
        Assert.Equal("New desc", policy.Description);
        Assert.Equal(5, policy.Priority);
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
