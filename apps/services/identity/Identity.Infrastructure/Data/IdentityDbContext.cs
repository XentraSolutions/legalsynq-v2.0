using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    // Existing
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
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

    // User organization membership
    public DbSet<UserOrganizationMembership> UserOrganizationMemberships => Set<UserOrganizationMembership>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
