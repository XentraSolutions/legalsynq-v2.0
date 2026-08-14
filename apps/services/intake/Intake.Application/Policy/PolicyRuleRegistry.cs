namespace Intake.Application.Policy;

public sealed class PolicyRuleRegistry(IEnumerable<IPolicyRule> rules)
    : IPolicyRuleRegistry
{
    public IReadOnlyList<IPolicyRule> Rules { get; } =
        rules.OrderBy(rule => rule.Order).ThenBy(rule => rule.Code).ToArray();
}