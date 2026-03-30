namespace Identity.Domain;

public class ProductRole
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    /// <summary>
    /// TODO [LEGACY — Phase F]: retire this field once all ProductRoles have OrgTypeRules seeded
    /// and AuthService IsEligible fully uses the ProductOrganizationTypeRule table.
    /// Keep populated for backward compatibility; do not use in new code.
    /// </summary>
    public string? EligibleOrgType { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Product Product { get; private set; } = null!;
    public ICollection<RoleCapability> RoleCapabilities { get; private set; } = [];
    public ICollection<ProductOrganizationTypeRule> OrgTypeRules { get; private set; } = [];

    private ProductRole() { }

    public static ProductRole Create(
        Guid productId,
        string code,
        string name,
        string? eligibleOrgType = null,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (eligibleOrgType is not null && !OrgType.IsValid(eligibleOrgType))
            throw new ArgumentException($"Invalid EligibleOrgType: {eligibleOrgType}", nameof(eligibleOrgType));

        return new ProductRole
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Code = code.ToUpperInvariant().Trim(),
            Name = name.Trim(),
            Description = description?.Trim(),
            EligibleOrgType = eligibleOrgType,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
