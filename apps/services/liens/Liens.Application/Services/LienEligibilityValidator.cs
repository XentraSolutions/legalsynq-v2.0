using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;

namespace Liens.Application.Services;

public sealed class LienEligibilityValidator : ILienEligibilityValidator
{
    private readonly IReadOnlyList<ILienEligibilityRule> _rules;

    public LienEligibilityValidator(ISellingPortfolioRepository portfolioRepo)
    {
        _rules =
        [
            new PositiveBalanceRule(),
            new NotClosedRule(),
            new NotWrittenOffRule(),
            new TenantOwnershipRule(),
            new NotAlreadyAssignedRule(portfolioRepo),
        ];
    }

    public async Task<LienEligibilityValidationResult> ValidateAsync(
        Lien lien,
        SellingPortfolio portfolio,
        CancellationToken ct = default)
    {
        var violations = new List<LienEligibilityViolation>();

        foreach (var rule in _rules)
        {
            var violation = await rule.ValidateAsync(lien, portfolio, ct);
            if (violation is not null)
                violations.Add(violation);
        }

        return violations.Count == 0
            ? LienEligibilityValidationResult.Eligible
            : new LienEligibilityValidationResult(false, violations);
    }

    private interface ILienEligibilityRule
    {
        Task<LienEligibilityViolation?> ValidateAsync(
            Lien lien,
            SellingPortfolio portfolio,
            CancellationToken ct);
    }

    private sealed class PositiveBalanceRule : ILienEligibilityRule
    {
        public Task<LienEligibilityViolation?> ValidateAsync(
            Lien lien,
            SellingPortfolio portfolio,
            CancellationToken ct)
        {
            var balance = lien.CurrentBalance ?? 0m;
            return Task.FromResult<LienEligibilityViolation?>(
                balance > 0m
                    ? null
                    : new("BALANCE_NOT_POSITIVE", "Lien balance must be greater than 0."));
        }
    }

    private sealed class NotClosedRule : ILienEligibilityRule
    {
        public Task<LienEligibilityViolation?> ValidateAsync(
            Lien lien,
            SellingPortfolio portfolio,
            CancellationToken ct)
        {
            return Task.FromResult<LienEligibilityViolation?>(
                IsStatus(lien.Status, "CLOSED", "Closed")
                    ? new("LIEN_CLOSED", "Closed liens cannot be assigned to a portfolio.")
                    : null);
        }
    }

    private sealed class NotWrittenOffRule : ILienEligibilityRule
    {
        public Task<LienEligibilityViolation?> ValidateAsync(
            Lien lien,
            SellingPortfolio portfolio,
            CancellationToken ct)
        {
            return Task.FromResult<LienEligibilityViolation?>(
                IsStatus(lien.Status, "WRITTEN_OFF", "WrittenOff", "Written Off")
                    ? new("LIEN_WRITTEN_OFF", "Written-off liens cannot be assigned to a portfolio.")
                    : null);
        }
    }

    private sealed class TenantOwnershipRule : ILienEligibilityRule
    {
        public Task<LienEligibilityViolation?> ValidateAsync(
            Lien lien,
            SellingPortfolio portfolio,
            CancellationToken ct)
        {
            return Task.FromResult<LienEligibilityViolation?>(
                lien.TenantId == portfolio.TenantId
                    ? null
                    : new("TENANT_MISMATCH", "Lien tenant does not match portfolio tenant."));
        }
    }

    private sealed class NotAlreadyAssignedRule : ILienEligibilityRule
    {
        private readonly ISellingPortfolioRepository _portfolioRepo;

        public NotAlreadyAssignedRule(ISellingPortfolioRepository portfolioRepo)
        {
            _portfolioRepo = portfolioRepo;
        }

        public async Task<LienEligibilityViolation?> ValidateAsync(
            Lien lien,
            SellingPortfolio portfolio,
            CancellationToken ct)
        {
            if (lien.TenantId != portfolio.TenantId)
                return null;

            var isAssigned = portfolio.Liens.Any(existing => existing.LienId == lien.Id)
                || await _portfolioRepo.IsLienAssignedToPortfolioAsync(portfolio.TenantId, lien.Id, ct);

            return isAssigned
                ? new("LIEN_ALREADY_ASSIGNED", "Lien is already assigned to a portfolio.")
                : null;
        }
    }

    private static bool IsStatus(string actual, params string[] expectedStatuses)
    {
        var normalizedActual = NormalizeStatus(actual);
        return expectedStatuses
            .Select(NormalizeStatus)
            .Any(expected => string.Equals(normalizedActual, expected, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeStatus(string value) =>
        new(value.Where(char.IsLetterOrDigit).ToArray());
}

