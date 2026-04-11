using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Authorization;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class PolicyEvaluationService : IPolicyEvaluationService
{
    private readonly IdentityDbContext _db;
    private readonly IAttributeProvider _attributeProvider;
    private readonly ILogger<PolicyEvaluationService> _logger;

    public PolicyEvaluationService(
        IdentityDbContext db,
        IAttributeProvider attributeProvider,
        ILogger<PolicyEvaluationService> logger)
    {
        _db = db;
        _attributeProvider = attributeProvider;
        _logger = logger;
    }

    public async Task<PolicyEvaluationResult> EvaluateAsync(
        ClaimsPrincipal user,
        string permissionCode,
        Dictionary<string, object?>? resourceContext = null,
        HttpContext? httpContext = null,
        CancellationToken ct = default)
    {
        var permissionPolicies = await _db.PermissionPolicies
            .Where(pp => pp.PermissionCode == permissionCode && pp.IsActive)
            .Select(pp => pp.PolicyId)
            .ToListAsync(ct);

        if (permissionPolicies.Count == 0)
            return PolicyEvaluationResult.Allow("No policies attached to permission");

        var policies = await _db.Policies
            .Where(p => permissionPolicies.Contains(p.Id) && p.IsActive)
            .Include(p => p.Rules)
            .OrderBy(p => p.Priority)
            .ToListAsync(ct);

        if (policies.Count == 0)
            return PolicyEvaluationResult.Allow("No active policies found");

        var userAttrs = await _attributeProvider.GetUserAttributesAsync(user, ct);
        var resourceAttrs = await _attributeProvider.GetResourceAttributesAsync(resourceContext, ct);

        Dictionary<string, object?> requestAttrs;
        if (httpContext != null)
            requestAttrs = await _attributeProvider.GetRequestContextAsync(httpContext, ct);
        else
            requestAttrs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var allAttributes = MergeAttributes(userAttrs, resourceAttrs, requestAttrs);

        var matchedPolicies = new List<MatchedPolicy>();
        var allPassed = true;

        foreach (var policy in policies)
        {
            var policyResult = EvaluatePolicy(policy, allAttributes);
            matchedPolicies.Add(policyResult);

            if (!policyResult.Passed)
            {
                allPassed = false;
                _logger.LogWarning(
                    "PolicyEvaluation: DENY permission={Permission} policy={PolicyCode} reason={Reason}",
                    permissionCode, policy.PolicyCode, policyResult.Reason);
            }
            else
            {
                _logger.LogInformation(
                    "PolicyEvaluation: ALLOW permission={Permission} policy={PolicyCode}",
                    permissionCode, policy.PolicyCode);
            }
        }

        if (allPassed)
        {
            return PolicyEvaluationResult.AllowWithPolicies(
                $"All {policies.Count} policies passed",
                matchedPolicies);
        }

        var failedPolicies = matchedPolicies.Where(p => !p.Passed).ToList();
        return PolicyEvaluationResult.Deny(
            $"Failed {failedPolicies.Count} of {policies.Count} policies: {string.Join(", ", failedPolicies.Select(p => p.PolicyCode))}",
            matchedPolicies);
    }

    private static MatchedPolicy EvaluatePolicy(Policy policy, Dictionary<string, object?> attributes)
    {
        if (policy.Rules.Count == 0)
        {
            return new MatchedPolicy
            {
                PolicyCode = policy.PolicyCode,
                PolicyName = policy.Name,
                Passed = true,
                Reason = "No rules defined — default allow"
            };
        }

        var ruleResults = new List<RuleResult>();
        foreach (var rule in policy.Rules)
        {
            var result = EvaluateRule(rule, attributes);
            ruleResults.Add(result);
        }

        var andRules = ruleResults.Where((_, i) => policy.Rules.ElementAt(i).LogicalGroup == LogicalGroupType.And).ToList();
        var orRules = ruleResults.Where((_, i) => policy.Rules.ElementAt(i).LogicalGroup == LogicalGroupType.Or).ToList();

        bool passed;
        if (orRules.Count > 0 && andRules.Count > 0)
        {
            passed = andRules.All(r => r.Passed) && orRules.Any(r => r.Passed);
        }
        else if (orRules.Count > 0)
        {
            passed = orRules.Any(r => r.Passed);
        }
        else
        {
            passed = andRules.All(r => r.Passed);
        }

        var failedRules = ruleResults.Where(r => !r.Passed).ToList();
        var reason = passed
            ? "All rules satisfied"
            : $"Failed rules: {string.Join(", ", failedRules.Select(r => $"{r.Field} {r.Operator} {r.ExpectedValue}"))}";

        return new MatchedPolicy
        {
            PolicyCode = policy.PolicyCode,
            PolicyName = policy.Name,
            Passed = passed,
            Reason = reason,
            RuleResults = ruleResults
        };
    }

    private static RuleResult EvaluateRule(PolicyRule rule, Dictionary<string, object?> attributes)
    {
        attributes.TryGetValue(rule.Field, out var rawValue);
        var actualValue = rawValue?.ToString();

        var passed = EvaluateOperator(rule.Operator, actualValue, rule.Value, rawValue);

        return new RuleResult
        {
            Field = rule.Field,
            Operator = rule.Operator.ToString(),
            ExpectedValue = rule.Value,
            ActualValue = actualValue,
            Passed = passed
        };
    }

    private static bool EvaluateOperator(RuleOperator op, string? actualValue, string expectedValue, object? rawValue)
    {
        if (actualValue == null && op != RuleOperator.NotEquals && op != RuleOperator.NotIn)
            return false;

        return op switch
        {
            RuleOperator.Equals => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            RuleOperator.NotEquals => !string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            RuleOperator.GreaterThan => TryCompareNumeric(actualValue!, expectedValue, out var cmp) && cmp > 0,
            RuleOperator.GreaterThanOrEqual => TryCompareNumeric(actualValue!, expectedValue, out var cmp2) && cmp2 >= 0,
            RuleOperator.LessThan => TryCompareNumeric(actualValue!, expectedValue, out var cmp3) && cmp3 < 0,
            RuleOperator.LessThanOrEqual => TryCompareNumeric(actualValue!, expectedValue, out var cmp4) && cmp4 <= 0,
            RuleOperator.In => EvaluateIn(actualValue!, expectedValue, rawValue),
            RuleOperator.NotIn => !EvaluateIn(actualValue ?? "", expectedValue, rawValue),
            RuleOperator.Contains => actualValue != null && actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
            RuleOperator.StartsWith => actualValue != null && actualValue.StartsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryCompareNumeric(string actual, string expected, out int result)
    {
        result = 0;
        if (!decimal.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out var a) ||
            !decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var b))
            return false;
        result = a.CompareTo(b);
        return true;
    }

    private static bool EvaluateIn(string actualValue, string expectedValue, object? rawValue)
    {
        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(expectedValue);
            if (values == null) return false;

            if (rawValue is List<string> listValue)
                return listValue.Any(v => values.Contains(v, StringComparer.OrdinalIgnoreCase));

            return values.Contains(actualValue, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            var parts = expectedValue.Split(',').Select(s => s.Trim().Trim('"', '\'')).ToList();
            return parts.Contains(actualValue, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, object?> MergeAttributes(params Dictionary<string, object?>[] sources)
    {
        var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var kvp in source)
            {
                merged.TryAdd(kvp.Key, kvp.Value);
            }
        }
        return merged;
    }
}
