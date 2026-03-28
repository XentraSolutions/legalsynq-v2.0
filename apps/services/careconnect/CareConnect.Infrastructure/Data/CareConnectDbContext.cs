using BuildingBlocks.Domain;
using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Data;

public class CareConnectDbContext : DbContext
{
    public CareConnectDbContext(DbContextOptions<CareConnectDbContext> options) : base(options) { }

    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProviderCategory> ProviderCategories => Set<ProviderCategory>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<ReferralStatusHistory> ReferralStatusHistories => Set<ReferralStatusHistory>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<ProviderFacility> ProviderFacilities => Set<ProviderFacility>();
    public DbSet<ServiceOffering> ServiceOfferings => Set<ServiceOffering>();
    public DbSet<ProviderServiceOffering> ProviderServiceOfferings => Set<ProviderServiceOffering>();
    public DbSet<ProviderAvailabilityTemplate> ProviderAvailabilityTemplates => Set<ProviderAvailabilityTemplate>();
    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareConnectDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAtUtc == default)
                    entry.Property(nameof(AuditableEntity.CreatedAtUtc)).CurrentValue = now;

                entry.Property(nameof(AuditableEntity.UpdatedAtUtc)).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(AuditableEntity.UpdatedAtUtc)).CurrentValue = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
