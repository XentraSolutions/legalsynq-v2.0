using Liens.Domain.Entities;

namespace Liens.Application.Interfaces;

public interface ILienEligibilityValidator
{
    Task<LienEligibilityValidationResult> ValidateAsync(
        Lien lien,
        SellingPortfolio portfolio,
        CancellationToken ct = default);
}

public sealed record LienEligibilityValidationResult(
    bool IsEligible,
    IReadOnlyList<LienEligibilityViolation> Violations)
{
    public static LienEligibilityValidationResult Eligible { get; } = new(true, []);
}

public sealed record LienEligibilityViolation(
    string RuleCode,
    string Message);

