namespace Identity.Application.DTOs;

public sealed record EffectiveAccessContext
{
    public Guid UserId { get; init; }
    public Guid TenantId { get; init; }
    public IReadOnlyList<ProductAccessEntry> ProductAccess { get; init; } = [];
    public IReadOnlyList<string> DeniedReasons { get; init; } = [];

    public IReadOnlyList<string> GetEffectiveProductRoles()
    {
        return ProductAccess
            .Where(p => p.IsGranted)
            .SelectMany(p => p.EffectiveRoles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool HasProductAccess(string productCode)
    {
        return ProductAccess.Any(p =>
            p.IsGranted &&
            string.Equals(p.ProductCode, productCode, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> GetRolesForProduct(string productCode)
    {
        return ProductAccess
            .Where(p => p.IsGranted &&
                        string.Equals(p.ProductCode, productCode, StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => p.EffectiveRoles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ProductAccessEntry> GetAccessForOrganization(Guid organizationId)
    {
        return ProductAccess
            .Where(p => p.IsGranted && p.OrganizationId == organizationId)
            .ToList();
    }
}

public sealed record ProductAccessEntry
{
    public string ProductCode { get; init; } = string.Empty;
    public Guid? OrganizationId { get; init; }
    public string? OrganizationName { get; init; }
    public string? OrgType { get; init; }
    public IReadOnlyList<string> EffectiveRoles { get; init; } = [];
    public bool IsGranted { get; init; }
    public string AccessSource { get; init; } = string.Empty;
    public string? DenialReason { get; init; }
}
