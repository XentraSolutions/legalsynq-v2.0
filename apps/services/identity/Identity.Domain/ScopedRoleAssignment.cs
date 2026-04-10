namespace Identity.Domain;

/// <summary>
/// Enriched role assignment with explicit scope context.
/// LS-COR-AUT-006A — Final Role Architecture (Intentional Dual-Boundary Model):
///   • ScopedRoleAssignment: Authoritative store for system/admin roles (PlatformAdmin,
///     TenantAdmin) via GLOBAL scope. Also supports fine-grained runtime authorization
///     checks via ORGANIZATION, PRODUCT, RELATIONSHIP, TENANT scopes through
///     ScopedAuthorizationService.
///   • UserRoleAssignment / GroupRoleAssignment: Authoritative store for product-scoped
///     roles used in JWT product_roles claims. EffectiveAccessService reads these exclusively
///     and emits PRODUCT:Role format claims.
/// System roles → role JWT claims (via ScopedRoleAssignment GLOBAL scope).
/// Product roles → product_roles JWT claims (via UserRoleAssignment/GroupRoleAssignment).
/// </summary>
public class ScopedRoleAssignment
{
    public static class ScopeTypes
    {
        public const string Global       = "GLOBAL";
        public const string Tenant       = "TENANT";
        public const string Organization = "ORGANIZATION";
        public const string Product      = "PRODUCT";
        public const string Relationship = "RELATIONSHIP";

        public static readonly IReadOnlyList<string> All =
            [Global, Tenant, Organization, Product, Relationship];

        public static bool IsValid(string value) => All.Contains(value);
    }

    public Guid   Id                       { get; private set; }
    public Guid   UserId                   { get; private set; }
    public Guid   RoleId                   { get; private set; }

    // Scope discriminator
    public string ScopeType               { get; private set; } = ScopeTypes.Global;

    // Nullable scope context — only the applicable field is populated
    public Guid?  TenantId                { get; private set; }
    public Guid?  OrganizationId          { get; private set; }
    public Guid?  OrganizationRelationshipId { get; private set; }
    public Guid?  ProductId               { get; private set; }

    public bool   IsActive                { get; private set; }
    public DateTime AssignedAtUtc         { get; private set; }
    public DateTime UpdatedAtUtc          { get; private set; }
    public Guid?  AssignedByUserId        { get; private set; }

    public User User { get; private set; } = null!;
    public Role Role { get; private set; } = null!;

    private ScopedRoleAssignment() { }

    public static ScopedRoleAssignment Create(
        Guid   userId,
        Guid   roleId,
        string scopeType,
        Guid?  tenantId                 = null,
        Guid?  organizationId           = null,
        Guid?  organizationRelationshipId = null,
        Guid?  productId                = null,
        Guid?  assignedByUserId         = null)
    {
        if (!ScopeTypes.IsValid(scopeType))
            throw new ArgumentException($"Invalid ScopeType: {scopeType}", nameof(scopeType));

        var now = DateTime.UtcNow;
        return new ScopedRoleAssignment
        {
            Id                        = Guid.NewGuid(),
            UserId                    = userId,
            RoleId                    = roleId,
            ScopeType                 = scopeType,
            TenantId                  = tenantId,
            OrganizationId            = organizationId,
            OrganizationRelationshipId = organizationRelationshipId,
            ProductId                 = productId,
            IsActive                  = true,
            AssignedAtUtc             = now,
            UpdatedAtUtc              = now,
            AssignedByUserId          = assignedByUserId
        };
    }

    public void Deactivate()
    {
        IsActive     = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
