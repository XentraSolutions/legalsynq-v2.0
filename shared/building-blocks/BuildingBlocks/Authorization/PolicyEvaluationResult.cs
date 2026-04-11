namespace BuildingBlocks.Authorization;

public class PolicyEvaluationResult
{
    public bool Allowed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<MatchedPolicy> MatchedPolicies { get; set; } = [];

    public static PolicyEvaluationResult Allow(string reason = "No policies attached") =>
        new() { Allowed = true, Reason = reason };

    public static PolicyEvaluationResult Deny(string reason, List<MatchedPolicy>? matchedPolicies = null) =>
        new() { Allowed = false, Reason = reason, MatchedPolicies = matchedPolicies ?? [] };

    public static PolicyEvaluationResult AllowWithPolicies(string reason, List<MatchedPolicy> matchedPolicies) =>
        new() { Allowed = true, Reason = reason, MatchedPolicies = matchedPolicies };
}

public class MatchedPolicy
{
    public string PolicyCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<RuleResult> RuleResults { get; set; } = [];
}

public class RuleResult
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string? ActualValue { get; set; }
    public bool Passed { get; set; }
}
