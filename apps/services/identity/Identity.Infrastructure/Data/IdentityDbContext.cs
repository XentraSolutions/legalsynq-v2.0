using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<TenantProduct> TenantProducts => Set<TenantProduct>();

    // Organizations
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationDomain> OrganizationDomains => Set<OrganizationDomain>();
    public DbSet<OrganizationProduct> OrganizationProducts => Set<OrganizationProduct>();

    // Product role model
    public DbSet<ProductRole> ProductRoles => Set<ProductRole>();
    public DbSet<Capability> Capabilities => Set<Capability>();
    public DbSet<RoleCapability> RoleCapabilities => Set<RoleCapability>();

    // UIX-005: Role ↔ Capability assignments (for tenant custom roles)
    public DbSet<RoleCapabilityAssignment> RoleCapabilityAssignments => Set<RoleCapabilityAssignment>();

    // User organization membership
    public DbSet<UserOrganizationMembership> UserOrganizationMemberships => Set<UserOrganizationMembership>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Platform Phase 1–4 (authoritative models)
    public DbSet<OrganizationType>              OrganizationTypes              => Set<OrganizationType>();
    public DbSet<RelationshipType>              RelationshipTypes              => Set<RelationshipType>();
    public DbSet<OrganizationRelationship>      OrganizationRelationships      => Set<OrganizationRelationship>();
    public DbSet<ProductRelationshipTypeRule>   ProductRelationshipTypeRules   => Set<ProductRelationshipTypeRule>();
    public DbSet<ProductOrganizationTypeRule>   ProductOrganizationTypeRules   => Set<ProductOrganizationTypeRule>();
    public DbSet<ScopedRoleAssignment>          ScopedRoleAssignments          => Set<ScopedRoleAssignment>();

    // UIX-002: User Management
    public DbSet<TenantGroup>                   TenantGroups                   => Set<TenantGroup>();
    public DbSet<GroupMembership>               GroupMemberships               => Set<GroupMembership>();
    public DbSet<UserInvitation>                UserInvitations                => Set<UserInvitation>();

    // UIX-003-03: Security / admin-triggered password reset
    public DbSet<PasswordResetToken>            PasswordResetTokens            => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
