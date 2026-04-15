using Microsoft.EntityFrameworkCore;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public class ReportsDbContext : DbContext
{
    public ReportsDbContext(DbContextOptions<ReportsDbContext> options) : base(options) { }

    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
    public DbSet<ReportTemplateVersion> ReportTemplateVersions => Set<ReportTemplateVersion>();
    public DbSet<ReportExecution> ReportExecutions => Set<ReportExecution>();
    public DbSet<ReportTemplateAssignment> ReportTemplateAssignments => Set<ReportTemplateAssignment>();
    public DbSet<ReportTemplateAssignmentTenant> ReportTemplateAssignmentTenants => Set<ReportTemplateAssignmentTenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ReportTemplate>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAtUtc == default)
                    entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ReportTemplateVersion>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAtUtc == default)
                entry.Entity.CreatedAtUtc = now;
        }

        foreach (var entry in ChangeTracker.Entries<ReportExecution>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAtUtc == default)
                entry.Entity.CreatedAtUtc = now;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
