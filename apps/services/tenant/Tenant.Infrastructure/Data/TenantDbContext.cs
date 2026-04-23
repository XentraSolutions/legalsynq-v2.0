using Microsoft.EntityFrameworkCore;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data;

public class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

    public DbSet<Domain.Tenant>  Tenants  => Set<Domain.Tenant>();
    public DbSet<TenantBranding> Brandings => Set<TenantBranding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Domain.Tenant>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property("UpdatedAtUtc").CurrentValue = now;
        }

        foreach (var entry in ChangeTracker.Entries<TenantBranding>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property("UpdatedAtUtc").CurrentValue = now;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
