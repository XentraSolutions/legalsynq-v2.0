namespace Identity.Domain;

/// <summary>
/// Declares which organization types are eligible for a given product role.
/// Replaces the hard-coded EligibleOrgType string on ProductRole.
/// Both the new rule table and the legacy EligibleOrgType field are checked
/// during the transitional migration window.
/// </summary>
public class ProductOrganizationTypeRule
{
    public Guid Id                { get; private set; }
    public Guid ProductId         { get; private set; }
    public Guid ProductRoleId     { get; private set; }
    public Guid OrganizationTypeId { get; private set; }
    public bool IsActive          { get; private set; }
    public DateTime CreatedAtUtc  { get; private set; }

    public Product          Product          { get; private set; } = null!;
    public ProductRole      ProductRole      { get; private set; } = null!;
    public OrganizationType OrganizationType { get; private set; } = null!;

    private ProductOrganizationTypeRule() { }

    public static ProductOrganizationTypeRule Create(
        Guid productId,
        Guid productRoleId,
        Guid organizationTypeId)
    {
        return new ProductOrganizationTypeRule
        {
            Id                 = Guid.NewGuid(),
            ProductId          = productId,
            ProductRoleId      = productRoleId,
            OrganizationTypeId = organizationTypeId,
            IsActive           = true,
            CreatedAtUtc       = DateTime.UtcNow
        };
    }
}
